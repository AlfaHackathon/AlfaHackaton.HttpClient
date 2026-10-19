using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace AlfaHackaton.HttpClient.Common.Interfaces;

public interface IHttpClientSettings
{
    TimeSpan Timeout { get; }
    string BaseAddress { get; }
    string ProxyAddress { get; }
    bool AutoRedirect { get; }
    bool IgnoreVerifySsl { get; }
    Action<HttpRequestHeaders>? DefaultHeaders { get; }
    JsonSerializerSettings? JsonSettings { get; }
}
