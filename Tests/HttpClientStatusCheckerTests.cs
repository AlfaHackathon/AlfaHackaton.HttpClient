using System.Net;
using AlfaHackaton.HttpClient.Common.Models;
using AlfaHackaton.HttpClient.Common.Settings;
using AlfaHackaton.Result.Errors;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AlfaHackaton.HttpClient.Tests;

public class HttpClientStatusCheckerTests
{
    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.Created, false)]
    [InlineData(HttpStatusCode.NoContent, false)]
    [InlineData(HttpStatusCode.NotFound, false)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    public async Task IsStatusOkAsync_AcceptsOnlyOkAndLogsOtherStatuses(HttpStatusCode status, bool expected)
    {
        // Проверка принимает ровно 200, остальные статусы логирует как ошибку.
        var response = await CreateResponseAsync(status);
        var logger = new RecordingLogger();

        var actual = await response.IsStatusOkAsync("Не удалось получить товар", logger);

        Assert.Equal(expected, actual);
        Assert.Equal(expected ? 0 : 1, logger.Errors.Count);
        if (!expected)
        {
            Assert.Contains("Не удалось получить товар", logger.Errors[0]);
            Assert.Contains(status.ToString(), logger.Errors[0]);
        }
    }

    [Fact]
    public async Task CreateErrorResult_PreservesHttpStatusAndErrorMetadata()
    {
        // Типизированный и нетипизированный ответы превращаются в Error с HTTP-статусом.
        var response = await CreateResponseAsync(HttpStatusCode.NotFound);

        var typed = response.CreateErrorResult("Товар не найден", ErrorSource.External, ErrorType.NotFound);
        var untyped = ((Response)response).CreateErrorResult<string>("Товар не найден", ErrorSource.External, ErrorType.NotFound);

        Assert.NotNull(typed.Error);
        Assert.NotNull(untyped.Error);
        Assert.Equal(HttpStatusCode.NotFound, typed.Error.HttpStatusCode);
        Assert.Equal(HttpStatusCode.NotFound, untyped.Error.HttpStatusCode);
        Assert.Equal("Товар не найден", typed.Error.Title);
        Assert.Equal(ErrorType.NotFound, typed.Error.Type);
        Assert.Equal(ErrorSource.External, typed.Error.Source);
        Assert.Null(typed.Content);
        Assert.Null(untyped.Content);
    }

    private static async Task<Response<object>> CreateResponseAsync(HttpStatusCode status)
    {
        using var native = new System.Net.Http.HttpClient(new StubHandler(status));
        var client = new Http.HttpClient<HttpClientSettings>(native, new HttpClientSettings());
        return await client.Get<object>("https://example.invalid/items/42");
    }

    private sealed class StubHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("{}") });
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Errors { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                Errors.Add(formatter(state, exception));
            }
        }
    }
}
