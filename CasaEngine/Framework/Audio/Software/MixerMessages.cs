namespace CasaEngine.Framework.Audio.Software;

internal enum MixerCommandKind
{
    None = 0,
    StartResident,
    CreateStreaming,
    StartStreaming,
    SetParameters,
    SetVolume,
    Pause,
    Resume,
    Stop,
    SubmitChunk,
    StopAll,
    SetStereoGains,
}

/// <summary>
/// Command from the producer (game) thread to the render thread. A plain value type: no
/// allocation to enqueue one. Only the fields used by <see cref="Kind"/> are read.
/// </summary>
internal struct MixerCommand
{
    public MixerCommandKind Kind;

    /// <summary>Voice slot, chosen by the caller.</summary>
    public int Slot;

    /// <summary>
    /// Caller supplied generation of the slot. <see cref="MixerCommandKind.StartResident"/> and
    /// <see cref="MixerCommandKind.CreateStreaming"/> establish it; every other command is ignored
    /// when it does not match the voice currently in the slot.
    /// </summary>
    public int Generation;

    public PcmAudioClip Clip;
    public AudioVoiceParameters Parameters;
    public int Channels;
    public int SampleRate;
    public float Volume;
    public SampleChunk Chunk;

    /// <summary>
    /// <see cref="MixerCommandKind.StartResident"/>: the mono voice uses explicit gains (no pan law).
    /// <see cref="MixerCommandKind.SetStereoGains"/>: unused, the gains are always explicit.
    /// </summary>
    public bool ExplicitGains;

    public float LeftGain;
    public float RightGain;
}

internal enum MixerEventKind
{
    None = 0,

    /// <summary>A resident, non-looped voice reached the end of its clip.</summary>
    VoiceEnded,

    /// <summary>A streaming voice fully consumed a submitted buffer.</summary>
    BufferConsumed,
}

/// <summary>Event from the render thread to the producer thread.</summary>
internal struct MixerEvent
{
    public MixerEventKind Kind;
    public int Slot;
    public int Generation;

    /// <summary>For <see cref="MixerEventKind.BufferConsumed"/>: the sequence given at submit.</summary>
    public int Sequence;
}
