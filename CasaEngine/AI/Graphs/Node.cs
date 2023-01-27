#region Using Directives

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;




using Microsoft.Xna.Framework;


#endregion

namespace CasaEngine.AI.Graphs
{
	/// <summary>
	/// This class is the base class for creating nodes in a graph. All other node types should be derived from this one
	/// </summary>
	[Serializable]
	public class Node : ICloneable
	{
		#region Constants

		/// <summary>
		/// The node has no parent assigned
		/// </summary>
		public const int NoParent = -1;

		#endregion

		#region Fields

		/// <summary>
		/// The index of the node in the graph
		/// </summary>
		protected internal int index;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor. Sets the node index to Definitions.InvalidNode
		/// </summary>
		public Node()
		{
			index = Edge.InvalidNode;
		}

		/// <summary>
		/// Creates a node with a index number
		/// </summary>
		/// <param name="index">The index that we want to assign to this node</param>
		public Node(int index)
		{
			String message = String.Empty;

			if (ValidateIndex(index, ref message) == false)
				throw new AIException("index", this.GetType().ToString(), message);

			this.index = index;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets the index value of the node. Only classes from this namespace should be setting this value (the Graph class)
		/// </summary>
		public int Index
		{
			get { return index; }
			protected internal set { this.index = value; }
		}

		#endregion

		#region Validators

		/// <summary>
		/// Validates if the node index value is correct (>= -1)
		/// </summary>
		/// <param name="index">The node index value we want to validate</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the value is correct. False if it is not</returns>
		public static bool ValidateIndex(int index, ref string message)
		{
			if (index < Edge.InvalidNode)
			{
				message = "The index value of the node can't be less than " + Edge.InvalidNode;
				return false;
			}

			return true;
		}

		#endregion

		#region ICloneable Members

		/// <summary>
		/// Clones the node
		/// </summary>
		/// <returns>The cloned node</returns>
		public object Clone()
		{
			MemoryStream memory;
			BinaryFormatter formater;
			object clone;

			using (memory = new MemoryStream())
			{
				//Serialize ourselves
				formater = new BinaryFormatter();
				formater.Serialize(memory, this);

				//Move the memory buffer to the start
				memory.Seek(0, SeekOrigin.Begin);

				//Undo the serialization in the new clone object
				clone = formater.Deserialize(memory);

				return clone;
			}
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
		/// <remarks>A normal node is always a neighbour by default (it doesn´t have position data)</remarks>
		protected internal virtual bool IsNeighbour(int spacePartitionSector, Vector3 searchPosition, float searchRange)
		{
			return true;
		}

		#endregion
	}
}
