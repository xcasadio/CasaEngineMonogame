using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CasaEngine.Framework.AI.Navigation;

public sealed record SteeringBehaviorPerformanceSnapshot(
    string Name,
    double AverageMilliseconds,
    double AverageEvaluationsPerFrame,
    double AverageCandidateScansPerFrame,
    double AverageAcceptedNeighborsPerFrame);

public sealed record SteeringPerformanceSnapshot(
    int CompletedFrames,
    double AgentUpdateMilliseconds,
    double AgentUpdateCount,
    double BehaviorEvaluationMilliseconds,
    double BehaviorEvaluationCount,
    double NeighborQueryMilliseconds,
    double NeighborQueryCount,
    double NeighborCandidateCount,
    double NeighborHitCount,
    double NeighborQueryWindowCellCount,
    double NeighborQueryNonEmptyCellCount,
    double NeighborGridActiveCellCount,
    double NeighborGridAverageOccupancy,
    double NeighborGridMaxOccupancy,
    double BridgeUpdateMilliseconds,
    double BridgeUpdateCount,
    double VehicleScriptUpdateMilliseconds,
    double VehicleScriptUpdateCount,
    double VehicleScriptWrapCount,
    double VehicleScriptDrawMilliseconds,
    double VehicleScriptDrawCount,
    double VehicleScriptSubmittedLineCount,
    IReadOnlyList<SteeringBehaviorPerformanceSnapshot> TopBehaviors)
{
    public static readonly SteeringPerformanceSnapshot Empty = new(
        0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        Array.Empty<SteeringBehaviorPerformanceSnapshot>());

    public bool HasData => CompletedFrames > 0;
}

public static class SteeringPerformanceDiagnostics
{
    private const double SmoothingFactor = 0.18;

    private sealed class FrameBehaviorMetrics
    {
        public double Milliseconds;
        public int Evaluations;
        public int CandidateScans;
        public int AcceptedNeighbors;
    }

    private sealed class FrameMetrics
    {
        public double AgentUpdateMilliseconds;
        public int AgentUpdateCount;
        public double BehaviorEvaluationMilliseconds;
        public int BehaviorEvaluationCount;
        public double NeighborQueryMilliseconds;
        public int NeighborQueryCount;
        public int NeighborCandidateCount;
        public int NeighborHitCount;
        public int NeighborQueryWindowCellCount;
        public int NeighborQueryNonEmptyCellCount;
        public int NeighborGridActiveCellCount;
        public double NeighborGridAverageOccupancy;
        public int NeighborGridMaxOccupancy;
        public double BridgeUpdateMilliseconds;
        public int BridgeUpdateCount;
        public double VehicleScriptUpdateMilliseconds;
        public int VehicleScriptUpdateCount;
        public int VehicleScriptWrapCount;
        public double VehicleScriptDrawMilliseconds;
        public int VehicleScriptDrawCount;
        public int VehicleScriptSubmittedLineCount;
        public Dictionary<string, FrameBehaviorMetrics> BehaviorMetrics { get; } = [];
        public Dictionary<string, double> BehaviorMillisecondsAdjustments { get; } = [];

        public void Reset()
        {
            AgentUpdateMilliseconds = 0.0;
            AgentUpdateCount = 0;
            BehaviorEvaluationMilliseconds = 0.0;
            BehaviorEvaluationCount = 0;
            NeighborQueryMilliseconds = 0.0;
            NeighborQueryCount = 0;
            NeighborCandidateCount = 0;
            NeighborHitCount = 0;
            NeighborQueryWindowCellCount = 0;
            NeighborQueryNonEmptyCellCount = 0;
            NeighborGridActiveCellCount = 0;
            NeighborGridAverageOccupancy = 0.0;
            NeighborGridMaxOccupancy = 0;
            BridgeUpdateMilliseconds = 0.0;
            BridgeUpdateCount = 0;
            VehicleScriptUpdateMilliseconds = 0.0;
            VehicleScriptUpdateCount = 0;
            VehicleScriptWrapCount = 0;
            VehicleScriptDrawMilliseconds = 0.0;
            VehicleScriptDrawCount = 0;
            VehicleScriptSubmittedLineCount = 0;
            BehaviorMetrics.Clear();
            BehaviorMillisecondsAdjustments.Clear();
        }

