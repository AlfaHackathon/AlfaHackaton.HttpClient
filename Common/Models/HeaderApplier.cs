using System.Net.Http.Headers;

namespace AlfaHackaton.HttpClient.Common.Models;

public delegate void HeaderApplier(HttpRequestHeaders requestHeaders, HttpContentHeaders? contentHeaders);
