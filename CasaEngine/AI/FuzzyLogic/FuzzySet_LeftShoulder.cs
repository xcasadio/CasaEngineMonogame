using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.AI.Fuzzy
{
	/// <summary>
	/// 
	/// </summary>
	public class FuzzySet_LeftShoulder
		: FuzzySet
	{
		#region Fields

		/// <summary>
		/// The values that define the shape of this FLV
		/// </summary>
		double m_dPeakPoint;
		double m_dRightOffset;
		double m_dLeftOffset;

		#endregion

		#region Properties

		#endregion

		#region Constructors

		/// <summary>
		/// 
		/// </summary>
		/// <param name="peak"></param>
		/// <param name="LeftOffset"></param>
		/// <param name="RightOffset"></param>
		public FuzzySet_LeftShoulder(double peak, double LeftOffset, double RightOffset) :
			base(((peak - LeftOffset) + peak) / 2)
		{
			m_dPeakPoint = peak;
			m_dLeftOffset = LeftOffset;
			m_dRightOffset = RightOffset;
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
			//test for the case where the left or right offsets are zero
			//(to prevent divide by zero errors below)
			if ((m_dRightOffset == 0.0 && m_dPeakPoint == val) ||
				 (m_dLeftOffset == 0.0 && m_dPeakPoint == val))
			{
				return 1.0;
			}
			//find DOM if right of center
			else if ((val >= m_dPeakPoint) && (val < (m_dPeakPoint + m_dRightOffset)))
			{
				double grad = 1.0 / -m_dRightOffset;

				return grad * (val - m_dPeakPoint) + 1.0;
			}
			//find DOM if left of center
			else if ((val < m_dPeakPoint) && (val >= m_dPeakPoint - m_dLeftOffset))
			{
				return 1.0;
			}
			else //out of range of this FLV, return zero
			{
				return 0.0;
			}
		}

		#endregion
	}
}
