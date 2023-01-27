using System;
using System.Collections.Generic;
using System.Text;

namespace CasaEngine.AI.NeuralNets.FeedForward
{
	/// <summary>
	/// 
	/// </summary>
	public class NeuralNetworkLayer
	{
		#region Fields

		int			m_NumberOfNodes;
		int			m_NumberOfChildNodes;
		int			m_NumberOfParentNodes;
		double[,]	m_Weights;
		double[,]	m_WeightChanges;
		double[]	m_NeuronValues;
		double[]	m_DesiredValues;
		double[]	m_Errors;
		double[]	m_BiasWeights;
		double[]	m_BiasValues;
		double		m_LearningRate;

		bool		m_LinearOutput = false;
		bool		m_UseMomentum = false;
		double		m_MomentumFactor = 0.9;

		NeuralNetworkLayer		m_ParentLayer;
		NeuralNetworkLayer		m_ChildLayer;

		#endregion

		#region Properties

		/// <summary>
		/// Get number of node
		/// </summary>
		public int NumberOfNodes
		{
			get
			{
				return m_NumberOfNodes;
			}
			set
			{
				m_NumberOfNodes = value;
			}
		}

		/// <summary>
		/// Get number of child node
		/// </summary>
		public int NumberOfChildNodes
		{
			get
			{
				return m_NumberOfChildNodes;
			}
			set
			{
				m_NumberOfChildNodes = value;
			}
		}

		/// <summary>
		/// Get/Set number of parent node
		/// </summary>
		public int NumberOfParentNodes
		{
			get
			{
				return m_NumberOfParentNodes;
			}
			set
			{
				m_NumberOfParentNodes = value;
			}
		}

		/// <summary>
		/// Get Weights
		/// </summary>
		public double[,] Weights
		{
			get
			{
				return m_Weights;
			}
		}

		/// <summary>
		/// Get Weight Changes
		/// </summary>
		public double[,] WeightChanges
		{
			get
			{
				return m_WeightChanges;
			}
		}

		/// <summary>
		/// Get Neuron Values
		/// </summary>
		public double[] NeuronValues
		{
			get
			{
				return m_NeuronValues;
			}
		}

		/// <summary>
		/// Get Desired Values
		/// </summary>
		public double[] DesiredValues
		{
			get
			{
				return m_DesiredValues;
			}
		}

		/// <summary>
		/// Get Errors
		/// </summary>
		public double[] Errors
		{
			get
			{
				return m_Errors;
			}
		}

		/// <summary>
		/// Get Bias Weights
		/// </summary>
		public double[] BiasWeights
		{
			get
			{
				return m_BiasWeights;
			}
		}

		/// <summary>
		/// Get Bias Values
		/// </summary>
		public double[] BiasValues
		{
			get
			{
				return m_BiasValues;
			}
		}

		/// <summary>
		/// Get/Set Learning Rate
		/// </summary>
		public double LearningRate
		{
			get
			{
				return m_LearningRate;
			}
			set
			{
				m_LearningRate = value;
			}
		}

		/// <summary>
		/// Get/Set Linear Output
		/// </summary>
		public bool LinearOutput
		{
			get
			{
				return m_LinearOutput;
			}
			set
			{
				m_LinearOutput = value;
			}
		}

		/// <summary>
		/// Get/Set Use Momentum
		/// </summary>
		public bool UseMomentum
		{
			get
			{
				return m_UseMomentum;
			}
			set
			{
				m_UseMomentum = value;
			}
		}

		/// <summary>
		/// Get/Set Momentum Factor
		/// </summary>
		public double MomentumFactor
		{
			get
			{
				return m_MomentumFactor;
			}
			set
			{
				m_MomentumFactor = value;
			}
		}

		/// <summary>
		/// Get Parent Layer
		/// </summary>
		public NeuralNetworkLayer ParentLayer
		{
			get
			{
				return m_ParentLayer;
			}
		}

		/// <summary>
		/// Get Child Layer
		/// </summary>
		public NeuralNetworkLayer ChildLayer
		{
			get
			{
				return m_ChildLayer;
			}
		}

		#endregion

		#region Constructor

		#endregion

		#region Methods

