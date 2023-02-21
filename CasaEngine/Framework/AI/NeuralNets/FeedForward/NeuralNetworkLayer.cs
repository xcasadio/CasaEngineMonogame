namespace CasaEngine.Framework.AI.NeuralNets.FeedForward
{
    public class NeuralNetworkLayer
    {
        private int _numberOfNodes;
        private int _numberOfChildNodes;
        private int _numberOfParentNodes;
        private double[,] _weights;
        private double[,] _weightChanges;
        private double[] _neuronValues;
        private double[] _desiredValues;
        private double[] _errors;
        private double[] _biasWeights;
        private double[] _biasValues;
        private double _learningRate;

        private bool _linearOutput;
        private bool _useMomentum;
        private double _momentumFactor = 0.9;

        private NeuralNetworkLayer _parentLayer;
        private NeuralNetworkLayer _childLayer;



        public int NumberOfNodes
        {
            get => _numberOfNodes;
            set => _numberOfNodes = value;
        }

        public int NumberOfChildNodes
        {
            get => _numberOfChildNodes;
            set => _numberOfChildNodes = value;
        }

        public int NumberOfParentNodes
        {
            get => _numberOfParentNodes;
            set => _numberOfParentNodes = value;
        }

        public double[,] Weights => _weights;

        public double[,] WeightChanges => _weightChanges;

        public double[] NeuronValues => _neuronValues;

        public double[] DesiredValues => _desiredValues;

        public double[] Errors => _errors;

        public double[] BiasWeights => _biasWeights;

        public double[] BiasValues => _biasValues;

        public double LearningRate
        {
            get => _learningRate;
            set => _learningRate = value;
        }

        public bool LinearOutput
        {
            get => _linearOutput;
            set => _linearOutput = value;
        }

        public bool UseMomentum
        {
            get => _useMomentum;
            set => _useMomentum = value;
        }

        public double MomentumFactor
        {
            get => _momentumFactor;
            set => _momentumFactor = value;
        }

        public NeuralNetworkLayer ParentLayer => _parentLayer;

        public NeuralNetworkLayer ChildLayer => _childLayer;


        public void Initialize(int numNodes, NeuralNetworkLayer parent, NeuralNetworkLayer child)
        {
            int i, j;

            _neuronValues = new double[_numberOfNodes];
            _desiredValues = new double[_numberOfNodes];
            _errors = new double[_numberOfNodes];

            if (parent != null)
            {
                _parentLayer = parent;
            }

            if (child != null)
            {
                _childLayer = child;

                _weights = new double[_numberOfNodes, _numberOfChildNodes];
                _weightChanges = new double[_numberOfNodes, _numberOfChildNodes];

                _biasValues = new double[_numberOfChildNodes];
                _biasWeights = new double[_numberOfChildNodes];
            }
            else
            {
                _weights = null;
                _biasValues = null;
                _biasWeights = null;
            }

            // Make sure everything contains zeros
            for (i = 0; i < _numberOfNodes; i++)
            {
                _neuronValues[i] = 0;
                _desiredValues[i] = 0;
                _errors[i] = 0;

                if (_childLayer != null)
                {
                    for (j = 0; j < _numberOfChildNodes; j++)
                    {
                        _weights[i, j] = 0;
                        _weightChanges[i, j] = 0;
                    }
                }
            }

            if (_childLayer != null)
            {
                for (j = 0; j < _numberOfChildNodes; j++)
                {
                    _biasValues[j] = -1;
                    _biasWeights[j] = 0;
                }
            }
        }

        public void CleanUp()
        {
            _neuronValues = null;
            _desiredValues = null;
            _errors = null;

            _weights = null;
            _weightChanges = null;

            _biasValues = null;
            _biasWeights = null;
        }

        public void RandomizeWeights()
        {
            int i, j;
            var min = 0;
            var max = 200;
            int number;

            var rand = new Random();

            for (i = 0; i < _numberOfNodes; i++)
            {
                for (j = 0; j < _numberOfChildNodes; j++)
                {
                    number = rand.Next(min, max);

                    if (number > max)
                    {
                        number = max;
                    }

                    if (number < min)
                    {
                        number = min;
                    }

                    _weights[i, j] = number / 100.0f - 1;
                }
            }

            for (j = 0; j < _numberOfChildNodes; j++)
            {
                number = rand.Next(min, max);

                if (number > max)
                {
                    number = max;
                }

                if (number < min)
                {
                    number = min;
                }

                _biasWeights[j] = number / 100.0f - 1;
            }
        }

        public void CalculateErrors()
        {
            int i, j;
            double sum;

            if (_childLayer == null) // output layer
            {
                for (i = 0; i < _numberOfNodes; i++)
                {
                    _errors[i] = (_desiredValues[i] - _neuronValues[i]) * _neuronValues[i] * (1.0f - _neuronValues[i]);
                }
            }
            else if (_parentLayer == null)
            { // input layer
                for (i = 0; i < _numberOfNodes; i++)
                {
                    _errors[i] = 0.0f;
                }
            }
            else
            { // hidden layer
                for (i = 0; i < _numberOfNodes; i++)
                {
                    sum = 0;

                    for (j = 0; j < _numberOfChildNodes; j++)
                    {
                        sum += _childLayer.Errors[j] * _weights[i, j];
                    }
                    _errors[i] = sum * _neuronValues[i] * (1.0f - _neuronValues[i]);
                }
            }
        }

        public void AdjustWeights()
        {
            int i, j;
            double dw;

            if (_childLayer != null)
            {
                for (i = 0; i < _numberOfNodes; i++)
                {
                    for (j = 0; j < _numberOfChildNodes; j++)
                    {
                        dw = _learningRate * _childLayer.Errors[j] * _neuronValues[i];
                        _weights[i, j] += dw + _momentumFactor * _weightChanges[i, j];
                        _weightChanges[i, j] = dw;
                    }
                }

                for (j = 0; j < _numberOfChildNodes; j++)
                {
                    _biasWeights[j] += _learningRate * _childLayer.Errors[j] * _biasValues[j];
                }
            }
        }

        public void CalculateNeuronValues()
        {
            int i, j;
            double x;

            if (_parentLayer != null)
            {
                for (j = 0; j < _numberOfNodes; j++)
                {
                    x = 0.0;

                    for (i = 0; i < _numberOfParentNodes; i++)
                    {
                        x += _parentLayer.NeuronValues[i] * _parentLayer.Weights[i, j];
                    }

                    x += _parentLayer.BiasValues[j] * _parentLayer.BiasWeights[j];

                    if (_childLayer == null && _linearOutput)
                    {
                        _neuronValues[j] = x;
                    }
                    else
                    {
                        _neuronValues[j] = 1.0f / (1 + Math.Exp(-x));
                    }
                }
            }
        }

    }
}
