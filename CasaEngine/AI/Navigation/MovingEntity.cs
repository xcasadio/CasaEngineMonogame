using Microsoft.Xna.Framework;
//using CasaEngine.GameLogic;
//using CasaEngine.PhysicEngine;


namespace CasaEngine.AI.Navigation
{
    [Serializable]
    public abstract class MovingEntity : BaseEntity
    {

        protected internal Vector3 Position;

        protected internal Vector3 Velocity;

        protected internal Vector3 Look;

        protected internal Vector3 Right;

        protected internal Vector3 Up;

        protected internal float Mass;

        protected internal float MaxSpeed;

        protected internal float MaxForce;

        protected internal float MaxTurnRate;

        protected internal object MeshObject;



        public Vector3 Position
        {
            get => position;
            set => position = value;
        }

        public Vector3 Velocity
        {
            get => velocity;
            set => velocity = value;
        }

        public Vector3 Look
        {
            get => look;
            set => look = value;
        }

        public Vector3 Right
        {
            get => right;
            set => right = value;
        }

        public Vector3 Up
        {
            get => up;
            set => up = value;
        }

        public float Mass
        {
            get => mass;
            set => mass = value;
        }

        public float Speed => velocity.Length();

        public float MaxSpeed
        {
            get => maxSpeed;
            set => maxSpeed = value;
        }

        public float MaxForce
        {
            get => maxForce;
            set => maxForce = value;
        }

        public float MaxTurnRate
        {
            get => maxTurnRate;
            set => maxTurnRate = value;
        }

        public object MeshObject
        {
            get => meshObject;
            set => meshObject = value;
        }



        public virtual bool CanMoveBetween(Vector3 start, Vector3 end)
        {
            if (PhysicEngine.Physic == null)
            {
                throw new NullReferenceException("MovingEntity.CanMoveBetween() : PhysicEngine.Physic not defined");
            }
            return !PhysicEngine.Physic.WorldRayCast(ref start, ref end, look);
        }

        protected override void Destroy()
        {
            if (meshObject is BaseEntity)
            {
                throw new NotImplementedException();
                //EntityManager.Instance.RemoveEntity(meshObject as BaseEntity);
            }
        }

    }
}
