#region Using Directives

using System;
using Microsoft.Xna.Framework;
//using CasaEngine.GameLogic;
//using CasaEngine.PhysicEngine;
//using BEPUphysics;
//using CasaEnginePhysics;

#endregion

namespace CasaEngine.AI.Navigation.SteeringsBehaviors
{
	/// <summary>
	/// This class represents the wall avoidance steering behavior. This behavior calculates the force
	/// needed to avoid crashing into walls or other objects of the scene
	/// </summary>
	public class WallAvoidance : SteeringBehavior
	{
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
		public WallAvoidance(String name, MovingEntity owner, float modifier)
			: base(name, owner, modifier)
		{ }

		#endregion

		#region Methods

		/// <summary>
		/// Calculates the resultant force of this behavior
		/// </summary>
		/// <returns>The force vector of this behavior</returns>
		public override Vector3 Calculate()
		{
			if (PhysicEngine.Physic == null)
			{
				throw new NullReferenceException("MovingEntity.CanMoveBetween() : PhysicEngine.Physic not defined");
			}

			Vector3 force, position, overShoot, contactPoint, contactNormal;
			Vector3[] feelers;
			float nearIntersectionDist, scale;

			feelers = new Vector3[3];
			scale = 2.0f + owner.Speed * 0.5f;
			force = Vector3.Zero;

			//Create the feelers
			feelers[0] = owner.Position + owner.Look * scale;
			feelers[1] = owner.Position + Vector3.TransformNormal(owner.Look, Matrix.CreateRotationY(MathHelper.ToRadians(-40.0f))) * scale * 0.5f;
			feelers[2] = owner.Position + Vector3.TransformNormal(owner.Look, Matrix.CreateRotationY(MathHelper.ToRadians(40.0f))) * scale * 0.5f;

			nearIntersectionDist = float.MaxValue;

			for (int i = 0; i < feelers.Length; i++)
			{
				position = owner.Position;

				//Test for a collision
				owner.Position = position;

				//If there was a collision see the collision distance
				if (PhysicEngine.Physic.NearBodyWorldRayCast(ref position, ref feelers[i], out contactPoint, out contactNormal) == true)
				{
					float intersectionDist = (contactPoint - owner.Position).Length();

					//If it was closer than the the closer collision so far, update the values
					if (intersectionDist < nearIntersectionDist)
					{
						nearIntersectionDist = intersectionDist;
						overShoot = contactPoint - feelers[i];
						force = contactNormal * overShoot.Length() * scale;
					}
				}
			}

			return force;
		}

		#endregion
	}
}
