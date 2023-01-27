using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.AI.Fuzzy
{
	public class FzVery
		: FuzzyTerm
	{
		#region Fields

		FuzzySet m_Set;

		//prevent copying and assignment by clients
		//FzVery& operator=(const FzVery&);

		#endregion

		#region Properties

		/// <summary>
		/// Gets DOM
		/// </summary>
		public double DOM
		{
			get { return m_Set.DOM * m_Set.DOM; }
		}

		#endregion

		#region Constructors

		/// <summary>
		/// 
		/// </summary>
		/// <param name="inst"></param>
		public FzVery(FzVery inst)
		{
			m_Set = inst.m_Set;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="?"></param>
		private FzVery(FzSet ft)
		{
			m_Set = ft.m_Set;
		}

		#endregion

		#region Methods

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public FuzzyTerm Clone()
		{
			return new FzVery(this);
		}

		/// <summary>
		/// 
		/// </summary>
		public void ClearDOM()
		{
			m_Set.ClearDOM();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="val"></param>
		public void ORwithDOM(double val)
		{
			m_Set.ORwithDOM(val * val);
		}

		#endregion
	}

	/// <summary>
	/// 
	/// </summary>
	public class FzFairly
		: FuzzyTerm
	{
		#region Fields

		FuzzySet m_Set;

		#endregion

		#region Properties

		/// <summary>
		/// Gets DOM
		/// </summary>
		public double DOM
		{
			get { return System.Math.Sqrt(m_Set.DOM); }
		}

		#endregion

		#region Constructors

		/// <summary>
		/// Prevent copying and assignment
		/// </summary>
		/// <param name="inst"></param>
		private FzFairly(FzFairly inst)
		{
			m_Set = inst.m_Set;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="ft"></param>
		public FzFairly(FzSet ft)
		{
			m_Set = ft.m_Set;
		}

		#endregion

		#region Methods

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public FuzzyTerm Clone()
		{
			return new FzFairly(this);
		}

		/// <summary>
		/// 
		/// </summary>
		public void ClearDOM()
		{
			m_Set.ClearDOM();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="val"></param>
		public void ORwithDOM(double val)
		{
			m_Set.ORwithDOM(System.Math.Sqrt(val));
		}

		#endregion
	}
}
