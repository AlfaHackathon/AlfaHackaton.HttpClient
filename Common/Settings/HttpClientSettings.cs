using System.Net.Http.Headers;
using AlfaHackaton.HttpClient.Common.Interfaces;
using Newtonsoft.Json;

namespace AlfaHackaton.HttpClient.Common.Settings;

public class HttpClientSettings : IHttpClientSettings
{
    public virtual TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);
    public virtual string BaseAddress { get; set; } = string.Empty;
    public virtual string ProxyAddress { get; set; } = string.Empty;
    public virtual bool AutoRedirect { get; set; } = true;
    public virtual bool IgnoreVerifySsl { get; set; }
    public virtual Action<HttpRequestHeaders>? DefaultHeaders { get; set; }
    public virtual JsonSerializerSettings? JsonSettings { get; set; }
}
