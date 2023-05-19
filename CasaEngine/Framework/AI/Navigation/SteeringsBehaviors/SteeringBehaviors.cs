using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation.SteeringsBehaviors;

public class Steeringbehaviors
{
    protected internal MovingObject owner;
    protected internal List<SteeringBehavior> behaviors;
    protected internal SumMethod sumAlgorithm;

    public Steeringbehaviors(MovingObject owner, SumMethod sumAlgorithm)
    {
        owner = owner;
        this.sumAlgorithm = sumAlgorithm;

        behaviors = new List<SteeringBehavior>();
    }

    public SumMethod SumAlgorithm
    {
        get => sumAlgorithm;
        set => sumAlgorithm = value;
    }

    public void RegisterBehavior<T>(string name, float modifier)
    {
        object newBehavior;
        object[] parametters;

        foreach (var behavior in behaviors)
            if (behavior is T && behavior.Name == name)
            {
                return;
            }

        parametters = new object[3];
        parametters[0] = name;
        parametters[1] = owner;
        parametters[2] = modifier;

        newBehavior = Activator.CreateInstance(typeof(T), parametters);

        behaviors.Add((SteeringBehavior)newBehavior);
    }

    public void RegisterBehavior(SteeringBehavior behavior)
    {
        for (var i = 0; i < behaviors.Count; i++)
        {
            if (behaviors[i].GetType() == behavior.GetType() && behaviors[i].Name == behavior.Name)
            {
                behaviors[i] = behavior;
                return;
            }
        }

        behaviors.Add(behavior);
    }

    public void UnregisterBehavior<T>(string name)
    {
        for (var i = 0; i < behaviors.Count; i++)
        {
            if (behaviors[i] is T && behaviors[i].Name == name)
            {
                behaviors.RemoveAt(i);
            }
        }
    }

    public void ActivateBehavior<T>(string name)
    {
        foreach (var behavior in behaviors)
            if (behavior is T && behavior.Name == name)
            {
                behavior.Active = true;
            }
    }

    public void DeactivateBehavior<T>(string name)
    {
        foreach (var behavior in behaviors)
            if (behavior is T && behavior.Name == name)
            {
                behavior.Active = false;
            }
    }

    public T GetBehavior<T>(string name) where T : SteeringBehavior
    {
        foreach (var behavior in behaviors)
            if (behavior is T && behavior.Name == name)
            {
                return (T)behavior;
            }

        return null;
    }

    public Vector3 Calculate()
    {
        return sumAlgorithm(behaviors).Truncate(owner.MaxForce);
    }

}