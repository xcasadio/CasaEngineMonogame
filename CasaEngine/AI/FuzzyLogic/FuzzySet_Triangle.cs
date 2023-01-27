using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.AI.Fuzzy
{
	/// <summary>
	/// 
	/// </summary>
	public class FuzzySet_Triangle
		: FuzzySet
	{
		#region Fields

		/// <summary>
		/// The values that define the shape of this FLV
		/// </summary>
		double m_dPeakPoint;
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
		public FuzzySet_Triangle(double mid, double lft, double rgt)
			: base(mid)
		{
			m_dPeakPoint = mid;
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
			//test for the case where the triangle's left or right offsets are zero
			//(to prevent divide by zero errors below)
			if ((m_dRightOffset == 0.0 && m_dPeakPoint == val) ||
				 (m_dLeftOffset == 0.0 && m_dPeakPoint == val))
			{
				return 1.0;
			}

			//find DOM if left of center
			if ((val <= m_dPeakPoint) && (val >= (m_dPeakPoint - m_dLeftOffset)))
			{
				double grad = 1.0 / m_dLeftOffset;

				return grad * (val - (m_dPeakPoint - m_dLeftOffset));
			}
			//find DOM if right of center
			else if ((val > m_dPeakPoint) && (val < (m_dPeakPoint + m_dRightOffset)))
			{
				double grad = 1.0 / -m_dRightOffset;

				return grad * (val - m_dPeakPoint) + 1.0;
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
