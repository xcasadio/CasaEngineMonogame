using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.AI.Fuzzy
{
	/// <summary>
	/// This defines a fuzzy set that is a singleton (a range over which the DOM is always 1.0)
	/// </summary>
	public class FuzzySet_Singleton
		: FuzzySet
	{
		#region Fields

		//the values that define the shape of this FLV
		double m_dMidPoint;
		double m_dLeftOffset;
		double m_dRightOffset;

		#endregion

		#region Properties

		#endregion

		#region Constructors

		/// <summary>
		/// 
		/// </summary>
		/// <param name="mid"></param>
		/// <param name="lft"></param>
		/// <param name="rgt"></param>
		public FuzzySet_Singleton(double mid, double lft, double rgt)
			: base(mid)
		{
			m_dMidPoint = mid;
			m_dLeftOffset = lft;
			m_dRightOffset = rgt;
		}

		#endregion

		#region Methods

		/// <summary>
		/// 
		/// </summary>
		/// <param name="val"></param>
		/// <returns></returns>
		public override double CalculateDOM(double val)
		{
			if ((val >= m_dMidPoint - m_dLeftOffset) &&
				 (val <= m_dMidPoint + m_dRightOffset))
			{
				return 1.0;
			}

			//out of range of this FLV, return zero
			else
			{
				return 0.0;
			}
		}

		#endregion
	}
}
