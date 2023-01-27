#region Using Directives

using System;
using Microsoft.Xna.Framework;

#endregion

namespace CasaEngine.AI.Graphs
{
	/// <summary>
	/// This class represents a node with information about the sector where it´s situated in the space
	/// </summary>
	[Serializable]
	public class SpacePartitionNode : NavigationNode
	{
		#region Fields

		/// <summary>
		/// The space sector where this node is situated
		/// </summary>
		protected internal int spaceSector;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		public SpacePartitionNode() : base()
			{}

		/// <summary>
		/// Constructor that assigns the sector to the node
		/// </summary>
		/// <param name="position">Position of the node</param>
		/// <param name="spaceSector">Space sector of the node</param>
		public SpacePartitionNode(Vector3 position, int spaceSector)
			: base(position)
		{
			this.spaceSector = spaceSector;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets the space vector of the node
		/// </summary>
		public int SpaceSector
		{
			get { return spaceSector; }
		}

		#endregion

		#region Methods

		/// <summary>
		/// This method indicates if the node can be considered a neighbour given certain parameters
		/// </summary>
		/// <param name="spacePartitionSector">The sector in the space partition structure</param>
		/// <param name="searchPosition">The point used to search</param>
		/// <param name="searchRange">The maximum distance we want to search from the start position</param>
		/// <returns>True if the node can be considered a neighbour, false if not</returns>
		protected internal override bool IsNeighbour(int spacePartitionSector, Vector3 searchPosition, float searchRange)
		{
			//Only search if the node and the search position are in the same space sector
			if (this.spaceSector == spacePartitionSector)
				return base.IsNeighbour(spacePartitionSector, searchPosition, searchRange);

			return false;
		}

		#endregion
	}
}
