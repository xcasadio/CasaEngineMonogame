using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Game.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation
{
    [Serializable]
    public abstract class MovingObject : Entity
    {
        protected internal Vector3 position;
        protected internal Vector3 velocity;
        protected internal Vector3 look;
        protected internal Vector3 right;
        protected internal Vector3 up;
        protected internal float mass;
        protected internal float maxSpeed;
        protected internal float maxForce;
        protected internal float maxTurnRate;
        protected internal object meshObject;

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
            var physicsEngineComponent = EngineComponents.Game.GetGameComponent<PhysicsEngineComponent>();
            if (physicsEngineComponent.PhysicsEngine == null)
            {
                throw new NullReferenceException("MovingObject.CanMoveBetween() : PhysicEngine.Physic not defined");
            }
            return !physicsEngineComponent.PhysicsEngine.WorldRayCast(ref start, ref end, look);
        }
    }
}
