#nullable enable

using System;
using System.Globalization;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Psx;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// Conversions and display constants of the audio meters (plan T6.2, decision P23).
/// </summary>
internal static class AudioMeterScale
{
    /// <summary>
    /// How long a peak stays on the hold marker before it starts to fall, in seconds. A choice of this panel (no standard is
    /// imposed): long enough to read a transient, short enough not to hide the next one.
    /// </summary>
    public const float PeakHoldSeconds = 1.5f;

    /// <summary>
    /// How fast the peak bar, the RMS bar and the hold marker fall once they are above the signal, in dB per second. A choice
    /// of this panel (no standard is imposed). The rise is instantaneous.
    /// </summary>
    public const float FallDbPerSecond = 20f;

    /// <summary>
    /// Level shown for silence, in dBFS: one least significant bit of a 16 bit sample (20 log10(1/32768) is about -90.3 dB).
    /// A level at or below it is displayed as "-inf".
    /// </summary>
    public const float FloorDb = -90f;

    /// <summary>Upper clamp of a measured level, in dBFS, so that a runaway signal never produces a non finite value.</summary>
    public const float CeilingDb = 60f;

    /// <summary>
    /// Converts a linear amplitude (1 is full scale) to dBFS, clamped to [<see cref="FloorDb"/>, <see cref="CeilingDb"/>].
    /// Zero, negative and NaN amplitudes give <see cref="FloorDb"/>.
    /// </summary>
    public static float ToDb(float amplitude)
    {
        if (!(amplitude > 0f))
        {
            return FloorDb;
        }

        return Math.Clamp(20f * MathF.Log10(amplitude), FloorDb, CeilingDb);
    }
}

/// <summary>
/// The levels of one meter (a bus, or the final output) as the panel integrates them: a peak bar and an RMS bar that rise at
/// once and fall at <see cref="AudioMeterScale.FallDbPerSecond"/>, a peak hold marker, the overs of the output, and the
/// texts that show them. Allocates only when its texts are rebuilt.
/// </summary>
internal sealed class AudioMeterTrack
{
    /// <summary>The tenth of a dB shown for a level at the floor.</summary>
    public const int SilentTenths = int.MinValue;

    private const int NeverShown = int.MaxValue;

    private float _holdAgeSeconds;
    private int _shownPeak = NeverShown;
    private int _shownHold = NeverShown;
    private int _shownRms = NeverShown;
    private long _shownOvers = -1;
    private AudioLevelHistory? _history;
    private float _historyPeak;
    private float _historyRms;
    private float _historySeconds;

    public AudioMeterTrack(string name, int depth, int backendIndex, bool isOutput)
    {
        Name = name;
        Depth = depth;
        BackendIndex = backendIndex;
        IsOutput = isOutput;
        Reset();
    }

    public string Name { get; }

    /// <summary>Number of ancestors of the bus, for the indentation.</summary>
    public int Depth { get; }

    /// <summary>Index to read this bus at in the levels the backend publishes, or -1 (the output, or a bus the backend could not hold).</summary>
    public int BackendIndex { get; }

    /// <summary>True for the final output (after the Master limiter and the hard clip); only it reports overs.</summary>
    public bool IsOutput { get; }

    /// <summary>False for a bus beyond the backend capacity: it is mixed into Master but has no level of its own.</summary>
    public bool HasLevel => IsOutput || BackendIndex >= 0;

    /// <summary>Peak with fall, in dBFS.</summary>
    public float PeakDb { get; private set; }

    /// <summary>RMS with fall, in dBFS.</summary>
    public float RmsDb { get; private set; }

    /// <summary>Peak hold, in dBFS.</summary>
    public float HoldDb { get; private set; }

    /// <summary>Samples beyond full scale before the limiter since the last reset (the output only).</summary>
    public long OversTotal { get; private set; }

    /// <summary>Seconds since the last over.</summary>
    public float SecondsSinceOver { get; private set; }

    /// <summary>True for <see cref="AudioMeterScale.PeakHoldSeconds"/> after an over.</summary>
    public bool IsOverLit => OversTotal > 0 && SecondsSinceOver < AudioMeterScale.PeakHoldSeconds;

    public string PeakText { get; private set; } = string.Empty;

    public string HoldText { get; private set; } = string.Empty;

    public string RmsText { get; private set; } = string.Empty;

    public string OversText { get; private set; } = string.Empty;

