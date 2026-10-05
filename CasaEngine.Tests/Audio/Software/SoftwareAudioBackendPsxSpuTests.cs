using System.Diagnostics;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Psx;
using Xunit;

namespace CasaEngine.Tests.Audio.Software;

/// <summary>
/// The software PlayStation SPU hosted by <see cref="SoftwareAudioBackend"/> (plan T4.4), driven through an offline
/// output: the test thread plays both the game thread and the audio thread, one <c>Pump</c> being one audio block
/// (480 frames at 48000 Hz, 10 ms). The hardware tables are synthetic (all ADPCM filters are the no-prediction one).
/// </summary>
public class SoftwareAudioBackendPsxSpuTests
{
    private const int Block = 480;
    private const uint FullEnvelope = 0x1FC0000F; // see PsxSpuTests: fast attack, sustain at the top, release 4000h per frame
    private const ushort MaxVolumeRegister = 0x3FFF;
    private const int SoundAddress = 0x1000;
    private const int RamSize = PsxSpu.RamSize;

    private static PsxSpuHardwareTables Tables() => new(new int[5], new int[5]);

    // One ADPCM block (shift 0, filter 0) of constant nibbles 4, i.e. samples of 4000h, looping on itself.
    private static byte[] LoudLoopBlock()
    {
        var block = new byte[16];
        block[1] = 7; // loop start + loop end + repeat
        Array.Fill(block, (byte)0x44, 2, 14);
        return block;
    }

    // A silent looping block: what the SPU RAM holds "before" in the order test.
    private static byte[] SilentLoopBlock()
    {
        var block = new byte[16];
        block[1] = 7;
        return block;
    }

    private static SoftwareAudioBackend CreateBackend(out OfflineAudioOutput output)
    {
        output = new OfflineAudioOutput();
        return new SoftwareAudioBackend(output, 16);
    }

    private static PsxSpuPort CreatePort(SoftwareAudioBackend backend)
    {
        Assert.True(((IPsxSpuHost)backend).TryCreatePsxSpu(Tables(), out var port));
        Assert.NotNull(port);
        return port;
    }

    private static void ProgramVoice(PsxSpuPort port, int voice)
    {
        Assert.True(port.TrySetStartAddress(voice, (ushort)(SoundAddress / 8)));
        Assert.True(port.TrySetPitch(voice, 0x1000));
        Assert.True(port.TrySetAdsr(voice, FullEnvelope));
        Assert.True(port.TrySetVolume(voice, false, MaxVolumeRegister));
        Assert.True(port.TrySetVolume(voice, true, MaxVolumeRegister));
    }

    private static float Peak(ReadOnlySpan<float> samples)
    {
        var peak = 0f;
        foreach (var sample in samples)
        {
            peak = Math.Max(peak, Math.Abs(sample));
        }

        return peak;
    }

    private static float PumpBlocks(OfflineAudioOutput output, int blocks)
    {
        var peak = 0f;
        for (var i = 0; i < blocks; i++)
        {
            peak = output.Pump(Block);
        }

        return peak;
    }

    private static byte[] ReadRam(PsxSpuPort port, int address, int length)
    {
        var data = new byte[length];
        port.Source.Spu.ReadRam(address, data);
        return data;
    }

    [Fact]
    public void EndToEnd_UploadKeyOnKeyOff_SoundsThenReleasesToSilence()
    {
        using var backend = CreateBackend(out var output);
        using var port = CreatePort(backend);

        Assert.True(port.TryUpload(SoundAddress, LoudLoopBlock()));
        ProgramVoice(port, 0);
        Assert.True(port.TryKeyOn(1));

        output.Pump(Block);
        var peak = PumpBlocks(output, 3);
        Assert.True(peak > 0.1f, $"peak {peak}");
        Assert.Equal(0x7FFF, port.GetEnvx(0));

        Assert.True(port.TryKeyOff(1));
        var silent = PumpBlocks(output, 3);
        Assert.Equal(0, port.GetEnvx(0));
        Assert.True(silent < 1e-6f, $"peak after release {silent}");
        Assert.Equal(0, port.RefusedWriteCount);
    }

    [Fact]
    public void AtTheBound_4096WritesAnd512KbUpload_AreAcceptedWithoutLossQuicklyAndWithoutAllocatingOnTheRenderThread()
    {
        using var backend = CreateBackend(out var output);
        using var port = CreatePort(backend);
        var bank = new byte[RamSize];
        new Random(1234).NextBytes(bank);

        // Warm up the code paths (JIT) before measuring.
        Assert.True(port.TrySetRepeatAddress(1, 1));
        Assert.True(port.TryUpload(0, bank.AsSpan(0, 64)));
        PumpBlocks(output, 2);

        // The pressure round is repeated: a real allocation on the render thread shows in every round, whereas the
        // runtime may charge a one-off tiered JIT step to the first rounds (open point O20 for the SPU render tests).
        var smallest = long.MaxValue;
        for (var round = 0; round < 3 && smallest != 0; round++)
        {
            smallest = Math.Min(smallest, PressureRound(port, output, bank, round));
        }

        Assert.Equal(0, smallest);
    }

