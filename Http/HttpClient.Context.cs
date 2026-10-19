using System.Net.Http.Headers;
using AlfaHackaton.HttpClient.Common.Interfaces;
using AlfaHackaton.HttpClient.Common.Models;
using RequestContext = AlfaHackaton.Context.Context;

namespace AlfaHackaton.HttpClient.Http;

public partial class HttpClient<TSettings> where TSettings : IHttpClientSettings
{
    public Task<Response<TOutput>> GetContext<TInput, TOutput>(RequestContext ctx, string uri, TInput data)
    {
        return Send<TInput, TOutput>(uri, HttpMethod.Get, data, GetHeaderApplier(ctx), ctx.CancellationToken);
    }

    public Task<Response<TOutput>> GetContext<TOutput>(RequestContext ctx, string uri)
    {
        return Send<None, TOutput>(uri, HttpMethod.Get, null, GetHeaderApplier(ctx), ctx.CancellationToken);
    }

    public async Task<Response> GetContext(RequestContext ctx, string uri)
    {
        return await Send<None, None>(uri, HttpMethod.Get, null, GetHeaderApplier(ctx), ctx.CancellationToken);
    }

    public Task<Response<TOutput>> PostContext<TInput, TOutput>(RequestContext ctx, string uri, TInput data)
    {
        return Send<TInput, TOutput>(uri, HttpMethod.Post, data, GetHeaderApplier(ctx), ctx.CancellationToken);
    }

    public async Task<Response> PostContext<TInput>(RequestContext ctx, string uri, TInput data)
    {
        return await Send<TInput, None>(uri, HttpMethod.Post, data, GetHeaderApplier(ctx), ctx.CancellationToken);
    }

    public Task<Response<TOutput>> PostContext<TOutput>(RequestContext ctx, string uri)
    {
        return Send<None, TOutput>(uri, HttpMethod.Post, null, GetHeaderApplier(ctx), ctx.CancellationToken);
    }

    public async Task<Response> PostContext(RequestContext ctx, string uri)
    {
        return await Send<None, None>(uri, HttpMethod.Post, null, GetHeaderApplier(ctx), ctx.CancellationToken);
    }

    public Task<Response<TOutput>> PutContext<TInput, TOutput>(RequestContext ctx, string uri, TInput data)
    {
        return Send<TInput, TOutput>(uri, HttpMethod.Put, data, GetHeaderApplier(ctx), ctx.CancellationToken);
    }

    public async Task<Response> PutContext<TInput>(RequestContext ctx, string uri, TInput? data = default)
    {
        return await Send<TInput, None>(uri, HttpMethod.Put, data, GetHeaderApplier(ctx), ctx.CancellationToken);
    }

    public async Task<Response> PutContext(RequestContext ctx, string uri)
    {
        return await Send<None, None>(uri, HttpMethod.Put, null, GetHeaderApplier(ctx), ctx.CancellationToken);
    }

    public Task<Response<TOutput>> PatchContext<TInput, TOutput>(RequestContext ctx, string uri, TInput data)
    {
        return Send<TInput, TOutput>(uri, HttpMethod.Patch, data, GetHeaderApplier(ctx), ctx.CancellationToken);
    }

    public async Task<Response> PatchContext<TInput>(RequestContext ctx, string uri, TInput? data = default)
    {
        return await Send<TInput, None>(uri, HttpMethod.Patch, data, GetHeaderApplier(ctx), ctx.CancellationToken);
    }

    public async Task<Response> PatchContext(RequestContext ctx, string uri)
    {
        return await Send<None, None>(uri, HttpMethod.Patch, null, GetHeaderApplier(ctx), ctx.CancellationToken);
    }

    public Task<Response<TOutput>> DeleteContext<TOutput>(RequestContext ctx, string uri)
    {
        return Send<None, TOutput>(uri, HttpMethod.Delete, null, GetHeaderApplier(ctx), ctx.CancellationToken);
    }

    public async Task<Response> DeleteContext<TInput>(RequestContext ctx, string uri, TInput data)
    {
        return await Send<TInput, None>(uri, HttpMethod.Delete, data, GetHeaderApplier(ctx), ctx.CancellationToken);
    }

    public async Task<Response> DeleteContext(RequestContext ctx, string uri)
    {
        return await Send<None, None>(uri, HttpMethod.Delete, null, GetHeaderApplier(ctx), ctx.CancellationToken);
    }

    private static HeaderApplier GetHeaderApplier(RequestContext ctx)
    {
        return (headers, contentHeaders) =>
        {
            if (!string.IsNullOrEmpty(ctx.Origin))
            {
                headers.Add("Origin", ctx.Origin);
            }

            if (!string.IsNullOrEmpty(ctx.AuthToken))
            {
                headers.Authorization = AuthenticationHeaderValue.Parse(ctx.AuthToken);
            }

            if (!string.IsNullOrEmpty(ctx.UserAgent))
            {
                headers.TryAddWithoutValidation("User-Agent", ctx.UserAgent);
            }

            if (!string.IsNullOrEmpty(ctx.Locale))
            {
                headers.Add("Locale-Id", ctx.Locale);
            }

            foreach (var header in ctx.AdditionalHeaders)
            {
                headers.Remove(header.Key);
                headers.Add(header.Key, header.Value);
            }
        };
    }
}
