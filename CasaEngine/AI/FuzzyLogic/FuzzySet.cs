using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.AI.Fuzzy
{
	/// <summary>
	/// Class to define an interface for a fuzzy set
	/// </summary>
	public abstract class FuzzySet
	{
		#region Fields

		/// <summary>
		/// This will hold the degree of membership of a given value in this set 
		/// </summary>
		protected double m_dDOM = 0.0;

		/// <summary>
		///this is the maximum of the set's membership function. For instamce, if
		///the set is triangular then this will be the peak point of the triangular.
		///if the set has a plateau then this value will be the mid point of the 
		///plateau. This value is set in the constructor to avoid run-time
		///calculation of mid-point values. 
		/// </summary>
		protected double m_dRepresentativeValue;
		
		#endregion

		#region Constructors

		/// <summary>
		/// Gets/Sets DOM
		/// </summary>
		public double DOM
		{
			get { return m_dDOM; }
			set
			{
				if ((value > 1) && (value < 0))
				{
					throw new ArgumentException("FuzzySet.DOM;set : value need to be between 0 and 1");
				}

				m_dDOM = value;
			}
		}

		/// <summary>
		/// Gets RepresentativeValue
		/// </summary>
		public double RepresentativeValue
		{
			get { return m_dRepresentativeValue; }
		}

		#endregion

		#region Constructors

		/// <summary>
		/// 
		/// </summary>
		/// <param name="RepVal"></param>
		public FuzzySet(double RepVal)
		{
			m_dRepresentativeValue = RepVal;
		}

		#endregion

		#region Methods
		
		/// <summary>
		/// Return the degree of membership in this set of the given value. NOTE,
		/// this does not set m_dDOM to the DOM of the value passed as the parameter.
		/// This is because the centroid defuzzification method also uses this method
		/// to determine the DOMs of the values it uses as its sample points. 
		/// </summary>
		/// <param name="val"></param>
		/// <returns></returns>
		public abstract double CalculateDOM(double val);

		/// <summary>
		/// If this fuzzy set is part of a consequent FLV, and it is fired by a rule 
		/// then this method sets the DOM (in this context, the DOM represents a
		/// confidence level)to the maximum of the parameter value or the set's 
		/// existing m_dDOM value
		/// </summary>
		/// <param name="val"></param>
		public void ORwithDOM(double val)
		{
			if (val > m_dDOM)
				m_dDOM = val;
		}

		/// <summary>
		/// 
		/// </summary>
		public void ClearDOM()
		{
			m_dDOM = 0.0;
		}

		#endregion
	}
}
