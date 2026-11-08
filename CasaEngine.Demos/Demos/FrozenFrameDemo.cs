using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.FrozenFrames;
using CasaEngine.Framework.Rendering.ScreenEffects;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// E19.TR-4a: the engine keeps the image of the previous frame and shows it frozen. A 2D scene with a hard vertical edge
/// and sprites of the four blends, plus a window that plays the life gauge, is played in a world with a virtual resolution
/// (320 x 240) by a script counted in turns (one turn is one <see cref="Update"/> followed by one draw and one
/// <see cref="PostDraw"/>). The back buffer is read in process (<see cref="GraphicsDevice.GetBackBufferData{T}(T[])"/>,
/// never a capture of the desktop) at turns 9, 11, 13, 14, 17, 19, 21, 23 and 27 and compared with predictions that come
/// from the colours the demo poses (D-TR4A-12): the pixels of a frozen image equal the pixels of the turn before the
/// request, a resize while it is shown stretches it with point sampling, and so on.
/// </summary>
/// <remarks>
/// Run it from the <c>CasaEngine.Demos</c> folder (the working folder is mandatory) with
/// <c>CASAENGINE_START_DEMO="Frozen frame demo"</c> and <c>CASAENGINE_DEMO_PIXELS_PATH</c> naming the report file: the
/// demo then writes the report, one PNG per reading beside it, sets the exit code (0 when every prediction holds) and
/// closes itself. Without the variable it plays the script once and stays on the last scene.
/// </remarks>
public class FrozenFrameDemo : Demo
{
    private const int VirtualWidth = 320;
    private const int VirtualHeight = 240;
    private const int WindowWidth = 960;
    private const int WindowHeight = 720;
    private const int StretchedWidth = 1280;
    private const int StretchedHeight = 960;
    private const byte VeilLevel = 40;
    private const int Tolerance = 1;

    private static readonly int[] ReadingTurns = { 9, 11, 13, 14, 17, 19, 21, 23, 27 };

    private static readonly WarningCounter Warnings = new();

    private CasaEngineGame? _game;
    private VirtualResolutionSettings? _previousVirtualResolution;
    private DisplaySettings _previousDisplay;
    private ScreenEffectLayer _previousLayer;
    private Texture2D? _pixel;
    private FrozenFrameScene? _scene;
    private FrozenFrameGaugeScreen? _gauge;
    private int _turn = -1;
    private long _captureCountAtTurn24;
    private readonly Dictionary<int, Reading> _readings = new();
    private Point _gaugeCentreVirtual;
    private bool _gaugeFound;
    private bool _reported;
    private bool _loaded;

    public override string Title => "Frozen frame demo";

    public override string Description =>
        "E19.TR-4a: the engine keeps the image of the previous frame and shows it frozen, user interface included. A scripted run (virtual resolution 320 x 240, a resize while the image is shown, a veil, a second request, the option turned off) reads the back buffer and checks it against predictions drawn from the colours it poses.";

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        var world = game.GameManager.CurrentWorld;

        if (!Warnings.Installed)
        {
            Logs.AddLogger(Warnings);
            Warnings.Installed = true;
        }

        Warnings.Count = 0;

        // The demos have no virtual resolution; this one plays in a world that has one. Put back in Clean.
        _previousVirtualResolution = game.RuntimeContext.ProjectSettings.VirtualResolution;
        game.RuntimeContext.ProjectSettings.VirtualResolution = new VirtualResolutionSettings
        {
            Width = VirtualWidth,
            Height = VirtualHeight,
        };

        // 960 x 720 is k = 3 of the virtual resolution: the view covers the whole window. Put back in Clean.
        _previousDisplay = game.GetDisplaySettings();
        game.ApplyDisplaySettings(
            new DisplaySettings(WindowWidth, WindowHeight, false, _previousDisplay.IsVSyncEnabled),
            persistToProjectSettings: false);

