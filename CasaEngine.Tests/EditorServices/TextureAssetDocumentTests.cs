using CasaEngine.Core.Serialization;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.Textures;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

/// <summary>The sampler edits of the <c>.texture</c> document (plan P7): load, change one field, serialize, read back.</summary>
public sealed class TextureAssetDocumentTests : IDisposable
{
    private const string TextureJson = """
        {
          "id": "0e470a8a-1582-5839-9a0c-96d2adc604eb",
          "name": "Ui.Menu.ButtonsTexture",
          "texture_asset_id": "a4f6260f-45cc-58b4-9d69-de59ac8e43ef",
          "sampler_state": {
            "texture_filter": "Linear",
            "address_u": "Clamp",
            "address_v": "Clamp",
            "address_w": "Clamp",
            "border_color": { "r": 10, "g": 20, "b": 30, "a": 255 },
            "max_anisotropy": 4,
            "max_mip_level": 3,
            "mip_map_level_of_detail_bias": 0.5,
            "comparison_function": "Less",
            "filter_mode": "Comparison"
          }
        }
        """;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "casa-texture-document-" + Guid.NewGuid().ToString("N"));

    public TextureAssetDocumentTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // A temp folder left behind is harmless.
        }
    }

    [Fact]
    public void Load_ReadsTheImageAssetIdAndTheSamplerState()
    {
        TextureAssetDocument document = Load();

        Assert.Equal(Guid.Parse("a4f6260f-45cc-58b4-9d69-de59ac8e43ef"), document.ImageAssetId);
        Assert.Equal(TextureFilter.Linear, document.Sampler.Filter);
        Assert.Equal(TextureAddressMode.Clamp, document.Sampler.AddressU);
        Assert.Equal(4, document.Sampler.MaxAnisotropy);
    }

    [Fact]
    public void RoundTrip_ChangesTheEditedFieldsAndKeepsTheOthers()
    {
        TextureAssetDocument document = Load();
        SamplerState before = document.Sampler;

        Assert.True(document.TryApply("address_u", "Wrap", out string error), error);
        Assert.True(document.TryApply("texture_filter", "point", out error), error);
        Assert.True(document.TryApply("max_anisotropy", "8", out error), error);

        Assert.True(EditorAssetJsonSerializer.TrySerialize(document.Texture, out JObject root));
        SamplerState after = root["sampler_state"].GetSamplerState();

        Assert.Equal(TextureAddressMode.Wrap, after.AddressU);
        Assert.Equal(TextureFilter.Point, after.Filter);
        Assert.Equal(8, after.MaxAnisotropy);

        // Everything else the file holds is unchanged.
        Assert.Equal(TextureAddressMode.Clamp, after.AddressV);
        Assert.Equal(TextureAddressMode.Clamp, after.AddressW);
        Assert.Equal(new Color(10, 20, 30, 255), after.BorderColor);
        Assert.Equal(3, after.MaxMipLevel);
        Assert.Equal(0.5f, after.MipMapLevelOfDetailBias);
        Assert.Equal(CompareFunction.Less, after.ComparisonFunction);
        Assert.Equal(TextureFilterMode.Comparison, after.FilterMode);
        Assert.Equal("a4f6260f-45cc-58b4-9d69-de59ac8e43ef", root["texture_asset_id"].GetGuid().ToString());
        Assert.Equal("0e470a8a-1582-5839-9a0c-96d2adc604eb", root["id"].ToString());

        // The sampler state that was there is a different instance and still holds its old values.
        Assert.NotSame(before, document.Sampler);
        Assert.Equal(TextureAddressMode.Clamp, before.AddressU);
        Assert.Equal(TextureFilter.Linear, before.Filter);
        Assert.Equal(4, before.MaxAnisotropy);
    }

    [Fact]
    public void TryApply_NeverTouchesTheSourceSamplerState()
    {
        SamplerState shared = SamplerState.AnisotropicWrap;
        TextureAddressMode addressBefore = shared.AddressV;

        Assert.True(TextureAssetDocument.TryApply(shared, "address_v", "Mirror", out SamplerState updated, out _));

        Assert.NotSame(shared, updated);
        Assert.Equal(addressBefore, shared.AddressV);
        Assert.Equal(TextureAddressMode.Mirror, updated.AddressV);
        Assert.Equal(shared.Filter, updated.Filter);
        Assert.Equal(shared.AddressU, updated.AddressU);
        Assert.Equal(shared.MaxAnisotropy, updated.MaxAnisotropy);
    }

    [Theory]
    [InlineData("address_u", "wrap", "Wrap")]
    [InlineData("ADDRESS_V", "MIRROR", "Mirror")]
    [InlineData("address_w", " border ", "Border")]
    [InlineData("texture_filter", "anisotropic", "Anisotropic")]
    [InlineData("max_anisotropy", "16", "16")]
    public void TryApply_ParsesKeysAndValuesWithoutRegardToCase(string key, string value, string expected)
    {
        TextureAssetDocument document = Load();

        Assert.True(document.TryApply(key, value, out string error), error);
        Assert.Equal(expected, TextureAssetDocument.Format(document.Sampler, key));
    }

    [Theory]
    [InlineData("address_u", "Bogus")]
    [InlineData("address_u", "1")]
    [InlineData("address_u", "99")]
    [InlineData("texture_filter", "Sharp")]
    [InlineData("max_anisotropy", "0")]
    [InlineData("max_anisotropy", "17")]
    [InlineData("max_anisotropy", "many")]
    [InlineData("border_color", "Wrap")]
    [InlineData("unknown_key", "Wrap")]
    [InlineData("address_u", "")]
    [InlineData("", "Wrap")]
    public void TryApply_RefusesAnInvalidKeyOrValueAndChangesNothing(string key, string value)
    {
        TextureAssetDocument document = Load();
        SamplerState before = document.Sampler;

        Assert.False(document.TryApply(key, value, out string error));
        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.Same(before, document.Sampler);
        Assert.Equal(TextureAddressMode.Clamp, document.Sampler.AddressU);
        Assert.Equal(4, document.Sampler.MaxAnisotropy);
    }

    [Fact]
    public void TryReadSamplerValue_ReadsTheValueFromTheFileOnDisk()
    {
        string path = WriteFile(TextureJson);

        Assert.True(TextureAssetDocument.TryReadSamplerValue(path, "address_u", out string value));
        Assert.Equal("Clamp", value);
        Assert.True(TextureAssetDocument.TryReadSamplerValue(path, "max_anisotropy", out value));
        Assert.Equal("4", value);
        Assert.False(TextureAssetDocument.TryReadSamplerValue(path, "missing", out _));
    }

    [Fact]
    public void TryLoad_ReportsAFileThatIsNotATexture()
    {
        string path = WriteFile("not json");

        Assert.False(TextureAssetDocument.TryLoad(path, out TextureAssetDocument document, out string error));
        Assert.Null(document);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    private TextureAssetDocument Load()
    {
        string path = WriteFile(TextureJson);
        Assert.True(TextureAssetDocument.TryLoad(path, out TextureAssetDocument document, out string error), error);
        return document;
    }

    private string WriteFile(string content)
    {
        string path = Path.Combine(_folder, Guid.NewGuid().ToString("N") + ".texture");
        File.WriteAllText(path, content);
        return path;
    }
}
