namespace CasaEngine.AI.Fuzzy
{
    public interface FuzzyTerm
    {
        FuzzyTerm Clone();

        //retrieves the degree of membership of the term
        double DOM { get; }

        //clears the degree of membership of the term
        void ClearDOM();

        //method for updating the DOM of a consequent when a rule fires
        void ORwithDOM(double val);
    }
}
