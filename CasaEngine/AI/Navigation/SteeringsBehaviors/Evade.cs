using Microsoft.Xna.Framework;




namespace CasaEngine.AI.Navigation.SteeringsBehaviors
{
    public class Evade : SteeringBehavior
    {

        protected MovingEntity pursuer;

        protected Flee flee;



        public Evade(String name, MovingEntity owner, float modifier)
            : base(name, owner, modifier)
        {
            flee = new Flee(name + "Flee", owner, 0);
        }



        public MovingEntity Pursuer
        {
            get => pursuer;
            set => pursuer = value;
        }



        public override Vector3 Calculate()
        {
            Vector3 toPursuer;
            float lookAheadTime;

            //Get the vector to the pursuer and the time it will take to cover it
            toPursuer = ConstraintVector(pursuer.Position) - ConstraintVector(owner.Position);
            lookAheadTime = toPursuer.Length() / (owner.MaxSpeed + pursuer.Speed);

            //Flee from the estimated position of the pursuer
            flee.FleePosition = pursuer.Position + pursuer.Velocity * lookAheadTime;
            return flee.Calculate();
        }

    }
}
