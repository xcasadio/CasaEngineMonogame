using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Particles.Authoring;

/// <summary>
/// Serializable authoring definition for a particle emitter inside an effect.
/// </summary>
public sealed class ParticleEmitterDefinition
{
    public string Name { get; set; } = "Emitter";

    public bool Enabled { get; set; } = true;

    public float Duration { get; set; } = 5.0f;

    public bool Looping { get; set; } = true;

    public float StartDelay { get; set; }

    public int MaxParticles { get; set; } = 1000;

    public ParticleEmissionModule Emission { get; set; } = new();

    public ParticleShapeModule Shape { get; set; } = new();

    public ParticleInitialModule Initial { get; set; } = new();

    public ParticleSimulationModule Simulation { get; set; } = new();

    public ParticleRendererModule Renderer { get; set; } = new();

    internal void Validate(List<string> errors, int emitterIndex)
    {
        string label = string.IsNullOrWhiteSpace(Name)
            ? $"Emitter[{emitterIndex}]"
            : $"Emitter '{Name}'";

        if (!IsFinite(Duration) || Duration <= 0.0f)
        {
            errors.Add($"{label} duration must be greater than zero.");
        }

        if (!IsFinite(StartDelay) || StartDelay < 0.0f)
        {
            errors.Add($"{label} start delay must be finite and non-negative.");
        }

        if (MaxParticles <= 0)
        {
            errors.Add($"{label} max particles must be greater than zero.");
        }

        if (Emission == null)
        {
            errors.Add($"{label} emission module is missing.");
        }
        else
        {
            Emission.Validate(errors, label);
        }

        if (Shape == null)
        {
            errors.Add($"{label} shape module is missing.");
        }
        else
        {
            Shape.Validate(errors, label);
        }

        if (Initial == null)
        {
            errors.Add($"{label} initial module is missing.");
        }

        if (Simulation == null)
        {
            errors.Add($"{label} simulation module is missing.");
        }
        else
        {
            Simulation.Validate(errors, label);
        }

        if (Renderer == null)
        {
            errors.Add($"{label} renderer module is missing.");
        }
        else
        {
            Renderer.Validate(errors, label);
        }
    }

    private static bool IsFinite(float value)
        => !float.IsNaN(value) && !float.IsInfinity(value);
}

/// <summary>
/// Authoring data for continuous and burst emission.
/// </summary>
public sealed class ParticleEmissionModule
{
    public float RateOverTime { get; set; } = 10.0f;

    public List<ParticleBurst> Bursts { get; } = new();

    internal void Validate(List<string> errors, string label)
    {
        if (!IsFinite(RateOverTime) || RateOverTime < 0.0f)
        {
            errors.Add($"{label} rate over time must be finite and non-negative.");
        }

        for (int burstIndex = 0; burstIndex < Bursts.Count; burstIndex++)
        {
            ParticleBurst? burst = Bursts[burstIndex];
            if (burst == null)
            {
                errors.Add($"{label} burst {burstIndex} is null.");
                continue;
            }

            if (!IsFinite(burst.Time) || burst.Time < 0.0f)
            {
                errors.Add($"{label} burst {burstIndex} time must be finite and non-negative.");
            }

            if (burst.CountMin < 0 || burst.CountMax < 0)
            {
                errors.Add($"{label} burst {burstIndex} counts must be non-negative.");
            }
        }
    }

    private static bool IsFinite(float value)
        => !float.IsNaN(value) && !float.IsInfinity(value);
}

/// <summary>
/// Authoring data for emitter shape sampling.
/// </summary>
public sealed class ParticleShapeModule
{
    public ParticleShapeType ShapeType { get; set; } = ParticleShapeType.Point;

    public Vector3 Size { get; set; } = Vector3.One;

    public float Radius { get; set; } = 1.0f;

    public float AngleDegrees { get; set; } = 25.0f;

    public bool EmitFromShell { get; set; }

    internal void Validate(List<string> errors, string label)
    {
        if (!IsFinite(Size.X) || !IsFinite(Size.Y) || !IsFinite(Size.Z)
            || Size.X < 0.0f || Size.Y < 0.0f || Size.Z < 0.0f)
        {
            errors.Add($"{label} shape size must be finite and non-negative.");
        }

        if (!IsFinite(Radius) || Radius < 0.0f)
        {
            errors.Add($"{label} shape radius must be finite and non-negative.");
        }

        if (!IsFinite(AngleDegrees) || AngleDegrees < 0.0f || AngleDegrees > 180.0f)
        {
            errors.Add($"{label} shape angle must be between 0 and 180 degrees.");
        }
    }

    private static bool IsFinite(float value)
        => !float.IsNaN(value) && !float.IsInfinity(value);
}

/// <summary>
/// Authoring data for initial particle attributes.
/// </summary>
public sealed class ParticleInitialModule
{
    public FloatRange Lifetime { get; set; } = new(1.0f, 2.0f);

    public FloatRange Speed { get; set; } = new(1.0f, 3.0f);

    public FloatRange Rotation { get; set; } = new(0.0f, 360.0f);

