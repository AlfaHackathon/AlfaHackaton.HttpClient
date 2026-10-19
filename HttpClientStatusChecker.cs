using System.Net;
using AlfaHackaton.HttpClient.Common.Models;
using AlfaHackaton.Result.Errors;
using Microsoft.Extensions.Logging;
using OperationResults = AlfaHackaton.Result;

namespace AlfaHackaton.HttpClient;

public static class HttpClientStatusChecker
{
    public static Task<bool> IsStatusOkAsync(this Response response, string errorPrefix, ILogger logger)
    {
        if (response.Status == HttpStatusCode.OK)
        {
            return Task.FromResult(true);
        }

        logger.LogError("{ErrorPrefix}: {Status}", errorPrefix, response.Status);
        return Task.FromResult(false);
    }

    public static Task<bool> IsStatusOkAsync<TOutput>(this Response<TOutput> response, string errorPrefix, ILogger logger)
    {
        return ((Response)response).IsStatusOkAsync(errorPrefix, logger);
    }

    public static OperationResults.Result<TOutput> CreateErrorResult<TOutput>(
        this Response<TOutput> response,
        string title,
        ErrorSource source = ErrorSource.External,
        ErrorType type = ErrorType.Undefined)
    {
        return new OperationResults.Result<TOutput>(response.CreateError(title, source, type));
    }

    public static OperationResults.Result<TOutput> CreateErrorResult<TOutput>(
        this Response response,
        string title,
        ErrorSource source = ErrorSource.External,
        ErrorType type = ErrorType.Undefined)
    {
        return new OperationResults.Result<TOutput>(response.CreateError(title, source, type));
    }

    public static Error CreateError(
        this Response response,
        string title,
        ErrorSource source = ErrorSource.External,
        ErrorType type = ErrorType.Undefined)
    {
        return new Error(title, source, type) { HttpStatusCode = response.Status };
    }

    public static Error CreateError<TOutput>(
        this Response<TOutput> response,
        string title,
        ErrorSource source = ErrorSource.External,
        ErrorType type = ErrorType.Undefined)
    {
        return ((Response)response).CreateError(title, source, type);
    }
}
