using System.Text;
using System.Text.Json;

namespace CasaEngine.Editor.Remote.Protocol;

/// <summary>Turns protocol messages into lines and back (ADR-0092). Never throws on malformed input.</summary>
public static class RemoteMessageCodec
{
    public static byte[] Encode<T>(T message)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(message, RemoteProtocol.JsonOptions);
        var line = new byte[json.Length + 1];
        json.CopyTo(line, 0);
        line[^1] = (byte)'\n';
        return line;
    }

    public static async Task WriteAsync<T>(Stream stream, T message, CancellationToken cancellationToken)
    {
        byte[] line = Encode(message);
        await stream.WriteAsync(line, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static JsonElement ToElement<T>(T value)
        => JsonSerializer.SerializeToElement(value, RemoteProtocol.JsonOptions);

    public static T? FromElement<T>(JsonElement element)
        => element.Deserialize<T>(RemoteProtocol.JsonOptions);

    /// <summary>Reads a request line. On failure, <paramref name="error"/> is the response to send back.</summary>
    public static bool TryDecodeRequest(string line, out RemoteRequest request, out RemoteResponse error)
    {
        request = new RemoteRequest();
        error = null!;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException ex)
        {
            error = RemoteResponse.Failure(null, new RemoteError { Code = RemoteErrorCodes.ParseError, Message = "Invalid JSON: " + ex.Message });
            return false;
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            long? id = root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("id", out JsonElement idElement)
                && idElement.ValueKind == JsonValueKind.Number
                && idElement.TryGetInt64(out long parsedId)
                    ? parsedId
                    : null;

            if (root.ValueKind != JsonValueKind.Object
                || id == null
                || !root.TryGetProperty("jsonrpc", out JsonElement version) || version.ValueKind != JsonValueKind.String || version.GetString() != RemoteProtocol.JsonRpcVersion
                || !root.TryGetProperty("method", out JsonElement method) || method.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(method.GetString()))
            {
                error = RemoteResponse.Failure(id, new RemoteError
                {
                    Code = RemoteErrorCodes.InvalidRequest,
                    Message = "A request needs \"jsonrpc\": \"2.0\", a numeric \"id\" and a \"method\".",
                });
                return false;
            }

            JsonElement? parameters = null;
            if (root.TryGetProperty("params", out JsonElement paramsElement) && paramsElement.ValueKind != JsonValueKind.Null)
            {
                if (paramsElement.ValueKind != JsonValueKind.Object)
                {
                    error = RemoteResponse.Failure(id, new RemoteError { Code = RemoteErrorCodes.InvalidParams, Message = "\"params\" must be an object." });
                    return false;
                }

                parameters = paramsElement.Clone();
            }

            request = new RemoteRequest { Id = id.Value, Method = method.GetString()!, Params = parameters };
            return true;
        }
    }

    /// <summary>Reads a response line; false when the line is not a protocol response.</summary>
    public static bool TryDecodeResponse(string line, out RemoteResponse response)
    {
        response = null!;
        try
        {
            RemoteResponse? decoded = JsonSerializer.Deserialize<RemoteResponse>(line, RemoteProtocol.JsonOptions);
            if (decoded == null || decoded.Jsonrpc != RemoteProtocol.JsonRpcVersion || (decoded.Result == null && decoded.Error == null))
            {
                return false;
            }

            response = decoded;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string Describe(byte[] line) => Encoding.UTF8.GetString(line).TrimEnd('\n');
}