        public FrameBehaviorMetrics GetBehaviorMetrics(string behaviorName)
        {
            if (!BehaviorMetrics.TryGetValue(behaviorName, out FrameBehaviorMetrics? behaviorMetrics))
            {
                behaviorMetrics = new FrameBehaviorMetrics();
                BehaviorMetrics.Add(behaviorName, behaviorMetrics);
            }

            return behaviorMetrics;
        }

        public void AdjustBehaviorMilliseconds(string behaviorName, double milliseconds)
        {
            if (Math.Abs(milliseconds) <= double.Epsilon)
            {
                return;
            }

            if (BehaviorMillisecondsAdjustments.TryGetValue(behaviorName, out double existingAdjustment))
            {
                BehaviorMillisecondsAdjustments[behaviorName] = existingAdjustment + milliseconds;
            }
            else
            {
                BehaviorMillisecondsAdjustments.Add(behaviorName, milliseconds);
            }
        }

        public double ConsumeBehaviorMillisecondsAdjustment(string behaviorName)
        {
            if (!BehaviorMillisecondsAdjustments.Remove(behaviorName, out double adjustment))
            {
                return 0.0;
            }

            return adjustment;
        }
    }

    private sealed class AveragedMetric
    {
        public double Value { get; private set; }

        public void Update(double sample)
        {
            Value = Value <= double.Epsilon
                ? sample
                : Value + ((sample - Value) * SmoothingFactor);
        }

        public void Reset()
        {
            Value = 0.0;
        }
    }

    private sealed class AveragedBehaviorMetric
    {
        public AveragedBehaviorMetric(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public double AverageMilliseconds { get; private set; }

        public double AverageEvaluationsPerFrame { get; private set; }

        public double AverageCandidateScansPerFrame { get; private set; }

        public double AverageAcceptedNeighborsPerFrame { get; private set; }

        public void Update(double milliseconds, double evaluations, double candidateScans, double acceptedNeighbors)
        {
            AverageMilliseconds = AverageMilliseconds <= double.Epsilon
                ? milliseconds
                : AverageMilliseconds + ((milliseconds - AverageMilliseconds) * SmoothingFactor);
            AverageEvaluationsPerFrame = AverageEvaluationsPerFrame <= double.Epsilon
                ? evaluations
                : AverageEvaluationsPerFrame + ((evaluations - AverageEvaluationsPerFrame) * SmoothingFactor);
            AverageCandidateScansPerFrame = AverageCandidateScansPerFrame <= double.Epsilon
                ? candidateScans
                : AverageCandidateScansPerFrame + ((candidateScans - AverageCandidateScansPerFrame) * SmoothingFactor);
            AverageAcceptedNeighborsPerFrame = AverageAcceptedNeighborsPerFrame <= double.Epsilon
                ? acceptedNeighbors
                : AverageAcceptedNeighborsPerFrame + ((acceptedNeighbors - AverageAcceptedNeighborsPerFrame) * SmoothingFactor);
        }

        public SteeringBehaviorPerformanceSnapshot ToSnapshot()
        {
            return new SteeringBehaviorPerformanceSnapshot(
                Name,
                AverageMilliseconds,
                AverageEvaluationsPerFrame,
                AverageCandidateScansPerFrame,
                AverageAcceptedNeighborsPerFrame);
        }
    }

