using Microsoft.Xna.Framework;




namespace CasaEngine.AI.Navigation.SteeringsBehaviors
{
    public abstract class SteeringBehavior
    {

        protected internal String Name;

        protected internal MovingEntity Owner;

        protected internal float Modifier;

        protected internal bool Active;

        protected internal bool IgnoreX;

        protected internal bool IgnoreY;

        protected internal bool IgnoreZ;



        public SteeringBehavior(String name, MovingEntity owner, float modifier)
        {
            this.name = name;
            this.owner = owner;
            this.modifier = modifier;

            this.active = false;

            ignoreX = false;
            ignoreY = false;
            ignoreZ = false;
        }



        public MovingEntity Owner => owner;

        public String Name
        {
            get => name;
            set => name = value;
        }

        public bool Active
        {
            get => active;
            set => active = value;
        }

        public float Modifier
        {
            get => modifier;
            set => modifier = value;
        }

        public virtual bool IgnoreX
        {
            get => ignoreX;
            set => ignoreX = value;
        }

        public virtual bool IgnoreY
        {
            get => ignoreY;
            set => ignoreY = value;
        }

        public virtual bool IgnoreZ
        {
            get => ignoreZ;
            set => ignoreZ = value;
        }



        public abstract Vector3 Calculate();

        protected Vector3 ConstraintVector(Vector3 vector)
        {
            if (ignoreX == true)
                vector.X = 0;

            if (ignoreY == true)
                vector.Y = 0;

            if (ignoreZ == true)
                vector.Y = 0;

            return vector;
        }

        protected void ConstraintVector(ref Vector3 vector)
        {
            if (ignoreX == true)
                vector.X = 0;

            if (ignoreY == true)
                vector.Y = 0;

            if (ignoreZ == true)
                vector.Y = 0;
        }

    }
}
