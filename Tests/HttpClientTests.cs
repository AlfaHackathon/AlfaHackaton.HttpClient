using System.Net;
using System.Net.Http.Headers;
using AlfaHackaton.HttpClient.Common.Interfaces;
using AlfaHackaton.HttpClient.Common.Models;
using AlfaHackaton.HttpClient.Common.Settings;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Xunit;

namespace AlfaHackaton.HttpClient.Tests;

public class HttpClientTests
{
    [Fact]
    public async Task Get_ReturnsJsonResult_FromRelativeAddress()
    {
        // Контракт переноса: BaseAddress применяется фабрикой, DTO читается Newtonsoft с JsonProperty.
        var handler = new StubHandler((request, token) =>
        {
            Assert.Equal("https://example.invalid/api/items/42", request.RequestUri!.AbsoluteUri);
            return Task.FromResult(JsonResponse("""{"display_name":"Хакатон"}"""));
        });
        var services = new ServiceCollection();
        services.AddAlfaHttpClient(new HttpClientSettings { BaseAddress = "https://example.invalid/api/" })
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHttpClient<HttpClientSettings>>();

        var response = await client.Get<Item>("items/42");

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("Хакатон", response.Result?.Name);
    }

    [Fact]
    public async Task Post_SendsJsonBody_WithNewtonsoftAttributes()
    {
        // Контракт: typed POST передаёт JSON и уважает имена JsonProperty входного DTO.
        using var handler = new StubHandler(async (request, token) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.NotNull(request.Content);
            Assert.Equal("application/json", request.Content.Headers.ContentType?.MediaType);
            Assert.Equal("""{"display_name":"Хакатон"}""", await request.Content.ReadAsStringAsync(token));
            return JsonResponse("""{"display_name":"Создано"}""", HttpStatusCode.Created);
        });
        using var native = new System.Net.Http.HttpClient(handler);
        var client = new Http.HttpClient<HttpClientSettings>(native, new HttpClientSettings());

