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

    public SteeringForceVector LastRawForcePrecise { get; private set; }

    public bool WasEvaluatedLastTick { get; private set; }

    public Vector3 Evaluate(SteeringAgentKinematics kinematics, SteeringAgentComponent agent, float elapsedTime)
    {
        return EvaluateAccurate(kinematics, agent, elapsedTime).ToVector3();
    }

    internal SteeringForceVector EvaluateAccurate(SteeringAgentKinematics kinematics, SteeringAgentComponent agent, float elapsedTime)
    {
        if (!IsEnabled)
        {
            WasEvaluatedLastTick = false;
            LastRawForce = Vector3.Zero;
            LastRawForcePrecise = SteeringForceVector.Zero;
            return SteeringForceVector.Zero;
        }

        WasEvaluatedLastTick = true;
        LastRawForcePrecise = CalculateAccurate(kinematics, agent, elapsedTime);
        LastRawForce = LastRawForcePrecise.ToVector3();
        return LastRawForcePrecise.Multiply(Weight);
    }

    internal void ResetEvaluationState()
    {
        WasEvaluatedLastTick = false;
        LastRawForce = Vector3.Zero;
        LastRawForcePrecise = SteeringForceVector.Zero;
    }

    protected virtual SteeringForceVector CalculateAccurate(SteeringAgentKinematics kinematics, SteeringAgentComponent agent, float elapsedTime)
    {
        Vector3 rawForce = Calculate(kinematics, agent, elapsedTime);
        return new SteeringForceVector(rawForce.X, rawForce.Y, rawForce.Z);
    }

    protected abstract Vector3 Calculate(SteeringAgentKinematics kinematics, SteeringAgentComponent agent, float elapsedTime);

    public abstract SteeringBehaviorRuntime Clone();
}