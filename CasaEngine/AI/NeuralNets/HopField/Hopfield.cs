using System;
using System.Collections.Generic;
using System.Text;

namespace HopField
{
	/// <summary>
	/// Reconnaissance de pattern
	/// </summary>
	class Hopfield
	{
		#region Fields

		/// <summary>
		/// number of units in this net
		/// </summary>
		int m_NumUnits;

		/// <summary>
		/// 
		/// </summary>
		List<int[]> m_Pattern = new List<int[]>();

		/// <summary>
		/// output of ith unit 
		/// </summary>
		int[] m_Output;
	
		/// <summary>
		/// threshold of ith unit
		/// </summary>
		int[] m_Threshold;

		/// <summary>
		/// connection weights to ith unit
		/// </summary>
		int[,] m_Weights;

		#endregion

		#region Properties

		/// <summary>
		/// Gets Output
		/// </summary>
		public int[] Output
		{
			get { return m_Output; }
			private set { m_Output = value; }
		}

		#endregion

		#region Constructor

		#endregion

		#region Methods

		#region Initialization

		/// <summary>
		/// 
		/// </summary>
		/// <param name="num_"></param>
		public void GenerateNetwork(int num_)
		{
			int i;

			m_NumUnits = num_;
			m_Output = new int[num_];
			m_Threshold = new int[num_];
			m_Weights = new int[num_, num_];

			for (i = 0; i < num_; i++)
			{
				m_Threshold[i] = 0;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="pattern_"></param>
		public void AddPattern(int[] pattern_)
		{
			if (pattern_.Length != m_NumUnits)
			{
				throw new ArgumentException("HopField.SetInput() : input_.Length != m_NumUnits");
			}

			m_Pattern.Add(pattern_);
		}

		/// <summary>
		/// 
		/// </summary>
		public void CalculateWeights()
		{
			int i, j, n;
			int Weight;

			for (i = 0; i < m_NumUnits; i++)
			{
				for (j = 0; j < m_NumUnits; j++)
				{
					Weight = 0;

					if (i != j)
					{
						for (n = 0; n < m_Pattern.Count; n++)
						{
							Weight += m_Pattern[n][i] * m_Pattern[n][j];
						}
					}

					this.m_Weights[i,j] = Weight;
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="input_"></param>
		public void SetInput(ref int[] input_)
		{
			if (input_.Length != m_NumUnits)
			{
				throw new ArgumentException("HopField.SetInput() : input_.Length != m_NumUnits");
			}

			m_Output = input_;
		}

		#endregion

		#region Propagating signals

		/// <summary>
		/// 
		/// </summary>
		/// <param name="i"></param>
		/// <returns></returns>
		private bool PropagateUnit(int i)
		{
			int j;
			int Sum, Out = 0;
			bool Changed;

			Changed = false;
			Sum = 0;

			for (j = 0; j < m_NumUnits; j++)
			{
				Sum += m_Weights[i,j] * m_Output[j];
			}

			if (Sum != m_Threshold[i])
			{
				if (Sum < m_Threshold[i]) Out = -1;
				if (Sum >= m_Threshold[i]) Out = 1;
				if (Out != m_Output[i])
				{
					Changed = true;
					m_Output[i] = Out;
				}
			}

			return Changed;
		}

		/// <summary>
		/// 
		/// </summary>
		public void PropagateNet()
		{
			int Iteration, IterationOfLastChange;

			Iteration = 0;
			IterationOfLastChange = 0;

			Random rand = new Random();

			do
			{
				Iteration++;
				if (PropagateUnit(rand.Next(0,m_NumUnits - 1 )))
				{
					IterationOfLastChange = Iteration;
				}
			}
			while (Iteration - IterationOfLastChange < 10 * m_NumUnits);
		}

		#endregion

		#endregion
	}
}