    private static readonly FrameMetrics Current = new();
    private static readonly AveragedMetric AgentUpdateMilliseconds = new();
    private static readonly AveragedMetric AgentUpdateCount = new();
    private static readonly AveragedMetric BehaviorEvaluationMilliseconds = new();
    private static readonly AveragedMetric BehaviorEvaluationCount = new();
    private static readonly AveragedMetric NeighborQueryMilliseconds = new();
    private static readonly AveragedMetric NeighborQueryCount = new();
    private static readonly AveragedMetric NeighborCandidateCount = new();
    private static readonly AveragedMetric NeighborHitCount = new();
    private static readonly AveragedMetric NeighborQueryWindowCellCount = new();
    private static readonly AveragedMetric NeighborQueryNonEmptyCellCount = new();
    private static readonly AveragedMetric NeighborGridActiveCellCount = new();
    private static readonly AveragedMetric NeighborGridAverageOccupancy = new();
    private static readonly AveragedMetric NeighborGridMaxOccupancy = new();
    private static readonly AveragedMetric BridgeUpdateMilliseconds = new();
    private static readonly AveragedMetric BridgeUpdateCount = new();
    private static readonly AveragedMetric VehicleScriptUpdateMilliseconds = new();
    private static readonly AveragedMetric VehicleScriptUpdateCount = new();
    private static readonly AveragedMetric VehicleScriptWrapCount = new();
    private static readonly AveragedMetric VehicleScriptDrawMilliseconds = new();
    private static readonly AveragedMetric VehicleScriptDrawCount = new();
    private static readonly AveragedMetric VehicleScriptSubmittedLineCount = new();
    private static readonly Dictionary<string, AveragedBehaviorMetric> AveragedBehaviorMetrics = [];

    private static SteeringPerformanceSnapshot _latestSnapshot = SteeringPerformanceSnapshot.Empty;
    private static int _completedFrames;

    public static bool Enabled { get; private set; }

    public static SteeringPerformanceSnapshot LatestSnapshot => _latestSnapshot;

