namespace CasaEngine.AI.Fuzzy
{
    public enum DefuzzifyMethod
    {
        MaxAv,
        Centroid
    };

    public class FuzzyModule
    {
        static public readonly int NumSamples = 15;


        private readonly Dictionary<string, FuzzyVariable> _variables = new Dictionary<string, FuzzyVariable>();

        private readonly List<FuzzyRule> _rules = new List<FuzzyRule>();





        public void Fuzzify(string nameOfFlv, double val)
        {
            if (_variables.ContainsKey(nameOfFlv) == false)
            {
                throw new KeyNotFoundException("FuzzyModule.Fuzzify() : " + "key " + nameOfFlv + " not found");
            }

            _variables[nameOfFlv].Fuzzify(val);
        }



        public double DeFuzzify(string nameOfFlv, DefuzzifyMethod method)
        {
            if (_variables.ContainsKey(nameOfFlv) == false)
            {
                throw new KeyNotFoundException("FuzzyModule.DeFuzzify() : " + "key " + nameOfFlv + " not found");
            }

            //clear the DOMs of all the consequents of all the rules
            SetConfidencesOfConsequentsToZero();

            //process the rules
            foreach (FuzzyRule rule in _rules)
            {
                rule.Calculate();
            }

            //now defuzzify the resultant conclusion using the specified method
            switch (method)
            {
                case DefuzzifyMethod.Centroid:

                    return _variables[nameOfFlv].DeFuzzifyCentroid(NumSamples);

                //break;

                case DefuzzifyMethod.MaxAv:

                    return _variables[nameOfFlv].DeFuzzifyMaxAv();

                    //break;
            }

            return 0;
        }

        private void SetConfidencesOfConsequentsToZero()
        {
            foreach (FuzzyRule rule in _rules)
            {
                rule.SetConfidenceOfConsequentToZero();
            }
        }

        public void AddRule(IFuzzyTerm antecedent, IFuzzyTerm consequence)
        {
            _rules.Add(new FuzzyRule(antecedent, consequence));
        }

        public FuzzyVariable CreateFlv(string varName)
        {
            _variables[varName] = new FuzzyVariable();

            return _variables[varName];
        }

        public void WriteAllDoMs(BinaryWriter binW)
        {
            binW.Write("\n\n");

            foreach (KeyValuePair<string, FuzzyVariable> pair in _variables)
            {
                binW.Write("\n--------------------------- " + pair.Key);
                pair.Value.WriteDoMs(binW);

                binW.Write("\n");
            }
        }

    }
}
