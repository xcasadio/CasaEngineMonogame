using System.Diagnostics;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Draws a per-view debug overlay with performance statistics.
///
/// Statistics shown:
/// <list type="bullet">
///   <item>Smoothed FPS (frames per second).</item>
///   <item>Global game Update and Draw timings with a rolling 10-second history.</item>
///   <item>View name and update mode.</item>
///   <item>View resolution and camera position.</item>
///   <item>Per-view render stats: draw calls, shader binds, texture binds, state changes.</item>
///   <item>Per-view opaque and transparent item counts.</item>
///   <item>Active RT count from <see cref="RenderTargetPool"/> (if available).</item>
/// </list>
///
/// Enable per view by setting <see cref="RenderView.ShowDebugOverlay"/> = true.
/// The overlay is drawn by <see cref="RenderPipeline"/> after each view's render pass.
/// </summary>
public sealed class DebugOverlay
{
    private const int Padding     = 6;
    private const int LineSpacing = 18;

    private readonly SpriteBatch   _spriteBatch;
    private readonly DynamicSpriteFont _font;
    private readonly Texture2D     _background;
    private readonly FrameTimingHistory _updateHistory = new();
    private readonly FrameTimingHistory _drawHistory = new();

    // FPS tracking
    private float _fpsAccum;
    private int   _fpsFrames;
    private float _fps;

    public DebugOverlay(SpriteBatch spriteBatch, FontSystem fontSystem)
    {
        _spriteBatch = spriteBatch;
        _font        = fontSystem.GetFont(13);

        // 1×1 semi-transparent background
        _background = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
        _background.SetData([new Color(0, 0, 0, 160)]);
    }

    internal void RecordUpdate(double milliseconds)
    {
        _updateHistory.AddSample(milliseconds);
    }

    internal void RecordDraw(double milliseconds, float deltaSeconds)
    {
        _drawHistory.AddSample(milliseconds);

        if (deltaSeconds <= 0f)
        {
            return;
        }

        _fpsAccum += deltaSeconds;
        _fpsFrames++;
        if (_fpsAccum >= 0.5f)
        {
            _fps = _fpsFrames / _fpsAccum;
            _fpsAccum = 0f;
            _fpsFrames = 0;
        }
    }

    /// <summary>
    /// Draws the debug overlay for <paramref name="view"/> into the current render target.
    /// Call after the view's pipeline has finished (surface still bound).
    /// </summary>
    public void Draw(RenderView view, Rectangle viewportRect, float deltaSeconds)
    {
        _ = deltaSeconds;

        // Collect stat lines
        FrameTimingSummary updateSummary = _updateHistory.GetSummary();
        FrameTimingSummary drawSummary = _drawHistory.GetSummary();
        var pool      = RenderTargetPool.Shared;
        var cam       = view.Camera;
        var camPos    = cam?.Position ?? Vector3.Zero;
        var stats     = view.RenderStats;
        var policies  = view.World.PolicyDiagnostics;
        var viewName  = string.IsNullOrEmpty(view.Name) ? "unnamed" : view.Name;
        var lines     = new List<string>(11)
        {
            $"View: {viewName}",
            $"FPS: {_fps:F1}  Mode: {view.UpdateMode}",
            $"Game Update: {updateSummary.LatestMilliseconds:F2} ms  avg10: {updateSummary.AverageMilliseconds:F2}  max10: {updateSummary.MaxMilliseconds:F2}",
            $"Game Draw: {drawSummary.LatestMilliseconds:F2} ms  avg10: {drawSummary.AverageMilliseconds:F2}  max10: {drawSummary.MaxMilliseconds:F2}",
            $"Size: {viewportRect.Width}x{viewportRect.Height}  Scale: {view.ResolutionScale:P0}",
            $"Cam: {camPos.X:F1}, {camPos.Y:F1}, {camPos.Z:F1}",
            $"Draws: {stats.DrawCalls}  FX: {stats.EffectBinds}  Tex: {stats.TextureBinds}",
            $"State: {stats.StateChanges}  O: {stats.OpaqueItems}  T: {stats.TransparentItems}",
            $"Particles: {stats.ParticleCount}  Lines: {stats.LineCount}  PRender: {stats.ParticleRenderCpuMilliseconds:F2} ms",
            $"Policies: total {policies.TotalEntities}  explicit {policies.ExplicitPolicyEntities}  defaults {policies.DefaultPolicyEntities}  updated {policies.UpdatedEntities}",
            $"Tick N/C/E {policies.TickNeverEntities}/{policies.TickConditionalEntities}/{policies.TickEveryFrameEntities}  Spatial S/D {policies.SpatialStaticEntities}/{policies.SpatialDynamicEntities}",
            $"Render S/M/G {policies.RenderStaticEntities}/{policies.RenderMaterialAnimatedEntities}/{policies.RenderGeometryAnimatedEntities}  Mobility S/M {policies.MobilityStaticEntities}/{policies.MobilityMovableEntities}  Warn {policies.SuspectCombinationCount}",
        };

        if (pool != null)
        {
            lines.Add($"RT pool: {pool.TotalCount - pool.FreeCount} active / {pool.FreeCount} free");
        }

        float maxLineWidth = 0f;
        foreach (var line in lines)
        {
            var lineWidth = _font.MeasureString(line).X;
            if (lineWidth > maxLineWidth)
            {
                maxLineWidth = lineWidth;
            }
        }

        // Background rect
        int bgW = (int)MathF.Ceiling(maxLineWidth) + Padding * 2;
        int bgH = Padding * 2 + lines.Count * LineSpacing;
        var bgRect = new Rectangle(
            Padding,
            Padding,
            bgW, bgH);

        _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
            null, DepthStencilState.None, RasterizerState.CullNone);

