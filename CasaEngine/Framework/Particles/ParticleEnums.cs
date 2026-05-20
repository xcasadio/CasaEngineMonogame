namespace CasaEngine.Framework.Particles;

/// <summary>
/// Blend mode used by a particle emitter renderer.
/// </summary>
public enum ParticleBlendMode
{
    Alpha,
    Additive,
    Multiply,
}

/// <summary>
/// Sorting strategy used before drawing particle quads.
/// </summary>
public enum ParticleSortMode
{
    None,
    Distance,
    Layer,
}

/// <summary>
/// Shape used to sample initial particle positions and directions.
/// </summary>
public enum ParticleShapeType
{
    Point,
    Circle,
    Box,
    Sphere,
    Cone,
}

/// <summary>
/// Space in which particle simulation is evaluated.
/// </summary>
public enum ParticleSimulationSpace
{
    Local,
    World,
}

/// <summary>
/// Runtime render mode for particle geometry.
/// </summary>
public enum ParticleRenderMode
{
    Billboard,
    Sprite,
}

/// <summary>
/// Playback state of a particle runtime instance.
/// </summary>
public enum ParticlePlaybackState
{
    Stopped,
    Playing,
    Paused,
}