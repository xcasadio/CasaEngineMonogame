using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Audio;
using Xunit;

namespace CasaEngine.Tests.Audio;

public sealed class WavAudioClipLoaderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "casaengine-wavloader-" + Guid.NewGuid().ToString("N"));

    public WavAudioClipLoaderTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Theory]
    [InlineData("step.wav")]
    [InlineData("STEP.WAV")]
    [InlineData(@"C:\project\Audio\menu_screenclick.wav")]
    public void IsFileSupported_AcceptsWav(string fileName)
    {
        Assert.True(new WavAudioClipLoader().IsFileSupported(fileName));
    }

    [Theory]
    [InlineData("music.ogg")]
    [InlineData("music.mp3")]
    [InlineData("texture.png")]
    [InlineData("noextension")]
    [InlineData("")]
    public void IsFileSupported_RejectsEverythingElse(string fileName)
    {
        Assert.False(new WavAudioClipLoader().IsFileSupported(fileName));
    }

    [Fact]
    public void LoadAsset_ReturnsNullInsteadOfThrowingOnAMissingFile()
    {
        var asset = new WavAudioClipLoader().LoadAsset(Path.Combine(_directory, "missing.wav"), null);

        Assert.Null(asset);
    }

    [Fact]
    public void LoadAsset_ReturnsAPcmClipForAValidWav()
    {
        var path = Path.Combine(_directory, "ok.wav");
        File.WriteAllBytes(path, WavBuilder.CreatePcm16(sampleRate: 11025, channelCount: 2, sampleCount: 100));

        var asset = new WavAudioClipLoader().LoadAsset(path, null);

        var clip = Assert.IsType<PcmAudioClip>(asset);
        Assert.Equal(11025, clip.SampleRate);
        Assert.Equal(2, clip.ChannelCount);
        Assert.Equal(100, clip.FrameCount);
        Assert.Equal(200, clip.Samples.Length);
    }

    [Fact]
    public void LoadAsset_ReturnsNullForAnInvalidFileWithoutThrowing()
    {
        var path = Path.Combine(_directory, "broken.wav");
        File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 });

        Assert.Null(new WavAudioClipLoader().LoadAsset(path, null));
    }

    [Fact]
    public void LoadAsset_ReturnsNullForAnUnsupportedEncoding()
    {
        var path = Path.Combine(_directory, "adpcm.wav");
        File.WriteAllBytes(path, WavBuilder.Create(2, 22050, 1, 4, new byte[16]));

        Assert.Null(new WavAudioClipLoader().LoadAsset(path, null));
    }
}