        var response = await client.Post<Item, Item>("https://example.invalid/items", new Item { Name = "Хакатон" });

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Created, response.Status);
        Assert.Equal("Создано", response.Result?.Name);
    }

    [Fact]
    public async Task Get_AppliesPublicHeaders_WithoutAuthorization()
    {
        // Инвариант HTTP: пользовательские заголовки не требуют наличия Authorization.
        using var handler = new StubHandler((request, token) =>
        {
            Assert.Equal("ru", Assert.Single(request.Headers.GetValues("Locale-Id")));
            Assert.Null(request.Headers.Authorization);
            return Task.FromResult(JsonResponse("{}"));
        });
        using var native = new System.Net.Http.HttpClient(handler);
        var client = new Http.HttpClient<HttpClientSettings>(native, new HttpClientSettings());

        await client.Get("https://example.invalid/items", (headers, contentHeaders) =>
        {
            Assert.Null(contentHeaders);
            headers.Add("Locale-Id", "ru");
        });
    }

    [Fact]
    public async Task Send_PreservesRawBytes_AndContentHeaders()
    {
        // Инвариант: бинарный запрос не должен проходить через JSON-сериализацию.
        byte[] payload = [0, 127, 255];
        using var handler = new StubHandler(async (request, token) =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.NotNull(request.Content);
            Assert.Equal(payload, await request.Content.ReadAsByteArrayAsync(token));
            Assert.Equal("application/octet-stream", request.Content.Headers.ContentType?.MediaType);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            return JsonResponse(string.Empty, HttpStatusCode.NoContent);
        });
        using var native = new System.Net.Http.HttpClient(handler);
        var client = new Http.HttpClient<HttpClientSettings>(native, new HttpClientSettings());

        var response = await client.Send<None, None>(new Request<None>
        {
            Uri = "https://example.invalid/blob",
            Method = HttpMethod.Put,
            RawData = payload,
            HeadersApplier = (headers, contentHeaders) =>
            {
                headers.Authorization = new AuthenticationHeaderValue("Bearer", "synthetic-token");
                Assert.NotNull(contentHeaders);
                contentHeaders.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            }
        });

        Assert.True(response.IsSuccessStatusCode);
        Assert.Empty(response.RawData);
    }

    [Fact]
    public async Task Post_PassesHttpContent_WithoutJsonEncoding()
    {
        // Контракт HTTP-формы: Content-Type и URL-encoding сохраняются без JSON-обёртки.
        using var handler = new StubHandler(async (request, token) =>
        {
            Assert.NotNull(request.Content);
            Assert.Equal("application/x-www-form-urlencoded", request.Content.Headers.ContentType?.MediaType);
            Assert.Equal("query=hello+world", await request.Content.ReadAsStringAsync(token));
            return JsonResponse("{}");
        });
        using var native = new System.Net.Http.HttpClient(handler);
        var client = new Http.HttpClient<HttpClientSettings>(native, new HttpClientSettings());

        await client.Post("https://example.invalid/form", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["query"] = "hello world"
        }));
    }

    [Fact]
    public async Task GetAsAsyncEnumerable_UsesNewtonsoftNames_AndPreservesNullElements()
    {
        // Контракт переноса: поток JSON-массива использует те же JsonProperty, что обычный ответ.
        using var handler = new StubHandler((request, token) => Task.FromResult(JsonResponse(
            """[{"display_name":"Первый"},null,{"display_name":"Последний"}]""")));
        using var native = new System.Net.Http.HttpClient(handler);
        var client = new Http.HttpClient<HttpClientSettings>(native, new HttpClientSettings());
        var items = new List<Item?>();

        await foreach (var item in client.GetAsAsyncEnumerable<None, Item>("https://example.invalid/items", null))
        {
            items.Add(item);
        }

        Assert.Equal(3, items.Count);
        Assert.Equal("Первый", items[0]?.Name);
        Assert.Null(items[1]);
        Assert.Equal("Последний", items[2]?.Name);
    }

    [Theory]
    [InlineData(199, false)]
    [InlineData(200, true)]
    [InlineData(201, true)]
    [InlineData(204, true)]
    [InlineData(299, true)]
    [InlineData(300, false)]
    [InlineData(400, false)]
    [InlineData(500, false)]
    public async Task Get_UsesHttpSuccessRange_AndRetainsUntypedBody(int status, bool expectedSuccess)
    {
        // Источник: диапазон HTTP 2xx; тело доступно даже без typed Result и при ошибочном статусе.
        using var handler = new StubHandler((request, token) => Task.FromResult(JsonResponse("diagnostic", (HttpStatusCode)status)));
        using var native = new System.Net.Http.HttpClient(handler);
        var client = new Http.HttpClient<HttpClientSettings>(native, new HttpClientSettings());

        var response = await client.Get("https://example.invalid/items");

        Assert.Equal(expectedSuccess, response.IsSuccessStatusCode);
        Assert.Equal(status, (int)response.Status);
        Assert.Equal("diagnostic", response.TextData);
    }

    [Fact]
    public async Task Get_PropagatesNetworkFailure()
    {
        // Контракт транспорта: сетевую ошибку нельзя превращать в успешный пустой Response.
        var failure = new HttpRequestException("Synthetic connection failure");
        using var handler = new StubHandler((request, token) => Task.FromException<HttpResponseMessage>(failure));
        using var native = new System.Net.Http.HttpClient(handler);
        var client = new Http.HttpClient<HttpClientSettings>(native, new HttpClientSettings());

        var observed = await Assert.ThrowsAsync<HttpRequestException>(() => client.Get("https://example.invalid/items"));

        Assert.Same(failure, observed);
    }

    [Fact]
    public async Task Get_CancelsInFlightRequest()
    {
        // Инвариант: отмена вызывающего кода доходит до HTTP handler, а не только до чтения готового ответа.
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new StubHandler(async (request, token) =>
        {
            started.SetResult();
            return await new TaskCompletionSource<HttpResponseMessage>().Task.WaitAsync(token);
        });
        using var native = new System.Net.Http.HttpClient(handler);
        var client = new Http.HttpClient<HttpClientSettings>(native, new HttpClientSettings());
        using var cancellation = new CancellationTokenSource();
        var pending = client.Get("https://example.invalid/items", cancellationToken: cancellation.Token);

        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task Get_CachesDeserializedResult()
    {
        // Контракт Response: повторное чтение свойства не создаёт второй объект DTO.
        using var handler = new StubHandler((request, token) => Task.FromResult(JsonResponse("""{"display_name":"Хакатон"}""")));
        using var native = new System.Net.Http.HttpClient(handler);
        var client = new Http.HttpClient<HttpClientSettings>(native, new HttpClientSettings());

        var response = await client.Get<Item>("https://example.invalid/items");

        Assert.NotNull(response.Result);
        Assert.Same(response.Result, response.Result);
    }

    [Fact]
    public async Task Get_DefersInvalidJsonFailure_UntilResultAccess()
    {
        // Контракт: HTML/текст ошибки можно прочитать без преждевременной десериализации DTO.
        using var handler = new StubHandler((request, token) => Task.FromResult(JsonResponse("not-json", HttpStatusCode.BadGateway)));
        using var native = new System.Net.Http.HttpClient(handler);
        var client = new Http.HttpClient<HttpClientSettings>(native, new HttpClientSettings());

        var response = await client.Get<Item>("https://example.invalid/items");

        Assert.Equal(HttpStatusCode.BadGateway, response.Status);
        Assert.Equal("not-json", response.TextData);
        Assert.Throws<JsonReaderException>(() => response.Result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("null")]
    public async Task Get_ReturnsNullResult_ForAbsentJsonObject(string json)
    {
        // Контракт отсутствующего результата: пустое тело/JSON null не требует фиктивного DTO.
        using var handler = new StubHandler((request, token) => Task.FromResult(JsonResponse(json, HttpStatusCode.NoContent)));
        using var native = new System.Net.Http.HttpClient(handler);
        var client = new Http.HttpClient<HttpClientSettings>(native, new HttpClientSettings());

        var response = await client.Get<Item>("https://example.invalid/items");

        Assert.Null(response.Result);
    }

    [Theory]
    [InlineData("[]", 0)]
    [InlineData("[null]", 1)]
    public async Task GetAsAsyncEnumerable_PreservesArrayCardinality(string json, int expectedCount)
    {
        // Инвариант JSON-массива: ни пустой массив, ни null-элемент не добавляют/теряют элементы.
        using var handler = new StubHandler((request, token) => Task.FromResult(JsonResponse(json)));
        using var native = new System.Net.Http.HttpClient(handler);
        var client = new Http.HttpClient<HttpClientSettings>(native, new HttpClientSettings());
        var count = 0;

        await foreach (var item in client.GetAsAsyncEnumerable<None, Item>("https://example.invalid/items", null))
        {
            Assert.Null(item);
            count++;
        }

        Assert.Equal(expectedCount, count);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[{\"display_name\":\"Не завершён\"}")]
    public async Task GetAsAsyncEnumerable_RejectsInvalidArray(string json)
    {
        // Контракт потокового метода: ответ обязан быть полным JSON-массивом, а не объектом/обрывком.
        using var handler = new StubHandler((request, token) => Task.FromResult(JsonResponse(json)));
        using var native = new System.Net.Http.HttpClient(handler);
        var client = new Http.HttpClient<HttpClientSettings>(native, new HttpClientSettings());

        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(async () =>
        {
            await foreach (var item in client.GetAsAsyncEnumerable<None, Item>("https://example.invalid/items", null))
            {
            }
        });
    }

    [Fact]
    public async Task GetAsAsyncEnumerable_ObservesCancellationBetweenItems()
    {
        // Инвариант: отмена действует и на уже буферизованные JSON-токены, не только сетевой ReadAsync.
        using var handler = new StubHandler((request, token) => Task.FromResult(JsonResponse("[{},{}]")));
        using var native = new System.Net.Http.HttpClient(handler);
        var client = new Http.HttpClient<HttpClientSettings>(native, new HttpClientSettings());
        using var cancellation = new CancellationTokenSource();
        await using var enumerator = client.GetAsAsyncEnumerable<None, Item>("https://example.invalid/items", null)
            .GetAsyncEnumerator(cancellation.Token);

        Assert.True(await enumerator.MoveNextAsync());
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => enumerator.MoveNextAsync().AsTask());
    }

    [Fact]
    public async Task GetAsAsyncEnumerable_RejectsHttpFailure()
    {
        // Контракт streaming: ошибочный HTTP-ответ бросает исключение до выдачи первого элемента.
        using var handler = new StubHandler((request, token) => Task.FromResult(JsonResponse("[]", HttpStatusCode.ServiceUnavailable)));
        using var native = new System.Net.Http.HttpClient(handler);
        var client = new Http.HttpClient<HttpClientSettings>(native, new HttpClientSettings());

        var failure = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var item in client.GetAsAsyncEnumerable<None, Item>("https://example.invalid/items", null))
            {
            }
        });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, failure.StatusCode);
    }

    [Fact]
    public async Task GetAsAsyncEnumerable_PreservesDecimalPrecision()
    {
        // Инвариант: промежуточный JSON-parser не должен превращать представимый decimal в double.
        using var handler = new StubHandler((request, token) => Task.FromResult(JsonResponse("[1234567890.123456789012345678]")));
        using var native = new System.Net.Http.HttpClient(handler);
        var client = new Http.HttpClient<HttpClientSettings>(native, new HttpClientSettings());
        var items = new List<decimal>();

        await foreach (var item in client.GetAsAsyncEnumerable<None, decimal>("https://example.invalid/items", null))
        {
            items.Add(item);
        }

        Assert.Equal(1234567890.123456789012345678m, Assert.Single(items));
    }

    [Fact]
    public async Task GetAsAsyncEnumerable_RespectsConfiguredDepth()
    {
        // Контракт JsonSettings: собственный MaxDepth применяется к DTO и в обычном, и в потоковом ответе.
        var json = "0";
        for (var level = 0; level < 70; level++)
        {
            json = "{\"child\":" + json + "}";
        }

        using var handler = new StubHandler((request, token) => Task.FromResult(JsonResponse(
            request.RequestUri!.AbsolutePath == "/array" ? "[" + json + "]" : json)));
        using var native = new System.Net.Http.HttpClient(handler);
        var client = new Http.HttpClient<HttpClientSettings>(native, new HttpClientSettings
        {
            JsonSettings = new JsonSerializerSettings { MaxDepth = 128 }
        });

        var response = await client.Get<Newtonsoft.Json.Linq.JObject>("https://example.invalid/object");
        Assert.Equal(json, response.Result?.ToString(Formatting.None));

        var count = 0;
        await foreach (var item in client.GetAsAsyncEnumerable<None, Newtonsoft.Json.Linq.JObject>("https://example.invalid/array", null))
        {
            Assert.Equal(json, item?.ToString(Formatting.None));
            count++;
        }

        Assert.Equal(1, count);
    }

    private static HttpResponseMessage JsonResponse(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(status) { Content = new StringContent(json) };
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return send(request, cancellationToken);
        }
    }

    private sealed class Item
    {
        [JsonProperty("display_name")]
        public string Name { get; set; } = string.Empty;
    }
}
