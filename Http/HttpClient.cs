using System.Runtime.CompilerServices;
using System.Text;
using AlfaHackaton.HttpClient.Common.Interfaces;
using AlfaHackaton.HttpClient.Common.Models;
using Newtonsoft.Json;

namespace AlfaHackaton.HttpClient.Http;

public partial class HttpClient<TSettings>(System.Net.Http.HttpClient httpClient, TSettings settings) : IHttpClient<TSettings>
    where TSettings : IHttpClientSettings
{
    public Task<Response<TOutput>> Get<TInput, TOutput>(
        string uri,
        TInput data,
        HeaderApplier? headerApplier = null,
        CancellationToken cancellationToken = default)
    {
        return Send<TInput, TOutput>(uri, HttpMethod.Get, data, headerApplier, cancellationToken);
    }

    public Task<Response<TOutput>> Get<TOutput>(
        string uri,
        HeaderApplier? headerApplier = null,
        CancellationToken cancellationToken = default)
    {
        return Send<None, TOutput>(uri, HttpMethod.Get, null, headerApplier, cancellationToken);
    }

    public async Task<Response> Get(string uri, HeaderApplier? headerApplier = null, CancellationToken cancellationToken = default)
    {
        return await Send<None, None>(uri, HttpMethod.Get, null, headerApplier, cancellationToken);
    }

    public Task<Response<TOutput>> Post<TInput, TOutput>(
        string uri,
        TInput data,
        HeaderApplier? headerApplier = null,
        CancellationToken cancellationToken = default)
    {
        return Send<TInput, TOutput>(uri, HttpMethod.Post, data, headerApplier, cancellationToken);
    }

    public async Task<Response> Post<TInput>(
        string uri,
        TInput data,
        HeaderApplier? headerApplier = null,
        CancellationToken cancellationToken = default)
    {
        return await Send<TInput, None>(uri, HttpMethod.Post, data, headerApplier, cancellationToken);
    }

    public async Task<Response> Post(string uri, HeaderApplier? headerApplier = null, CancellationToken cancellationToken = default)
    {
        return await Send<None, None>(uri, HttpMethod.Post, null, headerApplier, cancellationToken);
    }

    public Task<Response<TOutput>> Put<TInput, TOutput>(
        string uri,
        TInput data,
        HeaderApplier? headerApplier = null,
        CancellationToken cancellationToken = default)
    {
        return Send<TInput, TOutput>(uri, HttpMethod.Put, data, headerApplier, cancellationToken);
    }

    public async Task<Response> Put<TInput>(
        string uri,
        TInput data,
        HeaderApplier? headerApplier = null,
        CancellationToken cancellationToken = default)
    {
        return await Send<TInput, None>(uri, HttpMethod.Put, data, headerApplier, cancellationToken);
    }

    public async Task<Response> Put(string uri, HeaderApplier? headerApplier = null, CancellationToken cancellationToken = default)
    {
        return await Send<None, None>(uri, HttpMethod.Put, null, headerApplier, cancellationToken);
    }

    public Task<Response<TOutput>> Patch<TInput, TOutput>(
        string uri,
        TInput data,
        HeaderApplier? headerApplier = null,
        CancellationToken cancellationToken = default)
    {
        return Send<TInput, TOutput>(uri, HttpMethod.Patch, data, headerApplier, cancellationToken);
    }

    public async Task<Response> Patch<TInput>(
        string uri,
        TInput data,
        HeaderApplier? headerApplier = null,
        CancellationToken cancellationToken = default)
    {
        return await Send<TInput, None>(uri, HttpMethod.Patch, data, headerApplier, cancellationToken);
    }

    public async Task<Response> Patch(string uri, HeaderApplier? headerApplier = null, CancellationToken cancellationToken = default)
    {
        return await Send<None, None>(uri, HttpMethod.Patch, null, headerApplier, cancellationToken);
    }

    public Task<Response<TOutput>> Delete<TOutput>(
        string uri,
        HeaderApplier? headerApplier = null,
        CancellationToken cancellationToken = default)
    {
        return Send<None, TOutput>(uri, HttpMethod.Delete, null, headerApplier, cancellationToken);
    }

    public async Task<Response> Delete(string uri, HeaderApplier? headerApplier = null, CancellationToken cancellationToken = default)
    {
        return await Send<None, None>(uri, HttpMethod.Delete, null, headerApplier, cancellationToken);
    }

    public Task<Response<TOutput>> Send<TInput, TOutput>(
        string uri,
        HttpMethod method,
        TInput? data,
        HeaderApplier? headerApplier = null,
        CancellationToken cancellationToken = default)
    {
        return Send<TInput, TOutput>(new Request<TInput>
        {
            Uri = uri,
            Method = method,
            Data = data,
            HeadersApplier = headerApplier
        }, cancellationToken);
    }

    public async Task<Response<TOutput>> Send<TInput, TOutput>(Request<TInput> request, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(request.Method, request.Uri);
        ApplyRequest(message, request);
        using var response = await httpClient.SendAsync(message, cancellationToken);
        var rawData = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return new Response<TOutput>(response, rawData, settings.JsonSettings);
    }

    public async IAsyncEnumerable<TOutput?> GetAsAsyncEnumerable<TInput, TOutput>(
        string uri,
        TInput? data,
        HeaderApplier? headerApplier = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (httpClient.Timeout != Timeout.InfiniteTimeSpan)
        {
            timeout.CancelAfter(httpClient.Timeout);
        }

        var token = timeout.Token;
        using var message = new HttpRequestMessage(HttpMethod.Get, uri);
        ApplyRequest(message, new Request<TInput> { Data = data, HeadersApplier = headerApplier });
        using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        var maxDepth = JsonSerializer.CreateDefault(settings.JsonSettings).MaxDepth;
        var options = new System.Text.Json.JsonSerializerOptions
        {
            // Внешний массив добавляет один уровень к глубине каждого DTO.
            MaxDepth = maxDepth is int depth && depth < int.MaxValue ? depth + 1 : int.MaxValue
        };
        var items = System.Text.Json.JsonSerializer.DeserializeAsyncEnumerable<System.Text.Json.JsonElement>(stream, options, token);
        await foreach (var item in items)
        {
            token.ThrowIfCancellationRequested();
            // JsonElement сохраняет исходный JSON числа/даты; DTO разбирается тем же Newtonsoft, что обычный ответ.
            yield return JsonConvert.DeserializeObject<TOutput>(item.GetRawText(), settings.JsonSettings);
        }
    }

    private void ApplyRequest<TInput>(HttpRequestMessage message, Request<TInput> request)
    {
        if (request.Data is not null)
        {
            message.Content = request.Data as HttpContent
                ?? new StringContent(JsonConvert.SerializeObject(request.Data, settings.JsonSettings), Encoding.UTF8, "application/json");
        }

        if (request.Data is null && request.RawData is not null)
        {
            message.Content = new ByteArrayContent(request.RawData);
        }

        request.HeadersApplier?.Invoke(message.Headers, message.Content?.Headers);
    }
}
