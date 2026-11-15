using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Rendering;

namespace CasaEngine.Demos;

/// <summary>
/// Cost instrument of the demos (plan T10.2), switched on by <c>CASAENGINE_DEMO_FRAME_STATS=1</c>. It adds up, over the updates
/// <see cref="FirstFrame"/> (included) to <see cref="LastFrame"/> (excluded), which is <see cref="MeasuredFrames"/> images, the CPU
/// time of <c>Update</c>, the CPU time of <c>Draw</c> and the wall time between two images, and writes a single log line at the
/// end. Nothing is allocated or logged per image: the start is after the IBL generations of the demos.
/// </summary>
internal sealed class DemoFrameStats
{
    public const string EnvironmentVariable = "CASAENGINE_DEMO_FRAME_STATS";
    public const int FirstFrame = 100;
    public const int LastFrame = 700;
    public const int MeasuredFrames = LastFrame - FirstFrame;

    private long _updateTicks;
    private long _drawTicks;
    private long _wallTicks;
    private double _pipelineCpuMilliseconds;
    private long _previousDrawStart;
    private int _updates;
    private int _draws;
    private bool _reported;

    public static bool IsRequested => Environment.GetEnvironmentVariable(EnvironmentVariable) == "1";

    /// <summary>True once the last measured image is drawn and the line is written: the caller can stop the game.</summary>
    public bool IsFinished => _reported;

    /// <summary>Number of the update about to run (1 for the first one).</summary>
    public int BeginUpdate()
    {
        _updates++;
        return _updates;
    }

    public void EndUpdate(long startTimestamp)
    {
        if (_updates > FirstFrame && _updates <= LastFrame)
        {
            _updateTicks += Stopwatch.GetTimestamp() - startTimestamp;
        }
    }

    public void BeginDraw(long startTimestamp)
    {
        _draws++;
        if (_draws > FirstFrame && _draws <= LastFrame)
        {
            _wallTicks += startTimestamp - _previousDrawStart;
        }

        _previousDrawStart = startTimestamp;
    }

    public void EndDraw(long startTimestamp, IReadOnlyList<RenderView> views, string demoTitle, bool vSync, bool fixedStep)
    {
        if (_draws <= FirstFrame || _draws > LastFrame)
        {
            return;
        }

        _drawTicks += Stopwatch.GetTimestamp() - startTimestamp;
        if (views.Count > 0)
        {
            _pipelineCpuMilliseconds += views[0].RenderStats.TotalCpuMilliseconds;
        }

        if (_draws == LastFrame && !_reported)
        {
            _reported = true;
            Report(views, demoTitle, vSync, fixedStep);
        }
    }

    private void Report(IReadOnlyList<RenderView> views, string demoTitle, bool vSync, bool fixedStep)
    {
        double toMilliseconds = 1000.0 / Stopwatch.Frequency / MeasuredFrames;
        string counters = "no view";
        if (views.Count > 0)
        {
            var stats = views[0].RenderStats;
            counters = $"Draws={stats.DrawCalls} FX={stats.EffectBinds} Tex={stats.TextureBinds} State={stats.StateChanges} "
                + $"Opaque={stats.OpaqueItems} Transparent={stats.TransparentItems} Particles={stats.ParticleCount} Lines={stats.LineCount}";
        }

        Logs.WriteInfo(string.Create(CultureInfo.InvariantCulture,
            $"[FrameStats] demo='{demoTitle}' frames={MeasuredFrames} (updates {FirstFrame + 1}..{LastFrame}) vsync={vSync} fixedStep={fixedStep} "
            + $"updateCpuMs={_updateTicks * toMilliseconds:F3} drawCpuMs={_drawTicks * toMilliseconds:F3} "
            + $"frameWallMs={_wallTicks * toMilliseconds:F3} pipelineCpuMs={_pipelineCpuMilliseconds / MeasuredFrames:F3} "
            + $"lastFrame[{counters}]"));
    }
}
