#nullable enable

using System;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// A fixed-size ring of the last levels of one meter (plan T10.8, decision P55): for each column, the peak and the RMS of the
/// time that column covers, as the position along the meter scale (0 at <see cref="AudioMeterControl.DisplayFloorDb"/> and
/// below, 1 at full scale and above), exactly as <see cref="AudioMeterControl.ToFraction"/> gives it. The conversion from a
/// linear amplitude to that fraction (a <c>Log10</c>) is done once, when a column is written, so drawing the ring never converts
/// anything. Everything is allocated by the constructor: <see cref="Write"/> and <see cref="Reset"/> allocate nothing.
/// </summary>
/// <remarks>
/// As an <see cref="IAudioEnvelopeSource"/> the ring has a fixed number of columns, <see cref="Capacity"/>, the newest at the end:
/// a column that has not been written yet reads as silence, so the time axis does not move while the ring fills up (the right
/// edge is always "now"). The drawing is symmetric around the axis.
/// </remarks>
internal sealed class AudioLevelHistory : IAudioEnvelopeSource
{
    /// <summary>Columns of the history of the output in the mixer panel: with <see cref="DefaultSecondsPerColumn"/>, ten seconds.</summary>
    public const int DefaultColumns = 240;

    /// <summary>Time one column covers by default: 24 columns per second.</summary>
    public const float DefaultSecondsPerColumn = 1f / 24f;

    private readonly float[] _peaks;
    private readonly float[] _rmsValues;
    private int _head;
    private int _count;

    /// <param name="columns">How many columns the ring holds (at least 1).</param>
    /// <param name="secondsPerColumn">The time one column covers, in seconds; it only labels the ring, the writer decides when to write (positive, finite).</param>
    public AudioLevelHistory(int columns = DefaultColumns, float secondsPerColumn = DefaultSecondsPerColumn)
    {
        if (columns < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(columns), columns, "A level history needs at least one column.");
        }

        if (!(secondsPerColumn > 0f) || float.IsInfinity(secondsPerColumn))
        {
            throw new ArgumentOutOfRangeException(nameof(secondsPerColumn), secondsPerColumn, "The time of a column must be positive and finite.");
        }

        _peaks = new float[columns];
        _rmsValues = new float[columns];
        SecondsPerColumn = secondsPerColumn;
    }

    /// <summary>How many columns the ring holds.</summary>
    public int Capacity => _peaks.Length;

    /// <summary>The time one column covers, in seconds.</summary>
    public float SecondsPerColumn { get; }

    /// <summary>The time the whole ring covers, in seconds.</summary>
    public float TotalSeconds => _peaks.Length * SecondsPerColumn;

    /// <summary>How many columns were written, up to <see cref="Capacity"/> (the oldest ones are overwritten after that).</summary>
    public int Count => _count;

    /// <summary>The columns this history draws: always <see cref="Capacity"/>, see the remarks of the class.</summary>
    public int ColumnCount => _peaks.Length;

    /// <summary>
    /// Appends a column and drops the oldest one when the ring is full. Both arguments are linear amplitudes (1 is full scale);
    /// zero, negative and NaN amplitudes are silence.
    /// </summary>
    public void Write(float peakAmplitude, float rmsAmplitude)
    {
        _peaks[_head] = ToFraction(peakAmplitude);
        _rmsValues[_head] = ToFraction(rmsAmplitude);

        _head++;
        if (_head == _peaks.Length)
        {
            _head = 0;
        }

        if (_count < _peaks.Length)
        {
            _count++;
        }
    }

    /// <summary>The peak of a column on the meter scale, 0 for the oldest column that is still held, <see cref="Count"/> - 1 for the newest.</summary>
    public float GetPeakFraction(int index) => _peaks[ToSlot(index)];

    /// <summary>The RMS of a column on the meter scale, same order as <see cref="GetPeakFraction"/>.</summary>
    public float GetRmsFraction(int index) => _rmsValues[ToSlot(index)];

    /// <summary>Forgets every column. The ring stays allocated.</summary>
    public void Reset()
    {
        _head = 0;
        _count = 0;
    }

    /// <summary>
    /// A column of the drawing, 0 for the oldest (the left edge). The peak is drawn on both sides of the axis and the RMS inside it.
    /// An index outside the ring, or a column not written yet, is silence: nothing throws while drawing.
    /// </summary>
    public void GetColumn(int index, out float lower, out float upper, out float inner)
    {
        int written = index - (_peaks.Length - _count);

        if (index < 0 || index >= _peaks.Length || written < 0)
        {
            lower = 0f;
            upper = 0f;
            inner = 0f;
            return;
        }

        float peak = _peaks[ToSlot(written)];
        lower = -peak;
        upper = peak;
        inner = _rmsValues[ToSlot(written)];
    }

    private int ToSlot(int index)
    {
        if ((uint)index >= (uint)_count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The index must be in [0, Count).");
        }

        // The newest column is just before the head; the oldest held one is Count columns before it.
        int slot = _head - _count + index;
        return slot < 0 ? slot + _peaks.Length : slot;
    }

    private static float ToFraction(float amplitude)
    {
        return AudioMeterControl.ToFraction(AudioMeterScale.ToDb(amplitude));
    }
}
