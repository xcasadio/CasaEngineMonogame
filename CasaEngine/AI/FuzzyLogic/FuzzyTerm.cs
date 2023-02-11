namespace CasaEngine.AI.Fuzzy
{
    public interface IFuzzyTerm
    {
        IFuzzyTerm Clone();

        //retrieves the degree of membership of the term
        double Dom { get; }

        //clears the degree of membership of the term
        void ClearDom();

        //method for updating the DOM of a consequent when a rule fires
        void ORwithDom(double val);
    }
}
