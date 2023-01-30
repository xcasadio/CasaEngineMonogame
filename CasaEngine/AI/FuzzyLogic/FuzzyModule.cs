namespace CasaEngine.AI.Fuzzy
{
    public enum DefuzzifyMethod
    {
        MAX_AV,
        CENTROID
    };

    public class FuzzyModule
    {
        static public readonly int NUM_SAMPLES = 15;


        private readonly Dictionary<string, FuzzyVariable> m_Variables = new Dictionary<string, FuzzyVariable>();

        private readonly List<FuzzyRule> m_Rules = new List<FuzzyRule>();





        public void Fuzzify(string NameOfFLV, double val)
        {
            if (m_Variables.ContainsKey(NameOfFLV) == false)
            {
                throw new KeyNotFoundException("FuzzyModule.Fuzzify() : " + "key " + NameOfFLV + " not found");
            }

            m_Variables[NameOfFLV].Fuzzify(val);
        }



        public double DeFuzzify(string NameOfFLV, DefuzzifyMethod method)
        {
            if (m_Variables.ContainsKey(NameOfFLV) == false)
            {
                throw new KeyNotFoundException("FuzzyModule.DeFuzzify() : " + "key " + NameOfFLV + " not found");
            }

            //clear the DOMs of all the consequents of all the rules
            SetConfidencesOfConsequentsToZero();

            //process the rules
            foreach (FuzzyRule rule in m_Rules)
            {
                rule.Calculate();
            }

            //now defuzzify the resultant conclusion using the specified method
            switch (method)
            {
                case DefuzzifyMethod.CENTROID:

                    return m_Variables[NameOfFLV].DeFuzzifyCentroid(NUM_SAMPLES);

                //break;

                case DefuzzifyMethod.MAX_AV:

                    return m_Variables[NameOfFLV].DeFuzzifyMaxAv();

                    //break;
            }

            return 0;
        }

        private void SetConfidencesOfConsequentsToZero()
        {
            foreach (FuzzyRule rule in m_Rules)
            {
                rule.SetConfidenceOfConsequentToZero();
            }
        }

        public void AddRule(FuzzyTerm antecedent, FuzzyTerm consequence)
        {
            m_Rules.Add(new FuzzyRule(antecedent, consequence));
        }

        public FuzzyVariable CreateFLV(string VarName)
        {
            m_Variables[VarName] = new FuzzyVariable();

            return m_Variables[VarName];
        }

        public void WriteAllDOMs(BinaryWriter binW_)
        {
            binW_.Write("\n\n");

            foreach (KeyValuePair<string, FuzzyVariable> pair in m_Variables)
            {
                binW_.Write("\n--------------------------- " + pair.Key);
                pair.Value.WriteDOMs(binW_);

                binW_.Write("\n");
            }
        }

    }
}