        _pixel = new Texture2D(game.GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _scene = new FrozenFrameScene(game.SpriteRendererComponent, _pixel);
        var entity = new Entity { Name = "Frozen frame scene" };
        entity.RootComponent = _scene;
        world.AddEntity(entity);

        _previousLayer = game.ScreenEffectComponent.Service.Layer;
        var service = game.FrozenFrameComponent.Service;
        service.Release();
        service.RetainLastFrame = true;

        _turn = -1;
        _loaded = false;
        _readings.Clear();
        _gaugeFound = false;
        _reported = false;
    }

    public override void ConfigureSceneLighting(CasaEngine.Framework.Scene.World.World world)
    {
        // A 2D scene: no light.
    }

    public override CameraComponent CreateCamera(CasaEngineGame game)
    {
        var entity = new Entity();
        var camera = new Camera2dComponent();
        entity.AddComponent(camera);
        entity.Initialize();
        game.GameManager.CurrentWorld.AddEntity(entity);

        return camera;
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        // The camera is on the origin; the virtual resolution fitted its zoom to the window. The per-view UI runtime exists
        // once the world is loaded, so the gauge is pushed here (same ordering as UIOverlayDemo).
        _gauge = new FrozenFrameGaugeScreen();
        _gauge.SetColor(FrozenFramePalettes.A.Gauge);
        _game!.GameManager.ViewManager.GetActiveUIView()?.PushScreen(_gauge);
        _scene!.Palette = FrozenFramePalettes.A;
        _turn = -1;
        _loaded = true;
    }

    public override void Update(GameTime gameTime)
    {
        var game = _game;
        if (game == null || _scene == null || _gauge == null)
        {
            return;
        }

        // The world loads at the end of the first update of the demo (GameManager.UpdateWorld in base.Update): until
        // InitializeCamera has run there is no turn to count.
        if (!_loaded)
        {
            return;
        }

        _turn++;

        var frozen = game.FrozenFrameComponent.Service;
        var effects = game.ScreenEffectComponent.Service;

        switch (_turn)
        {
            case 10:
                // The request and the display are posed in the same Update; the scene changes at the same time.
                frozen.RequestCapture();
                frozen.ShowBase();
                SetScene(FrozenFramePalettes.B);
                break;
            case 12:
                // The window grows while the frozen image is shown: k = 3 becomes k = 4, a ratio 4 / 3 that is not whole.
                game.ApplyDisplaySettings(
                    new DisplaySettings(StretchedWidth, StretchedHeight, false, _previousDisplay.IsVSyncEnabled),
                    persistToProjectSettings: false);
                break;
            case 14:
                // The original size is given back before this turn is drawn (the plan gives it back in the PostDraw of
                // turn 13, effective at turn 14: same state at the draw of turn 14, but not a device reset inside Draw).
                game.ApplyDisplaySettings(
                    new DisplaySettings(WindowWidth, WindowHeight, false, _previousDisplay.IsVSyncEnabled),
                    persistToProjectSettings: false);
                effects.Layer = ScreenEffectLayer.AboveUI;
                effects.SetOverlay(VeilLevel, VeilLevel, VeilLevel, SpriteBlendMode.Subtractive);
                break;
            case 16:
                effects.Clear();
                break;
            case 18:
                frozen.HideBase();
                break;
            case 20:
                SetScene(FrozenFramePalettes.C);
                break;
            case 22:
                SetScene(FrozenFramePalettes.D);
                frozen.RequestCapture();
                frozen.ShowBase();
                break;
            case 24:
                frozen.Release();
                frozen.RetainLastFrame = false;
                _captureCountAtTurn24 = frozen.CaptureCount;
                break;
            case 26:
                // The option is off: the request finds no retained image.
                SetScene(FrozenFramePalettes.E);
                frozen.RequestCapture();
                frozen.ShowBase();
                break;
        }
    }

    private void SetScene(FrozenFramePalette palette)
    {
        _scene!.Palette = palette;
        _gauge!.SetColor(palette.Gauge);
    }

