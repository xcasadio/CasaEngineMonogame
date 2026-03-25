namespace CasaEngine.Framework.AI.Navigation;

public sealed class SteeringAgentSettings
{
    public float Mass { get; set; } = 1.0f;

    public float MaxSpeed { get; set; } = 1.0f;

    public float MaxForce { get; set; } = 1.0f;

    public float MaxTurnRate { get; set; } = 1.0f;

    public SteeringOutputMode OutputMode { get; set; } = SteeringOutputMode.Force;

    public SteeringAgentSettings Clone()
    {
        return new SteeringAgentSettings
        {
            Mass = Mass,
            MaxSpeed = MaxSpeed,
            MaxForce = MaxForce,
            MaxTurnRate = MaxTurnRate,
            OutputMode = OutputMode,
        };
    }
}