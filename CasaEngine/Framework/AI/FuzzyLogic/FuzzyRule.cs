namespace CasaEngine.Framework.AI.FuzzyLogic;

public class FuzzyRule
{
    private readonly IFuzzyTerm _pAntecedent;

    private readonly IFuzzyTerm _pConsequence;

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