    public override void PostDraw(CasaEngineGame game, GameTime gameTime)
    {
        if (_turn < 0 || _reported || Array.IndexOf(ReadingTurns, _turn) < 0 || _readings.ContainsKey(_turn))
        {
            return;
        }

        var device = game.GraphicsDevice;
        var width = device.PresentationParameters.BackBufferWidth;
        var height = device.PresentationParameters.BackBufferHeight;
        var data = new byte[width * height * 4];
        device.GetBackBufferData(data);

        var frozen = game.FrozenFrameComponent.Service;
        var view = game.GameManager.ViewManager.ActiveView;
        var rect = view?.Surface.ViewportRect ?? Rectangle.Empty;
        _readings[_turn] = new Reading(_turn, width, height, rect, data,
            frozen.HasImage, frozen.BaseVisible, frozen.CaptureCount, frozen.LastCapture,
            frozen.CapturedWidth, frozen.CapturedHeight);

        if (_turn == ReadingTurns[^1])
        {
            _reported = true;
            WriteReport(game);
        }
    }

    public override void Clean()
    {
        var game = _game;
        if (game != null)
        {
            var service = game.FrozenFrameComponent.Service;
            service.Release();
            service.RetainLastFrame = false;

            var effects = game.ScreenEffectComponent.Service;
            effects.Clear();
            effects.Layer = _previousLayer;

            game.RuntimeContext.ProjectSettings.VirtualResolution = _previousVirtualResolution;
            var current = game.GetDisplaySettings();
            if (current.Width != _previousDisplay.Width || current.Height != _previousDisplay.Height)
            {
                game.ApplyDisplaySettings(_previousDisplay, persistToProjectSettings: false);
            }
        }

        _pixel?.Dispose();
        _pixel = null;
        _scene = null;
        _gauge = null;
        _readings.Clear();
        _game = null;
    }

    // ---- Report ---------------------------------------------------------------------------------------------------

    private sealed record Reading(
        int Turn, int Width, int Height, Rectangle View, byte[] Data,
        bool HasImage, bool BaseVisible, long CaptureCount, FrozenFrameCaptureState Last, int CapturedWidth, int CapturedHeight);

    private sealed record Landmark(string Name, int X, int Y, Color Expected);

