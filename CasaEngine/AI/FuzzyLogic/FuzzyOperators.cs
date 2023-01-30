namespace CasaEngine.AI.Fuzzy
{
    public class FzAND
        : FuzzyTerm
    {

        //an instance of this class may AND together up to 4 terms
        readonly List<FuzzyTerm> m_Terms = new List<FuzzyTerm>();

        //disallow assignment
        //FzAND operator=(FzAND&);



        public double DOM
        {
            get
            {
                double smallest = double.MaxValue;

                foreach (FuzzyTerm t in m_Terms)
                {
                    if (t.DOM < smallest)
                    {
                        smallest = t.DOM;
                    }
                }

                return smallest;
            }
        }



        public FzAND(FzAND fa)
        {
            foreach (FuzzyTerm f in fa.m_Terms)
            {
                m_Terms.Add(f.Clone());
            }
        }

        public FzAND(FuzzyTerm op1, FuzzyTerm op2)
        {
            m_Terms.Add(op1.Clone());
            m_Terms.Add(op2.Clone());
        }

        public FzAND(FuzzyTerm op1, FuzzyTerm op2, FuzzyTerm op3)
        {
            m_Terms.Add(op1.Clone());
            m_Terms.Add(op2.Clone());
            m_Terms.Add(op3.Clone());
        }

        public FzAND(FuzzyTerm op1, FuzzyTerm op2, FuzzyTerm op3, FuzzyTerm op4)
        {
            m_Terms.Add(op1.Clone());
            m_Terms.Add(op2.Clone());
            m_Terms.Add(op3.Clone());
            m_Terms.Add(op4.Clone());
        }



        public void ORwithDOM(double val)
        {
            foreach (FuzzyTerm t in m_Terms)
            {
                t.ORwithDOM(val);
            }
        }

        public void ClearDOM()
        {
            foreach (FuzzyTerm t in m_Terms)
            {
                t.ClearDOM();
            }
        }

        public FuzzyTerm Clone()
        {
            return new FzAND(this);
        }

    }

    public class FzOR
        : FuzzyTerm
    {

        //an instance of this class may AND together up to 4 terms
        readonly List<FuzzyTerm> m_Terms = new List<FuzzyTerm>();

        //disallow assignment
        //FzAND operator=(FzAND&);



        public double DOM
        {
            get
            {
                double largest = float.MinValue;

                foreach (FuzzyTerm t in m_Terms)
                {
                    if (t.DOM > largest)
                    {
                        largest = t.DOM;
                    }
                }

                return largest;
            }
        }



        public FzOR(FzOR fa)
        {
            foreach (FuzzyTerm f in fa.m_Terms)
            {
                m_Terms.Add(f.Clone());
            }
        }

        public FzOR(FuzzyTerm op1, FuzzyTerm op2)
        {
            m_Terms.Add(op1.Clone());
            m_Terms.Add(op2.Clone());
        }

        public FzOR(FuzzyTerm op1, FuzzyTerm op2, FuzzyTerm op3)
        {
            m_Terms.Add(op1.Clone());
            m_Terms.Add(op2.Clone());
            m_Terms.Add(op3.Clone());
        }

        public FzOR(FuzzyTerm op1, FuzzyTerm op2, FuzzyTerm op3, FuzzyTerm op4)
        {
            m_Terms.Add(op1.Clone());
            m_Terms.Add(op2.Clone());
            m_Terms.Add(op3.Clone());
            m_Terms.Add(op4.Clone());
        }



        public void ClearDOM()
        {
            throw new InvalidOperationException("FzOR.ClearDOM() : invalid context");
        }

        public void ORwithDOM(double val)
        {
            throw new InvalidOperationException("FzOR.ORwithDOM() : invalid context");
        }

        public FuzzyTerm Clone()
        {
            return new FzOR(this);
        }

    }
}
