using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets.Sprites;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Assets;

/// <summary>
/// The optional <c>psx_semi_transparency</c> field of a <c>.sprite</c> (ADR-0051): absent means
/// <see cref="SpritePsxSemiTransparency.None"/>, and <c>None</c> is never written, so every existing sprite file keeps
/// its bytes.
/// </summary>
public class SpriteDataPsxSemiTransparencyTests
{
    private const string Key = "psx_semi_transparency";

    private static SpriteData NewSpriteData(SpritePsxSemiTransparency mode)
    {
        return new SpriteData(Guid.Parse("11111111-2222-3333-4444-555555555555"))
        {
            Name = "sprite_1",
            SpriteSheetAssetId = Guid.Parse("66666666-7777-8888-9999-000000000000"),
            PositionInTexture = new Rectangle(1, 2, 3, 4),
            Origin = new Point(1, 2),
            PsxSemiTransparency = mode,
        };
    }

    [Fact]
    public void None_IsNeverWritten()
    {
        Assert.True(EditorAssetJsonSerializer.TrySerialize(NewSpriteData(SpritePsxSemiTransparency.None), out var document));

        Assert.False(document.ContainsKey(Key));
    }

    [Theory]
    [InlineData(SpritePsxSemiTransparency.Mode0, "Mode0")]
    [InlineData(SpritePsxSemiTransparency.Mode1, "Mode1")]
    [InlineData(SpritePsxSemiTransparency.Mode2, "Mode2")]
    [InlineData(SpritePsxSemiTransparency.Mode3, "Mode3")]
    public void Mode_IsWrittenByName_AndReadBack(SpritePsxSemiTransparency mode, string expectedName)
    {
        Assert.True(EditorAssetJsonSerializer.TrySerialize(NewSpriteData(mode), out var document));
        Assert.Equal(expectedName, document[Key]?.Value<string>());

        var loaded = new SpriteData();
        loaded.Load(document);

        Assert.Equal(mode, loaded.PsxSemiTransparency);
    }

    [Fact]
    public void AbsentField_ReadsAsNone()
    {
        Assert.True(EditorAssetJsonSerializer.TrySerialize(NewSpriteData(SpritePsxSemiTransparency.Mode1), out var document));
        document.Remove(Key);

        var loaded = new SpriteData();
        loaded.Load(document);

        Assert.Equal(SpritePsxSemiTransparency.None, loaded.PsxSemiTransparency);
    }

    [Fact]
    public void UnknownName_ReadsAsNone()
    {
        Assert.True(EditorAssetJsonSerializer.TrySerialize(NewSpriteData(SpritePsxSemiTransparency.Mode1), out var document));
        document[Key] = "Mode9";

        var loaded = new SpriteData();
        loaded.Load(document);

        Assert.Equal(SpritePsxSemiTransparency.None, loaded.PsxSemiTransparency);
    }
}
