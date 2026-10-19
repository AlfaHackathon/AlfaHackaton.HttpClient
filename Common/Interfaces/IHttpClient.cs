using AlfaHackaton.HttpClient.Common.Models;
using RequestContext = AlfaHackaton.Context.Context;

namespace AlfaHackaton.HttpClient.Common.Interfaces;

public interface IHttpClient<TSettings> where TSettings : IHttpClientSettings
{
    Task<Response<TOutput>> Get<TInput, TOutput>(string uri, TInput data, HeaderApplier? headerApplier = null, CancellationToken cancellationToken = default);
    Task<Response<TOutput>> Get<TOutput>(string uri, HeaderApplier? headerApplier = null, CancellationToken cancellationToken = default);
    Task<Response> Get(string uri, HeaderApplier? headerApplier = null, CancellationToken cancellationToken = default);
    Task<Response<TOutput>> Post<TInput, TOutput>(string uri, TInput data, HeaderApplier? headerApplier = null, CancellationToken cancellationToken = default);
    Task<Response> Post<TInput>(string uri, TInput data, HeaderApplier? headerApplier = null, CancellationToken cancellationToken = default);
    Task<Response> Post(string uri, HeaderApplier? headerApplier = null, CancellationToken cancellationToken = default);
    Task<Response<TOutput>> Put<TInput, TOutput>(string uri, TInput data, HeaderApplier? headerApplier = null, CancellationToken cancellationToken = default);
    Task<Response> Put<TInput>(string uri, TInput data, HeaderApplier? headerApplier = null, CancellationToken cancellationToken = default);
    Task<Response> Put(string uri, HeaderApplier? headerApplier = null, CancellationToken cancellationToken = default);
    Task<Response<TOutput>> Patch<TInput, TOutput>(string uri, TInput data, HeaderApplier? headerApplier = null, CancellationToken cancellationToken = default);
    Task<Response> Patch<TInput>(string uri, TInput data, HeaderApplier? headerApplier = null, CancellationToken cancellationToken = default);
    Task<Response> Patch(string uri, HeaderApplier? headerApplier = null, CancellationToken cancellationToken = default);
    Task<Response<TOutput>> Delete<TOutput>(string uri, HeaderApplier? headerApplier = null, CancellationToken cancellationToken = default);
    Task<Response> Delete(string uri, HeaderApplier? headerApplier = null, CancellationToken cancellationToken = default);
    Task<Response<TOutput>> Send<TInput, TOutput>(string uri, HttpMethod method, TInput data, HeaderApplier? headerApplier = null, CancellationToken cancellationToken = default);
    Task<Response<TOutput>> Send<TInput, TOutput>(Request<TInput> request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<TOutput?> GetAsAsyncEnumerable<TInput, TOutput>(string uri, TInput? data, HeaderApplier? headerApplier = null, CancellationToken cancellationToken = default);
    Task<Response<TOutput>> GetContext<TInput, TOutput>(RequestContext ctx, string uri, TInput data);
    Task<Response<TOutput>> GetContext<TOutput>(RequestContext ctx, string uri);
    Task<Response> GetContext(RequestContext ctx, string uri);
    Task<Response<TOutput>> PostContext<TInput, TOutput>(RequestContext ctx, string uri, TInput data);
    Task<Response> PostContext<TInput>(RequestContext ctx, string uri, TInput data);
    Task<Response<TOutput>> PostContext<TOutput>(RequestContext ctx, string uri);
    Task<Response> PostContext(RequestContext ctx, string uri);
    Task<Response<TOutput>> PutContext<TInput, TOutput>(RequestContext ctx, string uri, TInput data);
    Task<Response> PutContext<TInput>(RequestContext ctx, string uri, TInput? data = default);
    Task<Response> PutContext(RequestContext ctx, string uri);
    Task<Response<TOutput>> PatchContext<TInput, TOutput>(RequestContext ctx, string uri, TInput data);
    Task<Response> PatchContext<TInput>(RequestContext ctx, string uri, TInput? data = default);
    Task<Response> PatchContext(RequestContext ctx, string uri);
    Task<Response<TOutput>> DeleteContext<TOutput>(RequestContext ctx, string uri);
    Task<Response> DeleteContext<TInput>(RequestContext ctx, string uri, TInput data);
    Task<Response> DeleteContext(RequestContext ctx, string uri);
}
