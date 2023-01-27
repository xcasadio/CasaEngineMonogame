using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace CasaEngine.AI.Fuzzy
{
	/// <summary>
	/// You must pass one of these values to the defuzzify method. This module
	/// only supports the MaxAv and centroid methods.
	/// </summary>
	public enum DefuzzifyMethod
	{
		MAX_AV,
		CENTROID
	};

	/// <summary>
	/// This class describes a fuzzy module: a collection of fuzzy variables
	/// and the rules that operate on them.
	/// </summary>
	public class FuzzyModule
	{	
		/// <summary>
		/// When calculating the centroid of the fuzzy manifold this value is used
		/// to determine how many cross-sections should be sampled
		/// </summary>	
		static public readonly int NUM_SAMPLES = 15;

		#region Fields

		/// <summary>
		/// A map of all the fuzzy variables this module uses
		/// </summary>
		private Dictionary<string, FuzzyVariable> m_Variables = new Dictionary<string, FuzzyVariable>();

		/// <summary>
		/// A list containing all the fuzzy rules
		/// </summary>
		private List<FuzzyRule> m_Rules = new List<FuzzyRule>();

		#endregion

		#region Properties

		#endregion

		#region Constructors

		/// <summary>
		/// This method calls the Fuzzify method of the variable with the same name
		/// as the key
		/// </summary>
		/// <param name="NameOfFLV"></param>
		/// <param name="val"></param>
		public void Fuzzify(string NameOfFLV, double val)
		{
			if (m_Variables.ContainsKey(NameOfFLV) == false)
			{
				throw new KeyNotFoundException("FuzzyModule.Fuzzify() : " + "key " + NameOfFLV + " not found");
			}

			m_Variables[NameOfFLV].Fuzzify(val);
		}

		#endregion

		#region Methods

		/// <summary>
		/// Given a fuzzy variable and a deffuzification method this returns a 
		/// crisp value
		/// </summary>
		/// <param name="NameOfFLV"></param>
		/// <param name="method"></param>
		/// <returns></returns>
		public double DeFuzzify(string NameOfFLV, DefuzzifyMethod method)
		{
			if (m_Variables.ContainsKey(NameOfFLV) == false)
			{
				throw new KeyNotFoundException("FuzzyModule.DeFuzzify() : " + "key " + NameOfFLV + " not found");
			}

			//clear the DOMs of all the consequents of all the rules
			SetConfidencesOfConsequentsToZero();

			//process the rules
			foreach (FuzzyRule rule in m_Rules)
			{
				rule.Calculate();
			}

			//now defuzzify the resultant conclusion using the specified method
			switch (method)
			{
				case DefuzzifyMethod.CENTROID:

					return m_Variables[NameOfFLV].DeFuzzifyCentroid(NUM_SAMPLES);

					//break;

				case DefuzzifyMethod.MAX_AV:

					return m_Variables[NameOfFLV].DeFuzzifyMaxAv();

					//break;
			}

			return 0;
		}

		/// <summary>
		/// Zeros the DOMs of the consequents of each rule
		/// </summary>
		private void SetConfidencesOfConsequentsToZero()
		{
			foreach (FuzzyRule rule in m_Rules)
			{
				rule.SetConfidenceOfConsequentToZero();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="antecedent"></param>
		/// <param name="consequence"></param>
		public void AddRule(FuzzyTerm antecedent, FuzzyTerm consequence)
		{
			m_Rules.Add(new FuzzyRule(antecedent, consequence));
		}

		/// <summary>
		/// Creates a new fuzzy variable and returns a reference to it
		/// </summary>
		/// <param name="VarName"></param>
		/// <returns></returns>
		public FuzzyVariable CreateFLV(string VarName)
		{
			m_Variables[VarName] = new FuzzyVariable();

			return m_Variables[VarName];
		}

		/// <summary>
		/// WriteAllDOMs
		/// </summary>
		/// <param name="binW_"></param>
		public void WriteAllDOMs(BinaryWriter binW_)
		{
			binW_.Write("\n\n");

			foreach (KeyValuePair<string, FuzzyVariable> pair in m_Variables)
			{
				binW_.Write("\n--------------------------- " + pair.Key);
				pair.Value.WriteDOMs(binW_);

				binW_.Write("\n");
			}
		}

		#endregion
	}
}