    // 4096 register writes and a 512 KB upload between two blocks; returns the bytes allocated by the two blocks.
    private static long PressureRound(PsxSpuPort port, OfflineAudioOutput output, byte[] bank, int round)
    {
        var slowest = 0L;
        for (var i = 0; i < 4096; i++)
        {
            var start = Stopwatch.GetTimestamp();
            var accepted = port.TrySetRepeatAddress(0, (ushort)(i + 1));
            slowest = Math.Max(slowest, Stopwatch.GetTimestamp() - start);
            Assert.True(accepted, $"write {i} refused");
        }

        var uploadStart = Stopwatch.GetTimestamp();
        Assert.True(port.TryUpload(0, bank));
        slowest = Math.Max(slowest, Stopwatch.GetTimestamp() - uploadStart);

        Assert.True(slowest * 1000.0 / Stopwatch.Frequency < 1.0, $"slowest call {slowest * 1000.0 / Stopwatch.Frequency} ms");
        Assert.Equal(0, port.RefusedWriteCount);

        var before = AllocationWindow.Start();
        output.Pump(Block); // first block: 256 KB of the bank and every write before the marker
        output.Pump(Block); // second block: the rest, the marker is released
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(4096, port.Source.Spu.GetRepeatAddress(0));
        Assert.True(bank.AsSpan().SequenceEqual(ReadRam(port, 0, RamSize)), $"round {round}");
        return allocated;
    }

    [Fact]
    public void BeyondTheRegisterRing_RefusesTheRestAndAppliesTheAcceptedPrefixOnly()
    {
        using var backend = CreateBackend(out var output);
        using var port = CreatePort(backend);
        var accepted = 0;

        for (var i = 0; i < 20000; i++)
        {
            if (port.TrySetRepeatAddress(0, (ushort)(i + 1)))
            {
                accepted++;
            }
        }

        Assert.Equal(PsxSpuSourceCapacity.RegisterRing, accepted);
        Assert.Equal(20000 - accepted, port.RefusedWriteCount);

        output.Pump(Block);

        Assert.Equal(accepted, port.Source.Spu.GetRepeatAddress(0));

        // The ring is free again.
        Assert.True(port.TrySetRepeatAddress(0, 7));
        Assert.Equal(20000 - accepted, port.RefusedWriteCount);
    }

    [Fact]
    public void BeyondTheStagingBuffer_RefusesTheUploadAndKeepsTheAcceptedOnesConsistent()
    {
        using var backend = CreateBackend(out var output);
        using var port = CreatePort(backend);
        var first = new byte[RamSize];
        var second = new byte[RamSize];
        Array.Fill(first, (byte)0xAA);
        Array.Fill(second, (byte)0x55);

        Assert.True(port.TryUpload(0, first));
        Assert.True(port.TryUpload(0, second));
        Assert.False(port.TryUpload(0, new byte[] { 0x11 }));
        Assert.False(port.TryUpload(0, new byte[PsxSpuSourceCapacity.Staging + 1]));
        Assert.Equal(2, port.RefusedWriteCount);

        // 1 MB at 256 KB per block: four blocks.
        PumpBlocks(output, 4);

        Assert.True(second.AsSpan().SequenceEqual(ReadRam(port, 0, RamSize)));

        // Room again.
        Assert.True(port.TryUpload(0, new byte[] { 0x11 }));
    }

    [Fact]
    public void Order_AKeyOnAfterA512KbUpload_StartsOnlyOnceTheUploadIsFullyApplied()
    {
        using var backend = CreateBackend(out var output);
        using var port = CreatePort(backend);

        // Previous SPU RAM content at the sound address: a silent loop.
        Assert.True(port.TryUpload(SoundAddress, SilentLoopBlock()));
        PumpBlocks(output, 1);

        var bank = new byte[RamSize];
        Array.Fill(bank, (byte)0xCC, RamSize / 2, RamSize / 2); // the second half lands in the second block
        LoudLoopBlock().CopyTo(bank, SoundAddress);
        Assert.True(port.TryUpload(0, bank));
        ProgramVoice(port, 0);
        Assert.True(port.TryKeyOn(1)); // same gap between two blocks, submitted after the upload

        var firstPeak = output.Pump(Block);
        Assert.Equal(0f, firstPeak);
        Assert.Equal(0, port.GetEnvx(0)); // the key on waits behind the marker: the upload is only half applied
        Assert.False(bank.AsSpan().SequenceEqual(ReadRam(port, 0, RamSize)));

        var secondPeak = output.Pump(Block);
        Assert.True(bank.AsSpan().SequenceEqual(ReadRam(port, 0, RamSize)));
        Assert.True(port.GetEnvx(0) > 0, "the voice must have started in the second block");
        Assert.True(secondPeak > 0.01f, $"the uploaded data must be decoded, peak {secondPeak}");
    }

