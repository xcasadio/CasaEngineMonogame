#region Using Directives

using System;
using Microsoft.Xna.Framework;



#endregion

namespace CasaEngine.AI.Navigation.SteeringsBehaviors
{
	/// <summary>
	/// This class represents the evade steering behavior. This behavior calculates the force
	/// needed to evade another entity
	/// </summary>
	public class Evade : SteeringBehavior
	{
		#region Fields

		/// <summary>
		/// The entity that the behavior tries to avoid
		/// </summary>
		protected MovingEntity pursuer;

		/// <summary>
		/// Flee helper behavior
		/// </summary>
		protected Flee flee;

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
		public Evade(String name, MovingEntity owner, float modifier)
			: base(name, owner, modifier)
		{
			flee = new Flee(name + "Flee", owner, 0);
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the entity that the behavior tries to avoid
		/// </summary>
		public MovingEntity Pursuer
		{
			get { return pursuer; }
			set { pursuer = value; }
		}

		#endregion

		#region Methods

		/// <summary>
		/// Calculates the resultant force of this behavior
		/// </summary>
		/// <returns>The force vector of this behavior</returns>
		public override Vector3 Calculate()
		{
			Vector3 toPursuer;
			float lookAheadTime;

			//Get the vector to the pursuer and the time it will take to cover it
			toPursuer = ConstraintVector(pursuer.Position) - ConstraintVector(owner.Position);
			lookAheadTime = toPursuer.Length() / (owner.MaxSpeed + pursuer.Speed);

			//Flee from the estimated position of the pursuer
			flee.FleePosition = pursuer.Position + pursuer.Velocity * lookAheadTime;
			return flee.Calculate();
		}

		#endregion
	}
}
