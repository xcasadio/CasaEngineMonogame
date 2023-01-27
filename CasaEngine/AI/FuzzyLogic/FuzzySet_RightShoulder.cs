using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.AI.Fuzzy
{
	/// <summary>
	/// 
	/// </summary>
	public class FuzzySet_RightShoulder
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
		/// <param name="peak"></param>
		/// <param name="LeftOffset"></param>
		/// <param name="RightOffset"></param>
		public FuzzySet_RightShoulder(double peak, double LeftOffset, double RightOffset)
			: base(((peak + RightOffset) + peak) / 2)
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

			//find DOM if left of center
			else if ((val <= m_dPeakPoint) && (val > (m_dPeakPoint - m_dLeftOffset)))
			{
				double grad = 1.0 / m_dLeftOffset;

				return grad * (val - (m_dPeakPoint - m_dLeftOffset));
			}
			//find DOM if right of center and less than center + right offset
			else if ((val > m_dPeakPoint) && (val <= m_dPeakPoint + m_dRightOffset))
			{
				return 1.0;
			}

			else
			{
				return 0;
			}
		}

		#endregion
	}
}
