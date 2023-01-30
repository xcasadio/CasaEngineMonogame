using Microsoft.Xna.Framework;




namespace CasaEngine.AI.Navigation.SteeringsBehaviors
{
    public class Interpose : SteeringBehavior
    {

        protected MovingEntity agentA;

        protected MovingEntity agentB;

        protected Arrive arrive;



        public Interpose(String name, MovingEntity owner, float modifier)
            : base(name, owner, modifier)
        {
            arrive = new Arrive(name + "Arrive", owner, 0, 0.1f);
        }



        public MovingEntity AgentA
        {
            get => agentA;
            set => agentA = value;
        }

        public MovingEntity AgentB
        {
            get => agentB;
            set => agentB = value;
        }



        public override Vector3 Calculate()
        {
            Vector3 midPoint, posA, posB;
            float timeToReachMidPoint;

            //Calculate the time to reach the midpoint between the two agents
            midPoint = (ConstraintVector(agentA.Position) + ConstraintVector(agentB.Position)) * 0.5f;
            timeToReachMidPoint = (ConstraintVector(owner.Position) - midPoint).Length() / owner.MaxSpeed;

            //Calculate the estimated agent positions at that time
            posA = agentA.Position + agentA.Velocity * timeToReachMidPoint;
            posB = agentB.Position + agentB.Velocity * timeToReachMidPoint;

            //Calculate the mid point of the estimated positions and asap to it
            midPoint = (posA + posB) * 0.5f;
            arrive.TargetPosition = midPoint;

            return arrive.Calculate();
        }

    }
}