    private void WriteReport(CasaEngineGame game)
    {
        var report = new StringBuilder();
        var failures = new List<string>();
        void Line(string text) => report.AppendLine(text);
        void Check(string id, bool ok, string text)
        {
            Line(string.Create(CultureInfo.InvariantCulture, $"{(ok ? "OK  " : "FAIL")} {id} {text}"));
            if (!ok)
            {
                failures.Add(id);
            }
        }

        Line($"demo={Title} virtual={VirtualWidth}x{VirtualHeight} tolerance=+-{Tolerance} on shader colours, octet for octet on the equalities");
        foreach (var turn in ReadingTurns)
        {
            var r = _readings[turn];
            Line(string.Create(CultureInfo.InvariantCulture,
                $"turn={turn} backbuffer={r.Width}x{r.Height} view=({r.View.X},{r.View.Y},{r.View.Width},{r.View.Height}) hasImage={r.HasImage} baseVisible={r.BaseVisible} captureCount={r.CaptureCount} last={r.Last} captured={r.CapturedWidth}x{r.CapturedHeight}"));
        }

        var r9 = _readings[9];
        var r11 = _readings[11];
        var r13 = _readings[13];
        var r14 = _readings[14];
        var r17 = _readings[17];
        var r19 = _readings[19];
        var r21 = _readings[21];
        var r23 = _readings[23];
        var r27 = _readings[27];

        // The gauge: its place is found by scanning the live turn 9 for the colour the demo posed (its pixel geometry belongs
        // to the UI layout, not to the property under test).
        FindGauge(r9, FrozenFramePalettes.A.Gauge);
        Check("G0", _gaugeFound, string.Create(CultureInfo.InvariantCulture,
            $"the gauge colour of scene A is found in the live turn 9 (centre in virtual pixels ({_gaugeCentreVirtual.X},{_gaugeCentreVirtual.Y}))"));

        var landmarksA = Landmarks(FrozenFramePalettes.A, 0);
        var landmarksVeil = Landmarks(FrozenFramePalettes.A, VeilLevel);

        // Turn 9 as a control: the live scene A reads as the colours posed.
        CheckLandmarks(Check, "D1", r9, landmarksA, "the live scene A at turn 9 reads as the colours posed");

        // D2: the frozen image is the draw of the turn before the request.
        var same11 = CompareView(r11, r9, out var detail11);
        Check("D2", r11.View == r9.View && same11,
            $"turn 11 equals turn 9 octet for octet over the whole view ({detail11}); view {r11.View} vs {r9.View}");
        CheckLandmarks(Check, "D2b", r11, landmarksA, "turn 11 reads as scene A although scene B is live behind it");

        // D8: the stretch with point sampling.
        var sizesDiffer = r13.View.Width == StretchedWidth && r13.View.Height == StretchedHeight
            && (r13.View.Width != r9.View.Width || r13.View.Height != r9.View.Height);
        Check("D8a", sizesDiffer, $"turn 13 view is {StretchedWidth}x{StretchedHeight} and differs from turn 9's {r9.View.Width}x{r9.View.Height} (the step is exercised)");
        if (sizesDiffer)
        {
            var mismatches = CompareStretched(r13, r9, out var first);
            Check("D8b", mismatches == 0,
                $"every pixel (x, y) of turn 13 is the pixel (floor((x + 0.5) * {r9.View.Width} / {r13.View.Width}), floor((y + 0.5) * {r9.View.Height} / {r13.View.Height})) of turn 9: {mismatches} mismatch(es){first}");
            CheckEdgeRow(Check, r13, r9);
            CheckLandmarks(Check, "D8d", r13, landmarksA, "turn 13 landmarks read as scene A");
        }

        // D4: the veil over the frozen image.
        var viewBack = r14.View == r9.View;
        Check("D4a", viewBack, $"turn 14 view {r14.View} equals turn 9 view {r9.View} (the original size is back)");
        CheckLandmarks(Check, "D4b", r14, landmarksVeil, $"turn 14 landmarks are max(0, channel - {VeilLevel}) of scene A (ReverseSubtract, screen-effects.md:160)");

        // D9: the image holds after other targets were bound and the veil was lifted.
        var same17 = CompareView(r17, r11, out var detail17);
        Check("D9", r17.View == r9.View && same17,
            $"turn 17 equals turn 11 octet for octet ({detail17}); view {r17.View} vs turn 9 {r9.View}");

        // D3: the live scene B after HideBase.
        var palB = Landmarks(FrozenFramePalettes.B, 0);
        CheckLandmarks(Check, "D3", r19, palB, "turn 19 (HideBase at 18) reads as the live scene B");
        Check("D3b", LandmarksDiffer(r19, landmarksA), "turn 19 landmarks differ from those of turn 9");

        // D5: the second request keeps the draw of the turn before it (scene C), not the scene of the request (D).
        var landmarksC = Landmarks(FrozenFramePalettes.C, 0);
        var landmarksD = Landmarks(FrozenFramePalettes.D, 0);
        var same23 = CompareView(r23, r21, out var detail23);
        Check("D5a", r23.View == r21.View && same23, $"turn 23 equals turn 21 octet for octet ({detail23})");
        CheckLandmarks(Check, "D5b", r21, landmarksC, "turn 21 reads as the live scene C");
        CheckLandmarks(Check, "D5c", r23, landmarksC, "turn 23 reads as scene C");
        Check("D5d", LandmarksDiffer(r23, landmarksD), "turn 23 differs from the live scene D (what the mutant G2 would show)");
        Check("D5e", LandmarksDiffer(r23, landmarksA), "turn 23 differs from turn 9");

        // D6: the option is off.
        var landmarksE = Landmarks(FrozenFramePalettes.E, 0);
        CheckLandmarks(Check, "D6a", r27, landmarksE, "turn 27 reads as the live scene E");
        Check("D6b", r27.Last == FrozenFrameCaptureState.Unavailable && !r27.HasImage,
            $"turn 27 LastCapture={r27.Last} (expected Unavailable), HasImage={r27.HasImage} (expected False)");
        Check("D6c", r27.CaptureCount == _captureCountAtTurn24 && _captureCountAtTurn24 == 2,
            $"CaptureCount is {_captureCountAtTurn24} at turn 24 (expected 2: turns 10 and 22) and {r27.CaptureCount} at turn 27 (unchanged)");
        Check("D6d", Warnings.Count == 1, $"the warning \"Capture unavailable\" was logged {Warnings.Count} time(s) (expected once)");

        // D7: the black bands.
        var outside = 0;
        var notBlack = 0;
        foreach (var turn in ReadingTurns)
        {
            var r = _readings[turn];
            for (var y = 0; y < r.Height; y++)
            {
                for (var x = 0; x < r.Width; x++)
                {
                    if (r.View.Contains(x, y))
                    {
                        continue;
                    }

                    outside++;
                    var o = (y * r.Width + x) * 4;
                    if (r.Data[o] != 0 || r.Data[o + 1] != 0 || r.Data[o + 2] != 0)
                    {
                        notBlack++;
                    }
                }
            }
        }

        Check("D7", notBlack == 0, $"{outside} pixel(s) outside the view over the nine readings, {notBlack} not black"
            + (outside == 0 ? " (the windows 960x720 and 1280x960 are whole multiples of 320x240: the view covers the window and no band exists, so D7 is vacuous here)" : string.Empty));

        Line(failures.Count == 0 ? "result=PASS" : "result=FAIL " + string.Join(",", failures));
        Logs.WriteInfo($"[{Title}] report{Environment.NewLine}{report}");

        var path = Environment.GetEnvironmentVariable("CASAENGINE_DEMO_PIXELS_PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            var full = Path.GetFullPath(path);
            File.WriteAllText(full, report.ToString());
            foreach (var turn in ReadingTurns)
            {
                SavePng(game, _readings[turn], Path.ChangeExtension(full, null) + $"-turn{turn:D2}.png");
            }

            Environment.ExitCode = failures.Count == 0 ? 0 : 1;
            game.Exit();
        }
    }

