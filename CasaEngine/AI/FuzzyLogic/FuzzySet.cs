namespace CasaEngine.AI.Fuzzy
{
    public abstract class FuzzySet
    {

        protected double m_dDOM = 0.0;

        protected double m_dRepresentativeValue;



        public double DOM
        {
            get => m_dDOM;
            set
            {
                if ((value > 1) && (value < 0))
                {
                    throw new ArgumentException("FuzzySet.DOM;set : value need to be between 0 and 1");
                }

                m_dDOM = value;
            }
        }

        public double RepresentativeValue => m_dRepresentativeValue;


        public FuzzySet(double RepVal)
        {
            m_dRepresentativeValue = RepVal;
        }



        public abstract double CalculateDOM(double val);

        public void ORwithDOM(double val)
        {
            if (val > m_dDOM)
                m_dDOM = val;
        }

        public void ClearDOM()
        {
            m_dDOM = 0.0;
        }

    }
}
