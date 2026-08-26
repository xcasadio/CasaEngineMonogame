using CasaEngine.Framework.Assets.Loaders;
using Xunit;

namespace CasaEngine.Tests.Audio;

public class SoundEffectLoaderTests
{
    [Theory]
    [InlineData("step.wav")]
    [InlineData("STEP.WAV")]
    [InlineData(@"C:\project\Audio\menu_screenclick.wav")]
    public void IsFileSupported_AcceptsWav(string fileName)
    {
        Assert.True(new SoundEffectLoader().IsFileSupported(fileName));
    }

    [Theory]
    [InlineData("music.ogg")]
    [InlineData("music.mp3")]
    [InlineData("texture.png")]
    [InlineData("noextension")]
    [InlineData("")]
    public void IsFileSupported_RejectsEverythingElse(string fileName)
    {
        // MonoGame DesktopGL only decodes RIFF wav for a resident sound: no mp3 at all, and ogg
        // only through the music streaming path.
        Assert.False(new SoundEffectLoader().IsFileSupported(fileName));
    }

    [Fact]
    public void LoadAsset_ReturnsNullInsteadOfThrowingOnAMissingFile()
    {
        var loader = new SoundEffectLoader();

        var asset = loader.LoadAsset(Path.Combine(Path.GetTempPath(), "casaengine-missing-sound.wav"), null);

        Assert.Null(asset);
    }
}
