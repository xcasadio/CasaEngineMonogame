using System.Runtime.InteropServices;

namespace CasaEngine.Framework.Audio.Output.OpenAl;

/// <summary>
/// Minimal hand written OpenAL Soft binding (subset used by <see cref="OpenAlAudioOutput"/>).
/// Library: the "openal" binary shipped by MonoGame.Library.OpenAL 1.24.3.4 (OpenAL Soft 1.24.3).
/// All values below are taken from the 1.24.3 headers:
/// https://raw.githubusercontent.com/kcat/openal-soft/1.24.3/include/AL/al.h ,
/// .../alc.h and .../alext.h (line numbers refer to those files at that tag).
/// Only blittable parameter types are used, so no call allocates.
/// </summary>
internal static class OpenAlNative
{
    // A single place to change the library name (for example "__Internal" on iOS later).
    public const string Library = "openal";

    // al.h:115 / :118
    public const int AlFalse = 0;
    public const int AlTrue = 1;

    // al.h:405
    public const int AlNoError = 0;

    // al.h:277 AL_SOURCE_STATE, :282 AL_PLAYING, :283 AL_STOPPED
    public const int AlSourceState = 0x1010;
    public const int AlPlaying = 0x1012;
    public const int AlStopped = 0x1014;

    // al.h:292 AL_BUFFERS_QUEUED, :304 AL_BUFFERS_PROCESSED
    public const int AlBuffersQueued = 0x1015;
    public const int AlBuffersProcessed = 0x1016;

    // alext.h:56 AL_FORMAT_STEREO_FLOAT32 (extension AL_EXT_float32, alext.h:54)
    public const int AlFormatStereoFloat32 = 0x10011;

    // alext.h:283 AL_DIRECT_CHANNELS_SOFT (extension AL_SOFT_direct_channels, alext.h:282)
    public const int AlDirectChannelsSoft = 0x1033;

    // alc.h:134 ALC_NO_ERROR, :119 ALC_FREQUENCY
    public const int AlcNoError = 0;
    public const int AlcFrequency = 0x1007;

    // Extension names: alext.h:164 (ALC_EXT_thread_local_context), alext.h:54 (AL_EXT_float32),
    // alext.h:282 (AL_SOFT_direct_channels).
    public const string AlcExtThreadLocalContext = "ALC_EXT_thread_local_context";
    public const string AlExtFloat32 = "AL_EXT_float32";
    public const string AlSoftDirectChannels = "AL_SOFT_direct_channels";

    // alc.h:227
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "alcOpenDevice")]
    public static extern IntPtr AlcOpenDevice([MarshalAs(UnmanagedType.LPStr)] string? deviceName);

    // alc.h:229
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "alcCloseDevice")]
    public static extern byte AlcCloseDevice(IntPtr device);

    // alc.h:207 (attribute list: pass IntPtr.Zero)
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "alcCreateContext")]
    public static extern IntPtr AlcCreateContext(IntPtr device, IntPtr attributes);

    // alc.h:218
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "alcDestroyContext")]
    public static extern void AlcDestroyContext(IntPtr context);

    // alext.h:168
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "alcSetThreadContext")]
    public static extern byte AlcSetThreadContext(IntPtr context);

    // alc.h:242
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "alcIsExtensionPresent")]
    public static extern byte AlcIsExtensionPresent(IntPtr device, [MarshalAs(UnmanagedType.LPStr)] string extensionName);

    // alc.h:259 (called with size 1: one value)
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "alcGetIntegerv")]
    public static extern void AlcGetIntegerv(IntPtr device, int parameter, int size, out int value);

    // alc.h:234
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "alcGetError")]
    public static extern int AlcGetError(IntPtr device);

    // al.h:518
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "alIsExtensionPresent")]
    public static extern byte AlIsExtensionPresent([MarshalAs(UnmanagedType.LPStr)] string extensionName);

    // al.h:515
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "alGetError")]
    public static extern int AlGetError();

    // al.h:548 (one source per call)
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "alGenSources")]
    public static extern void AlGenSources(int count, out uint source);

    // al.h:550 (one source per call)
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "alDeleteSources")]
    public static extern void AlDeleteSources(int count, ref uint source);

    // al.h:596
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "alGenBuffers")]
    public static extern void AlGenBuffers(int count, uint[] buffers);

    // al.h:598
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "alDeleteBuffers")]
    public static extern void AlDeleteBuffers(int count, uint[] buffers);

    // al.h:606 (size is in bytes)
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "alBufferData")]
    public static extern void AlBufferData(uint buffer, int format, float[] data, int sizeInBytes, int sampleRate);

    // al.h:558
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "alSourcei")]
    public static extern void AlSourcei(uint source, int parameter, int value);

    // al.h:566
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "alGetSourcei")]
    public static extern void AlGetSourcei(uint source, int parameter, out int value);

    // al.h:590 (one buffer per call)
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "alSourceQueueBuffers")]
    public static extern void AlSourceQueueBuffers(uint source, int count, ref uint buffer);

    // al.h:592 (one buffer per call)
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "alSourceUnqueueBuffers")]
    public static extern void AlSourceUnqueueBuffers(uint source, int count, out uint buffer);

    // al.h:572
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "alSourcePlay")]
    public static extern void AlSourcePlay(uint source);

    // al.h:574
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "alSourceStop")]
    public static extern void AlSourceStop(uint source);
}
