using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.AI.Fuzzy
{
	/// <summary>
	/// 
	/// </summary>
	public class FzSet
		: FuzzyTerm
	{
		#region Fields

		internal FuzzySet m_Set;

		#endregion

		#region Properties

		/// <summary>
		/// Gets DOM
		/// </summary>
		public double DOM
		{
			get { return m_Set.DOM; }
		}

		#endregion

		#region Constructors

		/// <summary>
		/// 
		/// </summary>
		/// <param name="fs"></param>
		public FzSet(FuzzySet fs)
		{
			m_Set = fs;
		}

		#endregion

		#region Methods

		/// <summary>
		/// Clone
		/// </summary>
		/// <returns></returns>
		public FuzzyTerm Clone()
		{
			return new FzSet(m_Set);
		}

		/// <summary>
		/// Clear DOM of the FuzzySet
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
			m_Set.ORwithDOM(val);
		}

		#endregion
	}
}
