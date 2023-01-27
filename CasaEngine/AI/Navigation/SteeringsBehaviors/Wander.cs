#region Using Directives

using System;
using Microsoft.Xna.Framework;



#endregion

namespace CasaEngine.AI.Navigation.SteeringsBehaviors
{
	/// <summary>
	/// This class represents the wander steering behavior. This behavior calculates a force
	/// that moves the entity in a random manner, but in a good looking way (like if the entity
	/// was wandering around the scene)
	/// </summary>
	public class Wander : SteeringBehavior
	{
		#region Fields

		/// <summary>
		/// The distance to project the wandering circle
		/// </summary>
		protected internal float distance;

		/// <summary>
		/// The radius of the wandering circle
		/// </summary>
		protected internal float radius;

		/// <summary>
		/// Small noise added to the wander target
		/// </summary>
		protected internal float jitter;

		/// <summary>
		/// The target position the entity tries to wander to. This position is constrained in 
		/// a circle around the entity
		/// </summary>
		protected internal Vector3 wanderTarget;
	
		/// <summary>
		/// Random number generator to create small variations in the movement
		/// </summary>
		protected internal Random generator;

		/// <summary>
		/// The force produced by the behavior
		/// </summary>
		protected internal Vector3 force;

		/// <summary>
		/// The target position the behavior moves to
		/// </summary>
		protected internal Vector3 targetPosition;

		#endregion

		#region Constructor

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="name">Name of the behavior, used to identify it</param>
		/// <param name="owner">The owner entity of the behavior</param>
		/// <param name="modifier">
		/// This value can modify the value of the behavior when the total force of all combined
		/// behaviors is updated
		/// </param>
		public Wander(String name, MovingEntity owner, float modifier)
			: base(name, owner, modifier)
		{
			generator = new Random();
		}

		/// <summary>
		/// Overloaded constructor
		/// </summary>
		/// <param name="name">Name of the behavior, used to identify it</param>
		/// <param name="owner">The owner entity of the behavior</param>
		/// <param name="modifier">
		/// This value can modify the value of the behavior when the total force of all combined
		/// behaviors is updated
		/// </param>
		/// <param name="radius">The radius of the wandering circle</param>
		/// <param name="distance">The distance to project the wandering circle</param>
		/// <param name="jitter">Small noise added to the wander target</param>
		public Wander(String name, MovingEntity owner, float modifier, float radius, float distance, float jitter)
			: base(name, owner, modifier)
		{
			double alfa, beta, theta;

			generator = new Random();

			this.radius = radius;
			this.distance = distance;
			this.jitter = jitter;

			//Create a vector to a target position on the wander circle						
			alfa = generator.NextDouble() * System.Math.PI * 2;
			beta = generator.NextDouble() * System.Math.PI * 2;
			theta = generator.NextDouble() * System.Math.PI * 2;

			wanderTarget = new Vector3(radius * (float)System.Math.Cos(alfa), radius * (float)System.Math.Cos(beta), radius * (float) System.Math.Cos(theta));
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the distance to project the wandering circle
		/// </summary>
		public float Distance
		{
			get { return distance; }
			set { distance = value; }
		}

		/// <summary>
		/// Gets or sets the radius of the wandering circle
		/// </summary>
		public float Radius
		{
			get { return radius; }
			set
			{
				double alfa, beta, theta;

				radius = value;

				//Create a vector to a target position on the wander circle						
				alfa = generator.NextDouble() * System.Math.PI * 2;
				beta = generator.NextDouble() * System.Math.PI * 2;
				theta = generator.NextDouble() * System.Math.PI * 2;

				wanderTarget = new Vector3(radius * (float) System.Math.Cos(alfa), radius * (float) System.Math.Cos(beta), radius * (float) System.Math.Cos(theta));
			}
		}

		/// <summary>
		/// Gets or sets a small noise added to the wander target
		/// </summary>
		public float Jitter
		{
			get { return jitter; }
			set { jitter = value; }
		}

		/// <summary>
		/// Gets the force produced by the behavior
		/// </summary>
		public Vector3 Force
		{
			get { return force; }
		}

		/// <summary>
		/// Gets the target position the entity tries to wander to.
		/// </summary>
		public Vector3 WanderTarget
		{
			get { return wanderTarget; }
		}


		/// <summary>
		/// The target position the behavior moves to
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
			Vector3 displacement;

			//Randomize a little the wander target
			wanderTarget += new Vector3(jitter * (float)(generator.NextDouble() * 2 - 1), jitter * (float)(generator.NextDouble() * 2 - 1), jitter * (float)(generator.NextDouble() * 2 - 1));
			ConstraintVector(ref wanderTarget);

			//Normalize it and reproject it again
			wanderTarget.Normalize();
			wanderTarget *= radius;

			//Project it in front of the entity and transform to world coordinates
			displacement = Vector3.Normalize(owner.Look) * distance;
			targetPosition = owner.Position + wanderTarget + displacement;

			force = ConstraintVector(targetPosition - owner.Position);
			return force;
		}

		#endregion
	}
}