    /// <summary>
    /// The levels of the last seconds, one column per <see cref="AudioLevelHistory.SecondsPerColumn"/>, or null (the default): a track
    /// only keeps a history after <see cref="EnableHistory"/>, so the meters of the "Audio" panel are the same as before.
    /// </summary>
    public AudioLevelHistory? History => _history;

    /// <summary>
    /// Starts keeping a history of this track (plan T10.8, decision P55), empty, replacing the previous one if any. The ring is
    /// allocated here; <see cref="Integrate"/> then fills it without allocating.
    /// </summary>
    /// <param name="columns">How many columns the ring holds.</param>
    /// <param name="secondsPerColumn">The time one column covers, in seconds.</param>
    public void EnableHistory(int columns, float secondsPerColumn)
    {
        _history = new AudioLevelHistory(columns, secondsPerColumn);
        ResetHistoryAccumulation();
    }

    /// <summary>
    /// Integrates what was published since the last call: <paramref name="level"/> aggregates every block not yet seen
    /// (the maximum peak and the combined RMS), <paramref name="elapsedSeconds"/> is the wall time since the last call.
    /// A silent level (no new block) only lets the bars and the marker fall.
    /// </summary>
    public void Integrate(in AudioLevel level, float elapsedSeconds)
    {
        var elapsed = elapsedSeconds > 0f ? elapsedSeconds : 0f;
        var fall = AudioMeterScale.FallDbPerSecond * elapsed;
        var peakDb = AudioMeterScale.ToDb(level.Peak);
        var rmsDb = AudioMeterScale.ToDb(level.Rms);

        PeakDb = MathF.Max(peakDb, PeakDb - fall);
        RmsDb = MathF.Max(rmsDb, RmsDb - fall);

        if (peakDb >= HoldDb)
        {
            HoldDb = peakDb;
            _holdAgeSeconds = 0f;
        }
        else
        {
            _holdAgeSeconds += elapsed;

            if (_holdAgeSeconds > AudioMeterScale.PeakHoldSeconds)
            {
                // The marker falls like the bars, and never below the live peak bar.
                HoldDb = MathF.Max(PeakDb, HoldDb - fall);
            }
        }

        if (level.Overs > 0)
        {
            OversTotal += level.Overs;
            SecondsSinceOver = 0f;
        }
        else
        {
            SecondsSinceOver += elapsed;
        }

        if (_history != null)
        {
            WriteHistory(_history, level.Peak, level.Rms, elapsed);
        }
    }

    /// <summary>
    /// Rebuilds the texts whose displayed value (a tenth of a dB, or the overs) changed. Returns false, without allocating,
    /// when nothing displayed changed.
    /// </summary>
    public bool RefreshText()
    {
        if (!HasLevel)
        {
            if (_shownPeak != NeverShown)
            {
                return false;
            }

            PeakText = HoldText = RmsText = "n/a";
            _shownPeak = _shownHold = _shownRms = SilentTenths;
            return true;
        }

        var peak = ToTenths(PeakDb);
        var hold = ToTenths(HoldDb);
        var rms = ToTenths(RmsDb);
        var overs = IsOutput ? OversTotal : 0L;

        if (peak == _shownPeak && hold == _shownHold && rms == _shownRms && overs == _shownOvers)
        {
            return false;
        }

        if (peak != _shownPeak)
        {
            PeakText = FormatTenths(peak);
            _shownPeak = peak;
        }

        if (hold != _shownHold)
        {
            HoldText = FormatTenths(hold);
            _shownHold = hold;
        }

        if (rms != _shownRms)
        {
            RmsText = FormatTenths(rms);
            _shownRms = rms;
        }

        if (overs != _shownOvers)
        {
            OversText = IsOutput ? overs.ToString(CultureInfo.InvariantCulture) : string.Empty;
            _shownOvers = overs;
        }

        return true;
    }

    /// <summary>Back to silence: bars, marker, overs. The texts are rebuilt by the next <see cref="RefreshText"/>.</summary>
    public void Reset()
    {
        PeakDb = RmsDb = HoldDb = AudioMeterScale.FloorDb;
        _holdAgeSeconds = 0f;
        OversTotal = 0;
        SecondsSinceOver = float.MaxValue;
        _history?.Reset();
        ResetHistoryAccumulation();
    }

