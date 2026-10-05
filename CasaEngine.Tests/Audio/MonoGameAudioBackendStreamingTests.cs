using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using Xunit;

namespace CasaEngine.Tests.Audio;

/// <summary>
/// Runs on the real MonoGame backend rather than the fake: a sample rate outside what
/// <c>DynamicSoundEffectInstance</c> accepts is refused before any OpenAL call, so no audio device
/// is needed. Before the check, such a rate reached MonoGame, which threw
/// <see cref="ArgumentOutOfRangeException"/> (or, with no device, disabled the audio) and the voice
/// slot taken for the stream was never given back.
/// </summary>
public class MonoGameAudioBackendStreamingTests
{
    [Theory]
    [InlineData(MonoGameAudioBackend.MinStreamingSampleRate - 1)]
    [InlineData(MonoGameAudioBackend.MaxStreamingSampleRate + 1)]
    [InlineData(6000)]
    [InlineData(96000)]
    public void CreateStreamingVoice_RefusesASampleRateMonoGameCannotStream(int sampleRate)
    {
        using var backend = new MonoGameAudioBackend(voiceCapacity: 1);

        // More attempts than voices: a refusal that kept its slot would show up as a lost voice.
        for (var i = 0; i < 3; i++)
        {
            var voice = backend.CreateStreamingVoice(sampleRate, 2, AudioVoiceParameters.Default);

            Assert.False(voice.IsValid);
        }

        Assert.True(backend.IsAvailable);
        Assert.Equal(0, backend.ActiveVoiceCount);
        Assert.Equal(backend.VoiceCapacity, backend.FreeVoiceCount);
    }
}
