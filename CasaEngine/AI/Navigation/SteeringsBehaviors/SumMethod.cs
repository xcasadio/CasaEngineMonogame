#region Using Directives

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;



#endregion

namespace CasaEngine.AI.Navigation.SteeringsBehaviors
{
	/// <summary>
	/// This delegate represents a method of combining a group of steering behaviors
	/// to calculate their combined force
	/// </summary>
	/// <param name="behaviors">The list of behaviors to calculate</param>
	/// <returns>The total combined sum of all the behaviors</returns>
	public delegate Vector3 SumMethod(List<SteeringBehavior> behaviors);
}
