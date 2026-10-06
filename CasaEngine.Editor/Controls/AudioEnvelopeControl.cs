#nullable enable

using System;
using CasaEngine.Editor.Styling;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// The columns an <see cref="AudioEnvelopeControl"/> draws (plan T10.8, decision D18): the level of the output over time
/// (<see cref="AudioLevelHistory"/>), and the drawing of a sound file in the inspector (plan T10.9).
/// </summary>
internal interface IAudioEnvelopeSource
{
    /// <summary>
    /// How many columns the source has, from the left edge to the right edge of the drawing. Constant between two reads of the
    /// columns: the control reads <see cref="ColumnCount"/> once per frame.
    /// </summary>
    int ColumnCount { get; }

    /// <summary>
    /// One column, drawn around the axis. <paramref name="lower"/> is in [-1, 0] and <paramref name="upper"/> in [0, 1], the
    /// extent of the column below and above the axis, as a fraction of the half height of the control: a symmetric source gives
    /// <c>-x</c> and <c>x</c>, a min and max source (a sound file) gives its minimum and its maximum. <paramref name="inner"/> is in
    /// [0, 1], the half height of a darker band that is drawn symmetrically inside the column (the RMS), 0 for none. An index
    /// outside [0, <see cref="ColumnCount"/>) must give an empty column rather than throw: the control draws on every frame.
    /// </summary>
    void GetColumn(int index, out float lower, out float upper, out float inner);
}

/// <summary>
/// A strip that draws an <see cref="IAudioEnvelopeSource"/> around a horizontal axis (plan T10.8, decision D18), modelled on
/// <see cref="AudioMeterControl"/>: fixed height, stretched width, no input, drawn on every frame from what the source holds and
/// without allocating. There is one drawn column per available pixel (several columns of the source share a pixel when there are
/// more of them, the extreme of each kind is kept so that a short peak is never lost), and as many pixels per column as there is room for.
/// </summary>
/// <remarks>
/// With <see cref="ShowLevelMarks"/> the source is on the scale of <see cref="AudioMeterControl"/> (0 at -60 dBFS, 1 at full scale):
/// the peak is drawn in the meters' green, yellow and red zones and marks are drawn at -12 and -3 dBFS, on both sides of the axis.
/// Without it the source is on any other scale, for instance linear: one colour, no mark.
/// </remarks>
internal sealed class AudioEnvelopeControl : MGElement
{
    public const int EnvelopeHeight = 48;
    public const int PreferredEnvelopeWidth = 240;

    private static readonly Color GreenZone = new(64, 176, 92);
    private static readonly Color YellowZone = new(214, 184, 64);
    private static readonly Color RedZone = new(222, 72, 62);

    /// <summary>The colour of a column without level marks (not on the dB scale, so without zones).</summary>
    private static readonly Color PlainColor = EditorThemePalette.InheritedBadge.BorderColor;

    private static readonly float WarningFraction = AudioMeterControl.ToFraction(AudioMeterControl.WarningDb);
    private static readonly float DangerFraction = AudioMeterControl.ToFraction(AudioMeterControl.DangerDb);

    public AudioEnvelopeControl(MGWindow window, IAudioEnvelopeSource? source = null)
        : base(window, MGElementType.Misc)
    {
        Source = source;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Center;
        IsHitTestVisible = false;
    }

    /// <summary>What is drawn, or null for an empty strip (background and axis only). It is read on every frame: changing it needs no other call.</summary>
    public IAudioEnvelopeSource? Source { get; set; }

    /// <summary>
    /// True when <see cref="Source"/> is on the dB scale of <see cref="AudioMeterControl"/>: the zone colours and the marks at
    /// -12 and -3 dBFS are drawn. False for a source on another scale (a linear drawing of a file). True by default.
    /// </summary>
    public bool ShowLevelMarks { get; set; } = true;

    public override Thickness MeasureSelfOverride(Size availableSize, out Thickness sharedSize)
    {
        sharedSize = new Thickness(0);
        return new Thickness(Math.Min(PreferredEnvelopeWidth, availableSize.Width), EnvelopeHeight, 0, 0);
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        if (layoutBounds.Width <= 0 || layoutBounds.Height <= 0)
        {
            return;
        }

        Vector2 origin = DA.Offset.ToVector2();
        float opacity = DA.Opacity;
        int left = layoutBounds.X;
        int width = layoutBounds.Width;
        float top = layoutBounds.Y;
        float height = layoutBounds.Height;
        float half = height * 0.5f;
        float axis = top + half;

        DA.Context.FillRectangle(origin, new RectangleF(left, top, width, height), EditorThemePalette.ContentBackground * opacity);
        DA.Context.FillRectangle(origin, new RectangleF(left, MathF.Floor(axis), width, 1f), EditorThemePalette.PreviewSurfaceBorder * opacity);

        IAudioEnvelopeSource? source = Source;
        int columns = source != null ? source.ColumnCount : 0;

        if (source != null && columns > 0)
        {
            int slices = Math.Min(columns, width);

            for (int slice = 0; slice < slices; slice++)
            {
                // The source columns of this slice (at least one, since there are no more slices than columns) and its pixels
                // (at least one, since there are no more slices than pixels).
                MergeColumns(source, SliceStart(slice, columns, slices), SliceStart(slice + 1, columns, slices), out float lower, out float upper, out float inner);

                int x = left + SliceStart(slice, width, slices);
                int nextX = left + SliceStart(slice + 1, width, slices);
                DrawColumn(DA, origin, opacity, x, nextX - x, axis, half, lower, upper, inner);
            }
        }

        if (ShowLevelMarks)
        {
            DrawLevelMarks(DA, origin, opacity, left, width, axis, half);
        }
    }