    /// <summary>
    /// Keeps the largest peak and the largest RMS seen since the last column, and writes one column for every
    /// <see cref="AudioLevelHistory.SecondsPerColumn"/> that elapsed, so that a one block peak between two frames is never lost.
    /// A step longer than a column (a hitch of the editor) writes several: the first one holds the peak and the RMS, the others
    /// are silent (what this step measured is one level, whose position in the step is unknown), and the time left over carries to the next step.
    /// </summary>
    private void WriteHistory(AudioLevelHistory history, float peak, float rms, float elapsed)
    {
        if (peak > _historyPeak)
        {
            _historyPeak = peak;
        }

        if (rms > _historyRms)
        {
            _historyRms = rms;
        }

        // A step longer than the whole ring is not worth more than the ring: it also keeps the division below finite.
        _historySeconds += MathF.Min(elapsed, history.TotalSeconds);

        var secondsPerColumn = history.SecondsPerColumn;

        if (_historySeconds < secondsPerColumn)
        {
            return;
        }

        var columns = Math.Min((int)(_historySeconds / secondsPerColumn), history.Capacity);
        history.Write(_historyPeak, _historyRms);

        for (var i = 1; i < columns; i++)
        {
            history.Write(0f, 0f);
        }

        _historyPeak = 0f;
        _historyRms = 0f;
        _historySeconds = MathF.Max(0f, _historySeconds - (columns * secondsPerColumn));
    }

    private void ResetHistoryAccumulation()
    {
        _historyPeak = 0f;
        _historyRms = 0f;
        _historySeconds = 0f;
    }

    /// <summary>The level as the integer number of tenths of a dB that is displayed; <see cref="SilentTenths"/> at the floor.</summary>
    internal static int ToTenths(float db)
    {
        return db <= AudioMeterScale.FloorDb ? SilentTenths : (int)MathF.Round(db * 10f);
    }

