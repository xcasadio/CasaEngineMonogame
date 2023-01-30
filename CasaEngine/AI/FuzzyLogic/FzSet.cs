namespace CasaEngine.AI.Fuzzy
{
    public class FzSet
        : FuzzyTerm
    {

        internal FuzzySet m_Set;



        public double DOM => m_Set.DOM;


        public FzSet(FuzzySet fs)
        {
            m_Set = fs;
        }



        public FuzzyTerm Clone()
        {
            return new FzSet(m_Set);
        }

        public void ClearDOM()
        {
            m_Set.ClearDOM();
        }

        public void ORwithDOM(double val)
        {
            m_Set.ORwithDOM(val);
        }

    }
}
