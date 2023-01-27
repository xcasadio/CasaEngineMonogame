using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace CasaEngine.AI.Probability
{
	/// <summary>
	/// 
	/// </summary>
	class Pattern
		: IEquatable<Pattern>
	{
		#region Fields

		int[] m_Pattern;
		
		#endregion

		#region Fields

		#endregion

		#region Methods

		/// <summary>
		/// 
		/// </summary>
		/// <param name="pattern_"></param>
		public Pattern(int[] pattern_)
		{
			m_Pattern = pattern_;
		}

		#region IEquatable<Pattern> Members

		/// <summary>
		/// Checks whether two pattern are equal disregarding the order of the elements
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public bool Equals(Pattern other)
		{
			for (int i = 0; i < m_Pattern.Length; i++)
			{
				if (other.m_Pattern[i] != m_Pattern[i])
				{
					return false;
				}
			}

			return true;
		}

		#endregion

		#endregion
	}

	/// <summary>
	/// 
	/// </summary>
	public class Bayesian
	{
		#region Fields

		int nbActions, nbPossibilities;
		Dictionary<Pattern, int[]> m_Probabilities = new Dictionary<Pattern, int[]>();
		List<Pattern> m_ListPattern = new List<Pattern>();

		#endregion

		#region Properties

		#endregion

		#region Constructors

		/// <summary>
		/// 
		/// </summary>
		/// <remarks>Bug si nbPossibilities_ > 2</remarks>
		/// <param name="nbAction_"></param>
		/// <param name="data_"></param>
		public Bayesian(int nbAction_, int nbPossibilities_)
		{
			nbActions = nbAction_;
			nbPossibilities = nbPossibilities_;

			BuildProbabilities();
		}

		#endregion

		#region Methods

		/// <summary>
		/// 
		/// </summary>
		private void BuildProbabilities()
		{
			List<int[]> list = new List<int[]>();
			int[] pattern = new int[nbPossibilities];
			int[] proba = new int[nbActions+1];

			for (int i = 0; i < nbActions+1; i++)
			{
				proba[i] = 0;
			}

			for (int i = 0; i < nbPossibilities; i++)
			{
				pattern[i] = 0;
			}

			CreatePattern(ref list, pattern, 1);

			foreach (int[] a in list)
			{
				Pattern p = new Pattern(a);
				m_ListPattern.Add(p);
				m_Probabilities.Add(p, proba.ToArray());
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="listPattern"></param>
		/// <param name="pattern_"></param>
		private void CreatePattern(ref List<int[]> listPattern, int[] pattern_, int pos_)
		{
			int p;

			listPattern.Add(pattern_.ToArray());

			for (p = 0; p < pos_; p++)
			{
				//TODO : quand p > 0 remettre a zero
				if (pattern_[p] < nbActions - 1)
				{
					pattern_[p]++;
					break;
				}
			}

			//change
			if ( p >= pos_ )
			{
				pattern_[pos_]++;

				for (int i = 0; i < pos_; i++)
				{
					pattern_[i] = 0;
				}

				if (pattern_[pos_] > nbActions - 1)
				{
					pattern_[pos_] = 0;
					pos_++;

					if (pos_ >= nbPossibilities)
					{
						return;
					}

					pattern_[pos_]++;
				}
			}

			CreatePattern(ref listPattern, pattern_, pos_);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="currentPattern_"></param>
		/// <returns></returns>
		public int ComputeProbabilities(int[] currentPattern_)
		{
			Pattern pattern = GetPattern(currentPattern_);

			int[] proba = m_Probabilities[pattern];
			int action = 0;
			double max = float.MinValue, x;

			for (int i=0; i<nbActions; i++)
			{
				x = (double)proba[i] / (double)proba[nbActions];
				if (max < x)
				{
					max = x;
					action = i;
				}
			}

			return action;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="currentPattern_"></param>
		/// <param name="action_"></param>
		/// <returns></returns>
		public void UpdateProbabilities(int[] currentPattern_, int action_)
		{
			Pattern pattern = GetPattern(currentPattern_);

			m_Probabilities[pattern][nbActions]++;
			m_Probabilities[pattern][action_]++;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="currentPattern_"></param>
		/// <returns></returns>
		Pattern GetPattern(int[] currentPattern_)
		{
			Pattern pattern = new Pattern(currentPattern_);

			foreach (Pattern p in m_ListPattern)
			{
				if (p.Equals(pattern))
				{
					return p;
				}
			}

			throw new ArgumentException("Bayesian.GetPattern() : Can't find Pattern");
		}

		#endregion
	}
}