    [Fact]
    public void Gain_AndBusGain_ScaleTheOutput_AndAreReappliedWhenABusGainChanges()
    {
        using var backend = CreateBackend(out var output);
        using var service = new AudioService(backend);
        Assert.True(service.TryCreatePsxSpu(Tables(), AudioBusNames.Music, out var port));
        Assert.True(port.TryUpload(SoundAddress, LoudLoopBlock()));
        ProgramVoice(port, 0);
        Assert.True(port.TryKeyOn(1));

        PumpBlocks(output, 4);
        var full = PumpBlocks(output, 1);
        Assert.True(full > 0.1f);

        service.Mixer.GetBus(AudioBusNames.Music).Volume = 0.5f;
        service.Update(0.016f);
        PumpBlocks(output, 2); // the gain ramps over one block
        Assert.Equal(full * 0.5f, PumpBlocks(output, 1), 3);

        port.SetGain(0.5f);
        PumpBlocks(output, 2);
        Assert.Equal(full * 0.25f, PumpBlocks(output, 1), 3);

        service.Mixer.GetBus(AudioBusNames.Music).Volume = 1f;
        service.Update(0.016f);
        PumpBlocks(output, 2);
        Assert.Equal(full * 0.5f, PumpBlocks(output, 1), 3);
    }

    [Fact]
    public void SecondCreationFails_WhileOneIsAlive_AndSucceedsAfterDispose()
    {
        using var backend = CreateBackend(out var output);
        var host = (IPsxSpuHost)backend;

        Assert.True(host.TryCreatePsxSpu(Tables(), out var port));
        Assert.False(host.TryCreatePsxSpu(Tables(), out var second));
        Assert.Null(second);

        port.Dispose();
        output.Pump(Block);

        Assert.True(host.TryCreatePsxSpu(Tables(), out var third));
        third.Dispose();
    }

    [Fact]
    public void Dispose_DetachesTheSpu_EvenWithAFullCommandRing()
    {
        using var backend = CreateBackend(out var output);
        var port = CreatePort(backend);
        Assert.True(port.TryUpload(SoundAddress, LoudLoopBlock()));
        ProgramVoice(port, 0);
        Assert.True(port.TryKeyOn(1));
        Assert.True(PumpBlocks(output, 4) > 0.1f);

        // Fill the mixer command ring (nothing drains it until the next Pump).
        var handle = backend.Play(new PcmAudioClip(new short[480], 48000, 1), AudioVoiceParameters.Default);
        Assert.True(handle.IsValid);
        for (var i = 0; i < 5000; i++)
        {
            backend.SetVolume(handle, 0.5f);
        }

        port.Dispose();
        Assert.True(port.IsDisposed);
        Assert.False(port.TryKeyOn(1));

        // The ring drained, but the detach was refused (ring full): the SPU still sounds.
        Assert.True(PumpBlocks(output, 1) > 0.1f);

        // The next backend call resends it; nothing was lost.
        backend.GetState(handle);
        Assert.True(PumpBlocks(output, 2) < 1e-6f);
    }

    [Fact]
    public void SnapshotReads_AreSafeWhileTheAudioThreadPublishes()
    {
        using var backend = CreateBackend(out var output);
        using var port = CreatePort(backend);
        Assert.True(port.TryUpload(SoundAddress, LoudLoopBlock()));
        ProgramVoice(port, 0);
        Assert.True(port.TryKeyOn(1));

        using var stop = new CancellationTokenSource();
        var pumps = 0;
        var audio = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                output.Pump(Block);
                Interlocked.Increment(ref pumps);
            }
        });

        // Read while the audio thread publishes, until it rendered enough blocks (bounded, a loaded machine may start it late).
        var deadline = Stopwatch.GetTimestamp() + 10 * Stopwatch.Frequency;
        while (Volatile.Read(ref pumps) < 200 && Stopwatch.GetTimestamp() < deadline)
        {
            var envx = port.GetEnvx(0);
            Assert.InRange(envx, 0, 0x7FFF);
            _ = port.ReadEndx();
        }

        stop.Cancel();
        audio.Wait();
        Assert.True(pumps >= 200, $"only {pumps} blocks rendered");
        Assert.Equal(0x7FFF, port.GetEnvx(0));
    }

    // The limits of the plan, mirrored here so a change of the constants is a visible test change.
    private static class PsxSpuSourceCapacity
    {
        public const int RegisterRing = 16384;
        public const int Staging = 1024 * 1024;
    }
}
