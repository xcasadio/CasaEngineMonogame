#region Using Directives

using System;
using Microsoft.Xna.Framework;



#endregion

namespace CasaEngine.AI.Navigation.SteeringsBehaviors
{
	/// <summary>
	/// This class represents the arrive steering behavior. This behavior calculates the force
	/// to arrive at a static position. The entity will  decelerate upon reaching the destination
	/// and stop at it
	/// </summary>
	public class Arrive : SteeringBehavior
	{
		#region Fields

		/// <summary>
		/// Deceleration value of the behavior. The lower the value, the faster the deceleration
		/// </summary>
		protected internal float slowingDistance;

		/// <summary>
		/// The target position the behavior tries to arrive
		/// </summary>
		protected internal Vector3 targetPosition;

		/// <summary>
		/// The force produced by the behavior
		/// </summary>
		protected internal Vector3 force;

		/// <summary>
		/// Maximum speed that the arrive behavior can produce
		/// </summary>
		protected internal float maximumSpeed;
	
		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="name">Name of the behavior, used to identify it</param>
		/// <param name="owner">The owner entity of the behavior</param>
		/// <param name="modifier">
		/// This value can modify the value of the behavior when the total force of all combined
		/// behaviors is updated
		/// </param>
		public Arrive(String name, MovingEntity owner, float modifier)
			: this(name, owner, modifier, 1.0f) {}

		/// <summary>
		/// Overloaded constructor
		/// </summary>
		/// <param name="name">Name of the behavior, used to identify it</param>
		/// <param name="owner">The owner entity of the behavior</param>
		/// <param name="modifier">
		/// This value can modify the value of the behavior when the total force of all combined
		/// behaviors is updated
		/// </param>
		/// <param name="slowingDistance">
		/// When the target is at this distance or less the behavior will start to decelerate to arrive at it
		/// </param>
		public Arrive(String name, MovingEntity owner, float modifier, float slowingDistance)
			: base(name, owner, modifier)
		{
			this.slowingDistance = slowingDistance;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the deceleration value of the behavior. The lower the value, the faster the deceleration
		/// </summary>
		public float SlowingDistance
		{
			get { return slowingDistance; }
			set { slowingDistance = value; }
		}

		/// <summary>
		/// Gets or sets the target position the behavior tries to arrive to
		/// </summary>
		public Vector3 TargetPosition
		{
			get { return targetPosition; }
			set { targetPosition = ConstraintVector(value); }
		}

		/// <summary>
		/// Gets the force produced by the behavior
		/// </summary>
		public Vector3 Force
		{
			get { return force; }
		}

		/// <summary>
		/// Maximum speed that the arrive behavior can produce
		/// </summary>
		public float MaximumSpeed
		{
			get { return maximumSpeed; }
		}

		#endregion

		#region Methods

		/// <summary>
		/// Calculates the resultant force of this behavior
		/// </summary>
		/// <returns>The force vector of this behavior</returns>
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

		#endregion
	}
}