        _spriteBatch.Draw(_background, bgRect, Color.White);

        for (int i = 0; i < lines.Count; i++)
        {
            var pos = new Vector2(
                bgRect.X + Padding,
                bgRect.Y + Padding + i * LineSpacing);
            _font.DrawText(_spriteBatch, lines[i], pos, Color.White);
        }

        _spriteBatch.End();
    }

    /// <summary>Disposes the 1×1 background texture.</summary>
    public void Dispose()
    {
        _background.Dispose();
    }

    private readonly struct FrameTimingSummary
    {
        public static readonly FrameTimingSummary Empty = new(0.0, 0.0, 0.0, 0);

        public FrameTimingSummary(double latestMilliseconds, double averageMilliseconds, double maxMilliseconds, int sampleCount)
        {
            LatestMilliseconds = latestMilliseconds;
            AverageMilliseconds = averageMilliseconds;
            MaxMilliseconds = maxMilliseconds;
            SampleCount = sampleCount;
        }

        public double LatestMilliseconds { get; }
        public double AverageMilliseconds { get; }
        public double MaxMilliseconds { get; }
        public int SampleCount { get; }
    }

    private sealed class FrameTimingHistory
    {
        private const int MaxSamples = 4096;
        private static readonly long WindowTicks = (long)(10.0 * Stopwatch.Frequency);

        private readonly long[] _timestamps = new long[MaxSamples];
        private readonly double[] _values = new double[MaxSamples];

        private int _startIndex;
        private int _count;
        private FrameTimingSummary _summary = FrameTimingSummary.Empty;

        public void AddSample(double milliseconds)
        {
            long timestamp = Stopwatch.GetTimestamp();

            if (_count == MaxSamples)
            {
                _startIndex = (_startIndex + 1) % MaxSamples;
                _count--;
            }

            int insertIndex = (_startIndex + _count) % MaxSamples;
            _timestamps[insertIndex] = timestamp;
            _values[insertIndex] = milliseconds;
            _count++;

            TrimExpired(timestamp);
            RebuildSummary();
        }

        public FrameTimingSummary GetSummary()
        {
            if (TrimExpired(Stopwatch.GetTimestamp()))
            {
                RebuildSummary();
            }

            return _summary;
        }

        private bool TrimExpired(long timestamp)
        {
            bool trimmed = false;
            long minTimestamp = timestamp - WindowTicks;

            while (_count > 0 && _timestamps[_startIndex] < minTimestamp)
            {
                _startIndex = (_startIndex + 1) % MaxSamples;
                _count--;
                trimmed = true;
            }

            if (_count == 0)
            {
                _summary = FrameTimingSummary.Empty;
            }

            return trimmed;
        }

        private void RebuildSummary()
        {
            if (_count == 0)
            {
                _summary = FrameTimingSummary.Empty;
                return;
            }

            double sum = 0.0;
            double max = 0.0;
            double latest = 0.0;

            for (int i = 0; i < _count; i++)
            {
                int index = (_startIndex + i) % MaxSamples;
                double value = _values[index];
                sum += value;
                if (value > max)
                {
                    max = value;
                }

                latest = value;
            }

            _summary = new FrameTimingSummary(latest, sum / _count, max, _count);
        }
    }
}