    private static void CheckLandmarks(Action<string, bool, string> check, string id, Reading reading, IReadOnlyList<Landmark> landmarks, string text)
    {
        var bad = new StringBuilder();
        foreach (var landmark in landmarks)
        {
            var read = Pixel(reading, landmark.X, landmark.Y, out var px, out var py);
            if (!Near(read, landmark.Expected))
            {
                bad.Append(string.Create(CultureInfo.InvariantCulture,
                    $" {landmark.Name}@({px},{py}) read=({read.R},{read.G},{read.B}) expected=({landmark.Expected.R},{landmark.Expected.G},{landmark.Expected.B});"));
            }
        }

        check(id, bad.Length == 0, text + (bad.Length == 0 ? string.Empty : ":" + bad));
    }

    /// <summary>True when every landmark of the reading is away from the colour listed by more than the tolerance in some channel.</summary>
    private static bool LandmarksDiffer(Reading reading, IReadOnlyList<Landmark> notExpected)
    {
        for (var i = 0; i < notExpected.Count; i++)
        {
            var read = Pixel(reading, notExpected[i].X, notExpected[i].Y, out _, out _);
            if (Near(read, notExpected[i].Expected))
            {
                return false;
            }
        }

        return true;
    }

    private static Color Pixel(Reading reading, int virtualX, int virtualY, out int px, out int py)
    {
        var k = reading.View.Width / VirtualWidth;
        px = reading.View.X + virtualX * k + k / 2;
        py = reading.View.Y + virtualY * k + k / 2;
        if (px < 0 || py < 0 || px >= reading.Width || py >= reading.Height)
        {
            return new Color(0, 0, 0, 0);
        }

        var o = (py * reading.Width + px) * 4;
        return new Color(reading.Data[o], reading.Data[o + 1], reading.Data[o + 2], reading.Data[o + 3]);
    }

    private static bool Near(Color a, Color b)
        => Math.Abs(a.R - b.R) <= Tolerance && Math.Abs(a.G - b.G) <= Tolerance && Math.Abs(a.B - b.B) <= Tolerance;

