using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Spatial;
using Xunit;
using Xunit.Abstractions;
using Vector3 = System.Numerics.Vector3;

namespace CasaEngine.Tests.Audio.Spatial;

/// <summary>
/// Fuzz of the spatial and parameter modulation (plan T9.4 to T9.6, decision P44) against the fallback: the same random
/// sequence of game-thread calls runs on a service over the software backend (modulation carried by the last-value channel,
/// orthogonal to volume, ramps and buses) and on a service over the fallback fake (gain, pan and pitch folded into the
/// volume and the parameters sent). Both must give the same audible result, whatever the order of the calls within a frame.
/// </summary>
/// <remarks>
/// Comparison. The software service renders a constant (DC) clip with the master limiter off, so the mixer is linear and
/// every contribution is positive: the left and right samples at the end of the block rendered after each
/// <see cref="AudioService.Update"/> are the sum of the voices. The fallback service is read through
/// <see cref="FakeAudioBackend.GetParameters"/> (volume already holds the bus gain and the folded gain, pan the folded pan)
/// and converted into left and right with the pan law of the software mixer (constant power on a mono clip, balance on a
/// stereo one). One audio block is rendered per Update with exactly the frames of the elapsed time, so both timelines
/// advance together. Voices are summed rather than isolated: a divergence of any voice shows in the sum, a cancellation
/// would need two opposite errors of the same size.
/// Tolerances. Final error (the quiet tail, no call, every fade done): 0.01 per level, the tolerance of the T9.4 probes
/// (16-bit samples, per-sample ramps) widened by the number of voices summed. Transient: the software level must stay within
/// the same tolerance of the range the fallback went through during the 10 ms (one block) around the tick.
/// </remarks>
[Collection(ProjectEnvironmentCollection.Name)]
public class AudioServiceModulationOrderTests
{
    private const int SampleRate = 48000;
    private const int SeedCount = 300;
    private const int MaxPlays = 8;
    private const float RunSeconds = 0.8f;
    private const float TailSeconds = 0.6f;
    private const float BlockSeconds = 0.01f;
    private const float Tolerance = 0.01f;
    private const short ClipSample = 2048;
    private const float LevelScale = 0.5f / (ClipSample / 32768f);
    private const string ParamA = "a";
    private const string ParamB = "b";

    private readonly ITestOutputHelper _output;

    public AudioServiceModulationOrderTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private enum OpType
    {
        Play,
        SetVoiceVolume,
        FadeVoice,
        CancelFade,
        StopWithFade,
        SetVoicePosition,
        SetListener,
        RemoveListener,
        SetGameParameter,
        FadeBus,
        SetBusMute,
        SetVoicePan,
    }

    private struct Op
    {
        public OpType Type;
        public int Voice;
        public int Kind;
        public int Bus;
        public float A;
        public float B;
        public Vector3 Position;
    }

    private static readonly string[] Buses = { AudioBusNames.Sfx, AudioBusNames.Music, AudioBusNames.Master };

    // ---- the fuzz -----------------------------------------------------------

    [Theory]
    [InlineData(0.010f)]
    [InlineData(1f / 60f)]
    [InlineData(0.007f)]
    [InlineData(0.033f)]
    public void TheSoftwareBackendAndTheFallbackGiveTheSameLevels_WhateverTheOrderOfTheCalls(float dt)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var worst = 0f;
        var worstTail = 0f;

        for (var seed = 0; seed < SeedCount; seed++)
        {
            var script = GenerateScript(seed, dt);
            var software = RunScript(SpatialBackendKind.Software, script, dt);
            var fallback = RunScript(SpatialBackendKind.Fallback, script, dt);

            var window = (int)Math.Ceiling((BlockSeconds / dt) - 1e-4);
            var tailStart = script.Length - (int)(0.3f / dt);

            for (var tick = 0; tick < script.Length; tick++)
            {
                var from = Math.Max(0, tick - window);
                var to = Math.Min(script.Length - 1, tick + window);

                CheckSide(software, fallback, tick, from, to, 0, seed, dt, ref worst);
                CheckSide(software, fallback, tick, from, to, 1, seed, dt, ref worst);

                if (tick >= tailStart)
                {
                    worstTail = Math.Max(worstTail, Math.Abs(software[tick].Left - fallback[tick].Left));
                    worstTail = Math.Max(worstTail, Math.Abs(software[tick].Right - fallback[tick].Right));
                    if (Math.Abs(software[tick].Left - fallback[tick].Left) > Tolerance || Math.Abs(software[tick].Right - fallback[tick].Right) > Tolerance)
                    {
                        Assert.Fail($"Final divergence: seed {seed}, dt {dt}, tick {tick}: software ({software[tick].Left:F4}, {software[tick].Right:F4}) fallback ({fallback[tick].Left:F4}, {fallback[tick].Right:F4}).\n{Describe(script)}");
                    }
                }
            }
        }

