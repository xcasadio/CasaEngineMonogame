#region Using Directives

using System;

using CasaEngine.AI.Graphs;
using Microsoft.Xna.Framework;

#endregion

namespace CasaEngine.AI.Navigation
{
	/// <summary>
	/// This class represents a navigation edge for a path of edges
	/// </summary>
	/// <remarks>Even if it´s called edge, it´s not used in graphs</remarks>
	[Serializable]
	public class NavigationEdge
	{
		#region Fields

		/// <summary>
		/// The start position of the edge
		/// </summary>
		protected internal Vector3 start;

		/// <summary>
		/// The end position of the edge
		/// </summary>
		protected internal Vector3 end;

		/// <summary>
		/// The navigation information associated with this edge
		/// </summary>
		protected internal EdgeInformation information;
		
		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="start">Start position of the edge</param>
		/// <param name="end">End position of the edge</param>
		/// <param name="information">Aditional information of the edge</param>
		public NavigationEdge(Vector3 start, Vector3 end, EdgeInformation information)
		{
			this.start = start;
			this.end = end;
			this.information = information;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the start of the edge
		/// </summary>
		public Vector3 Start
		{
			get { return start; }
			set { start = value; }
		}

		/// <summary>
		/// Gets or sets the end of the edge
		/// </summary>
		public Vector3 End
		{
			get { return end; }
			set { end = value; }
		}

		/// <summary>
		/// Gets or sets the navigation information of the edge
		/// </summary>
		public EdgeInformation Information
		{
			get { return information; }
			set	{ information = value; }
		}

		#endregion
	}
}
