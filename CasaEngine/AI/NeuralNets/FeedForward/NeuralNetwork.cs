using CasaEngine.Gameplay.Actor.Object;


namespace CasaEngine.AI.NeuralNets.FeedForward
{
    [Serializable]
    public class NeuralNetwork : BaseObject
    {

        NeuralNetworkLayer m_InputLayer = null;
        NeuralNetworkLayer m_HiddenLayer = null;
        NeuralNetworkLayer m_OutputLayer = null;



        public int NumberOfInputNode => m_InputLayer.NumberOfNodes;

        public int NumberOfOutputNode => m_OutputLayer.NumberOfNodes;


        public void Update(float elapsedTime)
        {
            FeedForward();
        }

        protected override void Destroy()
        {
            base.Destroy();
        }

        public override string ToString()
        {
            return "Neural Network " + base.ToString();
        }

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

        public void CleanUp()
        {
            m_InputLayer.CleanUp();
            m_HiddenLayer.CleanUp();
            m_OutputLayer.CleanUp();
        }

        public void SetInput(int i, double value)
        {
            if ((i >= 0) && (i < m_InputLayer.NumberOfNodes))
            {
                m_InputLayer.NeuronValues[i] = value;
            }
        }

        public double GetOutput(int i)
        {
            if ((i >= 0) && (i < m_OutputLayer.NumberOfNodes))
            {
                return m_OutputLayer.NeuronValues[i];
            }

            return (double)Int32.MaxValue; // to indicate an error
        }

        public void SetDesiredOutput(int i, double value)
        {
            if ((i >= 0) && (i < m_OutputLayer.NumberOfNodes))
            {
                m_OutputLayer.DesiredValues[i] = value;
            }
        }

        public void FeedForward()
        {
            m_InputLayer.CalculateNeuronValues();
            m_HiddenLayer.CalculateNeuronValues();
            m_OutputLayer.CalculateNeuronValues();
        }

        public void BackPropagate()
        {
            m_OutputLayer.CalculateErrors();
            m_HiddenLayer.CalculateErrors();

            m_HiddenLayer.AdjustWeights();
            m_InputLayer.AdjustWeights();
        }

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

        public void SetLearningRate(double rate)
        {
            m_InputLayer.LearningRate = rate;
            m_HiddenLayer.LearningRate = rate;
            m_OutputLayer.LearningRate = rate;
        }

        public void SetLinearOutput(bool useLinear)
        {
            m_InputLayer.LinearOutput = useLinear;
            m_HiddenLayer.LinearOutput = useLinear;
            m_OutputLayer.LinearOutput = useLinear;
        }

        public void SetMomentum(bool useMomentum, double factor)
        {
            m_InputLayer.UseMomentum = useMomentum;
            m_HiddenLayer.UseMomentum = useMomentum;
            m_OutputLayer.UseMomentum = useMomentum;

            m_InputLayer.MomentumFactor = factor;
            m_HiddenLayer.MomentumFactor = factor;
            m_OutputLayer.MomentumFactor = factor;
        }

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
    }
}
