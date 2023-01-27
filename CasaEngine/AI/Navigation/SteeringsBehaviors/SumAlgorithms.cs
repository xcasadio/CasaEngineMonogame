#region Using Directives

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;



#endregion

namespace CasaEngine.AI.Navigation.SteeringsBehaviors
{
	/// <summary>
	/// This class provides some example for sum algorithms to use in the calculation of the total
	/// force of a group of steering behaviors
	/// </summary>
	public static class SumAlgorithms
	{
		#region Methods

		/// <summary>
		/// This method performs a weighted sum between the force of all the active steering behaviors 
		/// </summary>
		/// <param name="behaviors">Behaviors to add</param>
		/// <returns>The weighted added force of all active behaviors</returns>
		public static Vector3 WeightedSum(List<SteeringBehavior> behaviors)
		{
			Vector3 totalForce;

			totalForce = Vector3.Zero;

			foreach (SteeringBehavior behavior in behaviors)
				if (behavior.Active == true)
					totalForce += behavior.Calculate() * behavior.Modifier;

			return totalForce;
		}

		/// <summary>
		/// TODO
		/// </summary>
		/// <param name="behaviors">TODO</param>
		/// <returns>TODO</returns>
		public static Vector3 WeightedRunningSumWithPrioritization(List<SteeringBehavior> behaviors)
		{
			Vector3 totalForce;

			totalForce = Vector3.Zero;

			return totalForce;
		}

		/// <summary>
		/// TODO
		/// </summary>
		/// <param name="behaviors">TODO</param>
		/// <returns>TODO</returns>
		public static Vector3 PrioritizedDithering(List<SteeringBehavior> behaviors)
		{
			Vector3 totalForce;

			totalForce = Vector3.Zero;

			return totalForce;
		}

		#endregion
	}
}
