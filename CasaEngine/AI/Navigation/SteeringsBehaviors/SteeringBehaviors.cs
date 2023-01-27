#region Using Directives

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using CasaEngine.Math;
using CasaEngineCommon.Helper;

//using Mathematics;

#endregion

namespace CasaEngine.AI.Navigation.SteeringsBehaviors
{
	/// <summary>
	/// This class manages all the registered behaviors that affect an entity and
	/// updates them to calculate the resulting force that should be applied to
	/// the entity
	/// </summary>
	public class SteeringBehaviors
	{
		#region Fields

		/// <summary>
		/// The owner entity of this class
		/// </summary>
		protected internal MovingEntity owner;

		/// <summary>
		/// The list of registered steering behaviors in order of priority
		/// </summary>
		/// <remarks>The order is important for the sum method</remarks>
		protected internal List<SteeringBehavior> behaviors;

		/// <summary>
		/// The way of combining the different registered behaviors to produce a total force
		/// </summary>
		protected internal SumMethod sumAlgorithm;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="owner">The owner entity of this class</param>
		/// <param name="sumAlgorithm">The way of combining the different behaviors to produce a total force</param>
		public SteeringBehaviors(MovingEntity owner, SumMethod sumAlgorithm)
		{
			this.owner = owner;
			this.sumAlgorithm = sumAlgorithm;

			behaviors = new List<SteeringBehavior>();
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the sumAlgorithm value. This value indicates the way of combining different behaviors to
		/// produce a total force
		/// </summary>
		public SumMethod SumAlgorithm
		{
			get { return sumAlgorithm; }
			set { sumAlgorithm = value; }
		}

		#endregion

		#region Methods

		/// <summary>
		/// Registers a new behavior to use it in the force calculations
		/// </summary>
		/// <typeparam name="T">The type of the behavior</typeparam>
		/// <param name="name">The name of the behavior</param>
		/// <param name="modifier">The modifier value of the behavior</param>
		/// <remarks>
		/// The order in which behaviors are registered is important, as it reflects the priority of each behavior when
		/// they are added together. The sooner the behavior is registered, the more important it is
		/// </remarks>
		public void RegisterBehavior<T>(String name, float modifier)
		{
			Object newBehavior;
			Object[] parametters;

			foreach (SteeringBehavior behavior in behaviors)
				if (behavior is T && behavior.Name == name)
					return;

			parametters = new object[3];
			parametters[0] = name;
			parametters[1] = this.owner;
			parametters[2] = modifier;

			newBehavior = Activator.CreateInstance(typeof(T), parametters);

			behaviors.Add((SteeringBehavior) newBehavior);
		}

		/// <summary>
		/// Registers a new behavior to use it in the force calculations
		/// </summary>
		/// <param name="behavior">The behavior to register</param>
		/// <remarks>
		/// The order in which behaviors are registered is important, as it reflects the priority of each behavior when
		/// they are added together. The sooner the behavior is registered, the more important it is
		/// </remarks>
		public void RegisterBehavior(SteeringBehavior behavior)
		{
			for (int i = 0; i < behaviors.Count; i++)
				if (behaviors[i].GetType() == behavior.GetType() && behaviors[i].Name  == behavior.Name)
				{
					behaviors[i] = behavior;
					return;
				}

			behaviors.Add(behavior);
		}

		/// <summary>
		/// Unregisters a behavior
		/// </summary>
		/// <param name="name">The name of the behavior to delete</param>
		public void UnregisterBehavior<T>(String name)
		{
			for (int i = 0; i < behaviors.Count; i++)
				if (behaviors[i] is T && behaviors[i].Name == name)
					behaviors.RemoveAt(i);
		}

		/// <summary>
		/// Activates a behavior so it´s taken into account in the sum calculations
		/// </summary>
		/// <param name="name">The name of the behavior to activate</param>
		public void ActivateBehavior<T>(String name)
		{
			foreach (SteeringBehavior behavior in behaviors)
				if (behavior is T && behavior.Name == name)
					behavior.Active = true;
		}

		/// <summary>
		/// Deactivates a behavior so it´s not taken into account in the sum calculations
		/// </summary>
		/// <param name="name">The name of the behavior to deactivate</param>
		public void DeactivateBehavior<T>(String name)
		{
			foreach (SteeringBehavior behavior in behaviors)
				if (behavior is T && behavior.Name == name)
					behavior.Active = false;
		}

		/// <summary>
		/// Gets a behavior
		/// </summary>
		/// <typeparam name="T">The type of the behavior to get</typeparam>
		/// <param name="name">The name of the behavior to get</param>
		/// <returns>The behavior asked</returns>
		public T GetBehavior<T>(String name) where T : SteeringBehavior
		{
			foreach (SteeringBehavior behavior in behaviors)
				if (behavior is T && behavior.Name == name)
					return (T) behavior;

			return null;
		}

		/// <summary>
		/// Calculates the total force of all the registered behaviors
		/// </summary>
		/// <returns>The total force produced by all the behaviors used</returns>
		public Vector3 Calculate()
		{
			return Vector3Helper.Truncate(sumAlgorithm(behaviors), owner.MaxForce);
		}

		#endregion
	}
}
