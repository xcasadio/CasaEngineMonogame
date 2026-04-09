namespace CasaEngine.Framework.AI.Navigation;

public interface ISteeringNeighborhoodService2D
{
    void PrepareForWorldUpdate();

    SteeringNeighborhoodAggregateContext GetNeighborhoodAggregate(SteeringAgentComponent agent, in SteeringNeighborhoodAggregateQuery query);
}