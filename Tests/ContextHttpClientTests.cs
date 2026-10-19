using System.Net;
using AlfaHackaton.Context;
using AlfaHackaton.HttpClient.Common.Interfaces;
using AlfaHackaton.HttpClient.Common.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Xunit;
using OperationResults = AlfaHackaton.Result;
using RequestContext = AlfaHackaton.Context.Context;

namespace AlfaHackaton.HttpClient.Tests;

public class ContextHttpClientTests
{
    [Fact]
    public async Task GetContext_ForwardsRequestMetadata()
    {
        // Контракт Context: заголовки и локаль передаются без неявного выбора версии внешнего API.
        using var handler = new StubHandler((request, token) =>
        {
            Assert.True(request.Headers.Contains("Origin"));
            Assert.Equal("https://example.com", Assert.Single(request.Headers.GetValues("Origin")));
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("synthetic-token", request.Headers.Authorization?.Parameter);
            Assert.Equal("Hackathon/1.0", request.Headers.UserAgent.ToString());
            Assert.Equal("ru", Assert.Single(request.Headers.GetValues("Locale-Id")));
            Assert.False(request.Headers.Contains("x-api-version"));
            Assert.Equal("request-42", Assert.Single(request.Headers.GetValues("X-Request-Id")));
            return Task.FromResult(JsonResponse());
        });
        var services = new ServiceCollection();
        services.AddAlfaHttpClient(new HttpClientSettings { BaseAddress = "https://example.invalid/" })
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHttpClient<HttpClientSettings>>();
        var ctx = new RequestContext("Bearer synthetic-token", "ru", "https://example.com", "Hackathon/1.0",
            new Authorize("42", "Иван", "ivan@example.com"), CancellationToken.None,
            new Dictionary<string, string> { ["X-Request-Id"] = "request-42" });

        var response = await client.GetContext<object>(ctx, "items/42");

        Assert.Equal(HttpStatusCode.OK, response.Status);
    }

    [Fact]
    public async Task GetContext_ForwardsPublicHeaders_AndAllowsExplicitOverrides()
    {
        // Контракт: Origin/AdditionalHeaders работают без JWT; явный заголовок имеет приоритет над полем Locale.
        using var handler = new StubHandler((request, token) =>
        {
            Assert.Null(request.Headers.Authorization);
            Assert.Equal("https://example.com", Assert.Single(request.Headers.GetValues("Origin")));
            Assert.Equal("en", Assert.Single(request.Headers.GetValues("Locale-Id")));
            return Task.FromResult(JsonResponse());
        });
        using var native = new System.Net.Http.HttpClient(handler);
        var client = new Http.HttpClient<HttpClientSettings>(native, new HttpClientSettings());
        var ctx = new RequestContext(string.Empty, "ru", "https://example.com", string.Empty, Authorize.Empty(), CancellationToken.None);
        ctx.AdditionalHeaders["locale-id"] = "en";

        await client.GetContext(ctx, "https://example.invalid/items");
    }

    [Fact]
    public async Task DeleteContext_ForwardsJsonBody()
    {
        // DeleteContext<TInput> поддерживает тело запроса.
        using var handler = new StubHandler(async (request, token) =>
        {
            Assert.Equal(HttpMethod.Delete, request.Method);
            Assert.NotNull(request.Content);
            var json = JObject.Parse(await request.Content.ReadAsStringAsync(token));
            Assert.Equal("Удалить", (string?)json["Name"]);
            return JsonResponse();
        });
        using var native = new System.Net.Http.HttpClient(handler);
        var client = new Http.HttpClient<HttpClientSettings>(native, new HttpClientSettings());

        await client.DeleteContext(RequestContext.Empty(), "https://example.invalid/items/42", new Item { Name = "Удалить" });
    }

    [Fact]
    public async Task PostContext_PropagatesContextCancellation()
    {
        // Инвариант: отмена Context прекращает уже начавшийся HTTP-запрос.
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new StubHandler(async (request, token) =>
        {
            started.SetResult();
            return await new TaskCompletionSource<HttpResponseMessage>().Task.WaitAsync(token);
        });
        using var native = new System.Net.Http.HttpClient(handler);
        var client = new Http.HttpClient<HttpClientSettings>(native, new HttpClientSettings());
        using var cancellation = new CancellationTokenSource();
        var ctx = RequestContext.New(string.Empty, cancellation.Token);
        var pending = client.PostContext(ctx, "https://example.invalid/items");

        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task ContextHeaders_AreIsolatedBetweenRequests()
    {
        // Инвариант: Authorization предыдущего контекста не попадает в следующий публичный запрос.
        var authorizations = new List<string?>();
        using var handler = new StubHandler((request, token) =>
        {
            authorizations.Add(request.Headers.Authorization?.ToString());
            return Task.FromResult(JsonResponse());
        });
        using var native = new System.Net.Http.HttpClient(handler);
        var client = new Http.HttpClient<HttpClientSettings>(native, new HttpClientSettings());

        await client.GetContext(RequestContext.New("Bearer first"), "https://example.invalid/items");
        await client.GetContext(RequestContext.Empty(), "https://example.invalid/items");

        Assert.Equal("Bearer first", authorizations[0]);
        Assert.Null(authorizations[1]);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task ExternalService_UsesExplicitResultFlow(HttpStatusCode status)
    {
        // Согласованный пользовательский сценарий: GetContext -> IsStatusOkAsync -> CreateErrorResult или return response.Result.
        var services = new ServiceCollection();
        services.AddAlfaHttpClient(new HttpClientSettings { BaseAddress = "https://example.invalid/" })
            .ConfigurePrimaryHttpMessageHandler(() => new StubHandler((request, token) => Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("""{"Name":"Хакатон"}""")
            })));
        using var provider = services.BuildServiceProvider();
        var external = new ExternalService(provider.GetRequiredService<IHttpClient<HttpClientSettings>>());

        var result = await external.GetItemAsync(RequestContext.Empty(), 42);

        if (status == HttpStatusCode.OK)
        {
            Assert.Null(result.Error);
            Assert.Equal("Хакатон", result.Content?.Name);
            return;
        }

        Assert.Null(result.Content);
        Assert.NotNull(result.Error);
        Assert.Equal("Не удалось получить товар", result.Error.Title);
        Assert.Equal(status, result.Error.HttpStatusCode);
        Assert.Equal(AlfaHackaton.Result.Errors.ErrorSource.External, result.Error.Source);
    }

    private sealed class ExternalService(IHttpClient<HttpClientSettings> client)
    {
        public async Task<OperationResults.Result<Item>> GetItemAsync(RequestContext ctx, int id)
        {
            var response = await client.GetContext<Item>(ctx, $"items/{id}");
            if (!await response.IsStatusOkAsync("Не удалось получить товар", NullLogger.Instance))
            {
                return response.CreateErrorResult<Item>("Не удалось получить товар");
            }

            return response.Result;
        }
    }

    private sealed class Item
    {
        public string Name { get; set; } = string.Empty;
    }

    private static HttpResponseMessage JsonResponse()
    {
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return send(request, cancellationToken);
        }
    }
}
