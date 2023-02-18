using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation.SteeringsBehaviors
{
    public class Pursuit : SteeringBehavior
    {
        protected internal MovingObject evader;
        protected internal Seek Seek;
        protected internal Vector3 force;
        protected internal Vector3 targetPosition;

        public Pursuit(string name, MovingObject owner, float modifier)
            : base(name, owner, modifier)
        {
            Seek = new Seek(name + "Seek", owner, 0);
        }

        public override bool IgnoreX
        {
            get => IgnoreX;
            set
            {
                IgnoreX = value;
                IgnoreX = value;
            }
        }

        public override bool IgnoreY
        {
            get => IgnoreY;
            set
            {
                IgnoreY = value;
                IgnoreY = value;
            }
        }

        public override bool IgnoreZ
        {
            get => IgnoreZ;
            set
            {
                IgnoreZ = value;
                IgnoreZ = value;
            }
        }

        public MovingObject Evader
        {
            get => evader;
            set => evader = value;
        }

        public Vector3 Force => force;

        public Vector3 TargetPosition => targetPosition;

        public override Vector3 Calculate()
        {
            Vector3 toEvader;
            float relativeHeading, lookAheadTime;

            //Get the distance and heading of the evader
            toEvader = ConstraintVector(evader.Position) - ConstraintVector(owner.Position);
            relativeHeading = Vector3.Dot(ConstraintVector(owner.Look), ConstraintVector(evader.Look));

            //If the evader is in front of the pursuer, seek to it
            if (relativeHeading < -0.95f && Vector3.Dot(toEvader, ConstraintVector(owner.Look)) > 0.0f)
            {
                targetPosition = evader.Position;
                Seek.TargetPosition = targetPosition;

                force = Seek.Calculate();
                return force;
            }

            //If not, try to estimate where it´s going to be in a future time
            lookAheadTime = toEvader.Length() / (owner.MaxSpeed + evader.Speed);

            //If the pursuer needs to turn
            if (owner.MaxTurnRate != 0.0f)
            {
                lookAheadTime += TurnTime(owner, evader.Position);
            }

            //Seek to the estimated future position
            targetPosition = evader.Position + evader.Velocity * lookAheadTime;
            Seek.TargetPosition = targetPosition;

            force = Seek.Calculate();
            return force;
        }

        private float TurnTime(MovingObject agent, Vector3 position)
        {
            Vector3 toTarget;
            float dot;

            //Calculate the vector to the target and then the dot product between that vector and the entity look
            toTarget = Vector3.Normalize(ConstraintVector(position) - ConstraintVector(agent.Position));
            dot = Vector3.Dot(ConstraintVector(agent.Look), toTarget);

            return (dot - 1.0f) * -agent.MaxTurnRate;
        }

    }
}
