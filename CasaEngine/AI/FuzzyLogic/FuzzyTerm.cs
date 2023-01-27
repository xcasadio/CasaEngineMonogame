using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.AI.Fuzzy
{
	/// <summary>
	/// Abstract class to provide an interface for classes able to be
	/// used as terms in a fuzzy if-then rule base.
	/// </summary>
	public interface FuzzyTerm
	{
		FuzzyTerm Clone();

		//retrieves the degree of membership of the term
		double DOM { get; }

		//clears the degree of membership of the term
		void ClearDOM();

		//method for updating the DOM of a consequent when a rule fires
		void ORwithDOM(double val);
	}
}
