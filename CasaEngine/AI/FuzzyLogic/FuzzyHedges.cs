namespace CasaEngine.AI.Fuzzy
{
    public class FzVery
        : FuzzyTerm
    {
        readonly FuzzySet m_Set;

        //prevent copying and assignment by clients
        //FzVery& operator=(const FzVery&);



        public double DOM => m_Set.DOM * m_Set.DOM;


        public FzVery(FzVery inst)
        {
            m_Set = inst.m_Set;
        }

        private FzVery(FzSet ft)
        {
            m_Set = ft.m_Set;
        }



        public FuzzyTerm Clone()
        {
            return new FzVery(this);
        }

        public void ClearDOM()
        {
            m_Set.ClearDOM();
        }

        public void ORwithDOM(double val)
        {
            m_Set.ORwithDOM(val * val);
        }

    }

    public class FzFairly
        : FuzzyTerm
    {
        readonly FuzzySet m_Set;



        public double DOM => System.Math.Sqrt(m_Set.DOM);


        private FzFairly(FzFairly inst)
        {
            m_Set = inst.m_Set;
        }

        public FzFairly(FzSet ft)
        {
            m_Set = ft.m_Set;
        }



        public FuzzyTerm Clone()
        {
            return new FzFairly(this);
        }

        public void ClearDOM()
        {
            m_Set.ClearDOM();
        }

        public void ORwithDOM(double val)
        {
            m_Set.ORwithDOM(System.Math.Sqrt(val));
        }

    }
}
