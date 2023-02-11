namespace CasaEngine.AI.Fuzzy
{
    public class FuzzyRule
    {
        readonly IFuzzyTerm _pAntecedent;

        readonly IFuzzyTerm _pConsequence;

        //it doesn't make sense to allow clients to copy rules
        //FuzzyRule(FuzzyRule);
        //FuzzyRule& operator=(const FuzzyRule&);



        public FuzzyRule(IFuzzyTerm ant, IFuzzyTerm con)
        {
            _pAntecedent = ant.Clone();
            _pConsequence = con.Clone();
        }

        public void SetConfidenceOfConsequentToZero()
        {
            _pConsequence.ClearDom();
        }

        public void Calculate()
        {
            _pConsequence.ORwithDom(_pAntecedent.Dom);
        }

    }
}
