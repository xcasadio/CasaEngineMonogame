namespace CasaEngine.Framework.Audio.Output.OpenAl;

/// <summary>
/// The handful of OpenAL source/buffer operations the refill loop needs, so the loop decisions can be
/// tested without a device. All calls happen on the audio thread.
/// </summary>
internal interface IOpenAlStream
{
    /// <summary>Number of queued buffers the device has finished with (AL_BUFFERS_PROCESSED).</summary>
    int GetProcessedBufferCount();

    /// <summary>Removes the oldest processed buffer from the queue and returns its handle.</summary>
    uint UnqueueBuffer();

    /// <summary>Uploads <paramref name="frameCount"/> stereo frames into <paramref name="buffer"/> and queues it.</summary>
    void FillAndQueueBuffer(uint buffer, float[] interleavedStereo, int frameCount);

    /// <summary>True when the source is in the AL_STOPPED state.</summary>
    bool IsSourceStopped();

    void PlaySource();
}
