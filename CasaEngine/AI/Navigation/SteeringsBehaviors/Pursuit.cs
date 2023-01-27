#region Using Directives

using System;
using Microsoft.Xna.Framework;

#endregion

namespace CasaEngine.AI.Navigation.SteeringsBehaviors
{
	/// <summary>
	/// This class represents the pursuit steering behavior. This behavior calculates the force
	/// needed to try to catch another moving entity. The pursuer try to estimates the future
	/// position of the evader and seeks towards that point
	/// </summary>
	public class Pursuit : SteeringBehavior
	{
		#region Fields

		/// <summary>
		/// The entity this behavior tries to pursue
		/// </summary>
		protected internal MovingEntity evader;

		/// <summary>
		/// Helper behavior used to pursue the evader
		/// </summary>
		protected internal Seek seek;

		/// <summary>
		/// The force produced by the behavior
		/// </summary>
		protected internal Vector3 force;
	
		/// <summary>
		/// The target position the behavior tries to seek
		/// </summary>
		/// <remarks>
		/// This position is estimated by the behavior
		/// </remarks>
		protected internal Vector3 targetPosition;

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
		public Pursuit(String name, MovingEntity owner, float modifier)
			: base(name, owner, modifier)
		{
			seek = new Seek(name + "Seek", owner, 0);
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets wheter the x-axis should be ignored for the behavior calculations or not
		/// </summary>
		public override bool IgnoreX
		{
			get { return base.IgnoreX; }
			set
			{
				base.IgnoreX = value;
				seek.IgnoreX = value;
			}
		}

		/// <summary>
		/// Gets or sets wheter the y-axis should be ignored for the behavior calculations or not
		/// </summary>
		public override bool IgnoreY
		{
			get { return base.IgnoreY; }
			set
			{
				base.IgnoreY = value;
				seek.IgnoreY = value;
			}
		}

		/// <summary>
		/// Gets or sets wheter the z-axis should be ignored for the behavior calculations or not
		/// </summary>
		public override bool IgnoreZ
		{
			get { return base.IgnoreZ; }
			set
			{
				base.IgnoreZ = value;
				seek.IgnoreZ = value;
			}
		}

		/// <summary>
		/// Gets or sets the entity this behavior tries to pursue
		/// </summary>
		public MovingEntity Evader
		{
			get { return evader; }
			set { evader = value; }
		}

		/// <summary>
		/// Gets the force produced by the behavior
		/// </summary>
		public Vector3 Force
		{
			get { return force; }
		}

		/// <summary>
		/// Gets the target position the behavior tries to seek
		/// </summary>
		public Vector3 TargetPosition
		{
			get { return targetPosition; }
		}

		#endregion

		#region Methods

		/// <summary>
		/// Calculates the resultant force of this behavior
		/// </summary>
		/// <returns>The force vector of this behavior</returns>
		public override Vector3 Calculate()
		{
			Vector3 toEvader;
			float relativeHeading, lookAheadTime;

			//Get the distance and heading of the evader
			toEvader = ConstraintVector(evader.Position) - ConstraintVector(owner.Position);
			relativeHeading = Vector3.Dot(ConstraintVector(owner.Look), ConstraintVector(evader.Look));

			//If the evader is in front of the pursuer, seek to it
			if ((relativeHeading < -0.95f) && (Vector3.Dot(toEvader, ConstraintVector(owner.Look)) > 0.0f))
			{
				targetPosition = evader.Position;
				seek.TargetPosition = targetPosition;

				force = seek.Calculate();
				return force;
			}

			//If not, try to estimate where it´s going to be in a future time
			lookAheadTime = toEvader.Length() / (owner.MaxSpeed + evader.Speed);

			//If the pursuer needs to turn
			if (owner.MaxTurnRate != 0.0f)
				lookAheadTime += TurnTime(owner, evader.Position);

			//Seek to the estimated future position
			targetPosition = evader.Position + evader.Velocity * lookAheadTime;
			seek.TargetPosition = targetPosition;

			force = seek.Calculate();
			return force;
		}

		#endregion

		#region Helper Methods

		/// <summary>
		/// Calculates the time needed for an entity to turn to face a given position
		/// </summary>
		/// <param name="agent">The entity that is going to turn</param>
		/// <param name="position">The position to face when turning</param>
		/// <returns></returns>
		private float TurnTime(MovingEntity agent, Vector3 position)
		{
			Vector3 toTarget;
			float dot;
			
			//Calculate the vector to the target and then the dot product between that vector and the entity look
			toTarget = Vector3.Normalize(ConstraintVector(position) - ConstraintVector(agent.Position));
			dot = Vector3.Dot(ConstraintVector(agent.Look), toTarget);

			return (dot - 1.0f) * (-agent.MaxTurnRate);
		}

		#endregion
	}
}
