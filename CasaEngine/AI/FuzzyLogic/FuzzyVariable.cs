using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace CasaEngine.AI.Fuzzy
{
	/// <summary>
	/// Class to define a fuzzy linguistic variable (FLV).
	/// An FLV comprises of a number of fuzzy sets  
	/// </summary>
	public class FuzzyVariable
	{
		#region Fields

		/// <summary>
		/// A map of the fuzzy sets that comprise this variable
		/// </summary>
		Dictionary<string, FuzzySet> m_MemberSets = new Dictionary<string,FuzzySet>();
		/// <summary>
		/// The minimum and maximum value of the range of this variable
		/// </summary>
		double m_dMinRange = 0.0;
		double m_dMaxRange = 0.0;

		#endregion

		#region Properties

		#endregion

		#region Constructors

		#endregion

		#region Methods

		/// <summary>
		/// takes a crisp value and calculates its degree of membership for each set
		/// in the variable.
		/// </summary>
		/// <param name="val"></param>
		public void Fuzzify(double val)
		{
			//make sure the value is within the bounds of this variable
			if ((val < m_dMinRange) && (val > m_dMaxRange))
			{
				throw new ArgumentException("FuzzyVariable.Fuzzify() : value out of bounds");
			}

			//for each set in the flv calculate the DOM for the given value
			foreach (KeyValuePair<string, FuzzySet> pair in m_MemberSets)
			{
				pair.Value.DOM = pair.Value.CalculateDOM(val);
			}
		}

		/// <summary>
		/// Defuzzifies the value by averaging the maxima of the sets that have fired
		/// </summary>
		/// <returns>OUTPUT = sum (maxima * DOM) / sum (DOMs) </returns>
		public double DeFuzzifyMaxAv()
		{
			double bottom = 0.0;
			double top = 0.0;

			foreach (KeyValuePair<string, FuzzySet> pair in m_MemberSets)
			{
				bottom += pair.Value.DOM;

				top += pair.Value.RepresentativeValue * pair.Value.DOM;
			}

			//make sure bottom is not equal to zero
			if (0 == bottom) return 0.0;

			return top / bottom;
		}

		/// <summary>
		/// Defuzzify the variable using the centroid method
		/// </summary>
		/// <param name="NumSamples"></param>
		/// <returns></returns>
		public double DeFuzzifyCentroid(int NumSamples)
		{
			//calculate the step size
			double StepSize = (m_dMaxRange - m_dMinRange) / (double)NumSamples;

			double TotalArea = 0.0;
			double SumOfMoments = 0.0;

			//step through the range of this variable in increments equal to StepSize
			//adding up the contribution (lower of CalculateDOM or the actual DOM of this
			//variable's fuzzified value) for each subset. This gives an approximation of
			//the total area of the fuzzy manifold.(This is similar to how the area under
			//a curve is calculated using calculus... the heights of lots of 'slices' are
			//summed to give the total area.)
			//
			//in addition the moment of each slice is calculated and summed. Dividing
			//the total area by the sum of the moments gives the centroid. (Just like
			//calculating the center of mass of an object)
			for (int samp = 1; samp <= NumSamples; ++samp)
			{
				//for each set get the contribution to the area. This is the lower of the 
				//value returned from CalculateDOM or the actual DOM of the fuzzified 
				//value itself   
				foreach (KeyValuePair<string, FuzzySet> pair in m_MemberSets)
				{
					double contribution =
						System.Math.Min(pair.Value.CalculateDOM(m_dMinRange + samp * StepSize), pair.Value.DOM);

					TotalArea += contribution;

					SumOfMoments += (m_dMinRange + samp * StepSize) * contribution;
				}
			}

			//make sure total area is not equal to zero
			if (0 == TotalArea) return 0.0;

			return (SumOfMoments / TotalArea);
		}

		/// <summary>
		/// Adds a triangular shaped fuzzy set to the variable
		/// </summary>
		/// <param name="?"></param>
		/// <returns></returns>
		public FzSet AddTriangularSet(string name, double minBound, double peak, double maxBound)
		{
			m_MemberSets[name] = new FuzzySet_Triangle(peak,
													   peak - minBound,
													   maxBound - peak);
			//adjust range if necessary
			AdjustRangeToFit(minBound, maxBound);

			return new FzSet(m_MemberSets[name]);
		}

		/// <summary>
		/// Adds a left shoulder type set
		/// </summary>
		/// <param name="?"></param>
		/// <returns></returns>
		public FzSet AddLeftShoulderSet(string name, double minBound, double peak, double maxBound)
		{
			m_MemberSets[name] = new FuzzySet_LeftShoulder(peak, peak - minBound, maxBound - peak);

			//adjust range if necessary
			AdjustRangeToFit(minBound, maxBound);

			return new FzSet(m_MemberSets[name]);
		}

		/// <summary>
		/// Adds a left shoulder type set
		/// </summary>
		/// <param name="?"></param>
		/// <returns></returns>
		public FzSet AddRightShoulderSet(string name, double minBound, double peak, double maxBound)
		{
			m_MemberSets[name] = new FuzzySet_RightShoulder(peak, peak - minBound, maxBound - peak);

			//adjust range if necessary
			AdjustRangeToFit(minBound, maxBound);

			return new FzSet(m_MemberSets[name]);
		}

		/// <summary>
		/// Adds a singleton to the variable
		/// </summary>
		/// <param name="?"></param>
		/// <returns></returns>
		public FzSet AddSingletonSet(string name, double minBound, double peak, double maxBound)
		{
			m_MemberSets[name] = new FuzzySet_Singleton(peak,
														peak - minBound,
														maxBound - peak);

			AdjustRangeToFit(minBound, maxBound);

			return new FzSet(m_MemberSets[name]);
		}

		/// <summary>
		///  this method is called with the upper and lower bound of a set each time a 
		///  new set is added to adjust the upper and lower range values accordingly
		/// </summary>
		/// <param name="minBound"></param>
		/// <param name="maxBound"></param>
		private void AdjustRangeToFit(double minBound, double maxBound)
		{
			if (minBound < m_dMinRange) m_dMinRange = minBound;
			if (maxBound > m_dMaxRange) m_dMaxRange = maxBound;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="binW_"></param>
		public void WriteDOMs(BinaryWriter binW_)
		{
			foreach (KeyValuePair<string, FuzzySet> pair in m_MemberSets)
			{
				binW_.Write("\n" + pair.Key + " is " + pair.Value.DOM);
			}

			binW_.Write("\nMin Range: " + m_dMinRange + "\nMax Range: " + m_dMaxRange);
		}

		#endregion
	}
}
