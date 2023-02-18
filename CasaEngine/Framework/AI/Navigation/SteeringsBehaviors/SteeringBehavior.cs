using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation.SteeringsBehaviors
{
    public abstract class SteeringBehavior
    {
        protected internal string name;
        protected internal MovingObject owner;
        protected internal float modifier;
        protected internal bool active;
        protected internal bool ignoreX;
        protected internal bool ignoreY;
        protected internal bool ignoreZ;

        public SteeringBehavior(string name, MovingObject owner, float modifier)
        {
            this.name = name;
            this.owner = owner;
            this.modifier = modifier;

            active = false;

            ignoreX = false;
            ignoreY = false;
            ignoreZ = false;
        }

        public MovingObject Owner => owner;

        public string Name
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
            if (ignoreX)
            {
                vector.X = 0;
            }

            if (ignoreY)
            {
                vector.Y = 0;
            }

            if (ignoreZ)
            {
                vector.Y = 0;
            }

            return vector;
        }

        protected void ConstraintVector(ref Vector3 vector)
        {
            if (ignoreX)
            {
                vector.X = 0;
            }

            if (ignoreY)
            {
                vector.Y = 0;
            }

            if (ignoreZ)
            {
                vector.Y = 0;
            }
        }

    }
}
