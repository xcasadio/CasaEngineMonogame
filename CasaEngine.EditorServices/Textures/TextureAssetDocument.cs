using System.Globalization;
using CasaEngine.Core.Serialization;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.EditorServices.Textures;

/// <summary>
/// The model behind the <c>.texture</c> document of the editor (plan P7): a <see cref="Texture"/> read from its file, the
/// catalogued image it points to, and the edits of its sampler state. The edits live here, not in the panel, so they are
/// tested without a window and the automation option uses the same code as the controls.
/// </summary>
/// <remarks>
/// A MonoGame <see cref="SamplerState"/> cannot change once bound to a device, and the built-in ones (such as
/// <see cref="SamplerState.AnisotropicWrap"/>) are shared. An edit therefore builds a new sampler state that copies every field
/// the JSON holds and changes one; the previous instance is never touched.
/// </remarks>
public sealed class TextureAssetDocument
{
    public const string FilterKey = "texture_filter";
    public const string AddressUKey = "address_u";
    public const string AddressVKey = "address_v";
    public const string AddressWKey = "address_w";
    public const string MaxAnisotropyKey = "max_anisotropy";

    /// <summary>Lowest and highest value of the maximum anisotropy (the Direct3D range).</summary>
    public const int MinMaxAnisotropy = 1;

    public const int MaxMaxAnisotropy = 16;

    /// <summary>The sampler fields the editor can change, by their JSON key.</summary>
    public static IReadOnlyList<string> EditableKeys { get; } =
        [FilterKey, AddressUKey, AddressVKey, AddressWKey, MaxAnisotropyKey];

    private TextureAssetDocument(Texture texture, Guid imageAssetId)
    {
        Texture = texture;
        ImageAssetId = imageAssetId;
    }

    /// <summary>The texture as the serializer writes it back.</summary>
    public Texture Texture { get; }

    /// <summary>The <c>texture_asset_id</c> of the file: the catalogued image the texture samples.</summary>
    public Guid ImageAssetId { get; }

    public SamplerState Sampler => Texture.PreferredSamplerState;

    public static bool TryLoad(string fullPath, out TextureAssetDocument document, out string error)
    {
        document = null;
        error = null;

        try
        {
            var root = JObject.Parse(File.ReadAllText(fullPath));
            var texture = new Texture();
            texture.Load(root);
            texture.FileName = fullPath;
            document = new TextureAssetDocument(texture, root["texture_asset_id"]?.GetGuid() ?? Guid.Empty);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException or NullReferenceException or FormatException or ArgumentException)
        {
            error = $"Cannot read texture '{fullPath}': {exception.Message}";
            return false;
        }
    }

    /// <summary>
    /// Changes one field of the sampler state. On failure nothing changes and <paramref name="error"/> says why.
    /// </summary>
    public bool TryApply(string key, string value, out string error)
    {
        if (!TryApply(Texture.PreferredSamplerState, key, value, out SamplerState updated, out error))
        {
            return false;
        }

        Texture.PreferredSamplerState = updated;
        return true;
    }

    /// <summary>
    /// Builds a copy of <paramref name="source"/> with the field <paramref name="key"/> set from <paramref name="value"/>, parsed
    /// without regard to case. <paramref name="source"/> is not modified.
    /// </summary>
    public static bool TryApply(SamplerState source, string key, string value, out SamplerState updated, out string error)
    {
        updated = null;
        error = null;
        ArgumentNullException.ThrowIfNull(source);

        if (string.IsNullOrWhiteSpace(key))
        {
            error = "the sampler key is empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            error = $"the value of '{key}' is empty.";
            return false;
        }

        SamplerState copy = Clone(source);
        switch (key.Trim().ToLowerInvariant())
        {
            case FilterKey:
                if (!TryParseEnum(value, out TextureFilter filter, out error))
                {
                    return false;
                }

                copy.Filter = filter;
                break;

            case AddressUKey:
                if (!TryParseEnum(value, out TextureAddressMode addressU, out error))
                {
                    return false;
                }

                copy.AddressU = addressU;
                break;

            case AddressVKey:
                if (!TryParseEnum(value, out TextureAddressMode addressV, out error))
                {
                    return false;
                }

                copy.AddressV = addressV;
                break;

            case AddressWKey:
                if (!TryParseEnum(value, out TextureAddressMode addressW, out error))
                {
                    return false;
                }

                copy.AddressW = addressW;
                break;

            case MaxAnisotropyKey:
                if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int anisotropy)
                    || anisotropy < MinMaxAnisotropy
                    || anisotropy > MaxMaxAnisotropy)
                {
                    error = $"'{value}' is not a maximum anisotropy between {MinMaxAnisotropy} and {MaxMaxAnisotropy}.";
                    return false;
                }

                copy.MaxAnisotropy = anisotropy;
                break;

            default:
                error = $"'{key}' is not an editable sampler key ({string.Join(", ", EditableKeys)}).";
                return false;
        }

        updated = copy;
        return true;
    }

    /// <summary>A new, unbound sampler state holding every field of <paramref name="source"/> that the asset file stores.</summary>
    public static SamplerState Clone(SamplerState source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new SamplerState
        {
            Filter = source.Filter,
            AddressU = source.AddressU,
            AddressV = source.AddressV,
            AddressW = source.AddressW,
            BorderColor = source.BorderColor,
            MaxAnisotropy = source.MaxAnisotropy,
            MaxMipLevel = source.MaxMipLevel,
            MipMapLevelOfDetailBias = source.MipMapLevelOfDetailBias,
            ComparisonFunction = source.ComparisonFunction,
            FilterMode = source.FilterMode,
        };
    }

    /// <summary>The text of a sampler field as the file stores it (for example <c>Wrap</c> or <c>4</c>).</summary>
    public static string Format(SamplerState sampler, string key)
    {
        ArgumentNullException.ThrowIfNull(sampler);

        return key?.Trim().ToLowerInvariant() switch
        {
            FilterKey => sampler.Filter.ToString(),
            AddressUKey => sampler.AddressU.ToString(),
            AddressVKey => sampler.AddressV.ToString(),
            AddressWKey => sampler.AddressW.ToString(),
            MaxAnisotropyKey => sampler.MaxAnisotropy.ToString(CultureInfo.InvariantCulture),
            _ => null,
        };
    }

    /// <summary>Reads a field of <c>sampler_state</c> from the file on disk, for checking what a save really wrote.</summary>
    public static bool TryReadSamplerValue(string fullPath, string key, out string value)
    {
        value = null;
        try
        {
            var token = JObject.Parse(File.ReadAllText(fullPath))["sampler_state"]?[key?.Trim().ToLowerInvariant() ?? string.Empty];
            if (token == null || token.Type == JTokenType.Null)
            {
                return false;
            }

            value = Convert.ToString(token, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
        {
            return false;
        }
    }

    private static bool TryParseEnum<T>(string value, out T result, out string error) where T : struct, Enum
    {
        error = null;
        string text = value.Trim();

        // A number would parse as an undefined member; only the names are accepted.
        if (!char.IsLetter(text[0]) || !Enum.TryParse(text, true, out result) || !Enum.IsDefined(result))
        {
            result = default;
            error = $"'{value}' is not a {typeof(T).Name} (one of: {string.Join(", ", Enum.GetNames<T>())}).";
            return false;
        }

        return true;
    }
}
