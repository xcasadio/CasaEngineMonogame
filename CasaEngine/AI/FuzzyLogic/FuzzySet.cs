namespace CasaEngine.AI.Fuzzy
{
    public abstract class FuzzySet
    {

        protected double DDom = 0.0;

        protected double DRepresentativeValue;



        public double Dom
        {
            get => DDom;
            set
            {
                if ((value > 1) && (value < 0))
                {
                    throw new ArgumentException("FuzzySet.DOM;set : value need to be between 0 and 1");
                }

                DDom = value;
            }
        }

        public double RepresentativeValue => DRepresentativeValue;


        public FuzzySet(double repVal)
        {
            DRepresentativeValue = repVal;
        }



        public abstract double CalculateDom(double val);

        public void ORwithDom(double val)
        {
            if (val > DDom)
                DDom = val;
        }

        public void ClearDom()
        {
            DDom = 0.0;
        }

    }
}
