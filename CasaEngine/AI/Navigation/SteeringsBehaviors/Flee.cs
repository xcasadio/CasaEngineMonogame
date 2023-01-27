#region Using Directives

using System;
using Microsoft.Xna.Framework;



#endregion

namespace CasaEngine.AI.Navigation.SteeringsBehaviors
{
	/// <summary>
	/// This class represents the flee steering behavior. This behavior calculates the force
	/// needed to flee from a given position. The behavior can be modified to only take effect
	/// if the entity is situated at a certain distance or less to the flee position
	/// </summary>
	public class Flee : SteeringBehavior
	{
		#region Constants

		/// <summary>
		/// This constant represents that the entity always tries to flee
		/// </summary>
		public const float AlwaysFlee = -1.0f;

		#endregion

		#region Fields

		/// <summary>
		/// The distance at which the entity starts trying to flee
		/// </summary>
		protected internal float fleeDistance;

		/// <summary>
		/// The position the entity tries to flee from
		/// </summary>
		protected internal Vector3 fleePosition;

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
		public Flee(String name, MovingEntity owner, float modifier)
			: this(name, owner, modifier, AlwaysFlee) {}

		/// <summary>
		/// Overloaded constructor
		/// </summary>
		/// <param name="name">Name of the behavior, used to identify it</param>
		/// <param name="owner">The owner entity of the behavior</param>
		/// <param name="modifier">
		/// This value can modify the value of the behavior when the total force of all combined
		/// behaviors is updated
		/// </param>
		/// <param name="fleeDistance">The distance at which the entity starts trying to flee</param>
		public Flee(String name, MovingEntity owner, float modifier, float fleeDistance)
			: base(name, owner, modifier)
		{
			this.fleeDistance = fleeDistance;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the distance at which the entity starts trying to flee
		/// </summary>
		public float FleeDistance
		{
			get { return fleeDistance; }
			set { fleeDistance = value; }
		}

		/// <summary>
		/// Gets or sets the position the entity tries to flee from
		/// </summary>
		public Vector3 FleePosition
		{
			get { return fleePosition; }
			set { fleePosition = ConstraintVector(value); }
		}

		#endregion

		#region Methods

		/// <summary>
		/// Calculates the resultant force of this behavior
		/// </summary>
		/// <returns>The force vector of this behavior</returns>
		public override Vector3 Calculate()
		{
			Vector3 desiredVelocity;
			Vector3 toTarget;

			toTarget = ConstraintVector(owner.Position) - fleePosition;

			//If the flee position is too far away, don´t flee from it
			if ((fleeDistance != AlwaysFlee) && toTarget.Length() > fleeDistance)
				return Vector3.Zero;

			//If the entity should flee, calculate the velocity
			desiredVelocity = Vector3.Normalize(toTarget) * owner.MaxSpeed;
			return desiredVelocity - ConstraintVector(owner.Velocity);
		}

		#endregion
	}
}
