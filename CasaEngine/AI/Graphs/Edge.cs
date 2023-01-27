#region Using Directives

using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;




#endregion

namespace CasaEngine.AI.Graphs
{
	/// <summary>
	/// This class is the base class for creating edges in a graph. All other edge types should be derived from this one
	/// </summary>
	[Serializable]
	public class Edge : ICloneable
	{
		#region Constants

		/// <summary>
		/// The edge points to an invalid node
		/// </summary>
		public const int InvalidNode = -1;

		#endregion

		#region Fields

		/// <summary>
		/// The start node of the edge
		/// </summary>
		protected internal int start;

		/// <summary>
		/// The end node of the edge
		/// </summary>
		protected internal int end;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor. The edge points to Definitions.InvalidNode
		/// </summary>
		public Edge()
		{
			start = Edge.InvalidNode;
			end = Edge.InvalidNode;
		}

		/// <summary>
		/// Creates an edge with its start and its end
		/// </summary>
		/// <param name="start">Start node of the edge</param>
		/// <param name="end">End node of the edge</param>
		public Edge(int start, int end)
		{
			String message = String.Empty;

			if (ValidateNode(start, ref message) == false)
				throw new AIException("start", this.GetType().ToString(), message);

			if (ValidateNode(end, ref message) == false)
				throw new AIException("end", this.GetType().ToString(), message);

			this.start = start;
			this.end = end;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the start of the edge
		/// </summary>
		public int Start
		{
			get { return start; }
			set
			{
				String message = String.Empty;

				if (ValidateNode(value, ref message) == false)
					throw new AIException("start", this.GetType().ToString(), message);

				this.start = value;
			}
		}

		/// <summary>
		/// Gets or sets the end of the edge
		/// </summary>
		public int End
		{
			get { return end; }
			set
			{

				String message = String.Empty;

				if (ValidateNode(value, ref message) == false)
					throw new AIException("end", this.GetType().ToString(), message);

				this.end = value;
			}
		}

		#endregion

		#region Validators

		/// <summary>
		/// Validates if the node index value is correct (>= -1)
		/// </summary>
		/// <param name="index">The node index value we want to validate</param>
		/// <param name="message">Message explaining why the validation failed</param>
		/// <returns>True if the value is correct. False if it is not</returns>
		public static bool ValidateNode(int index, ref string message)
		{
			if (index < Edge.InvalidNode)
			{
				message = "The node index number must be equal or greater than " + Edge.InvalidNode;
				return false;
			}

			return true;
		}

		#endregion

		#region ICloneable Members

		/// <summary>
		/// Clones the edge
		/// </summary>
		/// <returns>The cloned edge</returns>
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
	}
}
