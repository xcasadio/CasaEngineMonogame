namespace CasaEngine.Framework.AI.NeuralNets.FeedForward;

public class NeuralNetwork
{
    private NeuralNetworkLayer _inputLayer = null;
    private NeuralNetworkLayer _hiddenLayer = null;
    private NeuralNetworkLayer _outputLayer = null;

    public int NumberOfInputNode => _inputLayer.NumberOfNodes;

    public int NumberOfOutputNode => _outputLayer.NumberOfNodes;

    public void Update(float elapsedTime)
    {
        FeedForward();
    }

    public override string ToString()
    {
        return "Neural Network " + base.ToString();
    }

    public void Initialize(int nNodesInput, int nNodesHidden, int nNodesOutput)
    {
        _inputLayer.NumberOfNodes = nNodesInput;
        _inputLayer.NumberOfChildNodes = nNodesHidden;
        _inputLayer.NumberOfParentNodes = 0;
        _inputLayer.Initialize(nNodesInput, null, _hiddenLayer);
        _inputLayer.RandomizeWeights();

        _hiddenLayer.NumberOfNodes = nNodesHidden;
        _hiddenLayer.NumberOfChildNodes = nNodesOutput;
        _hiddenLayer.NumberOfParentNodes = nNodesInput;
        _hiddenLayer.Initialize(nNodesHidden, _inputLayer, _outputLayer);
        _hiddenLayer.RandomizeWeights();

        _outputLayer.NumberOfNodes = nNodesOutput;
        _outputLayer.NumberOfChildNodes = 0;
        _outputLayer.NumberOfParentNodes = nNodesHidden;
        _outputLayer.Initialize(nNodesOutput, _hiddenLayer, null);
    }

    public void CleanUp()
    {
        _inputLayer.CleanUp();
        _hiddenLayer.CleanUp();
        _outputLayer.CleanUp();
    }

    public void SetInput(int i, double value)
    {
        if (i >= 0 && i < _inputLayer.NumberOfNodes)
        {
            _inputLayer.NeuronValues[i] = value;
        }
    }

    public double GetOutput(int i)
    {
        if (i >= 0 && i < _outputLayer.NumberOfNodes)
        {
            return _outputLayer.NeuronValues[i];
        }

        return int.MaxValue; // to indicate an error
    }

    public void SetDesiredOutput(int i, double value)
    {
        if (i >= 0 && i < _outputLayer.NumberOfNodes)
        {
            _outputLayer.DesiredValues[i] = value;
        }
    }

    public void FeedForward()
    {
        _inputLayer.CalculateNeuronValues();
        _hiddenLayer.CalculateNeuronValues();
        _outputLayer.CalculateNeuronValues();
    }

    public void BackPropagate()
    {
        _outputLayer.CalculateErrors();
        _hiddenLayer.CalculateErrors();

        _hiddenLayer.AdjustWeights();
        _inputLayer.AdjustWeights();
    }

    public int GetMaxOutputId()
    {
        int i, id;
        double maxval;

        maxval = _outputLayer.NeuronValues[0];
        id = 0;

        for (i = 1; i < _outputLayer.NumberOfNodes; i++)
        {
            if (_outputLayer.NeuronValues[i] > maxval)
            {
                maxval = _outputLayer.NeuronValues[i];
                id = i;
            }
        }

        return id;
    }

    public double CalculateError()
    {
        int i;
        double error = 0;

        for (i = 0; i < _outputLayer.NumberOfNodes; i++)
        {
            error += Math.Pow(_outputLayer.NeuronValues[i] - _outputLayer.DesiredValues[i], 2);
        }

        error = error / _outputLayer.NumberOfNodes;

        return error;
    }

    public void SetLearningRate(double rate)
    {
        _inputLayer.LearningRate = rate;
        _hiddenLayer.LearningRate = rate;
        _outputLayer.LearningRate = rate;
    }

    public void SetLinearOutput(bool useLinear)
    {
        _inputLayer.LinearOutput = useLinear;
        _hiddenLayer.LinearOutput = useLinear;
        _outputLayer.LinearOutput = useLinear;
    }

    public void SetMomentum(bool useMomentum, double factor)
    {
        _inputLayer.UseMomentum = useMomentum;
        _hiddenLayer.UseMomentum = useMomentum;
        _outputLayer.UseMomentum = useMomentum;

        _inputLayer.MomentumFactor = factor;
        _hiddenLayer.MomentumFactor = factor;
        _outputLayer.MomentumFactor = factor;
    }

    public void DumpData(string filename)
    {

    }
}