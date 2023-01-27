using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.AI.Fuzzy
{
	/// <summary>
	///  This class implements a fuzzy rule of the form: 
	///  IF fzVar1 AND fzVar2 AND ... fzVarn THEN fzVar.c
	/// </summary>
	public class FuzzyRule
	{
		#region Fields

		/// <summary>
		/// Antecedent (usually a composite of several fuzzy sets and operators)
		/// </summary>
		FuzzyTerm m_pAntecedent;

		/// <summary>
		/// Consequence (usually a single fuzzy set, but can be several ANDed together)
		/// </summary>
		FuzzyTerm m_pConsequence;

		//it doesn't make sense to allow clients to copy rules
		//FuzzyRule(FuzzyRule);
		//FuzzyRule& operator=(const FuzzyRule&);

		#endregion

		#region Methods

		/// <summary>
		/// 
		/// </summary>
		/// <param name="ant"></param>
		/// <param name="con"></param>
		public FuzzyRule(FuzzyTerm ant, FuzzyTerm con)
		{
			m_pAntecedent = ant.Clone();
			m_pConsequence = con.Clone();
		}

		/// <summary>
		/// 
		/// </summary>
		public void SetConfidenceOfConsequentToZero()
		{
			m_pConsequence.ClearDOM();
		}
		
		/// <summary>
		/// This method updates the DOM (the confidence) of the consequent term with
		/// the DOM of the antecedent term. 
		/// </summary>
		public void Calculate()
		{
			m_pConsequence.ORwithDOM(m_pAntecedent.DOM);
		}

		#endregion
	}
}
