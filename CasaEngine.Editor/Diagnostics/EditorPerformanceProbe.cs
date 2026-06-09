using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace CasaEngine.Editor.Diagnostics;

internal static class EditorPerformanceProbe
{
    private const int DefaultSampleInterval = 30;
    private const double DefaultSlowFrameThresholdMs = 12.0;
    private const int MaxPhases = 64;

    private static readonly string OutputPath;
    private static readonly int SampleInterval;
    private static readonly double SlowFrameThresholdMs;
    private static readonly PhaseMetric[] Phases = new PhaseMetric[MaxPhases];

    private static int _frameIndex;
    private static int _phaseCount;
    private static long _frameStartTimestamp;
    private static string _frameName = string.Empty;
    private static string _context = string.Empty;

    static EditorPerformanceProbe()
    {
        string configuredPath = Environment.GetEnvironmentVariable("CASA_EDITOR_PERF_PROBE");
        if (string.IsNullOrWhiteSpace(configuredPath) || string.Equals(configuredPath, "0", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        OutputPath = string.Equals(configuredPath, "1", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFullPath("editor-performance-probe.txt")
            : Path.GetFullPath(configuredPath);

        SampleInterval = TryReadInt("CASA_EDITOR_PERF_SAMPLE_INTERVAL", DefaultSampleInterval, 1);
        SlowFrameThresholdMs = TryReadDouble("CASA_EDITOR_PERF_THRESHOLD_MS", DefaultSlowFrameThresholdMs, 0.0);

        string directory = Path.GetDirectoryName(OutputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(OutputPath,
            "CasaEngine Editor performance probe" + Environment.NewLine +
            $"sampleInterval={SampleInterval} slowFrameThresholdMs={SlowFrameThresholdMs.ToString("F2", CultureInfo.InvariantCulture)}" + Environment.NewLine + Environment.NewLine);
    }

    public static bool IsEnabled => OutputPath != null;

    public static FrameScope BeginFrame(string frameName)
    {
        if (!IsEnabled)
        {
            return default;
        }

        _frameIndex++;
        _phaseCount = 0;
        _frameName = frameName;
        _context = string.Empty;
        _frameStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        return new FrameScope(true);
    }

    public static void SetContext(string context)
    {
        if (!IsEnabled)
        {
            return;
        }

        _context = context;
    }

    public static PhaseScope BeginPhase(string phaseName)
    {
        if (!IsEnabled)
        {
            return default;
        }

        return new PhaseScope(phaseName, System.Diagnostics.Stopwatch.GetTimestamp());
    }

    private static void EndFrame()
    {
        long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - _frameStartTimestamp;
        double elapsedMs = ToMilliseconds(elapsedTicks);
        if (elapsedMs < SlowFrameThresholdMs && _frameIndex % SampleInterval != 0)
        {
            return;
        }

        var builder = new StringBuilder(512);
        builder.Append("[EditorPerf] frame=");
        builder.Append(_frameIndex.ToString(CultureInfo.InvariantCulture));
        builder.Append(" name=");
        builder.Append(_frameName);
        builder.Append(" totalMs=");
        builder.Append(elapsedMs.ToString("F2", CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(_context))
        {
            builder.Append(" context=");
            builder.Append(_context);
        }

        builder.AppendLine();

        for (int index = 0; index < _phaseCount; index++)
        {
            builder.Append("  - ");
            builder.Append(Phases[index].Name);
            builder.Append(" ms=");
            builder.Append(ToMilliseconds(Phases[index].ElapsedTicks).ToString("F2", CultureInfo.InvariantCulture));
            builder.AppendLine();
        }

        builder.AppendLine();
        File.AppendAllText(OutputPath!, builder.ToString());
    }

    private static void RecordPhase(string phaseName, long elapsedTicks)
    {
        if (_phaseCount >= Phases.Length)
        {
            return;
        }

        Phases[_phaseCount] = new PhaseMetric(phaseName, elapsedTicks);
        _phaseCount++;
    }

    private static int TryReadInt(string name, int fallback, int minimum)
    {
        string value = Environment.GetEnvironmentVariable(name);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? Math.Max(minimum, parsed)
            : fallback;
    }

    private static double TryReadDouble(string name, double fallback, double minimum)
    {
        string value = Environment.GetEnvironmentVariable(name);
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? Math.Max(minimum, parsed)
            : fallback;
    }

    private static double ToMilliseconds(long ticks)
        => ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

    private readonly record struct PhaseMetric(string Name, long ElapsedTicks);

    public readonly struct FrameScope : IDisposable
    {
        private readonly bool _enabled;

        public FrameScope(bool enabled)
        {
            _enabled = enabled;
        }

        public void Dispose()
        {
            if (_enabled)
            {
                EndFrame();
            }
        }
    }

    public readonly struct PhaseScope : IDisposable
    {
        private readonly string _name;
        private readonly long _startTimestamp;

        public PhaseScope(string name, long startTimestamp)
        {
            _name = name;
            _startTimestamp = startTimestamp;
        }

        public void Dispose()
        {
            if (_name == null)
            {
                return;
            }

            RecordPhase(_name, System.Diagnostics.Stopwatch.GetTimestamp() - _startTimestamp);
        }
    }
}