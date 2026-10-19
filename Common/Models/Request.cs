namespace AlfaHackaton.HttpClient.Common.Models;

public class Request<TInput>
{
    public string Uri { get; set; } = string.Empty;
    public HttpMethod Method { get; set; } = HttpMethod.Get;
    public TInput? Data { get; set; }
    public byte[]? RawData { get; set; }
    public HeaderApplier? HeadersApplier { get; set; }
}