    private void FindGauge(Reading reading, Color colour)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (var y = reading.View.Top; y < reading.View.Bottom; y++)
        {
            for (var x = reading.View.Left; x < reading.View.Right; x++)
            {
                var o = (y * reading.Width + x) * 4;
                if (Math.Abs(reading.Data[o] - colour.R) <= Tolerance
                    && Math.Abs(reading.Data[o + 1] - colour.G) <= Tolerance
                    && Math.Abs(reading.Data[o + 2] - colour.B) <= Tolerance)
                {
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }
        }

        if (maxX < 0)
        {
            return;
        }

        var k = reading.View.Width / VirtualWidth;
        _gaugeFound = true;
        _gaugeCentreVirtual = new Point(((minX + maxX) / 2 - reading.View.X) / k, ((minY + maxY) / 2 - reading.View.Y) / k);
    }

    private List<Landmark> Landmarks(FrozenFramePalette palette, int subtract)
    {
        Color Veil(Color c) => new(
            (byte)Math.Max(0, c.R - subtract), (byte)Math.Max(0, c.G - subtract), (byte)Math.Max(0, c.B - subtract));

        var list = new List<Landmark>
        {
            new("left", 110, 200, Veil(palette.Left)),
            new("right", 230, 200, Veil(palette.Right)),
            new("edgeL", 159, 60, Veil(palette.Left)),
            new("edgeR", 160, 60, Veil(palette.Right)),
            new("patch", 40, 120, Veil(palette.Patch)),
            new("alpha", 100, 120, Veil(FrozenFramePalette.AlphaOver(palette.Alpha, palette.Left))),
            new("add", 220, 120, Veil(FrozenFramePalette.AddOver(palette.Add, palette.Right))),
            new("sub", 280, 120, Veil(FrozenFramePalette.SubtractOver(palette.Sub, palette.Right))),
        };

        if (_gaugeFound)
        {
            list.Add(new Landmark("gauge", _gaugeCentreVirtual.X, _gaugeCentreVirtual.Y, Veil(palette.Gauge)));
        }

        return list;
    }

    private static bool CompareView(Reading a, Reading b, out string detail)
    {
        if (a.View != b.View)
        {
            detail = "views differ";
            return false;
        }

        var differing = 0;
        var firstX = -1;
        var firstY = -1;
        for (var y = a.View.Top; y < a.View.Bottom; y++)
        {
            for (var x = a.View.Left; x < a.View.Right; x++)
            {
                var oa = (y * a.Width + x) * 4;
                var ob = (y * b.Width + x) * 4;
                if (a.Data[oa] != b.Data[ob] || a.Data[oa + 1] != b.Data[ob + 1]
                    || a.Data[oa + 2] != b.Data[ob + 2] || a.Data[oa + 3] != b.Data[ob + 3])
                {
                    if (differing == 0)
                    {
                        firstX = x;
                        firstY = y;
                    }

                    differing++;
                }
            }
        }

        detail = differing == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{a.View.Width * a.View.Height} pixels compared, 0 differ")
            : string.Create(CultureInfo.InvariantCulture, $"{differing} pixel(s) differ, first at ({firstX},{firstY})");
        return differing == 0;
    }

    private static int CompareStretched(Reading stretched, Reading source, out string first)
    {
        var mismatches = 0;
        first = string.Empty;
        for (var y = 0; y < stretched.View.Height; y++)
        {
            var sy = (int)((2L * y + 1) * source.View.Height / (2L * stretched.View.Height));
            for (var x = 0; x < stretched.View.Width; x++)
            {
                var sx = (int)((2L * x + 1) * source.View.Width / (2L * stretched.View.Width));
                var oa = ((stretched.View.Y + y) * stretched.Width + stretched.View.X + x) * 4;
                var ob = ((source.View.Y + sy) * source.Width + source.View.X + sx) * 4;
                if (stretched.Data[oa] != source.Data[ob] || stretched.Data[oa + 1] != source.Data[ob + 1]
                    || stretched.Data[oa + 2] != source.Data[ob + 2] || stretched.Data[oa + 3] != source.Data[ob + 3])
                {
                    if (mismatches == 0)
                    {
                        first = string.Create(CultureInfo.InvariantCulture,
                            $", first at ({x},{y}) from source ({sx},{sy}): read=({stretched.Data[oa]},{stretched.Data[oa + 1]},{stretched.Data[oa + 2]}) source=({source.Data[ob]},{source.Data[ob + 1]},{source.Data[ob + 2]})");
                    }

                    mismatches++;
                }
            }
        }

        return mismatches;
    }

