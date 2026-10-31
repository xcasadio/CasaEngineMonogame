using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using Xunit;

namespace CasaEngine.Tests.UI;

/// <summary>
/// The checks a data-driven screen runs against the project's catalogue (ADR-0078): a target is found by name or id,
/// and it must be of the kind the action needs. The catalogue is static, so this class runs in
/// <see cref="ProjectEnvironmentCollection"/>.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class ScreenAssetLookupTests : IDisposable
{
    private static readonly Guid ScreenId = Guid.Parse("11111111-aaaa-4aaa-8aaa-111111111111");
    private static readonly Guid WorldId = Guid.Parse("22222222-aaaa-4aaa-8aaa-222222222222");
    private static readonly Guid SoundId = Guid.Parse("33333333-aaaa-4aaa-8aaa-333333333333");
    private static readonly Guid FontId = Guid.Parse("44444444-aaaa-4aaa-8aaa-444444444444");

    public ScreenAssetLookupTests()
    {
        AssetCatalog.ClearInternal();
        AssetCatalog.AddInternal(new AssetInfo(ScreenId) { Name = "Menu", FileName = "menu.uiscreen", AssetType = "uiscreen" });
        AssetCatalog.AddInternal(new AssetInfo(WorldId) { Name = "Level", FileName = "level.world", AssetType = "world" });
        AssetCatalog.AddInternal(new AssetInfo(SoundId) { Name = "Click", FileName = "click.sound", AssetType = "sound" });
        AssetCatalog.AddInternal(new AssetInfo(FontId) { Name = "Block", FileName = "block.fnt", AssetType = "fnt" });
    }

    public void Dispose() => AssetCatalog.ClearInternal();

    [Fact]
    public void AScreen_IsFoundByNameAndById()
    {
        Assert.True(GameManager.IsScreenAsset("Menu"));
        Assert.True(GameManager.IsScreenAsset(ScreenId.ToString()));
    }

    [Theory]
    [InlineData("Nope")]
    [InlineData("Click")]
    [InlineData("33333333-aaaa-4aaa-8aaa-333333333333")]
    [InlineData("Level")]
    public void ATargetThatIsNotAScreen_IsRefused(string target)
    {
        Assert.False(GameManager.IsScreenAsset(target));
    }

    [Fact]
    public void AWorld_IsFoundByItsFile()
    {
        Assert.True(GameManager.IsWorldAsset("level.world"));
    }

    [Theory]
    [InlineData("nope.world")]
    [InlineData("menu.uiscreen")]
    [InlineData("Level")]
    public void ATargetThatIsNotAWorldFile_IsRefused(string target)
    {
        Assert.False(GameManager.IsWorldAsset(target));
    }

    [Fact]
    public void ASound_IsFoundByNameAndById()
    {
        Assert.Equal(SoundId, GameManager.FindSoundAsset("Click").Id);
        Assert.Equal(SoundId, GameManager.FindSoundAsset(SoundId.ToString()).Id);
    }

    [Theory]
    [InlineData("Nope")]
    [InlineData("Menu")]
    [InlineData("Level")]
    [InlineData("11111111-aaaa-4aaa-8aaa-111111111111")]
    public void ATargetThatIsNotASound_IsRefused_AndNamed(string target)
    {
        var error = Assert.Throws<InvalidOperationException>(() => GameManager.FindSoundAsset(target));

        Assert.Contains(target, error.Message);
    }

    [Fact]
    public void AFont_IsFoundByNameAndById()
    {
        Assert.Equal(FontId, GameManager.FindFontAsset("Block").Id);
        Assert.Equal(FontId, GameManager.FindFontAsset(FontId.ToString()).Id);
    }

    [Theory]
    [InlineData("Nope")]
    [InlineData("")]
    [InlineData("Menu")]
    [InlineData("11111111-aaaa-4aaa-8aaa-111111111111")]
    public void AFontThatIsNotOne_IsRefused(string target)
    {
        var error = Assert.Throws<InvalidOperationException>(() => GameManager.FindFontAsset(target));

        Assert.Contains($"'{target}'", error.Message);
    }
}
