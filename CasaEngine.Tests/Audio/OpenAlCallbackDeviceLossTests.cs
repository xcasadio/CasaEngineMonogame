using System.Diagnostics;
using System.Runtime.InteropServices;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Output;
using CasaEngine.Framework.Audio.Output.OpenAl;
using Xunit;

namespace CasaEngine.Tests.Audio;

/// <summary>
/// <see cref="IAudioOutput"/> with no device that plays what <see cref="OpenAlAudioOutput"/> does in callback mode
/// (ADR-0094) through <see cref="OpenAlCallbackFeeder"/>: a thread stands for OpenAL Soft's mixing thread and calls the
/// feeder. <see cref="Disconnect"/> stops these calls, as a device loss does, and the same thread then drains the mixer
/// as the audio thread of the real output does.
/// </summary>
internal sealed class CallbackModeAudioOutput : IAudioOutput
{
    private const int RequestFrames = 480;

    private readonly ManualResetEventSlim _stop = new(false);
    private OpenAlCallbackFeeder _feeder;
    private Thread _thread;
    private volatile bool _disconnected;
    private volatile bool _available;
    private int _callbackCount;
    private long _drainedFrames;

    public bool IsAvailable => _available;

    public int SampleRate => 48000;

    public int BufferFrames => 480;

    // Callback mode: nothing is queued.
    public int BufferCount => 0;

    public int UnderrunCount => _feeder?.UnderrunCount ?? 0;

    public int CallbackCount => Volatile.Read(ref _callbackCount);

    public long DrainedFrames => Interlocked.Read(ref _drainedFrames);

    public bool TryOpen()
    {
        _available = true;
        return true;
    }

    public void Start(AudioRenderCallback callback)
    {
        _feeder = new OpenAlCallbackFeeder(callback, SampleRate, BufferFrames, Stopwatch.Frequency, Stopwatch.GetTimestamp);
        _thread = new Thread(Run) { IsBackground = true, Name = "Callback mode test output" };
        _thread.Start();
    }

    /// <summary>Simulates the device loss: no callback arrives any more, the drain takes over.</summary>
    public void Disconnect()
    {
        _disconnected = true;
    }

    public void Dispose()
    {
        _available = false;
        _stop.Set();
        _thread?.Join(10_000);
    }

    private void Run()
    {
        var memory = Marshal.AllocHGlobal(RequestFrames * OpenAlCallbackFeeder.BytesPerFrame);
        try
        {
            var draining = false;
            while (!_stop.IsSet)
            {
                if (!_disconnected)
                {
                    _feeder.Fill(memory, RequestFrames * OpenAlCallbackFeeder.BytesPerFrame);
                    Interlocked.Increment(ref _callbackCount);
                    _stop.Wait(10);
                    continue;
                }

                if (!draining)
                {
                    draining = true;
                    _feeder.StartDrain(Stopwatch.GetTimestamp());
                }

                Interlocked.Add(ref _drainedFrames, _feeder.Drain());
                // Passes much closer together than the 2 ms of the real thread, so a call of the game that finds the
                // ring full never waits for the drain on a coarse timer.
                Thread.Yield();
            }
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }
}

public class OpenAlCallbackDeviceLossTests
{
    private const int RingCapacity = 4096;
    private const double MaxCallMilliseconds = 5.0;

    private static void WaitUntil(Func<bool> condition)
    {
        var deadline = Stopwatch.GetTimestamp() + 20 * Stopwatch.Frequency;
        while (!condition())
        {
            Assert.True(Stopwatch.GetTimestamp() < deadline, "The condition was not met within 20 s.");
            Thread.Sleep(5);
        }
    }

    [Fact]
    public void After_the_device_is_lost_game_commands_beyond_the_ring_capacity_do_not_wait_and_the_backend_stays_available()
    {
        var output = new CallbackModeAudioOutput();
        using var backend = new SoftwareAudioBackend(output, 16);
        Assert.True(backend.IsAvailable);
        Assert.Equal(0, backend.LeadMilliseconds);

        var clip = new PcmAudioClip(new short[48000 * 30], 48000, 1);
        var voice = backend.Play(clip, AudioVoiceParameters.Default);
        Assert.NotEqual(AudioVoiceHandle.None, voice);
        WaitUntil(() => output.CallbackCount >= 3);

        output.Disconnect();
        WaitUntil(() => output.DrainedFrames > 0);

        var slowestMilliseconds = 0.0;
        var parameters = AudioVoiceParameters.Default;
        for (var i = 0; i < 3 * RingCapacity; i++)
        {
            var start = Stopwatch.GetTimestamp();
            backend.SetParameters(voice, in parameters);
            var elapsed = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            slowestMilliseconds = Math.Max(slowestMilliseconds, elapsed);
        }

        Assert.True(slowestMilliseconds < MaxCallMilliseconds,
            $"A game command waited {slowestMilliseconds:F2} ms after the device loss.");
        Assert.True(backend.IsAvailable);
        Assert.True(output.IsAvailable);
    }
}
