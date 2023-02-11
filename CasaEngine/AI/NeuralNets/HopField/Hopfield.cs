namespace HopField
{
    class Hopfield
    {

        int _numUnits;

        readonly List<int[]> _pattern = new List<int[]>();

        int[] _output;

        int[] _threshold;

        int[,] _weights;



        public int[] Output
        {
            get => _output;
            private set => _output = value;
        }






        public void GenerateNetwork(int nu)
        {
            int i;

            _numUnits = nu;
            _output = new int[nu];
            _threshold = new int[nu];
            _weights = new int[nu, nu];

            for (i = 0; i < nu; i++)
            {
                _threshold[i] = 0;
            }
        }

        public void AddPattern(int[] pattern)
        {
            if (pattern.Length != _numUnits)
            {
                throw new ArgumentException("HopField.SetInput() : input_.Length != _NumUnits");
            }

            _pattern.Add(pattern);
        }

        public void CalculateWeights()
        {
            int i, j, n;
            int weight;

            for (i = 0; i < _numUnits; i++)
            {
                for (j = 0; j < _numUnits; j++)
                {
                    weight = 0;

                    if (i != j)
                    {
                        for (n = 0; n < _pattern.Count; n++)
                        {
                            weight += _pattern[n][i] * _pattern[n][j];
                        }
                    }

                    this._weights[i, j] = weight;
                }
            }
        }

        public void SetInput(ref int[] input)
        {
            if (input.Length != _numUnits)
            {
                throw new ArgumentException("HopField.SetInput() : input_.Length != _NumUnits");
            }

            _output = input;
        }



        private bool PropagateUnit(int i)
        {
            int j;
            int sum, @out = 0;
            bool changed;

            changed = false;
            sum = 0;

            for (j = 0; j < _numUnits; j++)
            {
                sum += _weights[i, j] * _output[j];
            }

            if (sum != _threshold[i])
            {
                if (sum < _threshold[i]) @out = -1;
                if (sum >= _threshold[i]) @out = 1;
                if (@out != _output[i])
                {
                    changed = true;
                    _output[i] = @out;
                }
            }

            return changed;
        }

        public void PropagateNet()
        {
            int iteration, iterationOfLastChange;

            iteration = 0;
            iterationOfLastChange = 0;

            Random rand = new Random();

            do
            {
                iteration++;
                if (PropagateUnit(rand.Next(0, _numUnits - 1)))
                {
                    iterationOfLastChange = iteration;
                }
            }
            while (iteration - iterationOfLastChange < 10 * _numUnits);
        }


    }
}
