namespace CasaEngine.AI.NeuralNets.FeedForward
{
    public class NeuralNetworkTraining
    {
        readonly NeuralNetwork _neuralNetwork = null;
        int _numberOfData = 0;

        double[,] _trainingSet = null;





        public NeuralNetworkTraining(NeuralNetwork neuralNet)
        {
            _neuralNetwork = neuralNet;
        }



        public void InitTrainingData(int numberOfSet)
        {
            _numberOfData = numberOfSet;
            _trainingSet = new double[_numberOfData, _neuralNetwork.NumberOfInputNode + _neuralNetwork.NumberOfOutputNode];
        }

        public void InitTrainingData(int numberOfSet, double[,] data)
        {
            InitTrainingData(numberOfSet);
            _trainingSet = data;
        }

        public void InitTrainingData(int numberOfSet, string fileName)
        {
            double[,] data = null;

            //fileName

            InitTrainingData(numberOfSet, data);
        }

        public void Training()
        {
            int i;
            var error = 1.0;
            var c = 0;

            while ((error > 0.05) && (c < 50000))
            {
                error = 0;
                c++;
                for (i = 0; i < _numberOfData; i++)
                {
                    for (var j = 0; j < _neuralNetwork.NumberOfInputNode; j++)
                    {
                        _neuralNetwork.SetInput(j, _trainingSet[i, j]);
                    }

                    for (var j = _neuralNetwork.NumberOfInputNode; j < _neuralNetwork.NumberOfOutputNode; j++)
                    {
                        _neuralNetwork.SetDesiredOutput(j, _trainingSet[i, j]);
                    }

                    _neuralNetwork.FeedForward();
                    error += _neuralNetwork.CalculateError();
                    _neuralNetwork.BackPropagate();
                }
                error = error / 14.0f;
            }
        }

    }
}
