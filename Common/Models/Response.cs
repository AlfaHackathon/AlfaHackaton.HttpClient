using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace AlfaHackaton.HttpClient.Common.Models;

public class Response
{
    private readonly Lazy<string> textData;

    internal Response(HttpResponseMessage response, byte[] rawData)
    {
        Status = response.StatusCode;
        Headers = response.Headers;
        ContentHeaders = response.Content.Headers;
        RawData = rawData;
        textData = new Lazy<string>(() => Encoding.UTF8.GetString(RawData));
    }

    public HttpStatusCode Status { get; }
    public HttpResponseHeaders Headers { get; }
    public HttpContentHeaders ContentHeaders { get; }
    public byte[] RawData { get; }
    public string TextData => textData.Value;
    public bool IsSuccessStatusCode => (int)Status >= 200 && (int)Status <= 299;
}