		/// <summary>
		/// 
		/// </summary>
		/// <param name="NumNodes"></param>
		/// <param name="parent"></param>
		/// <param name="child"></param>
		public void Initialize(int NumNodes, NeuralNetworkLayer parent, NeuralNetworkLayer child)
		{
			int	i, j;

			m_NeuronValues = new double[m_NumberOfNodes];
			m_DesiredValues = new double[m_NumberOfNodes];
			m_Errors = new double[m_NumberOfNodes];

			if(parent != null)
			{		
				m_ParentLayer = parent;
			}

			if(child != null)
			{
				m_ChildLayer = child;
			
				m_Weights = new double[m_NumberOfNodes, m_NumberOfChildNodes];
				m_WeightChanges = new double[m_NumberOfNodes, m_NumberOfChildNodes];

				m_BiasValues = new double[m_NumberOfChildNodes];
				m_BiasWeights = new double[m_NumberOfChildNodes];
			} 
			else 
			{
				m_Weights = null;
				m_BiasValues = null;
				m_BiasWeights = null;
			}

			// Make sure everything contains zeros
			for (i=0; i<m_NumberOfNodes; i++)
			{
				m_NeuronValues[i] = 0;
				m_DesiredValues[i] = 0;
				m_Errors[i] = 0;
				
				if(m_ChildLayer != null)
					for(j=0; j<m_NumberOfChildNodes; j++)
					{
						m_Weights[i,j] = 0;
						m_WeightChanges[i,j] = 0;
					}
			}

			if (m_ChildLayer != null)
				for (j=0; j<m_NumberOfChildNodes; j++)
				{
					m_BiasValues[j] = -1;
					m_BiasWeights[j] = 0;
				}

		}

		/// <summary>
		/// 
		/// </summary>
		public void CleanUp()
		{
			m_NeuronValues = null;
			m_DesiredValues = null;
			m_Errors = null;

			m_Weights = null;
			m_WeightChanges = null;
			
			m_BiasValues = null;
			m_BiasWeights = null;
		}

		/// <summary>
		/// 
		/// </summary>
		public void RandomizeWeights()
		{
			int i, j;
			int min = 0;
			int max = 200;
			int number;

			Random rand = new Random();

			for (i = 0; i < m_NumberOfNodes; i++)
			{
				for (j = 0; j < m_NumberOfChildNodes; j++)
				{
					number = rand.Next(min, max);

					if (number > max)
						number = max;

					if (number < min)
						number = min;

					m_Weights[i,j] = number / 100.0f - 1;
				}
			}

			for (j = 0; j < m_NumberOfChildNodes; j++)
			{
				number = rand.Next(min, max);

				if (number > max)
					number = max;

				if (number < min)
					number = min;

				m_BiasWeights[j] = number / 100.0f - 1;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public void CalculateErrors()
		{
			int i, j;
			double sum;

			if (m_ChildLayer == null) // output layer
			{
				for (i = 0; i < m_NumberOfNodes; i++)
				{
					m_Errors[i] = (m_DesiredValues[i] - m_NeuronValues[i]) * m_NeuronValues[i] * (1.0f - m_NeuronValues[i]);
				}
			}
			else if (m_ParentLayer == null)
			{ // input layer
				for (i = 0; i < m_NumberOfNodes; i++)
				{
					m_Errors[i] = 0.0f;
				}
			}
			else
			{ // hidden layer
				for (i = 0; i < m_NumberOfNodes; i++)
				{
					sum = 0;

					for (j = 0; j < m_NumberOfChildNodes; j++)
					{
						sum += m_ChildLayer.Errors[j] * m_Weights[i,j];
					}
					m_Errors[i] = sum * m_NeuronValues[i] * (1.0f - m_NeuronValues[i]);
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public void AdjustWeights()
		{
			int i, j;
			double dw;

			if (m_ChildLayer != null)
			{
				for (i = 0; i < m_NumberOfNodes; i++)
				{
					for (j = 0; j < m_NumberOfChildNodes; j++)
					{
						dw = m_LearningRate * m_ChildLayer.Errors[j] * m_NeuronValues[i];
						m_Weights[i,j] += dw + m_MomentumFactor * m_WeightChanges[i,j];
						m_WeightChanges[i,j] = dw;
					}
				}

				for (j = 0; j < m_NumberOfChildNodes; j++)
				{
					m_BiasWeights[j] += m_LearningRate * m_ChildLayer.Errors[j] * m_BiasValues[j];
				}
			}
		}
	
		/// <summary>
		/// 
		/// </summary>
		public void CalculateNeuronValues()
		{
			int i, j;
			double x;

			if (m_ParentLayer != null)
			{
				for (j = 0; j < m_NumberOfNodes; j++)
				{
					x = 0.0;

					for (i = 0; i < m_NumberOfParentNodes; i++)
					{
						x += m_ParentLayer.NeuronValues[i] * m_ParentLayer.Weights[i,j];
					}

					x += m_ParentLayer.BiasValues[j] * m_ParentLayer.BiasWeights[j];

					if ((m_ChildLayer == null) && m_LinearOutput)
						m_NeuronValues[j] = x;
					else
						m_NeuronValues[j] = 1.0f / (1 + System.Math.Exp(-x));
				}
			}
		}

		#endregion
	}
}