    internal static string FormatTenths(int tenths)
    {
        return tenths == SilentTenths
            ? "-inf"
            : (tenths / 10f).ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// What the "Audio" panel shows and how it keeps it up to date, without any UI (plan T6.2, decision P23): the statistics of
/// the editor's single <see cref="AudioService"/> and one <see cref="AudioMeterTrack"/> per bus of its mixer plus one for the
/// final output.
/// </summary>
/// <remarks>
/// Two distinct rates. <see cref="ReadMeters"/> runs on every call of <see cref="Update"/> (the editor calls it on every
/// frame while the panel is docked, so well within the 80 ms the backend keeps its blocks for), reads through one
/// <see cref="AudioMeterCursor"/> owned by this object, so every block published since the previous call is integrated, and
/// allocates nothing. The texts are rebuilt by <see cref="Update"/> at most once per <see cref="TextIntervalSeconds"/> (four
/// times per second), and only when a displayed value changed.
/// </remarks>
internal sealed class AudioProfilerModel
{
    /// <summary>Shortest time between two rebuilds of the texts: four per second.</summary>
    public const float TextIntervalSeconds = 0.25f;

    /// <summary>Number of statistics lines; a line can be empty (a statistic the backend does not have).</summary>
    public const int StatLineCount = 6;

    private readonly Func<AudioService?> _serviceProvider;
    private readonly string[] _statLines = new string[StatLineCount];

    private AudioService? _service;
    private bool _isMeteringAvailable;
    private AudioMeterTrack[] _buses = Array.Empty<AudioMeterTrack>();
    private AudioMeterTrack? _output;
    private AudioLevel[] _levels = Array.Empty<AudioLevel>();
    private AudioMeterCursor _cursor;
    private long _missedBlocks;
    private float _secondsSinceText = TextIntervalSeconds;
    private Stats _shownStats;
    private bool _areStatsShown;

    /// <param name="serviceProvider">Gives the audio service to observe, or null while there is none. Called once per update.</param>
    public AudioProfilerModel(Func<AudioService?> serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        for (var i = 0; i < _statLines.Length; i++)
        {
            _statLines[i] = string.Empty;
        }
    }

    /// <summary>True when the observed backend measures levels (<see cref="IAudioMeteringBackend"/>).</summary>
    public bool IsMeteringAvailable => _isMeteringAvailable;

    /// <summary>One meter per bus of the mixer, in mixer order (Master first). Empty without metering.</summary>
    public AudioMeterTrack[] Buses => _buses;

    /// <summary>The meter of the final output, or null without metering.</summary>
    public AudioMeterTrack? Output => _output;

    public string[] StatLines => _statLines;

    /// <summary>Incremented when the set of meters changes (another service, or a bus was added to the mixer).</summary>
    public int StructureVersion { get; private set; }

    /// <summary>How many times the texts were rebuilt since creation (also the version of the texts).</summary>
    public int TextRebuildCount { get; private set; }

    /// <summary>Blocks the backend published that this reader skipped because it waited too long, since the last reset.</summary>
    public long MissedBlockCount => _missedBlocks;

    /// <summary>
    /// Reads the meters, then rebuilds the texts when <see cref="TextIntervalSeconds"/> elapsed since the previous rebuild
    /// and a displayed value changed. Returns true when the texts were rebuilt.
    /// </summary>
    public bool Update(float elapsedSeconds)
    {
        ReadMeters(elapsedSeconds);

        _secondsSinceText += elapsedSeconds > 0f ? elapsedSeconds : 0f;

        if (_secondsSinceText < TextIntervalSeconds)
        {
            return false;
        }

        if (!RefreshText())
        {
            return false;
        }

        _secondsSinceText = 0f;
        return true;
    }

    /// <summary>
    /// Integrates every block published since the previous call into the meters. Allocation free when the set of buses did
    /// not change. <paramref name="elapsedSeconds"/> is the wall time since the previous call, it drives the fall.
    /// </summary>
    public void ReadMeters(float elapsedSeconds)
    {
        var service = _serviceProvider();

        if (!ReferenceEquals(service, _service) || (service != null && service.Mixer.Buses.Count != _buses.Length && _isMeteringAvailable))
        {
            RebuildStructure(service);
        }

        if (_service == null || !_isMeteringAvailable || _output == null)
        {
            return;
        }

        if (!_service.TryReadLevels(ref _cursor, _levels, out var output, out var read))
        {
            return;
        }

        _missedBlocks += read.MissedBlockCount;

        var buses = _buses;

        for (var i = 0; i < buses.Length; i++)
        {
            var track = buses[i];

            if (track.BackendIndex >= 0 && track.BackendIndex < _levels.Length)
            {
                track.Integrate(in _levels[track.BackendIndex], elapsedSeconds);
            }
            else
            {
                track.Integrate(default(AudioLevel), elapsedSeconds);
            }
        }

        _output.Integrate(in output, elapsedSeconds);
    }

    /// <summary>
    /// Forgets what was integrated (the panel was docked again): the next read starts from the history the backend still
    /// holds, the bars, the markers, the overs and the missed blocks go back to zero, and the texts are rebuilt at the next update.
    /// </summary>
    public void Reset()
    {
        _cursor = default;
        _missedBlocks = 0;
        _secondsSinceText = TextIntervalSeconds;
        _areStatsShown = false;

        for (var i = 0; i < _buses.Length; i++)
        {
            _buses[i].Reset();
        }

        _output?.Reset();
    }

    /// <summary>
    /// Rebuilds the statistics lines and the meter texts that changed, whatever the time since the previous rebuild. Returns
    /// false, without allocating, when no displayed value changed.
    /// </summary>
    public bool RefreshText()
    {
        var changed = false;
        var stats = ReadStats();

        if (!_areStatsShown || !stats.Equals(_shownStats))
        {
            BuildStatLines(in stats);
            _shownStats = stats;
            _areStatsShown = true;
            changed = true;
        }

        for (var i = 0; i < _buses.Length; i++)
        {
            changed |= _buses[i].RefreshText();
        }

        if (_output != null)
        {
            changed |= _output.RefreshText();
        }

        if (changed)
        {
            TextRebuildCount++;
        }

        return changed;
    }

    private void RebuildStructure(AudioService? service)
    {
        var serviceChanged = !ReferenceEquals(service, _service);

        if (serviceChanged)
        {
            _cursor = default;
            _missedBlocks = 0;
        }

        _service = service;
        _isMeteringAvailable = service != null && service.IsMeteringAvailable;
        StructureVersion++;

        if (!_isMeteringAvailable)
        {
            _buses = Array.Empty<AudioMeterTrack>();
            _output = null;
            _levels = Array.Empty<AudioLevel>();
            return;
        }

        var mixerBuses = service!.Mixer.Buses;
        var old = serviceChanged ? Array.Empty<AudioMeterTrack>() : _buses;
        var tracks = new AudioMeterTrack[mixerBuses.Count];
        var maxIndex = -1;

        for (var i = 0; i < tracks.Length; i++)
        {
            if (i < old.Length)
            {
                // Buses are only ever added, in a stable order: the meter keeps its state.
                tracks[i] = old[i];
            }
            else
            {
                var bus = mixerBuses[i];
                var index = service.TryGetMeterBusIndex(bus.Name, out var found) ? found : -1;
                tracks[i] = new AudioMeterTrack(bus.Name, DepthOf(bus), index, isOutput: false);
            }

            maxIndex = Math.Max(maxIndex, tracks[i].BackendIndex);
        }

        _buses = tracks;
        _output = serviceChanged || _output == null ? new AudioMeterTrack("Output", 0, -1, isOutput: true) : _output;

        if (_levels.Length < maxIndex + 1)
        {
            _levels = new AudioLevel[maxIndex + 1];
        }
    }

    private static int DepthOf(AudioBus bus)
    {
        var depth = 0;

        for (var parent = bus.Parent; parent != null; parent = parent.Parent)
        {
            depth++;
        }

        return depth;
    }

    private Stats ReadStats()
    {
        var service = _service;

        if (service == null)
        {
            return default;
        }

        var backend = service.Backend;
        var software = backend as SoftwareAudioBackend;

        return new Stats(
            HasService: true,
            BackendType: backend.GetType(),
            IsAvailable: service.IsAudioAvailable,
            IsMeteringAvailable: _isMeteringAvailable,
            ActiveVoices: service.ActiveVoiceCount,
            RefusedVoices: service.RefusedVoiceCount,
            IsSoftware: software != null,
            VoiceCapacity: software?.VoiceCapacity ?? 0,
            OutputSampleRate: software?.OutputSampleRate ?? 0,
            LeadMilliseconds: software?.LeadMilliseconds ?? 0,
            Underruns: software?.UnderrunCount ?? 0,
            DroppedChunks: software?.DroppedChunkCount ?? 0,
            DroppedEvents: software?.DroppedEventCount ?? 0,
            HostsSpu: backend is IPsxSpuHost,
            MissedBlocks: _missedBlocks,
            OutputOvers: _output?.OversTotal ?? 0);
    }

    private void BuildStatLines(in Stats stats)
    {
        if (!stats.HasService)
        {
            _statLines[0] = "Audio service: not available";

            for (var i = 1; i < _statLines.Length; i++)
            {
                _statLines[i] = string.Empty;
            }

            return;
        }

        var inv = CultureInfo.InvariantCulture;

        _statLines[0] = string.Create(inv, $"Backend: {stats.BackendType!.Name} ({(stats.IsAvailable ? "output running" : "no output device")})");
        _statLines[1] = stats.IsSoftware
            ? string.Create(inv, $"Voices: {stats.ActiveVoices} active of {stats.VoiceCapacity}, {stats.RefusedVoices} refused")
            : string.Create(inv, $"Voices: {stats.ActiveVoices} active, {stats.RefusedVoices} refused");
        _statLines[2] = stats.IsSoftware
            ? string.Create(inv, $"Output: {stats.OutputSampleRate} Hz, lead {stats.LeadMilliseconds} ms, {stats.Underruns} underruns")
            : string.Empty;
        _statLines[3] = stats.IsSoftware
            ? string.Create(inv, $"Dropped: {stats.DroppedChunks} stream chunks, {stats.DroppedEvents} events")
            : string.Empty;
        _statLines[4] = stats.HostsSpu ? "SPU: hosted by the backend" : "SPU: not hosted by this backend";
        _statLines[5] = stats.IsMeteringAvailable
            ? string.Create(inv, $"Meters: output overs {stats.OutputOvers}, missed blocks {stats.MissedBlocks}")
            : "Metering unavailable: this backend does not measure levels";
    }

    /// <summary>What the statistics lines show; compared as a whole to know whether they must be rebuilt.</summary>
    private readonly record struct Stats(
        bool HasService,
        Type? BackendType,
        bool IsAvailable,
        bool IsMeteringAvailable,
        int ActiveVoices,
        int RefusedVoices,
        bool IsSoftware,
        int VoiceCapacity,
        int OutputSampleRate,
        int LeadMilliseconds,
        int Underruns,
        int DroppedChunks,
        int DroppedEvents,
        bool HostsSpu,
        long MissedBlocks,
        long OutputOvers);
}
