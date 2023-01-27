#region Using Directives

using System;
using System.Collections.Generic;




using Microsoft.Xna.Framework;

#endregion

namespace CasaEngine.AI.Graphs
{
	/// <summary>
	/// This class represents a node with a physical position. It´s used by the pathplanner to search a path
	/// </summary>
	[Serializable]
	public class NavigationNode : Node
	{
		#region Fields

		/// <summary>
		/// Position of the node
		/// </summary>
		protected internal Vector3 position;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		public NavigationNode() : base()
			{}

		/// <summary>
		/// Constructor that assigns the position to the node
		/// </summary>
		/// <param name="position">Position of the node</param>
		public NavigationNode(Vector3 position) : base()
		{
			this.position = position;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the node position
		/// </summary>
		public Vector3 Position
		{
			get { return position; }
			set { position = value;	}
		}

		#endregion

		#region Methods

		/// <summary>
		/// This method indicates if the node can be considered a neighbour given certain parameters
		/// </summary>
		/// <param name="spacePartitionSector">The sector in the space partition structure</param>
		/// <param name="searchPosition">The point used to search</param>
		/// <param name="searchRange">The maximum distance we want to search from the start position (in squared distance)</param>
		/// <returns>True if the node can be considered a neighbour, false if not</returns>
		/// <remarks>A navigation node doesn´t use the space partition data</remarks>
		protected internal override bool IsNeighbour(int spacePartitionSector, Vector3 searchPosition, float searchRange)
		{

			//float parameter;

			//If the node is too far from the search position return false
			//float dist = (this.position - searchPosition).LengthSq();
			//if ((this.position - searchPosition).LengthSq() > searchRange)
			//    return false;

			return true;

			////If not try to cast a ray from one position to the other
			//return !Jad.Physics.WorldRayCast(ref this.position, ref searchPosition, out parameter);
		}

		#endregion
	}
}
