#nullable enable

using System;
using CasaEngine.Editor.Styling;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// A horizontal level meter for one <see cref="AudioMeterTrack"/> (plan T6.2, decision P23): the peak as a green, yellow
/// and red bar, the RMS as a thinner bar inside it, the peak hold as a marker, and, for the final output, a lamp that is lit
/// for a while after an over. It only draws what the track holds, on every frame, and handles no input.
/// </summary>
internal sealed class AudioMeterControl : MGElement
{
    /// <summary>The level at the empty end of the meter, in dBFS. Full scale is the full end.</summary>
    public const float DisplayFloorDb = -60f;

    /// <summary>Above this level the bar turns yellow, in dBFS.</summary>
    public const float WarningDb = -12f;

    /// <summary>Above this level the bar turns red, in dBFS.</summary>
    public const float DangerDb = -3f;

    public const int MeterHeight = 14;
    public const int PreferredMeterWidth = 180;

    private const float OverLampWidth = 6f;

    private static readonly Color GreenZone = new(64, 176, 92);
    private static readonly Color YellowZone = new(214, 184, 64);
    private static readonly Color RedZone = new(222, 72, 62);

    private readonly AudioMeterTrack _track;

    public AudioMeterControl(MGWindow window, AudioMeterTrack track)
        : base(window, MGElementType.Misc)
    {
        _track = track ?? throw new ArgumentNullException(nameof(track));
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Center;
        IsHitTestVisible = false;
    }

    /// <summary>Where a level sits along the meter: 0 at <see cref="DisplayFloorDb"/> and below, 1 at full scale and above.</summary>
    public static float ToFraction(float db)
    {
        if (!(db > DisplayFloorDb))
        {
            return 0f;
        }

        return Math.Min(1f, (db - DisplayFloorDb) / -DisplayFloorDb);
    }

    public override Thickness MeasureSelfOverride(Size availableSize, out Thickness sharedSize)
    {
        sharedSize = new Thickness(0);
        return new Thickness(Math.Min(PreferredMeterWidth, availableSize.Width), MeterHeight, 0, 0);
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        if (layoutBounds.Width <= 0 || layoutBounds.Height <= 0)
        {
            return;
        }

        Vector2 origin = DA.Offset.ToVector2();
        float opacity = DA.Opacity;
        float left = layoutBounds.X;
        float top = layoutBounds.Y;
        float width = layoutBounds.Width;
        float height = layoutBounds.Height;

        DA.Context.FillRectangle(origin, new RectangleF(left, top, width, height), EditorThemePalette.ContentBackground * opacity);

        if (_track.HasLevel)
        {
            DrawPeakBar(DA, origin, opacity, left, top, width, height);
            DrawRmsBar(DA, origin, opacity, left, top, width, height);
            DrawHoldMarker(DA, origin, opacity, left, top, width, height);
        }

        if (_track.IsOutput && _track.IsOverLit)
        {
            DA.Context.FillRectangle(origin, new RectangleF(left + width - OverLampWidth, top, OverLampWidth, height), RedZone * opacity);
        }

        DrawScale(DA, origin, opacity, left, top, width, height);
    }

    private void DrawPeakBar(ElementDrawArgs DA, Vector2 origin, float opacity, float left, float top, float width, float height)
    {
        float peak = ToFraction(_track.PeakDb);
        float warning = ToFraction(WarningDb);
        float danger = ToFraction(DangerDb);

        float green = Math.Min(peak, warning);
        float yellow = Math.Max(0f, Math.Min(peak, danger) - warning);
        float red = Math.Max(0f, peak - danger);

        FillSegment(DA, origin, GreenZone * opacity, left, top, height, 0f, green, width);
        FillSegment(DA, origin, YellowZone * opacity, left, top, height, warning, yellow, width);
        FillSegment(DA, origin, RedZone * opacity, left, top, height, danger, red, width);
    }

    private void DrawRmsBar(ElementDrawArgs DA, Vector2 origin, float opacity, float left, float top, float width, float height)
    {
        float rms = ToFraction(_track.RmsDb);
        if (rms <= 0f)
        {
            return;
        }

        float inset = MathF.Floor(height * 0.3f);
        DA.Context.FillRectangle(
            origin,
            new RectangleF(left, top + inset, width * rms, height - (inset * 2f)),
            Color.White * (0.65f * opacity));
    }

    private void DrawHoldMarker(ElementDrawArgs DA, Vector2 origin, float opacity, float left, float top, float width, float height)
    {
        float hold = ToFraction(_track.HoldDb);
        if (hold <= 0f)
        {
            return;
        }

        float x = Math.Min(left + (width * hold), left + width - 2f);
        DA.Context.FillRectangle(origin, new RectangleF(x, top, 2f, height), Color.White * opacity);
    }

    private static void DrawScale(ElementDrawArgs DA, Vector2 origin, float opacity, float left, float top, float width, float height)
    {
        Color tick = EditorThemePalette.PreviewSurfaceBorder * opacity;

        // A tick every 12 dB from -48 dBFS up to full scale.
        for (float db = -48f; db <= 0f; db += 12f)
        {
            float x = Math.Min(left + (width * ToFraction(db)), left + width - 1f);
            DA.Context.FillRectangle(origin, new RectangleF(x, top + (height * 0.7f), 1f, height * 0.3f), tick);
        }

        DA.Context.FillRectangle(origin, new RectangleF(left, top + height - 1f, width, 1f), tick);
    }

    private static void FillSegment(ElementDrawArgs DA, Vector2 origin, Color color, float left, float top, float height, float startFraction, float lengthFraction, float width)
    {
        if (lengthFraction <= 0f)
        {
            return;
        }

        DA.Context.FillRectangle(origin, new RectangleF(left + (width * startFraction), top, width * lengthFraction, height), color);
    }
}
