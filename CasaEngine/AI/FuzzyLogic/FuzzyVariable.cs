namespace CasaEngine.AI.FuzzyLogic
{
    public class FuzzyVariable
    {
        readonly Dictionary<string, FuzzySet> _memberSets = new();
        double _dMinRange = 0.0;
        double _dMaxRange = 0.0;







        public void Fuzzify(double val)
        {
            //make sure the value is within the bounds of this variable
            if ((val < _dMinRange) && (val > _dMaxRange))
            {
                throw new ArgumentException("FuzzyVariable.Fuzzify() : value out of bounds");
            }

            //for each set in the flv calculate the DOM for the given value
            foreach (var pair in _memberSets)
            {
                pair.Value.Dom = pair.Value.CalculateDom(val);
            }
        }

        public double DeFuzzifyMaxAv()
        {
            var bottom = 0.0;
            var top = 0.0;

            foreach (var pair in _memberSets)
            {
                bottom += pair.Value.Dom;

                top += pair.Value.RepresentativeValue * pair.Value.Dom;
            }

            //make sure bottom is not equal to zero
            if (0 == bottom)
            {
                return 0.0;
            }

            return top / bottom;
        }

        public double DeFuzzifyCentroid(int numSamples)
        {
            //calculate the step size
            var stepSize = (_dMaxRange - _dMinRange) / (double)numSamples;

            var totalArea = 0.0;
            var sumOfMoments = 0.0;

            //step through the range of this variable in increments equal to StepSize
            //adding up the contribution (lower of CalculateDOM or the actual DOM of this
            //variable's fuzzified value) for each subset. This gives an approximation of
            //the total area of the fuzzy manifold.(This is similar to how the area under
            //a curve is calculated using calculus... the heights of lots of 'slices' are
            //summed to give the total area.)
            //
            //in addition the moment of each slice is calculated and summed. Dividing
            //the total area by the sum of the moments gives the centroid. (Just like
            //calculating the center of mass of an object)
            for (var samp = 1; samp <= numSamples; ++samp)
            {
                //for each set get the contribution to the area. This is the lower of the 
                //value returned from CalculateDOM or the actual DOM of the fuzzified 
                //value itself   
                foreach (var pair in _memberSets)
                {
                    var contribution =
                        System.Math.Min(pair.Value.CalculateDom(_dMinRange + samp * stepSize), pair.Value.Dom);

                    totalArea += contribution;

                    sumOfMoments += (_dMinRange + samp * stepSize) * contribution;
                }
            }

            //make sure total area is not equal to zero
            if (0 == totalArea)
            {
                return 0.0;
            }

            return (sumOfMoments / totalArea);
        }

        public FzSet AddTriangularSet(string name, double minBound, double peak, double maxBound)
        {
            _memberSets[name] = new FuzzySetTriangle(peak,
                                                       peak - minBound,
                                                       maxBound - peak);
            //adjust range if necessary
            AdjustRangeToFit(minBound, maxBound);

            return new FzSet(_memberSets[name]);
        }

        public FzSet AddLeftShoulderSet(string name, double minBound, double peak, double maxBound)
        {
            _memberSets[name] = new FuzzySetLeftShoulder(peak, peak - minBound, maxBound - peak);

            //adjust range if necessary
            AdjustRangeToFit(minBound, maxBound);

            return new FzSet(_memberSets[name]);
        }

        public FzSet AddRightShoulderSet(string name, double minBound, double peak, double maxBound)
        {
            _memberSets[name] = new FuzzySetRightShoulder(peak, peak - minBound, maxBound - peak);

            //adjust range if necessary
            AdjustRangeToFit(minBound, maxBound);

            return new FzSet(_memberSets[name]);
        }

        public FzSet AddSingletonSet(string name, double minBound, double peak, double maxBound)
        {
            _memberSets[name] = new FuzzySetSingleton(peak,
                                                        peak - minBound,
                                                        maxBound - peak);

            AdjustRangeToFit(minBound, maxBound);

            return new FzSet(_memberSets[name]);
        }

        private void AdjustRangeToFit(double minBound, double maxBound)
        {
            if (minBound < _dMinRange)
            {
                _dMinRange = minBound;
            }

            if (maxBound > _dMaxRange)
            {
                _dMaxRange = maxBound;
            }
        }

        public void WriteDoMs(BinaryWriter binW)
        {
            foreach (var pair in _memberSets)
            {
                binW.Write("\n" + pair.Key + " is " + pair.Value.Dom);
            }

            binW.Write("\nMin Range: " + _dMinRange + "\nMax Range: " + _dMaxRange);
        }

    }
}
