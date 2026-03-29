using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public abstract class SteeringBehaviorRuntime
{
    protected SteeringBehaviorRuntime(string name, float weight = 1.0f)
    {
        Name = name;
        Weight = weight;
        IsEnabled = true;
    }

    public string Name { get; }

    public bool IsEnabled { get; set; }

    public float Weight { get; set; }

    public Vector3 LastRawForce { get; private set; }

    public Vector3 Evaluate(SteeringAgentKinematics kinematics, SteeringAgentComponent agent, float elapsedTime)
    {
        if (!IsEnabled)
        {
            LastRawForce = Vector3.Zero;
            return Vector3.Zero;
        }

        LastRawForce = Calculate(kinematics, agent, elapsedTime);
        return LastRawForce * Weight;
    }

    protected abstract Vector3 Calculate(SteeringAgentKinematics kinematics, SteeringAgentComponent agent, float elapsedTime);

    public abstract SteeringBehaviorRuntime Clone();
}