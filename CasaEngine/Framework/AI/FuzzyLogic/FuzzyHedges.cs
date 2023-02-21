namespace CasaEngine.Framework.AI.FuzzyLogic
{
    public class FzVery
        : IFuzzyTerm
    {
        private readonly FuzzySet _set;

        //prevent copying and assignment by clients
        //FzVery& operator=(const FzVery&);



        public double Dom => _set.Dom * _set.Dom;


        public FzVery(FzVery inst)
        {
            _set = inst._set;
        }

        private FzVery(FzSet ft)
        {
            _set = ft.Set;
        }



        public IFuzzyTerm Clone()
        {
            return new FzVery(this);
        }

        public void ClearDom()
        {
            _set.ClearDom();
        }

        public void ORwithDom(double val)
        {
            _set.ORwithDom(val * val);
        }

    }

    public class FzFairly
        : IFuzzyTerm
    {
        private readonly FuzzySet _set;



        public double Dom => Math.Sqrt(_set.Dom);


        private FzFairly(FzFairly inst)
        {
            _set = inst._set;
        }

        public FzFairly(FzSet ft)
        {
            _set = ft.Set;
        }



        public IFuzzyTerm Clone()
        {
            return new FzFairly(this);
        }

        public void ClearDom()
        {
            _set.ClearDom();
        }

        public void ORwithDom(double val)
        {
            _set.ORwithDom(Math.Sqrt(val));
        }

    }
}
