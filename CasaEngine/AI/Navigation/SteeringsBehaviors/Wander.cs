using Microsoft.Xna.Framework;




namespace CasaEngine.AI.Navigation.SteeringsBehaviors
{
    public class Wander : SteeringBehavior
    {

        protected internal float Distance;

        protected internal float Radius;

        protected internal float Jitter;

        protected internal Vector3 WanderTarget;

        protected internal Random Generator;

        protected internal Vector3 Force;

        protected internal Vector3 TargetPosition;



        public Wander(String name, MovingEntity owner, float modifier)
            : base(name, owner, modifier)
        {
            Generator = new Random();
        }

        public Wander(String name, MovingEntity owner, float modifier, float radius, float distance, float jitter)
            : base(name, owner, modifier)
        {
            double alfa, beta, theta;

            Generator = new Random();

            this.radius = radius;
            this.distance = distance;
            this.jitter = jitter;

            //Create a vector to a target position on the wander circle						
            alfa = Generator.NextDouble() * System.Math.PI * 2;
            beta = Generator.NextDouble() * System.Math.PI * 2;
            theta = Generator.NextDouble() * System.Math.PI * 2;

            wanderTarget = new Vector3(radius * (float)System.Math.Cos(alfa), radius * (float)System.Math.Cos(beta), radius * (float)System.Math.Cos(theta));
        }



        public float Distance
        {
            get => distance;
            set => distance = value;
        }

        public float Radius
        {
            get => radius;
            set
            {
                double alfa, beta, theta;

                radius = value;

                //Create a vector to a target position on the wander circle						
                alfa = Generator.NextDouble() * System.Math.PI * 2;
                beta = Generator.NextDouble() * System.Math.PI * 2;
                theta = Generator.NextDouble() * System.Math.PI * 2;

                wanderTarget = new Vector3(radius * (float)System.Math.Cos(alfa), radius * (float)System.Math.Cos(beta), radius * (float)System.Math.Cos(theta));
            }
        }

        public float Jitter
        {
            get => jitter;
            set => jitter = value;
        }

        public Vector3 Force => force;

        public Vector3 WanderTarget => wanderTarget;


        public Vector3 TargetPosition => targetPosition;


        public override Vector3 Calculate()
        {
            Vector3 displacement;

            //Randomize a little the wander target
            wanderTarget += new Vector3(jitter * (float)(Generator.NextDouble() * 2 - 1), jitter * (float)(Generator.NextDouble() * 2 - 1), jitter * (float)(Generator.NextDouble() * 2 - 1));
            ConstraintVector(ref wanderTarget);

            //Normalize it and reproject it again
            wanderTarget.Normalize();
            wanderTarget *= radius;

            //Project it in front of the entity and transform to world coordinates
            displacement = Vector3.Normalize(owner.Look) * distance;
            targetPosition = owner.Position + wanderTarget + displacement;

            force = ConstraintVector(targetPosition - owner.Position);
            return force;
        }

    }
}
