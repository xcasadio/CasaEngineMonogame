#region Using Directives

using System;
using Microsoft.Xna.Framework;



#endregion

namespace CasaEngine.AI.Navigation.SteeringsBehaviors
{
	/// <summary>
	/// This class represents the seek steering behavior. This behavior calculates the force
	/// to seek towards a static position. The entity won´t decelerate upon reaching the destination
	/// (see <see cref="Arrive"/> for that)
	/// </summary>
	public class Seek : SteeringBehavior
	{
		#region Fields

		/// <summary>
		/// The target position the behavior tries to seek
		/// </summary>
		protected internal Vector3 targetPosition;

		/// <summary>
		/// The force produced by the behavior
		/// </summary>
		protected internal Vector3 force;
	
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
		public Seek(String name, MovingEntity owner, float modifier)
			: base(name, owner, modifier) { }

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the target position the behavior tries to seek
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

		#endregion

		#region Methods

		/// <summary>
		/// Calculates the resultant force of this behavior
		/// </summary>
		/// <returns>The force vector of this behavior</returns>
		public override Vector3 Calculate()
		{
			Vector3 desiredVelocity;

			desiredVelocity = Vector3.Normalize(targetPosition - ConstraintVector(owner.Position)) * owner.MaxSpeed;
			force = desiredVelocity - ConstraintVector(owner.Velocity);

			return force;
		}

		#endregion
	}
}
