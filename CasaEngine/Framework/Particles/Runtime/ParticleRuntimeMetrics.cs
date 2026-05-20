namespace CasaEngine.Framework.Particles.Runtime;

public readonly record struct ParticleRuntimeMetrics(
    int Capacity,
    int AliveCount,
    int DeadCount,
    int LastEmittedCount,
    int LastKilledCount,
    int MaxAliveCountReached,
    bool MaxReached,
    double SimulationCpuMilliseconds)
{
    public static ParticleRuntimeMetrics Empty => default;
}