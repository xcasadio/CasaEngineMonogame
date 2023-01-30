namespace CasaEngine.AI.Fuzzy
{
    public class FuzzyRule
    {
        readonly FuzzyTerm m_pAntecedent;

        readonly FuzzyTerm m_pConsequence;

        //it doesn't make sense to allow clients to copy rules
        //FuzzyRule(FuzzyRule);
        //FuzzyRule& operator=(const FuzzyRule&);



        public FuzzyRule(FuzzyTerm ant, FuzzyTerm con)
        {
            m_pAntecedent = ant.Clone();
            m_pConsequence = con.Clone();
        }

        public void SetConfidenceOfConsequentToZero()
        {
            m_pConsequence.ClearDOM();
        }

        public void Calculate()
        {
            m_pConsequence.ORwithDOM(m_pAntecedent.DOM);
        }

    }
}
