using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Application;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Demos.Demos.PsxSemiTransparency;

/// <summary>
/// Reads the back-buffer of the running game in process (<see cref="GraphicsDevice.GetBackBufferData{T}(T[])"/>, the same
/// call as the <c>CASAENGINE_CAPTURE_SCREENSHOT_PATH</c> capture of <see cref="DemosGame"/>, never a capture of the desktop)
/// and compares the pixels of the demo scene with the values the demo expects, to within one level per RGB channel.
/// The result goes to the log and, when <c>CASAENGINE_DEMO_PIXELS_PATH</c> names a file, to that file.
/// </summary>
internal sealed class BackBufferProbe
{
    private const string OutputPathVariable = "CASAENGINE_DEMO_PIXELS_PATH";
    private const int FramesBeforeProbe = 20;

    private readonly string _demoName;
    private readonly List<Check> _checks = new();
    private int _frames;
    private bool _done;

    public BackBufferProbe(string demoName)
    {
        _demoName = demoName;
    }

    public void Add(string label, Vector3 worldPosition, Color expected)
    {
        _checks.Add(new Check(label, worldPosition, expected));
    }

    /// <summary>Call once per frame after the render pipeline; probes once, after a few frames.</summary>
    public void OnPostDraw(CasaEngineGame game)
    {
        if (_done || ++_frames < FramesBeforeProbe)
        {
            return;
        }

        _done = true;
        var camera = game.GameManager.ViewManager.ActiveView?.Camera;
        if (camera == null)
        {
            Logs.WriteError($"[{_demoName}] no active camera, nothing probed");
            return;
        }

        var device = game.GraphicsDevice;
        var width = device.PresentationParameters.BackBufferWidth;
        var height = device.PresentationParameters.BackBufferHeight;
        var data = new byte[width * height * 4];
        device.GetBackBufferData(data);

        var report = new StringBuilder();
        report.AppendLine(string.Create(CultureInfo.InvariantCulture, $"demo={_demoName} backbuffer={width}x{height}"));
        var failures = 0;

        foreach (var check in _checks)
        {
            var screen = device.Viewport.Project(check.WorldPosition, camera.ProjectionMatrix, camera.ViewMatrix, Matrix.Identity);
            var x = (int)MathF.Floor(screen.X);
            var y = (int)MathF.Floor(screen.Y);
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                report.AppendLine(string.Create(CultureInfo.InvariantCulture, $"FAIL {check.Label} pixel=({x},{y}) outside the back-buffer"));
                failures++;
                continue;
            }

            var offset = (y * width + x) * 4;
            var r = data[offset];
            var g = data[offset + 1];
            var b = data[offset + 2];
            var a = data[offset + 3];
            var ok = Math.Abs(r - check.Expected.R) <= 1
                     && Math.Abs(g - check.Expected.G) <= 1
                     && Math.Abs(b - check.Expected.B) <= 1;
            failures += ok ? 0 : 1;
            report.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{(ok ? "OK  " : "FAIL")} {check.Label} pixel=({x},{y}) read=({r},{g},{b}) alpha={a} expected=({check.Expected.R},{check.Expected.G},{check.Expected.B})"));
        }

        report.AppendLine(string.Create(CultureInfo.InvariantCulture, $"result={(failures == 0 ? "PASS" : "FAIL")} failures={failures} checks={_checks.Count}"));
        Logs.WriteInfo($"[{_demoName}] probe{Environment.NewLine}{report}");

        var path = Environment.GetEnvironmentVariable(OutputPathVariable);
        if (!string.IsNullOrWhiteSpace(path))
        {
            File.WriteAllText(Path.GetFullPath(path), report.ToString());
        }
    }

    private readonly record struct Check(string Label, Vector3 WorldPosition, Color Expected);
}