    public FloatRange AngularVelocity { get; set; } = new(-90.0f, 90.0f);

    public Vector2Range Size { get; set; } = Vector2Range.Constant(Vector2.One);

    public ColorGradient StartColor { get; set; } = ColorGradient.White;
}

/// <summary>
/// Authoring data for particle integration over lifetime.
/// </summary>
public sealed class ParticleSimulationModule
{
    public ParticleSimulationSpace SimulationSpace { get; set; } = ParticleSimulationSpace.Local;

    public Vector3 Gravity { get; set; } = new(0.0f, -9.81f, 0.0f);

    public float GravityScale { get; set; }

    public float Drag { get; set; }

    public FloatCurve SizeOverLifetime { get; set; } = FloatCurve.Constant(1.0f);

    public FloatCurve AlphaOverLifetime { get; set; } = FloatCurve.Constant(1.0f);

    public FloatCurve VelocityOverLifetime { get; set; } = FloatCurve.Constant(1.0f);

    public ColorGradient ColorOverLifetime { get; set; } = ColorGradient.White;

    internal void Validate(List<string> errors, string label)
    {
        if (!IsFinite(Gravity.X) || !IsFinite(Gravity.Y) || !IsFinite(Gravity.Z))
        {
            errors.Add($"{label} gravity must be finite.");
        }

        if (!IsFinite(GravityScale))
        {
            errors.Add($"{label} gravity scale must be finite.");
        }

        if (!IsFinite(Drag) || Drag < 0.0f)
        {
            errors.Add($"{label} drag must be finite and non-negative.");
        }

        if (SizeOverLifetime == null)
        {
            errors.Add($"{label} size over lifetime curve is missing.");
        }

        if (AlphaOverLifetime == null)
        {
            errors.Add($"{label} alpha over lifetime curve is missing.");
        }

        if (VelocityOverLifetime == null)
        {
            errors.Add($"{label} velocity over lifetime curve is missing.");
        }

        if (ColorOverLifetime == null)
        {
            errors.Add($"{label} color over lifetime gradient is missing.");
        }
    }

    private static bool IsFinite(float value)
        => !float.IsNaN(value) && !float.IsInfinity(value);
}

/// <summary>
/// Authoring data for particle rendering.
/// </summary>
public sealed class ParticleRendererModule
{
    public ParticleRenderMode RenderMode { get; set; } = ParticleRenderMode.Billboard;

    public Guid TextureAssetId { get; set; } = Guid.Empty;

    public ParticleFlipbookModule Flipbook { get; set; } = new();

    public ParticleBlendMode BlendMode { get; set; } = ParticleBlendMode.Alpha;

    public ParticleSortMode SortMode { get; set; } = ParticleSortMode.None;

    public bool DepthTest { get; set; } = true;

    public bool DepthWrite { get; set; }

    public int RenderQueue { get; set; } = 3000;

    public int Layer { get; set; }

    public bool AlwaysVisible { get; set; }

    internal void Validate(List<string> errors, string label)
    {
        if (RenderQueue < 0)
        {
            errors.Add($"{label} render queue must be non-negative.");
        }

        if (Flipbook == null)
        {
            errors.Add($"{label} flipbook module is missing.");
        }
        else
        {
            Flipbook.Validate(errors, label);
        }
    }
}

/// <summary>
/// Authoring data for texture-sheet particle animation.
/// </summary>
public sealed class ParticleFlipbookModule
{
    public int Columns { get; set; } = 1;

    public int Rows { get; set; } = 1;

    public int FrameCount { get; set; } = 1;

    public bool RandomStartFrame { get; set; }

    public float FramesPerSecond { get; set; }

    public FloatCurve FrameOverLifetime { get; set; } = FloatCurve.Constant(0.0f);

    public int EffectiveFrameCount
    {
        get
        {
            int atlasCapacity = GetAtlasCapacity();
            return Math.Clamp(FrameCount, 1, atlasCapacity);
        }
    }

    internal void Validate(List<string> errors, string label)
    {
        if (Columns <= 0)
        {
            errors.Add($"{label} flipbook columns must be greater than zero.");
        }

        if (Rows <= 0)
        {
            errors.Add($"{label} flipbook rows must be greater than zero.");
        }

        int atlasCapacity = GetAtlasCapacity();
        if (FrameCount <= 0 || FrameCount > atlasCapacity)
        {
            errors.Add($"{label} flipbook frame count must be between 1 and atlas capacity.");
        }

        if (float.IsNaN(FramesPerSecond) || float.IsInfinity(FramesPerSecond) || FramesPerSecond < 0.0f)
        {
            errors.Add($"{label} flipbook FPS must be finite and non-negative.");
        }

        if (FrameOverLifetime == null)
        {
            errors.Add($"{label} flipbook frame-over-lifetime curve is missing.");
        }
    }

    private int GetAtlasCapacity()
    {
        long columns = Math.Max(1, Columns);
        long rows = Math.Max(1, Rows);
        long capacity = columns * rows;
        return capacity > int.MaxValue ? int.MaxValue : (int)capacity;
    }
}