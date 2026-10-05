using CasaEngine.Framework.Audio.Effects;

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

    /// <summary>Attaches <see cref="MixerCommand.Spu"/> as the pulled SPU source (replacing any).</summary>
    AttachPsxSpu,

    /// <summary>Detaches <see cref="MixerCommand.Spu"/> if it is the attached source.</summary>
    DetachPsxSpu,

    /// <summary>Creates bus <see cref="MixerCommand.Bus"/> as a child of <see cref="MixerCommand.ParentBus"/>.</summary>
    CreateBus,

    /// <summary>Routes the attached <see cref="MixerCommand.Spu"/> to bus <see cref="MixerCommand.Bus"/>.</summary>
    RoutePsxSpu,

    /// <summary>Ramps the volume of a voice to <see cref="MixerCommand.Volume"/> over <see cref="MixerCommand.Frames"/> frames.</summary>
    RampVoice,

    /// <summary>Stops the volume ramp of a voice at its current value.</summary>
    FreezeVoice,

    /// <summary>Ramps the own gain of bus <see cref="MixerCommand.Bus"/> to <see cref="MixerCommand.Volume"/> over <see cref="MixerCommand.Frames"/> frames.</summary>
    RampBus,

    /// <summary>Stops the gain ramp of bus <see cref="MixerCommand.Bus"/> at its current value.</summary>
    FreezeBus,

    /// <summary>Appends <see cref="MixerCommand.Effect"/> to the insert effects of bus <see cref="MixerCommand.Bus"/>.</summary>
    AddEffect,

    /// <summary>Removes <see cref="MixerCommand.Effect"/> from the insert effects of bus <see cref="MixerCommand.Bus"/>.</summary>
    RemoveEffect,

    /// <summary>Sets the send of bus <see cref="MixerCommand.Bus"/> to bus <see cref="MixerCommand.ParentBus"/> at level <see cref="MixerCommand.Volume"/> (0 removes it).</summary>
    SetSend,

    /// <summary>Sets <see cref="MixerCommand.Effect"/> as the limiter of the Master output (null removes it).</summary>
    SetMasterLimiter,
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
    public PsxSpuSource Spu;
    public AudioEffect Effect;

    /// <summary><see cref="MixerCommandKind.AddEffect"/>: audio-side memory of the effect, built on the producer thread.</summary>
    public object EffectState;

    /// <summary>Bus index: the bus a voice starts on, the bus to create, or the bus the SPU is routed to.</summary>
    public int Bus;

    /// <summary><see cref="MixerCommandKind.CreateBus"/>: index of the parent bus. <see cref="MixerCommandKind.SetSend"/>: index of the target bus.</summary>
    public int ParentBus;

    /// <summary>
    /// <see cref="MixerCommandKind.StartResident"/>: the mono voice uses explicit gains (no pan law).
    /// <see cref="MixerCommandKind.SetStereoGains"/>: unused, the gains are always explicit.
    /// </summary>
    public bool ExplicitGains;

    public float LeftGain;
    public float RightGain;

    /// <summary><see cref="MixerCommandKind.RampVoice"/> and <see cref="MixerCommandKind.RampBus"/>: length of the ramp in output frames.</summary>
    public int Frames;
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
