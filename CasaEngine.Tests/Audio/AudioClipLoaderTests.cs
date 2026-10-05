using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Audio;
using Xunit;

namespace CasaEngine.Tests.Audio;

public sealed class AudioClipLoaderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "casaengine-audioloader-" + Guid.NewGuid().ToString("N"));

    public AudioClipLoaderTests()
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
    [InlineData("music.ogg")]
    [InlineData("MUSIC.OGG")]
    public void IsFileSupported_AcceptsWavAndOgg(string fileName)
    {
        Assert.True(new AudioClipLoader().IsFileSupported(fileName));
    }

    [Theory]
    [InlineData("music.mp3")]
    [InlineData("music.flac")]
    [InlineData("texture.png")]
    [InlineData("noextension")]
    [InlineData("")]
    public void IsFileSupported_RejectsEverythingElse(string fileName)
    {
        Assert.False(new AudioClipLoader().IsFileSupported(fileName));
        Assert.False(AudioClipLoader.IsSoundFile(fileName));
    }

    [Fact]
    public void LoadAsset_ReturnsNullInsteadOfThrowingOnAMissingFile()
    {
        var asset = new AudioClipLoader().LoadAsset(Path.Combine(_directory, "missing.wav"), null);

        Assert.Null(asset);
    }

    [Fact]
    public void LoadAsset_ReturnsAPcmClipForAValidWav()
    {
        var path = Path.Combine(_directory, "ok.wav");
        File.WriteAllBytes(path, WavBuilder.CreatePcm16(sampleRate: 11025, channelCount: 2, sampleCount: 100));

        var asset = new AudioClipLoader().LoadAsset(path, null);

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

        Assert.Null(new AudioClipLoader().LoadAsset(path, null));
    }

    [Fact]
    public void LoadAsset_ReturnsNullForAnUnsupportedEncoding()
    {
        var path = Path.Combine(_directory, "mp3.wav");
        File.WriteAllBytes(path, WavBuilder.Create(0x55, 22050, 1, 4, new byte[16]));

        Assert.Null(new AudioClipLoader().LoadAsset(path, null));
    }

    [Fact]
    public void LoadAsset_ReturnsAPcmClipForAnOggFile()
    {
        var asset = new AudioClipLoader().LoadAsset(AudioFixtures.GetPath(AudioFixtures.StereoOgg44100), null);

        var clip = Assert.IsType<PcmAudioClip>(asset);
        Assert.Equal(44100, clip.SampleRate);
        Assert.Equal(2, clip.ChannelCount);
        Assert.InRange(clip.FrameCount, 22050 - 2048, 22050 + 2048);
    }

    [Fact]
    public void LoadAsset_PicksTheDecoderByExtensionCaseInsensitively()
    {
        var path = Path.Combine(_directory, "UPPER.OGG");
        File.Copy(AudioFixtures.GetPath(AudioFixtures.MonoOgg22050), path);

        var clip = Assert.IsType<PcmAudioClip>(new AudioClipLoader().LoadAsset(path, null));

        Assert.Equal(22050, clip.SampleRate);
        Assert.Equal(1, clip.ChannelCount);
    }

    [Fact]
    public void LoadAsset_ReturnsNullForACorruptOggWithoutThrowing()
    {
        var path = Path.Combine(_directory, "broken.ogg");
        File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 });

        Assert.Null(new AudioClipLoader().LoadAsset(path, null));
    }

    [Fact]
    public void LoadAsset_ReturnsNullForAMissingOgg()
    {
        Assert.Null(new AudioClipLoader().LoadAsset(Path.Combine(_directory, "missing.ogg"), null));
    }

    [Fact]
    public void LoadAsset_ReturnsNullForAnUnknownExtension()
    {
        var path = Path.Combine(_directory, "music.mp3");
        File.Copy(AudioFixtures.GetPath(AudioFixtures.MonoOgg22050), path);

        // Not a supported extension: it falls to the wav decoder, which rejects the content.
        Assert.Null(new AudioClipLoader().LoadAsset(path, null));
    }
}
