using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace CasaEngine.AI.Navigation
{
	/// <summary>
	/// 
	/// </summary>
	public interface IPhysicEngine
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="position_">start position</param>
		/// <param name="feelers_"></param>
		/// <param name="contactPoint_">the nearest contact point</param>
		/// /// <param name="contactPoint_">the nearest contact point normal</param>
		/// <returns>true if contact</returns>
		bool NearBodyWorldRayCast(ref Vector3 position_, ref Vector3 feelers_, out Vector3 contactPoint_, out Vector3 ContactNormal_);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="start"></param>
		/// <param name="end"></param>
		/// <param name="dir_"></param>
		/// <returns>true if contact with a body</returns>
		bool WorldRayCast(ref Vector3 start, ref Vector3 end, Vector3 dir_);
	}

	/// <summary>
	/// 
	/// </summary>
	public static class PhysicEngine
	{
		static public IPhysicEngine Physic = null;
	}
}
