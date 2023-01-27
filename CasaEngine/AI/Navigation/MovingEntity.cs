#region Using Directives

using System;

using Microsoft.Xna.Framework;
//using CasaEngine.GameLogic;
//using CasaEngine.PhysicEngine;

#endregion

namespace CasaEngine.AI.Navigation
{
	/// <summary>
	/// This class represents an entity that is able to move in a 3D environment. This
	/// entity has several physical characteristics associated to itself to allow it to move
	/// through the world
	/// </summary>
	[Serializable]
	public abstract class MovingEntity : BaseEntity
	{
		#region Fields

		/// <summary>
		/// The position of the entity
		/// </summary>
		protected internal Vector3 position;

		/// <summary>
		/// The velocity of the entity
		/// </summary>
		protected internal Vector3 velocity;

		/// <summary>
		/// The look vector of the transformation matrix of the entity
		/// </summary>
		protected internal Vector3 look;

		/// <summary>
		/// The right vector of the transformation matrix of the entity
		/// </summary>
		protected internal Vector3 right;

		/// <summary>
		/// The up vector of the transformation matrix of the entity
		/// </summary>
		protected internal Vector3 up;

		/// <summary>
		/// The mass of the entity
		/// </summary>
		protected internal float mass;

		/// <summary>
		/// The maximum speed of the entity
		/// </summary>
		protected internal float maxSpeed;

		/// <summary>
		/// The maximum force of the entity
		/// </summary>
		protected internal float maxForce;

		/// <summary>
		/// The maximum turn rate of the entity
		/// </summary>
		protected internal float maxTurnRate;

		/// <summary>
		/// The mesh object associated with the entity
		/// </summary>
		protected internal object meshObject;

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the position of the entity
		/// </summary>
		public Vector3 Position
		{
			get { return position; }
			set { position = value; }
		}

		/// <summary>
		/// Gets or sets the velocity of the entity
		/// </summary>
		public Vector3 Velocity
		{
			get { return velocity; }
			set { velocity = value; }
		}

		/// <summary>
		/// Gets or sets the look vector of the transformation matrix of the entity
		/// </summary>
		public Vector3 Look
		{
			get { return look; }
			set { look = value; }
		}

		/// <summary>
		/// Gets or sets the right vector of the transformation matrix of the entity
		/// </summary>
		public Vector3 Right
		{
			get { return right; }
			set { right = value; }
		}

		/// <summary>
		/// Gets or sets the up vector of the transformation matrix of the entity
		/// </summary>
		public Vector3 Up
		{
			get { return up; }
			set { up = value; }
		}

		/// <summary>
		/// Gets or sets the mass of the entity
		/// </summary>
		public float Mass
		{
			get { return mass; }
			set { mass = value; }
		}

		/// <summary>
		/// Gets the speed of the entity
		/// </summary>
		public float Speed
		{
			get { return velocity.Length(); }
		}

		/// <summary>
		/// Gets or sets the maximum speed of the entity
		/// </summary>
		public float MaxSpeed
		{
			get { return maxSpeed; }
			set { maxSpeed = value; }
		}

		/// <summary>
		/// Gets or sets the maximum force of the entity
		/// </summary>
		public float MaxForce
		{
			get { return maxForce; }
			set { maxForce = value; }
		}

		/// <summary>
		/// Gets or sets the maximum turn rate of the entity
		/// </summary>
		public float MaxTurnRate
		{
			get { return maxTurnRate; }
			set { maxTurnRate = value; }
		}

		/// <summary>
		/// Gets or sets the mesh object associated with the entity
		/// </summary>
		public object MeshObject
		{
			get { return meshObject; }
			set { meshObject = value; }
		}

		#endregion

		#region Methods

		/// <summary>
		/// This method tells if the entity can move from one point to another in a straight line
		/// </summary>
		/// <param name="start">The start point</param>
		/// <param name="end">The end point</param>
		/// <returns>True if the entity can reach the end from the start, false if it can´t</returns>
		public virtual bool CanMoveBetween(Vector3 start, Vector3 end)
		{
			if (PhysicEngine.Physic == null)
			{
				throw new NullReferenceException("MovingEntity.CanMoveBetween() : PhysicEngine.Physic not defined");
			}
			return !PhysicEngine.Physic.WorldRayCast(ref start, ref end, look);
		}

		/// <summary>
		/// Method executed when the entity is destroyed
		/// </summary>
		/// <remarks>Deletes the mesh associated with this entity if it exists</remarks>
		protected override void Destroy()
		{
			if (meshObject is BaseEntity)
			{
				throw new NotImplementedException();
				//EntityManager.Instance.RemoveEntity(meshObject as BaseEntity);
			}
		}

		#endregion
	}
}
