namespace CasaEngine.AI.FuzzyLogic
{
    public class FzSet
        : IFuzzyTerm
    {

        internal FuzzySet Set;



        public double Dom => Set.Dom;


        public FzSet(FuzzySet fs)
        {
            Set = fs;
        }



        public IFuzzyTerm Clone()
        {
            return new FzSet(Set);
        }

        public void ClearDom()
        {
            Set.ClearDom();
        }

        public void ORwithDom(double val)
        {
            Set.ORwithDom(val);
        }

    }
}