    public static double GetElapsedMilliseconds(long startTimestamp)
    {
        return (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
    }

    public static void SetEnabled(bool enabled)
    {
        if (Enabled == enabled)
        {
            return;
        }

        Enabled = enabled;
        if (!enabled)
        {
            Reset();
        }
    }

    public static void BeginFrame()
    {
        if (!Enabled)
        {
            return;
        }

        Current.Reset();
    }

    public static void CompleteFrame()
    {
        if (!Enabled)
        {
            return;
        }

        _completedFrames++;

        AgentUpdateMilliseconds.Update(Current.AgentUpdateMilliseconds);
        AgentUpdateCount.Update(Current.AgentUpdateCount);
        BehaviorEvaluationMilliseconds.Update(Current.BehaviorEvaluationMilliseconds);
        BehaviorEvaluationCount.Update(Current.BehaviorEvaluationCount);
        NeighborQueryMilliseconds.Update(Current.NeighborQueryMilliseconds);
        NeighborQueryCount.Update(Current.NeighborQueryCount);
        NeighborCandidateCount.Update(Current.NeighborCandidateCount);
        NeighborHitCount.Update(Current.NeighborHitCount);
        NeighborQueryWindowCellCount.Update(Current.NeighborQueryWindowCellCount);
        NeighborQueryNonEmptyCellCount.Update(Current.NeighborQueryNonEmptyCellCount);
        NeighborGridActiveCellCount.Update(Current.NeighborGridActiveCellCount);
        NeighborGridAverageOccupancy.Update(Current.NeighborGridAverageOccupancy);
        NeighborGridMaxOccupancy.Update(Current.NeighborGridMaxOccupancy);
        BridgeUpdateMilliseconds.Update(Current.BridgeUpdateMilliseconds);
        BridgeUpdateCount.Update(Current.BridgeUpdateCount);
        VehicleScriptUpdateMilliseconds.Update(Current.VehicleScriptUpdateMilliseconds);
        VehicleScriptUpdateCount.Update(Current.VehicleScriptUpdateCount);
        VehicleScriptWrapCount.Update(Current.VehicleScriptWrapCount);
        VehicleScriptDrawMilliseconds.Update(Current.VehicleScriptDrawMilliseconds);
        VehicleScriptDrawCount.Update(Current.VehicleScriptDrawCount);
        VehicleScriptSubmittedLineCount.Update(Current.VehicleScriptSubmittedLineCount);

        string[] behaviorNames = AveragedBehaviorMetrics.Keys
            .Concat(Current.BehaviorMetrics.Keys)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (string behaviorName in behaviorNames)
        {
            if (!AveragedBehaviorMetrics.TryGetValue(behaviorName, out AveragedBehaviorMetric? averagedBehaviorMetric))
            {
                averagedBehaviorMetric = new AveragedBehaviorMetric(behaviorName);
                AveragedBehaviorMetrics.Add(behaviorName, averagedBehaviorMetric);
            }

            if (!Current.BehaviorMetrics.TryGetValue(behaviorName, out FrameBehaviorMetrics? frameBehaviorMetrics))
            {
                averagedBehaviorMetric.Update(0.0, 0.0, 0.0, 0.0);
                continue;
            }

            averagedBehaviorMetric.Update(
                frameBehaviorMetrics.Milliseconds,
                frameBehaviorMetrics.Evaluations,
                frameBehaviorMetrics.CandidateScans,
                frameBehaviorMetrics.AcceptedNeighbors);
        }

        _latestSnapshot = new SteeringPerformanceSnapshot(
            _completedFrames,
            AgentUpdateMilliseconds.Value,
            AgentUpdateCount.Value,
            BehaviorEvaluationMilliseconds.Value,
            BehaviorEvaluationCount.Value,
            NeighborQueryMilliseconds.Value,
            NeighborQueryCount.Value,
            NeighborCandidateCount.Value,
            NeighborHitCount.Value,
            NeighborQueryWindowCellCount.Value,
            NeighborQueryNonEmptyCellCount.Value,
            NeighborGridActiveCellCount.Value,
            NeighborGridAverageOccupancy.Value,
            NeighborGridMaxOccupancy.Value,
            BridgeUpdateMilliseconds.Value,
            BridgeUpdateCount.Value,
            VehicleScriptUpdateMilliseconds.Value,
            VehicleScriptUpdateCount.Value,
            VehicleScriptWrapCount.Value,
            VehicleScriptDrawMilliseconds.Value,
            VehicleScriptDrawCount.Value,
            VehicleScriptSubmittedLineCount.Value,
            AveragedBehaviorMetrics.Values
                .Where(metric => metric.AverageMilliseconds > 0.01 || metric.AverageEvaluationsPerFrame > 0.05)
                .OrderByDescending(metric => metric.AverageMilliseconds)
                .Take(6)
                .Select(metric => metric.ToSnapshot())
                .ToArray());
    }

    public static void RecordAgentUpdate(double elapsedMilliseconds)
    {
        if (!Enabled)
        {
            return;
        }

        Current.AgentUpdateMilliseconds += elapsedMilliseconds;
        Current.AgentUpdateCount++;
    }

    public static void RecordBehaviorEvaluation(string behaviorName, double elapsedMilliseconds)
    {
        if (!Enabled)
        {
            return;
        }

        string effectiveBehaviorName = string.IsNullOrWhiteSpace(behaviorName)
            ? "(unnamed)"
            : behaviorName;

        double adjustedElapsedMilliseconds = Math.Max(0.0, elapsedMilliseconds + Current.ConsumeBehaviorMillisecondsAdjustment(effectiveBehaviorName));

        Current.BehaviorEvaluationMilliseconds += adjustedElapsedMilliseconds;
        Current.BehaviorEvaluationCount++;

        FrameBehaviorMetrics behaviorMetrics = Current.GetBehaviorMetrics(effectiveBehaviorName);
        behaviorMetrics.Milliseconds += adjustedElapsedMilliseconds;
        behaviorMetrics.Evaluations++;
    }

    public static void RecordSharedBehaviorPhase(string sourceBehaviorName, string sharedPhaseName, double elapsedMilliseconds, int candidateScans, int acceptedNeighbors)
    {
        if (!Enabled)
        {
            return;
        }

        string effectiveSharedPhaseName = string.IsNullOrWhiteSpace(sharedPhaseName)
            ? "(shared-phase)"
            : sharedPhaseName;

        FrameBehaviorMetrics sharedPhaseMetrics = Current.GetBehaviorMetrics(effectiveSharedPhaseName);
        sharedPhaseMetrics.Milliseconds += Math.Max(0.0, elapsedMilliseconds);
        sharedPhaseMetrics.Evaluations++;
        sharedPhaseMetrics.CandidateScans += Math.Max(0, candidateScans);
        sharedPhaseMetrics.AcceptedNeighbors += Math.Max(0, acceptedNeighbors);

        string effectiveSourceBehaviorName = string.IsNullOrWhiteSpace(sourceBehaviorName)
            ? "(unnamed)"
            : sourceBehaviorName;
        Current.AdjustBehaviorMilliseconds(effectiveSourceBehaviorName, -Math.Max(0.0, elapsedMilliseconds));
    }

    public static void RecordBehaviorNeighborhoodScan(string behaviorName, int candidateScans, int acceptedNeighbors)
    {
        if (!Enabled)
        {
            return;
        }

        string effectiveBehaviorName = string.IsNullOrWhiteSpace(behaviorName)
            ? "(unnamed)"
            : behaviorName;

        FrameBehaviorMetrics behaviorMetrics = Current.GetBehaviorMetrics(effectiveBehaviorName);
        behaviorMetrics.CandidateScans += Math.Max(0, candidateScans);
        behaviorMetrics.AcceptedNeighbors += Math.Max(0, acceptedNeighbors);
    }

    public static void RecordNeighborQuery(double elapsedMilliseconds, int candidateCount, int hitCount, int windowCellCount, int nonEmptyCellCount)
    {
        if (!Enabled)
        {
            return;
        }

        Current.NeighborQueryMilliseconds += elapsedMilliseconds;
        Current.NeighborQueryCount++;
        Current.NeighborCandidateCount += Math.Max(0, candidateCount);
        Current.NeighborHitCount += Math.Max(0, hitCount);
        Current.NeighborQueryWindowCellCount += Math.Max(0, windowCellCount);
        Current.NeighborQueryNonEmptyCellCount += Math.Max(0, nonEmptyCellCount);
    }

    public static void RecordNeighborGridBuild(int activeCellCount, double averageOccupancy, int maxOccupancy)
    {
        if (!Enabled)
        {
            return;
        }

        Current.NeighborGridActiveCellCount = Math.Max(0, activeCellCount);
        Current.NeighborGridAverageOccupancy = Math.Max(0.0, averageOccupancy);
        Current.NeighborGridMaxOccupancy = Math.Max(0, maxOccupancy);
    }

    public static void RecordBridgeUpdate(double elapsedMilliseconds)
    {
        if (!Enabled)
        {
            return;
        }

        Current.BridgeUpdateMilliseconds += elapsedMilliseconds;
        Current.BridgeUpdateCount++;
    }

    public static void RecordVehicleScriptUpdate(double elapsedMilliseconds, bool wrappedLastUpdate)
    {
        if (!Enabled)
        {
            return;
        }

        Current.VehicleScriptUpdateMilliseconds += elapsedMilliseconds;
        Current.VehicleScriptUpdateCount++;
        if (wrappedLastUpdate)
        {
            Current.VehicleScriptWrapCount++;
        }
    }

    public static void RecordVehicleScriptDraw(double elapsedMilliseconds, int submittedLineCount)
    {
        if (!Enabled)
        {
            return;
        }

        Current.VehicleScriptDrawMilliseconds += elapsedMilliseconds;
        Current.VehicleScriptDrawCount++;
        Current.VehicleScriptSubmittedLineCount += Math.Max(0, submittedLineCount);
    }

    public static void Reset()
    {
        Current.Reset();
        AgentUpdateMilliseconds.Reset();
        AgentUpdateCount.Reset();
        BehaviorEvaluationMilliseconds.Reset();
        BehaviorEvaluationCount.Reset();
        NeighborQueryMilliseconds.Reset();
        NeighborQueryCount.Reset();
        NeighborCandidateCount.Reset();
        NeighborHitCount.Reset();
        NeighborQueryWindowCellCount.Reset();
        NeighborQueryNonEmptyCellCount.Reset();
        NeighborGridActiveCellCount.Reset();
        NeighborGridAverageOccupancy.Reset();
        NeighborGridMaxOccupancy.Reset();
        BridgeUpdateMilliseconds.Reset();
        BridgeUpdateCount.Reset();
        VehicleScriptUpdateMilliseconds.Reset();
        VehicleScriptUpdateCount.Reset();
        VehicleScriptWrapCount.Reset();
        VehicleScriptDrawMilliseconds.Reset();
        VehicleScriptDrawCount.Reset();
        VehicleScriptSubmittedLineCount.Reset();
        AveragedBehaviorMetrics.Clear();
        _latestSnapshot = SteeringPerformanceSnapshot.Empty;
        _completedFrames = 0;
    }
}