    /// <summary>
    /// Where slice <paramref name="slice"/> of <paramref name="slices"/> starts when <paramref name="total"/> items (source columns, or
    /// pixels) are split into that many slices of nearly the same size. <c>SliceStart(slices, total, slices)</c> is <paramref name="total"/>.
    /// </summary>
    internal static int SliceStart(int slice, int total, int slices)
    {
        return (int)((long)slice * total / slices);
    }

    /// <summary>
    /// The extent of several columns of a source drawn in one pixel slice: the lowest lower, the highest upper and the highest inner,
    /// each clamped to its range, so that a short peak is never lost when there are more columns than pixels. Written as comparisons
    /// so that a NaN from a source is ignored. An empty range gives an empty column.
    /// </summary>
    internal static void MergeColumns(IAudioEnvelopeSource source, int firstColumn, int endColumn, out float lower, out float upper, out float inner)
    {
        lower = 0f;
        upper = 0f;
        inner = 0f;

        for (int column = firstColumn; column < endColumn; column++)
        {
            source.GetColumn(column, out float columnLower, out float columnUpper, out float columnInner);

            if (columnLower < lower)
            {
                lower = columnLower;
            }

            if (columnUpper > upper)
            {
                upper = columnUpper;
            }

            if (columnInner > inner)
            {
                inner = columnInner;
            }
        }

        lower = Math.Max(lower, -1f);
        upper = Math.Min(upper, 1f);
        inner = Math.Min(inner, 1f);
    }

    private void DrawColumn(ElementDrawArgs DA, Vector2 origin, float opacity, int x, int columnWidth, float axis, float half, float lower, float upper, float inner)
    {
        float below = -lower;

        if (upper <= 0f && below <= 0f)
        {
            return;
        }

        if (ShowLevelMarks)
        {
            // Green through the axis up to the warning level on both sides, then yellow and red outwards, above and below.
            float greenUp = Math.Min(upper, WarningFraction) * half;
            float greenDown = Math.Min(below, WarningFraction) * half;
            DA.Context.FillRectangle(origin, new RectangleF(x, axis - greenUp, columnWidth, greenUp + greenDown), GreenZone * opacity);

            FillZone(DA, origin, YellowZone * opacity, x, columnWidth, axis, half, upper, below, WarningFraction, DangerFraction);
            FillZone(DA, origin, RedZone * opacity, x, columnWidth, axis, half, upper, below, DangerFraction, 1f);
        }
        else
        {
            float up = upper * half;
            float down = below * half;
            DA.Context.FillRectangle(origin, new RectangleF(x, axis - up, columnWidth, up + down), PlainColor * opacity);
        }

        // The inner band (the RMS) never exceeds the column, whatever the source says.
        float reach = Math.Min(inner, Math.Max(upper, below)) * half;
        if (reach > 0f)
        {
            DA.Context.FillRectangle(origin, new RectangleF(x, axis - reach, columnWidth, reach * 2f), Color.Black * (0.45f * opacity));
        }
    }

    /// <summary>Fills the part of the column above and below the axis that lies between two levels of the scale.</summary>
    private static void FillZone(ElementDrawArgs DA, Vector2 origin, Color color, int x, int columnWidth, float axis, float half, float upper, float below, float fromFraction, float toFraction)
    {
        if (upper > fromFraction)
        {
            float length = (Math.Min(upper, toFraction) - fromFraction) * half;
            DA.Context.FillRectangle(origin, new RectangleF(x, axis - (fromFraction * half) - length, columnWidth, length), color);
        }

        if (below > fromFraction)
        {
            float length = (Math.Min(below, toFraction) - fromFraction) * half;
            DA.Context.FillRectangle(origin, new RectangleF(x, axis + (fromFraction * half), columnWidth, length), color);
        }
    }

    private static void DrawLevelMarks(ElementDrawArgs DA, Vector2 origin, float opacity, int left, int width, float axis, float half)
    {
        Color yellow = YellowZone * (0.6f * opacity);
        Color red = RedZone * (0.6f * opacity);
        float warning = MathF.Round(WarningFraction * half);
        float danger = MathF.Round(DangerFraction * half);

        DA.Context.FillRectangle(origin, new RectangleF(left, axis - warning, width, 1f), yellow);
        DA.Context.FillRectangle(origin, new RectangleF(left, axis + warning - 1f, width, 1f), yellow);
        DA.Context.FillRectangle(origin, new RectangleF(left, axis - danger, width, 1f), red);
        DA.Context.FillRectangle(origin, new RectangleF(left, axis + danger - 1f, width, 1f), red);
    }
}
