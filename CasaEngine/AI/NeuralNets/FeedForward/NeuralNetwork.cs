using System;
using System.Collections.Generic;
using System.Text;
using CasaEngine.Gameplay.Actor.Object;


namespace CasaEngine.AI.NeuralNets.FeedForward
{
	/// <summary>
	/// This class represents a neural network
	/// </summary>
	/// <remarks>
	/// Based on the implementation of Mat Buckland from his book "Programming Game AI By Example"
	/// </remarks>
	[Serializable]
	public class NeuralNetwork : BaseObject
	{
		#region Fields

		NeuralNetworkLayer m_InputLayer = null;
		NeuralNetworkLayer m_HiddenLayer = null;
		NeuralNetworkLayer m_OutputLayer = null;

		#endregion

		#region Properties

		/// <summary>
		/// Get Number Of Input Node
		/// </summary>
		public int NumberOfInputNode
		{
			get
			{
				return m_InputLayer.NumberOfNodes;
			}
		}

		/// <summary>
		/// Get Number Of Output Node
		/// </summary>
		public int NumberOfOutputNode
		{
			get
			{
				return m_OutputLayer.NumberOfNodes;
			}
		}

		#endregion

		#region Constructor

		#endregion

		#region Methods

		/// <summary>
		/// 
		/// </summary>
		/// <param name="elapsedTime"></param>
		public void Update(float elapsedTime)
		{
			FeedForward();
		}

		/// <summary>
		/// 
		/// </summary>
		protected override void Destroy()
		{
			base.Destroy();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return "Neural Network " + base.ToString();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="nNodesInput"></param>
		/// <param name="nNodesHidden"></param>
		/// <param name="nNodesOutput"></param>
		public void Initialize(int nNodesInput, int nNodesHidden, int nNodesOutput)
		{
			m_InputLayer.NumberOfNodes = nNodesInput;
			m_InputLayer.NumberOfChildNodes = nNodesHidden;
			m_InputLayer.NumberOfParentNodes = 0;
			m_InputLayer.Initialize(nNodesInput, null, m_HiddenLayer);
			m_InputLayer.RandomizeWeights();

			m_HiddenLayer.NumberOfNodes = nNodesHidden;
			m_HiddenLayer.NumberOfChildNodes = nNodesOutput;
			m_HiddenLayer.NumberOfParentNodes = nNodesInput;
			m_HiddenLayer.Initialize(nNodesHidden, m_InputLayer, m_OutputLayer);
			m_HiddenLayer.RandomizeWeights();

			m_OutputLayer.NumberOfNodes = nNodesOutput;
			m_OutputLayer.NumberOfChildNodes = 0;
			m_OutputLayer.NumberOfParentNodes = nNodesHidden;
			m_OutputLayer.Initialize(nNodesOutput, m_HiddenLayer, null);
		}

		/// <summary>
		/// 
		/// </summary>
		public void CleanUp()
		{
			m_InputLayer.CleanUp();
			m_HiddenLayer.CleanUp();
			m_OutputLayer.CleanUp();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="i"></param>
		/// <param name="value"></param>
		public void SetInput(int i, double value)
		{
			if ((i >= 0) && (i < m_InputLayer.NumberOfNodes))
			{
				m_InputLayer.NeuronValues[i] = value;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="i"></param>
		/// <returns></returns>
		public double GetOutput(int i)
		{
			if ((i >= 0) && (i < m_OutputLayer.NumberOfNodes))
			{
				return m_OutputLayer.NeuronValues[i];
			}

			return (double) Int32.MaxValue; // to indicate an error
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="i"></param>
		/// <param name="value"></param>
		public void SetDesiredOutput(int i, double value)
		{
			if ((i >= 0) && (i < m_OutputLayer.NumberOfNodes))
			{
				m_OutputLayer.DesiredValues[i] = value;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public void FeedForward()
		{
			m_InputLayer.CalculateNeuronValues();
			m_HiddenLayer.CalculateNeuronValues();
			m_OutputLayer.CalculateNeuronValues();
		}

		/// <summary>
		/// 
		/// </summary>
		public void BackPropagate()
		{
			m_OutputLayer.CalculateErrors();
			m_HiddenLayer.CalculateErrors();

			m_HiddenLayer.AdjustWeights();
			m_InputLayer.AdjustWeights();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public int GetMaxOutputID()
		{
			int i, id;
			double maxval;

			maxval = m_OutputLayer.NeuronValues[0];
			id = 0;

			for (i = 1; i < m_OutputLayer.NumberOfNodes; i++)
			{
				if (m_OutputLayer.NeuronValues[i] > maxval)
				{
					maxval = m_OutputLayer.NeuronValues[i];
					id = i;
				}
			}

			return id;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public double CalculateError()
		{
			int i;
			double error = 0;

			for (i = 0; i < m_OutputLayer.NumberOfNodes; i++)
			{
				error += System.Math.Pow(m_OutputLayer.NeuronValues[i] - m_OutputLayer.DesiredValues[i], 2);
			}

			error = error / m_OutputLayer.NumberOfNodes;

			return error;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="rate"></param>
		public void SetLearningRate(double rate)
		{
			m_InputLayer.LearningRate = rate;
			m_HiddenLayer.LearningRate = rate;
			m_OutputLayer.LearningRate = rate;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="useLinear"></param>
		public void SetLinearOutput(bool useLinear)
		{
			m_InputLayer.LinearOutput = useLinear;
			m_HiddenLayer.LinearOutput = useLinear;
			m_OutputLayer.LinearOutput = useLinear;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="useMomentum"></param>
		/// <param name="factor"></param>
		public void SetMomentum(bool useMomentum, double factor)
		{
			m_InputLayer.UseMomentum = useMomentum;
			m_HiddenLayer.UseMomentum = useMomentum;
			m_OutputLayer.UseMomentum = useMomentum;

			m_InputLayer.MomentumFactor = factor;
			m_HiddenLayer.MomentumFactor = factor;
			m_OutputLayer.MomentumFactor = factor;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="filename"></param>
		public void DumpData(string filename)
		{

		}

#if EDITOR

        public override bool CompareTo(BaseObject other_)
        {
            throw new Exception("The method or operation is not implemented.");
        }

#endif

        public override BaseObject Clone()
        {
            throw new Exception("The method or operation is not implemented.");
        }
		#endregion
	}
}
