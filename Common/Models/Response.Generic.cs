using Newtonsoft.Json;

namespace AlfaHackaton.HttpClient.Common.Models;

public class Response<TOutput> : Response
{
    private readonly Lazy<TOutput?> result;

    internal Response(
        HttpResponseMessage response,
        byte[] rawData,
        JsonSerializerSettings? jsonSettings) : base(response, rawData)
    {
        result = new Lazy<TOutput?>(() =>
            string.IsNullOrWhiteSpace(TextData) ? default : JsonConvert.DeserializeObject<TOutput>(TextData, jsonSettings));
    }

    public TOutput? Result => result.Value;
}