    /// <summary>Row of the hard edge, read at turn 13: only the two colours of the edge, the edge at column 640.</summary>
    private void CheckEdgeRow(Action<string, bool, string> check, Reading stretched, Reading source)
    {
        var k = stretched.View.Width / VirtualWidth;
        var y = stretched.View.Y + 60 * k + k / 2;
        var ks = source.View.Width / VirtualWidth;
        var leftRef = Pixel(source, 159, 60, out _, out _);
        var rightRef = Pixel(source, 160, 60, out _, out _);
        var other = 0;
        var firstRight = -1;
        for (var x = stretched.View.Left; x < stretched.View.Right; x++)
        {
            var o = (y * stretched.Width + x) * 4;
            var c = new Color(stretched.Data[o], stretched.Data[o + 1], stretched.Data[o + 2]);
            var isLeft = c.R == leftRef.R && c.G == leftRef.G && c.B == leftRef.B;
            var isRight = c.R == rightRef.R && c.G == rightRef.G && c.B == rightRef.B;
            if (!isLeft && !isRight)
            {
                other++;
            }

            if (isRight && firstRight < 0)
            {
                firstRight = x;
            }
        }

        _ = ks;
        check("D8c", other == 0 && firstRight == stretched.View.X + 640,
            string.Create(CultureInfo.InvariantCulture,
                $"row {y} of turn 13 holds only the two colours of the edge ({other} pixel(s) of another colour) and the edge falls on column {firstRight} (expected {stretched.View.X + 640}; a linear sampling mixes column 639)"));
    }

    private static void SavePng(CasaEngineGame game, Reading reading, string path)
    {
        using var texture = new Texture2D(game.GraphicsDevice, reading.Width, reading.Height, false, SurfaceFormat.Color);
        texture.SetData(reading.Data);
        using var stream = File.Create(path);
        texture.SaveAsPng(stream, reading.Width, reading.Height);
    }

    // ---- Counting logger --------------------------------------------------------------------------------------------

    private sealed class WarningCounter : ILogger
    {
        public bool Installed { get; set; }

        public int Count { get; set; }

        public void Close()
        {
        }

        public void WriteTrace(string msg)
        {
        }

        public void WriteDebug(string msg)
        {
        }

        public void WriteInfo(string msg)
        {
        }

        public void WriteWarning(string msg)
        {
            if (msg.Contains("[FrozenFrame] Capture unavailable", StringComparison.Ordinal))
            {
                Count++;
            }
        }

        public void WriteError(string msg)
        {
        }
    }
}

/// <summary>The colours of one scene of <see cref="FrozenFrameDemo"/>.</summary>
internal sealed record FrozenFramePalette(Color Left, Color Right, Color Patch, Color Alpha, Color Add, Color Sub, Color Gauge)
{
    /// <summary><see cref="BlendState.NonPremultiplied"/>: source * alpha + destination * (1 - alpha), alpha of the source colour.</summary>
    public static Color AlphaOver(Color source, Color destination)
    {
        var a = source.A / 255f;
        return new Color(
            (byte)MathF.Round(source.R * a + destination.R * (1 - a)),
            (byte)MathF.Round(source.G * a + destination.G * (1 - a)),
            (byte)MathF.Round(source.B * a + destination.B * (1 - a)));
    }

    /// <summary>Additive: destination + source, saturated.</summary>
    public static Color AddOver(Color source, Color destination)
        => new(
            (byte)Math.Min(255, destination.R + source.R),
            (byte)Math.Min(255, destination.G + source.G),
            (byte)Math.Min(255, destination.B + source.B));

