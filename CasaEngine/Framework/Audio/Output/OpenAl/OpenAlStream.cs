namespace CasaEngine.Framework.Audio.Output.OpenAl;

/// <summary>Real <see cref="IOpenAlStream"/> over one OpenAL source; checks alGetError after each call group.</summary>
internal sealed class OpenAlStream : IOpenAlStream
{
    private readonly uint _source;
    private readonly int _sampleRate;

    public OpenAlStream(uint source, int sampleRate)
    {
        _source = source;
        _sampleRate = sampleRate;
    }

    public int GetProcessedBufferCount()
    {
        OpenAlNative.AlGetSourcei(_source, OpenAlNative.AlBuffersProcessed, out var processed);
        ThrowOnError("alGetSourcei(AL_BUFFERS_PROCESSED)");
        return processed;
    }

    public uint UnqueueBuffer()
    {
        OpenAlNative.AlSourceUnqueueBuffers(_source, 1, out var buffer);
        ThrowOnError("alSourceUnqueueBuffers");
        return buffer;
    }

    public void FillAndQueueBuffer(uint buffer, float[] interleavedStereo, int frameCount)
    {
        OpenAlNative.AlBufferData(buffer, OpenAlNative.AlFormatStereoFloat32, interleavedStereo,
            frameCount * 2 * sizeof(float), _sampleRate);
        ThrowOnError("alBufferData");
        OpenAlNative.AlSourceQueueBuffers(_source, 1, ref buffer);
        ThrowOnError("alSourceQueueBuffers");
    }

    public bool IsSourceStopped()
    {
        OpenAlNative.AlGetSourcei(_source, OpenAlNative.AlSourceState, out var state);
        ThrowOnError("alGetSourcei(AL_SOURCE_STATE)");
        return state == OpenAlNative.AlStopped;
    }

    public void PlaySource()
    {
        OpenAlNative.AlSourcePlay(_source);
        ThrowOnError("alSourcePlay");
    }

    private static void ThrowOnError(string call)
    {
        var error = OpenAlNative.AlGetError();
        if (error != OpenAlNative.AlNoError)
        {
            throw new InvalidOperationException($"OpenAL error 0x{error:X} after {call}.");
        }
    }
}
