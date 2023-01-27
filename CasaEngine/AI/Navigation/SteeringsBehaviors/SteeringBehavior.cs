#region Using Directives

using System;
using Microsoft.Xna.Framework;



#endregion

namespace CasaEngine.AI.Navigation.SteeringsBehaviors
{
	/// <summary>
	/// This class represents a steering behavior. A steering behavior represents a type of movement
	/// that produces a force in an entity. The produced force depends on the type of behavior
	/// </summary>
	public abstract class SteeringBehavior
	{
		#region Fields

		/// <summary>
		/// Name of the behavior, it´s used to identify it
		/// </summary>
		protected internal String name;

		/// <summary>
		/// The owner entity of the behavior
		/// </summary>
		protected internal MovingEntity owner;

		/// <summary>
		/// This value can modify the value of the behavior when the total force of all combined
		/// behaviors is updated in the <see cref="SumMethod"/>
		/// </summary>
		protected internal float modifier;

		/// <summary>
		/// Indicates if the behavior is active or not. This value indicates if the behavior should be used
		/// or not in the updating of the entity total steering force
		/// </summary>
		protected internal bool active;

		/// <summary>
		/// The x-axis is ignored in the behavior calculations
		/// </summary>
		protected internal bool ignoreX;

		/// <summary>
		/// The y-axis is ignored in the behavior calculations
		/// </summary>
		protected internal bool ignoreY;

		/// <summary>
		/// The z-axis is ignored in the behavior calculations
		/// </summary>
		protected internal bool ignoreZ;

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
		public SteeringBehavior(String name, MovingEntity owner, float modifier)
		{
			this.name = name;
			this.owner = owner; 
			this.modifier = modifier;

			this.active = false;

			ignoreX = false;
			ignoreY = false;
			ignoreZ = false;
		}

		#endregion

		#region Propertes

		/// <summary>
		/// Gets the owner entity of the behavior
		/// </summary>
		public MovingEntity Owner
		{
			get { return owner; }
		}
		/// <summary>
		/// Gets or sets the name of the behavior. This value it´s used to identify it
		/// </summary>
		public String Name
		{
			get { return name; }
			set { name = value; }
		}

		/// <summary>
		/// Gets or sets the active value. This value indicates if the behavior should be used or not in the
		/// updating of the entity total steering force
		/// </summary>
		public bool Active
		{
			get { return active; }
			set { active = value; }
		}

		/// <summary>
		/// Gets or sets the modifier value value. This value can modify the value of the behavior when the
		/// total force of all combined behaviors is updated
		/// </summary>
		public float Modifier
		{
			get { return modifier; }
			set { modifier = value; }
		}

		/// <summary>
		/// Gets or sets wheter the x-axis should be ignored for the behavior calculations or not
		/// </summary>
		public virtual bool IgnoreX
		{
			get { return ignoreX; }
			set { ignoreX = value; }
		}

		/// <summary>
		/// Gets or sets wheter the y-axis should be ignored for the behavior calculations or not
		/// </summary>
		public virtual bool IgnoreY
		{
			get { return ignoreY; }
			set { ignoreY = value; }
		}

		/// <summary>
		/// Gets or sets wheter the z-axis should be ignored for the behavior calculations or not
		/// </summary>
		public virtual bool IgnoreZ
		{
			get { return ignoreZ; }
			set { ignoreZ = value; }
		}

		#endregion

		#region Methods

		/// <summary>
		/// Calculates the resultant force of this behavior
		/// </summary>
		/// <returns>The force vector of this behavior</returns>
		public abstract Vector3 Calculate();

		/// <summary>
		/// Constraints a vector taking out the values of the axis the behavior
		/// has been set to ignore
		/// </summary>
		/// <param name="vector">The vector to constraint</param>
		/// <returns>The constrained vector</returns>
		protected Vector3 ConstraintVector(Vector3 vector)
		{
			if (ignoreX == true)
				vector.X = 0;

			if (ignoreY == true)
				vector.Y = 0;

			if (ignoreZ == true)
				vector.Y = 0;

			return vector;
		}

		/// <summary>
		/// Constraints a vector taking out the values of the axis the behavior
		/// has been set to ignore
		/// </summary>
		/// <param name="vector">The vector to constraint</param>
		protected void ConstraintVector(ref Vector3 vector)
		{
			if (ignoreX == true)
				vector.X = 0;

			if (ignoreY == true)
				vector.Y = 0;

			if (ignoreZ == true)
				vector.Y = 0;
		}

		#endregion
	}
}
