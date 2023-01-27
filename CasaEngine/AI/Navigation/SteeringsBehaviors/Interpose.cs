#region Using Directives

using System;
using Microsoft.Xna.Framework;



#endregion

namespace CasaEngine.AI.Navigation.SteeringsBehaviors
{
	/// <summary>
	/// This class represents the interpose steering behavior. This behavior calculates the force
	/// needed to arrive to a middle position between to entities (to interpose between them)
	/// </summary>
	/// <example>
	/// A bodyguard putting himself between his protegee and a bad guy with a gun could be an
	/// example of this behavior. A soccer player intercepting a pass between to players
	/// of the enemy team could be another example
	/// </example>
	public class Interpose : SteeringBehavior
	{
		#region Fields

		/// <summary>
		/// The first entity to interpose
		/// </summary>
		protected MovingEntity agentA;

		/// <summary>
		/// The second entity to interpose
		/// </summary>
		protected MovingEntity agentB;

		/// <summary>
		/// Arrive helper behavior to move to the interposing position
		/// </summary>
		protected Arrive arrive;

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
		public Interpose(String name, MovingEntity owner, float modifier)
			: base(name, owner, modifier)
		{
			arrive = new Arrive(name + "Arrive", owner, 0, 0.1f);
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the first entity to interpose
		/// </summary>
		public MovingEntity AgentA
		{
			get { return agentA; }
			set { agentA = value; }
		}

		/// <summary>
		/// Gets or sets the second entity to interpose
		/// </summary>
		public MovingEntity AgentB
		{
			get { return agentB; }
			set { agentB = value; }
		}

		#endregion

		#region Methods

		/// <summary>
		/// Calculates the resultant force of this behavior
		/// </summary>
		/// <returns>The force vector of this behavior</returns>
		public override Vector3 Calculate()
		{
			Vector3 midPoint, posA, posB;
			float timeToReachMidPoint;

			//Calculate the time to reach the midpoint between the two agents
			midPoint = (ConstraintVector(agentA.Position) + ConstraintVector(agentB.Position)) * 0.5f;
			timeToReachMidPoint = (ConstraintVector(owner.Position) - midPoint).Length() / owner.MaxSpeed;

			//Calculate the estimated agent positions at that time
			posA = agentA.Position + agentA.Velocity * timeToReachMidPoint;
			posB = agentB.Position + agentB.Velocity * timeToReachMidPoint;

			//Calculate the mid point of the estimated positions and asap to it
			midPoint = (posA + posB) * 0.5f;
			arrive.TargetPosition = midPoint;

			return arrive.Calculate();
		}

		#endregion
	}
}
