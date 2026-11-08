using CasaEngine.Framework.Configuration.Project;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.World;

/// <summary>
/// Reads the optional <c>virtual_resolution</c> object of a <c>.world</c> (ADR-0087): <c>width</c>, <c>height</c> and an
/// optional <c>mode</c>. An invalid declaration fails at load, naming the world and the key.
/// </summary>
internal static class WorldVirtualResolutionReader
{
    public const string Key = "virtual_resolution";

    public static VirtualResolutionSettings Read(JToken token, string worldName)
    {
        if (token == null || token.Type == JTokenType.Null)
        {
            return null;
        }

        if (token is not JObject declaration)
        {
            throw new InvalidDataException(
                $"World '{worldName}': '{Key}' must be an object with width, height and an optional mode.");
        }

        int width = ReadPositiveDimension(declaration, "width", worldName);
        int height = ReadPositiveDimension(declaration, "height", worldName);

        var mode = VirtualResolutionMode.IntegerFit;
        if (declaration["mode"] is { } modeToken)
        {
            string modeName = modeToken.Type == JTokenType.String ? modeToken.Value<string>() : null;
            if (modeName == null || !Enum.TryParse(modeName, ignoreCase: false, out mode) || !Enum.IsDefined(mode))
            {
                throw new InvalidDataException(
                    $"World '{worldName}': '{Key}.mode' is '{modeToken}'; the supported modes are '{nameof(VirtualResolutionMode.IntegerFit)}', "
                    + $"'{nameof(VirtualResolutionMode.BufferedIntegerFit)}' and '{nameof(VirtualResolutionMode.BufferedFit)}'.");
            }
        }

        return new VirtualResolutionSettings { Width = width, Height = height, Mode = mode };
    }

    private static int ReadPositiveDimension(JObject declaration, string key, string worldName)
    {
        // Same rule as the project setting: only a JSON integer in 1..int.MaxValue.
        JToken token = declaration[key];
        long? value = token is JValue { Type: JTokenType.Integer, Value: int or long } ? token.Value<long>() : null;
        if (value is not (> 0 and <= int.MaxValue))
        {
            throw new InvalidDataException($"World '{worldName}': '{Key}.{key}' must be a positive integer.");
        }

        return (int)value.Value;
    }
}
