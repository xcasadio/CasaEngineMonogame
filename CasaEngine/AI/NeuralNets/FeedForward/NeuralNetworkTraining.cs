using System;
using System.Collections.Generic;
using System.Text;

namespace CasaEngine.AI.NeuralNets.FeedForward
{
	/// <summary>
	/// 
	/// </summary>
	public class NeuralNetworkTraining
	{
		#region Fields

		NeuralNetwork m_NeuralNetwork = null;
		int m_NumberOfData = 0;

		double[,] TrainingSet = null;

		#endregion

		#region Properties

		#endregion

		#region Constructor

		/// <summary>
		/// 
		/// </summary>
		/// <param name="neuralNet_"></param>
		public NeuralNetworkTraining(NeuralNetwork neuralNet_)
		{
			m_NeuralNetwork = neuralNet_;
		}

		#endregion

		#region Methods

		/// <summary>
		/// 
		/// </summary>
		/// <param name="numberOfSet"></param>
		public void InitTrainingData(int numberOfSet)
		{
			m_NumberOfData = numberOfSet;
			TrainingSet = new double[m_NumberOfData, m_NeuralNetwork.NumberOfInputNode + m_NeuralNetwork.NumberOfOutputNode];
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="numberOfSet"></param>
		/// <param name="data"></param>
		public void InitTrainingData(int numberOfSet, double[,] data)
		{
			InitTrainingData(numberOfSet);
			TrainingSet = data;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="numberOfSet"></param>
		/// <param name="fileName"></param>
		public void InitTrainingData(int numberOfSet, string fileName)
		{
			double[,] data = null;

			//fileName

			InitTrainingData(numberOfSet, data);
		}

		/// <summary>
		/// 
		/// </summary>
		public void Training()
		{
			int i;
			double error = 1.0;
			int c = 0;

			while ((error > 0.05) && (c < 50000))
			{
				error = 0;
				c++;
				for (i = 0; i < m_NumberOfData; i++)
				{
					for (int j = 0; j < m_NeuralNetwork.NumberOfInputNode; j++)
					{
						m_NeuralNetwork.SetInput(j, TrainingSet[i, j]);
					}

					for (int j = m_NeuralNetwork.NumberOfInputNode; j < m_NeuralNetwork.NumberOfOutputNode; j++)
					{
						m_NeuralNetwork.SetDesiredOutput(j, TrainingSet[i, j]);
					}

					m_NeuralNetwork.FeedForward();
					error += m_NeuralNetwork.CalculateError();
					m_NeuralNetwork.BackPropagate();
				}
				error = error / 14.0f;
			}
		}

		#endregion
	}
}
