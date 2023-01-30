namespace CasaEngine.AI.Fuzzy
{
    public class FuzzyVariable
    {
        readonly Dictionary<string, FuzzySet> m_MemberSets = new Dictionary<string, FuzzySet>();
        double m_dMinRange = 0.0;
        double m_dMaxRange = 0.0;







        public void Fuzzify(double val)
        {
            //make sure the value is within the bounds of this variable
            if ((val < m_dMinRange) && (val > m_dMaxRange))
            {
                throw new ArgumentException("FuzzyVariable.Fuzzify() : value out of bounds");
            }

            //for each set in the flv calculate the DOM for the given value
            foreach (KeyValuePair<string, FuzzySet> pair in m_MemberSets)
            {
                pair.Value.DOM = pair.Value.CalculateDOM(val);
            }
        }

        public double DeFuzzifyMaxAv()
        {
            double bottom = 0.0;
            double top = 0.0;

            foreach (KeyValuePair<string, FuzzySet> pair in m_MemberSets)
            {
                bottom += pair.Value.DOM;

                top += pair.Value.RepresentativeValue * pair.Value.DOM;
            }

            //make sure bottom is not equal to zero
            if (0 == bottom) return 0.0;

            return top / bottom;
        }

        public double DeFuzzifyCentroid(int NumSamples)
        {
            //calculate the step size
            double StepSize = (m_dMaxRange - m_dMinRange) / (double)NumSamples;

            double TotalArea = 0.0;
            double SumOfMoments = 0.0;

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
            for (int samp = 1; samp <= NumSamples; ++samp)
            {
                //for each set get the contribution to the area. This is the lower of the 
                //value returned from CalculateDOM or the actual DOM of the fuzzified 
                //value itself   
                foreach (KeyValuePair<string, FuzzySet> pair in m_MemberSets)
                {
                    double contribution =
                        System.Math.Min(pair.Value.CalculateDOM(m_dMinRange + samp * StepSize), pair.Value.DOM);

                    TotalArea += contribution;

                    SumOfMoments += (m_dMinRange + samp * StepSize) * contribution;
                }
            }

            //make sure total area is not equal to zero
            if (0 == TotalArea) return 0.0;

            return (SumOfMoments / TotalArea);
        }

        public FzSet AddTriangularSet(string name, double minBound, double peak, double maxBound)
        {
            m_MemberSets[name] = new FuzzySet_Triangle(peak,
                                                       peak - minBound,
                                                       maxBound - peak);
            //adjust range if necessary
            AdjustRangeToFit(minBound, maxBound);

            return new FzSet(m_MemberSets[name]);
        }

        public FzSet AddLeftShoulderSet(string name, double minBound, double peak, double maxBound)
        {
            m_MemberSets[name] = new FuzzySet_LeftShoulder(peak, peak - minBound, maxBound - peak);

            //adjust range if necessary
            AdjustRangeToFit(minBound, maxBound);

            return new FzSet(m_MemberSets[name]);
        }

        public FzSet AddRightShoulderSet(string name, double minBound, double peak, double maxBound)
        {
            m_MemberSets[name] = new FuzzySet_RightShoulder(peak, peak - minBound, maxBound - peak);

            //adjust range if necessary
            AdjustRangeToFit(minBound, maxBound);

            return new FzSet(m_MemberSets[name]);
        }

        public FzSet AddSingletonSet(string name, double minBound, double peak, double maxBound)
        {
            m_MemberSets[name] = new FuzzySet_Singleton(peak,
                                                        peak - minBound,
                                                        maxBound - peak);

            AdjustRangeToFit(minBound, maxBound);

            return new FzSet(m_MemberSets[name]);
        }

        private void AdjustRangeToFit(double minBound, double maxBound)
        {
            if (minBound < m_dMinRange) m_dMinRange = minBound;
            if (maxBound > m_dMaxRange) m_dMaxRange = maxBound;
        }

        public void WriteDOMs(BinaryWriter binW_)
        {
            foreach (KeyValuePair<string, FuzzySet> pair in m_MemberSets)
            {
                binW_.Write("\n" + pair.Key + " is " + pair.Value.DOM);
            }

            binW_.Write("\nMin Range: " + m_dMinRange + "\nMax Range: " + m_dMaxRange);
        }

    }
}