        _output.WriteLine($"dt {dt}: worst transient excess {worst:F5}, worst tail error {worstTail:F5}, {clock.ElapsedMilliseconds} ms");
    }

    private static void CheckSide(Level[] software, Level[] fallback, int tick, int from, int to, int channel, int seed, float dt, ref float worst)
    {
        var soft = channel == 0 ? software[tick].Left : software[tick].Right;
        var low = float.MaxValue;
        var high = float.MinValue;

        for (var i = from; i <= to; i++)
        {
            var value = channel == 0 ? fallback[i].Left : fallback[i].Right;
            low = Math.Min(low, value);
            high = Math.Max(high, value);
        }

        var excess = Math.Max(low - soft, soft - high);
        worst = Math.Max(worst, excess);

        if (excess > Tolerance)
        {
            Assert.Fail($"Transient divergence: seed {seed}, dt {dt}, tick {tick}, {(channel == 0 ? "left" : "right")}: software {soft:F4}, fallback range [{low:F4}, {high:F4}] over ticks {from} to {to}.\n{Describe(GenerateScript(seed, dt))}");
        }
    }

    private struct Level
    {
        public float Left;
        public float Right;
    }

    // ---- script -------------------------------------------------------------

    private static Op[][] GenerateScript(int seed, float dt)
    {
        var random = new Random(1000 + seed);
        var runTicks = (int)Math.Ceiling(RunSeconds / dt);
        var tailTicks = (int)Math.Ceiling(TailSeconds / dt);
        var script = new Op[runTicks + tailTicks][];
        var plays = 4;

        for (var tick = 0; tick < script.Length; tick++)
        {
            var ops = new List<Op>();

            if (tick == 0)
            {
                if (random.NextDouble() < 0.8)
                {
                    ops.Add(new Op { Type = OpType.SetListener, Position = RandomPosition(random, 3f) });
                }

                var voices = 2 + random.Next(3);
                for (var i = 0; i < voices; i++)
                {
                    ops.Add(NewPlay(random));
                }
            }

            if (tick < runTicks)
            {
                var count = random.Next(0, 4);
                for (var i = 0; i < count; i++)
                {
                    var op = NewOp(random);

                    // Few voices overall: the sum of their levels must stay far from the clipping of the output.
                    if (op.Type == OpType.Play && ++plays > MaxPlays)
                    {
                        op.Type = OpType.SetVoiceVolume;
                    }

                    ops.Add(op);
                }
            }

            script[tick] = ops.ToArray();
        }

        return script;
    }

    private static Vector3 RandomPosition(Random random, float range)
    {
        return new Vector3(
            ((float)random.NextDouble() * 2f - 1f) * range,
            ((float)random.NextDouble() * 2f - 1f) * range,
            ((float)random.NextDouble() * 2f - 1f) * range);
    }

    private static Op NewPlay(Random random)
    {
        return new Op { Type = OpType.Play, Kind = random.Next(7), Position = RandomPosition(random, 6f), A = (float)(0.3 + 0.7 * random.NextDouble()) };
    }

    private static readonly float[] Durations = { 0f, 0.02f, 0.05f, 0.2f, 0.5f };

    private static Op NewOp(Random random)
    {
        var roll = random.Next(100);
        var op = new Op { Voice = random.Next(8), Bus = random.Next(Buses.Length), A = (float)random.NextDouble(), B = Durations[random.Next(Durations.Length)] };

        if (roll < 14)
        {
            op.Type = OpType.SetVoiceVolume;
        }
        else if (roll < 26)
        {
            op.Type = OpType.FadeVoice;
        }
        else if (roll < 34)
        {
            op.Type = OpType.CancelFade;
        }
        else if (roll < 38)
        {
            op.Type = OpType.StopWithFade;
        }
        else if (roll < 52)
        {
            op.Type = OpType.SetVoicePosition;
            op.Position = RandomPosition(random, 6f);
        }
        else if (roll < 64)
        {
            op.Type = OpType.SetListener;
            op.Position = RandomPosition(random, 4f);
        }
        else if (roll < 66)
        {
            op.Type = OpType.RemoveListener;
        }
        else if (roll < 76)
        {
            op.Type = OpType.SetGameParameter;
            op.Bus = random.Next(2);
            op.A = (float)(random.NextDouble() * 2.0 - 0.5);
        }
        else if (roll < 84)
        {
            op.Type = OpType.FadeBus;
        }
        else if (roll < 90)
        {
            op.Type = OpType.SetBusMute;
            op.A = random.Next(2);
        }
        else if (roll < 96)
        {
            op.Type = OpType.SetVoicePan;
            op.A = (float)(random.NextDouble() * 2.0 - 1.0);
        }
        else
        {
            op = NewPlay(random);
        }

        return op;
    }

    private static string Describe(Op[][] script)
    {
        var text = new System.Text.StringBuilder();

        for (var tick = 0; tick < script.Length; tick++)
        {
            if (script[tick].Length == 0)
            {
                continue;
            }

            text.Append("tick ").Append(tick).Append(':');
            foreach (var op in script[tick])
            {
                text.Append(' ').Append(op.Type).Append('(');
                switch (op.Type)
                {
                    case OpType.Play:
                        text.Append("kind ").Append(op.Kind).Append(" at ").Append(op.Position).Append(" vol ").Append(op.A.ToString("F2"));
                        break;
                    case OpType.SetListener:
                    case OpType.SetVoicePosition:
                        text.Append("v").Append(op.Voice).Append(' ').Append(op.Position);
                        break;
                    case OpType.SetGameParameter:
                        text.Append(op.Bus == 0 ? ParamA : ParamB).Append('=').Append(op.A.ToString("F2"));
                        break;
                    case OpType.FadeBus:
                        text.Append(Buses[op.Bus]).Append(" to ").Append(op.A.ToString("F2")).Append(" in ").Append(op.B);
                        break;
                    case OpType.SetBusMute:
                        text.Append(Buses[op.Bus]).Append(op.A > 0.5f ? " mute" : " unmute");
                        break;
                    default:
                        text.Append("v").Append(op.Voice).Append(' ').Append(op.A.ToString("F2")).Append(' ').Append(op.B);
                        break;
                }

                text.Append(')');
            }

            text.AppendLine();
        }

        return text.ToString();
    }

    // ---- one run --------------------------------------------------------------

    private sealed class VoiceInfo
    {
        public AudioVoiceHandle Handle;
        public bool Stereo;
    }

    private static Level[] RunScript(SpatialBackendKind kind, Op[][] script, float dt)
    {
        OfflineAudioOutput output = null;
        FakeAudioBackend fake = null;
        AudioService service;

        if (kind == SpatialBackendKind.Software)
        {
            output = new OfflineAudioOutput();
            service = new AudioService(new SoftwareAudioBackend(output, 16));
            service.MasterLimiter.IsEnabled = false;
        }
        else
        {
            fake = new FakeAudioBackend(16);
            service = new AudioService(fake);
        }

        using (service)
        {
            var provider = new FakeAudioClipProvider();
            service.ClipProvider = provider;
            IAudioClip monoClip = kind == SpatialBackendKind.Software ? MonoConstant : new FakeAudioClip(channelCount: 1);
            IAudioClip stereoClip = kind == SpatialBackendKind.Software ? StereoConstant : new FakeAudioClip(channelCount: 2);
            var listenerSource = new object();
            var voices = new List<VoiceInfo>();
            var levels = new Level[script.Length];
            var rendered = 0;
            var elapsed = 0.0;

            for (var tick = 0; tick < script.Length; tick++)
            {
                foreach (var op in script[tick])
                {
                    Apply(service, provider, monoClip, stereoClip, listenerSource, voices, op);
                }

                service.Update(dt);
                elapsed += dt;

                if (output != null)
                {
                    var target = (int)Math.Round(elapsed * SampleRate);
                    output.Pump(target - rendered);
                    rendered = target;
                    var block = output.LastBlock;
                    levels[tick] = new Level { Left = block[^2] * LevelScale, Right = block[^1] * LevelScale };
                }
                else
                {
                    levels[tick] = Fallback(service, fake, voices);
                }
            }

            return levels;
        }
    }

    private static readonly PcmAudioClip MonoConstant = ConstantClip(1);
    private static readonly PcmAudioClip StereoConstant = ConstantClip(2);

    private static PcmAudioClip ConstantClip(int channels)
    {
        var samples = new short[SampleRate * 4 * channels];
        Array.Fill(samples, ClipSample);
        return new PcmAudioClip(samples, SampleRate, channels);
    }

    private static Level Fallback(AudioService service, FakeAudioBackend fake, List<VoiceInfo> voices)
    {
        var level = new Level();

        foreach (var voice in voices)
        {
            if (!service.IsAlive(voice.Handle))
            {
                continue;
            }

            var parameters = fake.GetParameters(voice.Handle);
            var amplitude = 0.5f * parameters.Volume;
            var pan = parameters.Pan;

            if (voice.Stereo)
            {
                level.Left += amplitude * (pan > 0f ? 1f - pan : 1f);
                level.Right += amplitude * (pan < 0f ? 1f + pan : 1f);
            }
            else
            {
                level.Left += amplitude * (float)Math.Cos((pan + 1.0) * Math.PI / 4.0);
                level.Right += amplitude * (float)Math.Sin((pan + 1.0) * Math.PI / 4.0);
            }
        }

        return level;
    }

    private static SoundAsset MakeAsset(FakeAudioClipProvider provider, IAudioClip clip, AudioSpatialMode mode, string bus, params AudioParameterBinding[] bindings)
    {
        var asset = new SoundAsset
        {
            Name = "fuzz",
            AudioFileAssetId = provider.Register(clip),
            SpatialMode = mode,
            DistanceModel = AudioDistanceModel.InverseDistanceClamped,
            ReferenceDistance = 1f,
            MaxDistance = float.MaxValue,
            RolloffFactor = 1f,
            BusName = bus,
        };
        asset.SetParameterBindings(bindings);
        return asset;
    }

    private static void Apply(
        AudioService service,
        FakeAudioClipProvider provider,
        IAudioClip monoClip,
        IAudioClip stereoClip,
        object listenerSource,
        List<VoiceInfo> voices,
        Op op)
    {
        AudioVoiceHandle Target(out VoiceInfo info)
        {
            info = voices.Count == 0 ? null : voices[op.Voice % voices.Count];
            return info?.Handle ?? AudioVoiceHandle.None;
        }

        var volumeBinding = new AudioParameterBinding(ParamA, AudioParameterTarget.Volume, 0f, 1f, 0f, 1f);
        var pitchBinding = new AudioParameterBinding(ParamB, AudioParameterTarget.Pitch, -1f, 1f, -1f, 1f);
        var none = SoundPlaybackOverrides.None;

        switch (op.Type)
        {
            case OpType.Play:
            {
                AudioVoiceHandle handle;
                var stereo = false;

                switch (op.Kind)
                {
                    case 0:
                        handle = service.PlayClip(monoClip, AudioBusNames.Sfx, AudioVoiceParameters.Default.WithVolume(op.A));
                        break;
                    case 1:
                        stereo = true;
                        handle = service.PlayClip(stereoClip, AudioBusNames.Music, AudioVoiceParameters.Default.WithVolume(op.A));
                        break;
                    case 2:
                        handle = service.PlaySoundAt(MakeAsset(provider, monoClip, AudioSpatialMode.Spatial3D, AudioBusNames.Sfx), op.Position, none);
                        break;
                    case 3:
                        stereo = true;
                        handle = service.PlaySoundAt(MakeAsset(provider, stereoClip, AudioSpatialMode.Spatial2D, AudioBusNames.Music), op.Position, none);
                        break;
                    case 4:
                        handle = service.PlaySound(MakeAsset(provider, monoClip, AudioSpatialMode.None, AudioBusNames.Sfx, volumeBinding, pitchBinding), none);
                        break;
                    case 5:
                        handle = service.PlaySoundAt(MakeAsset(provider, monoClip, AudioSpatialMode.Spatial3D, AudioBusNames.Music, volumeBinding, pitchBinding), op.Position, none);
                        break;
                    default:
                        stereo = true;
                        handle = service.PlaySoundAt(MakeAsset(provider, stereoClip, AudioSpatialMode.Spatial3D, AudioBusNames.Sfx, volumeBinding), op.Position, none);
                        break;
                }

                Assert.True(handle.IsValid);
                voices.Add(new VoiceInfo { Handle = handle, Stereo = stereo });
                break;
            }

            case OpType.SetVoiceVolume:
                service.SetVoiceVolume(Target(out _), op.A);
                break;
            case OpType.FadeVoice:
                service.FadeVoice(Target(out _), op.A, op.B);
                break;
            case OpType.CancelFade:
                service.CancelFade(Target(out _));
                break;
            case OpType.StopWithFade:
                service.StopWithFade(Target(out _), op.B);
                break;
            case OpType.SetVoicePosition:
                service.SetVoicePosition(Target(out _), op.Position);
                break;
            case OpType.SetListener:
                service.SetListener(listenerSource, AudioListenerPose.Create(op.Position, new Vector3(0f, 0f, -1f), Vector3.UnitY));
                break;
            case OpType.RemoveListener:
                service.RemoveListener(listenerSource);
                break;
            case OpType.SetGameParameter:
                service.SetGameParameter(op.Bus == 0 ? ParamA : ParamB, op.A);
                break;
            case OpType.FadeBus:
                service.FadeBus(Buses[op.Bus], op.A, op.B);
                break;
            case OpType.SetBusMute:
                service.Mixer.GetBus(Buses[op.Bus]).IsMuted = op.A > 0.5f;
                break;
            case OpType.SetVoicePan:
                service.SetVoicePan(Target(out _), op.A * 2f - 1f);
                break;
        }
    }

    // ---- rate -----------------------------------------------------------------

    /// <summary>
    /// The playback rate is not measurable on the constant clip of the fuzz, so it has its own probe: a ramp clip whose slope is
    /// the rate on the software backend, against the pitch folded on the fallback, with the volume calls of the fuzz around the
    /// parameter write.
    /// </summary>
    [Theory]
    [InlineData(0.5f)]
    [InlineData(-0.5f)]
    [InlineData(0.25f)]
    public void ABoundPitch_GivesTheSameRateOnTheSoftwareBackendAndTheFallback(float pitch)
    {
        var slopeUnbound = RampSlope(null);
        var slope = RampSlope(pitch);

        Assert.Equal(Math.Pow(2.0, pitch), slope / slopeUnbound, 0.02);

        var folded = FoldedPitch(pitch);
        Assert.Equal(pitch, folded, 1e-3f);
    }

    private static float RampSlope(float? pitch)
    {
        var samples = new short[60000];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (short)(i / 2);
        }

        var output = new OfflineAudioOutput();
        using var service = new AudioService(new SoftwareAudioBackend(output, 16));
        service.MasterLimiter.IsEnabled = false;
        var provider = new FakeAudioClipProvider();
        service.ClipProvider = provider;
        var asset = new SoundAsset { Name = "ramp", AudioFileAssetId = provider.Register(new PcmAudioClip(samples, SampleRate, 1)) };
        asset.SetParameterBindings(new[] { new AudioParameterBinding(ParamB, AudioParameterTarget.Pitch, -1f, 1f, -1f, 1f) });
        var voice = service.PlaySound(asset);

        service.SetVoiceVolume(voice, 1f);
        service.SetGameParameter(ParamB, pitch ?? 0f);
        service.FadeVoice(voice, 1f, 0.05f);
        service.CancelFade(voice);

        for (var i = 0; i < 3; i++)
        {
            service.Update(0.01f);
            output.Pump(480);
        }

        var block = output.LastBlock;
        return (block[^2] - block[0]) / 479f;
    }

    private static float FoldedPitch(float pitch)
    {
        var fake = new FakeAudioBackend(16);
        using var service = new AudioService(fake);
        var provider = new FakeAudioClipProvider();
        service.ClipProvider = provider;
        var asset = new SoundAsset { Name = "ramp", AudioFileAssetId = provider.Register(new FakeAudioClip(channelCount: 1)) };
        asset.SetParameterBindings(new[] { new AudioParameterBinding(ParamB, AudioParameterTarget.Pitch, -1f, 1f, -1f, 1f) });
        var voice = service.PlaySound(asset);

        service.SetVoiceVolume(voice, 1f);
        service.SetGameParameter(ParamB, pitch);
        service.FadeVoice(voice, 1f, 0.05f);
        service.CancelFade(voice);

        for (var i = 0; i < 3; i++)
        {
            service.Update(0.01f);
        }

        return fake.GetParameters(voice).Pitch;
    }
}
