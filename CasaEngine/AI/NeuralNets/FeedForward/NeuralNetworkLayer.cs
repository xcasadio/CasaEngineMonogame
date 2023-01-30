namespace CasaEngine.AI.NeuralNets.FeedForward
{
    public class NeuralNetworkLayer
    {

        int m_NumberOfNodes;
        int m_NumberOfChildNodes;
        int m_NumberOfParentNodes;
        double[,] m_Weights;
        double[,] m_WeightChanges;
        double[] m_NeuronValues;
        double[] m_DesiredValues;
        double[] m_Errors;
        double[] m_BiasWeights;
        double[] m_BiasValues;
        double m_LearningRate;

        bool m_LinearOutput = false;
        bool m_UseMomentum = false;
        double m_MomentumFactor = 0.9;

        NeuralNetworkLayer m_ParentLayer;
        NeuralNetworkLayer m_ChildLayer;



        public int NumberOfNodes
        {
            get => m_NumberOfNodes;
            set => m_NumberOfNodes = value;
        }

        public int NumberOfChildNodes
        {
            get => m_NumberOfChildNodes;
            set => m_NumberOfChildNodes = value;
        }

        public int NumberOfParentNodes
        {
            get => m_NumberOfParentNodes;
            set => m_NumberOfParentNodes = value;
        }

        public double[,] Weights => m_Weights;

        public double[,] WeightChanges => m_WeightChanges;

        public double[] NeuronValues => m_NeuronValues;

        public double[] DesiredValues => m_DesiredValues;

        public double[] Errors => m_Errors;

        public double[] BiasWeights => m_BiasWeights;

        public double[] BiasValues => m_BiasValues;

        public double LearningRate
        {
            get => m_LearningRate;
            set => m_LearningRate = value;
        }

        public bool LinearOutput
        {
            get => m_LinearOutput;
            set => m_LinearOutput = value;
        }

        public bool UseMomentum
        {
            get => m_UseMomentum;
            set => m_UseMomentum = value;
        }

        public double MomentumFactor
        {
            get => m_MomentumFactor;
            set => m_MomentumFactor = value;
        }

        public NeuralNetworkLayer ParentLayer => m_ParentLayer;

        public NeuralNetworkLayer ChildLayer => m_ChildLayer;


        public void Initialize(int NumNodes, NeuralNetworkLayer parent, NeuralNetworkLayer child)
        {
            int i, j;

            m_NeuronValues = new double[m_NumberOfNodes];
            m_DesiredValues = new double[m_NumberOfNodes];
            m_Errors = new double[m_NumberOfNodes];

            if (parent != null)
            {
                m_ParentLayer = parent;
            }

            if (child != null)
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
            for (i = 0; i < m_NumberOfNodes; i++)
            {
                m_NeuronValues[i] = 0;
                m_DesiredValues[i] = 0;
                m_Errors[i] = 0;

                if (m_ChildLayer != null)
                    for (j = 0; j < m_NumberOfChildNodes; j++)
                    {
                        m_Weights[i, j] = 0;
                        m_WeightChanges[i, j] = 0;
                    }
            }

            if (m_ChildLayer != null)
                for (j = 0; j < m_NumberOfChildNodes; j++)
                {
                    m_BiasValues[j] = -1;
                    m_BiasWeights[j] = 0;
                }

        }

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

                    m_Weights[i, j] = number / 100.0f - 1;
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
                        sum += m_ChildLayer.Errors[j] * m_Weights[i, j];
                    }
                    m_Errors[i] = sum * m_NeuronValues[i] * (1.0f - m_NeuronValues[i]);
                }
            }
        }

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
                        m_Weights[i, j] += dw + m_MomentumFactor * m_WeightChanges[i, j];
                        m_WeightChanges[i, j] = dw;
                    }
                }

                for (j = 0; j < m_NumberOfChildNodes; j++)
                {
                    m_BiasWeights[j] += m_LearningRate * m_ChildLayer.Errors[j] * m_BiasValues[j];
                }
            }
        }

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
                        x += m_ParentLayer.NeuronValues[i] * m_ParentLayer.Weights[i, j];
                    }

                    x += m_ParentLayer.BiasValues[j] * m_ParentLayer.BiasWeights[j];

                    if ((m_ChildLayer == null) && m_LinearOutput)
                        m_NeuronValues[j] = x;
                    else
                        m_NeuronValues[j] = 1.0f / (1 + System.Math.Exp(-x));
                }
            }
        }

    }
}