    /// <summary>Subtractive (ReverseSubtract): destination - source, saturated at zero.</summary>
    public static Color SubtractOver(Color source, Color destination)
        => new(
            (byte)Math.Max(0, destination.R - source.R),
            (byte)Math.Max(0, destination.G - source.G),
            (byte)Math.Max(0, destination.B - source.B));
}

/// <summary>The five scenes of the demo: A (turn 0), B (10), C (20), D (22), E (26). Every colour differs from scene to scene.</summary>
internal static class FrozenFramePalettes
{
    public static readonly FrozenFramePalette A = new(
        new Color(200, 120, 40), new Color(30, 90, 160), new Color(220, 20, 20), new Color(20, 220, 20, 128),
        new Color(80, 60, 40), new Color(60, 30, 90), new Color(200, 200, 10));

    public static readonly FrozenFramePalette B = new(
        new Color(60, 160, 200), new Color(210, 70, 130), new Color(240, 240, 20), new Color(230, 40, 40, 128),
        new Color(50, 70, 90), new Color(100, 40, 20), new Color(10, 180, 220));

    public static readonly FrozenFramePalette C = new(
        new Color(90, 200, 90), new Color(170, 50, 50), new Color(30, 30, 220), new Color(240, 200, 20, 128),
        new Color(20, 100, 60), new Color(40, 60, 30), new Color(220, 110, 10));

    public static readonly FrozenFramePalette D = new(
        new Color(240, 100, 160), new Color(20, 140, 70), new Color(150, 150, 150), new Color(10, 10, 240, 128),
        new Color(90, 30, 100), new Color(50, 20, 70), new Color(120, 20, 200));

    public static readonly FrozenFramePalette E = new(
        new Color(250, 250, 60), new Color(60, 20, 240), new Color(10, 120, 120), new Color(250, 20, 250, 128),
        new Color(40, 160, 10), new Color(120, 120, 120), new Color(230, 60, 60));
}

/// <summary>
/// The scene of <see cref="FrozenFrameDemo"/>: two flat halves with a hard edge on the virtual column 160 and four patches
/// (opaque, alpha, additive, subtractive), drawn by the sprite renderer in the world pass. World units are pixels, +Y up,
/// the camera is on the origin, so the virtual pixel (vx, vy) is the world point (vx - 160, 120 - vy).
/// </summary>
internal sealed class FrozenFrameScene : SceneComponent
{
    private readonly SpriteRendererComponent _renderer;
    private readonly Texture2D _pixel;

    public FrozenFrameScene(SpriteRendererComponent renderer, Texture2D pixel)
    {
        _renderer = renderer;
        _pixel = pixel;
        Palette = FrozenFramePalettes.A;
    }

    public FrozenFramePalette Palette { get; set; }

    public override EntityComponent Clone() => throw new NotSupportedException();

    public override BoundingBox GetBoundingBox()
        => new(new Vector3(-400f, -400f, -10f), new Vector3(400f, 400f, 10f));

    public override void Draw(float elapsedTime)
    {
        Fill(0, 0, 160, 240, Palette.Left, SpriteBlendMode.Opaque, 0);
        Fill(160, 0, 160, 240, Palette.Right, SpriteBlendMode.Opaque, 1);
        Fill(20, 100, 40, 40, Palette.Patch, SpriteBlendMode.Opaque, 2);
        Fill(80, 100, 40, 40, Palette.Alpha, SpriteBlendMode.AlphaBlend, 3);
        Fill(200, 100, 40, 40, Palette.Add, SpriteBlendMode.Additive, 4);
        Fill(260, 100, 40, 40, Palette.Sub, SpriteBlendMode.Subtractive, 5);

        base.Draw(elapsedTime);
    }

    private void Fill(int vx, int vy, int width, int height, Color color, SpriteBlendMode blend, int order)
    {
        var sortKey = new RenderSortKey2D((int)RenderPass2D.YSortedWorld, 0, order, 0, 0, 0, order);
        _renderer.DrawSprite(
            _pixel, _pixel.Bounds, Point.Zero, new Vector2(vx - 160, 120 - vy), 0f, new Vector2(width, height), color, 0f,
            in sortKey, SpriteEffects.None, blend);
    }
}
