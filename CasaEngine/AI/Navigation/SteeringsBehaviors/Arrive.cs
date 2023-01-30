using Microsoft.Xna.Framework;




namespace CasaEngine.AI.Navigation.SteeringsBehaviors
{
    public class Arrive : SteeringBehavior
    {

        protected internal float slowingDistance;

        protected internal Vector3 targetPosition;

        protected internal Vector3 force;

        protected internal float maximumSpeed;



        public Arrive(String name, MovingEntity owner, float modifier)
            : this(name, owner, modifier, 1.0f) { }

        public Arrive(String name, MovingEntity owner, float modifier, float slowingDistance)
            : base(name, owner, modifier)
        {
            this.slowingDistance = slowingDistance;
        }



        public float SlowingDistance
        {
            get => slowingDistance;
            set => slowingDistance = value;
        }

        public Vector3 TargetPosition
        {
            get => targetPosition;
            set => targetPosition = ConstraintVector(value);
        }

        public Vector3 Force => force;

        public float MaximumSpeed => maximumSpeed;


        public override Vector3 Calculate()
        {
            Vector3 toTarget, desiredVelocity;
            float distance, rampedSpeed;

            toTarget = targetPosition - ConstraintVector(owner.Position);
            distance = toTarget.Length();

            rampedSpeed = owner.MaxSpeed * (distance / slowingDistance);
            maximumSpeed = System.Math.Min(rampedSpeed, owner.MaxSpeed);

            desiredVelocity = (maximumSpeed / distance) * toTarget;
            force = desiredVelocity - owner.velocity;

            return force;
        }

    }
}
