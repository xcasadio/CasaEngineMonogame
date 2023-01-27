#region Using Directives

using System;

#endregion

namespace CasaEngine.AI.Graphs
{
	/// <summary>
	/// This class represents a weighted edge with extra information
	/// </summary>
	[Serializable]
	public class AnnotatedEdge : WeightedEdge
	{
		#region Fields

		/// <summary>
		/// Extra information about the edge
		/// </summary>
		protected internal EdgeInformation information;

		#endregion

		#region Constructors

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <remarks>The edge information is set to normal</remarks>
		public AnnotatedEdge()
		{
			this.information = EdgeInformation.Normal;
		}

		/// <summary>
		/// Parametrized constructor
		/// </summary>
		/// <param name="start">Start node of the edge</param>
		/// <param name="end">End node of the edge</param>
		/// <param name="cost">Cost of the edge</param>
		/// <param name="information">Extra information of the edge</param>
		public AnnotatedEdge(int start, int end, float cost, EdgeInformation information)
			: base(start, end, cost)
		{
			this.information = information;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the edge extra information
		/// </summary>
		public EdgeInformation Information
		{
			get { return information; }
			set { information = value; }
		}

		#endregion
	}
}
