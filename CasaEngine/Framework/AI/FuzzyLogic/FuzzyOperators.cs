namespace CasaEngine.Framework.AI.FuzzyLogic
{
    public class FzAnd
        : IFuzzyTerm
    {

        //an instance of this class may AND together up to 4 terms
        private readonly List<IFuzzyTerm> _terms = new();

        //disallow assignment
        //FzAND operator=(FzAND&);



        public double Dom
        {
            get
            {
                var smallest = double.MaxValue;

                foreach (var t in _terms)
                {
                    if (t.Dom < smallest)
                    {
                        smallest = t.Dom;
                    }
                }

                return smallest;
            }
        }



        public FzAnd(FzAnd fa)
        {
            foreach (var f in fa._terms)
            {
                _terms.Add(f.Clone());
            }
        }

        public FzAnd(IFuzzyTerm op1, IFuzzyTerm op2)
        {
            _terms.Add(op1.Clone());
            _terms.Add(op2.Clone());
        }

        public FzAnd(IFuzzyTerm op1, IFuzzyTerm op2, IFuzzyTerm op3)
        {
            _terms.Add(op1.Clone());
            _terms.Add(op2.Clone());
            _terms.Add(op3.Clone());
        }

        public FzAnd(IFuzzyTerm op1, IFuzzyTerm op2, IFuzzyTerm op3, IFuzzyTerm op4)
        {
            _terms.Add(op1.Clone());
            _terms.Add(op2.Clone());
            _terms.Add(op3.Clone());
            _terms.Add(op4.Clone());
        }



        public void ORwithDom(double val)
        {
            foreach (var t in _terms)
            {
                t.ORwithDom(val);
            }
        }

        public void ClearDom()
        {
            foreach (var t in _terms)
            {
                t.ClearDom();
            }
        }

        public IFuzzyTerm Clone()
        {
            return new FzAnd(this);
        }

    }

    public class FzOr
        : IFuzzyTerm
    {

        //an instance of this class may AND together up to 4 terms
        private readonly List<IFuzzyTerm> _terms = new();

        //disallow assignment
        //FzAND operator=(FzAND&);



        public double Dom
        {
            get
            {
                double largest = float.MinValue;

                foreach (var t in _terms)
                {
                    if (t.Dom > largest)
                    {
                        largest = t.Dom;
                    }
                }

                return largest;
            }
        }



        public FzOr(FzOr fa)
        {
            foreach (var f in fa._terms)
            {
                _terms.Add(f.Clone());
            }
        }

        public FzOr(IFuzzyTerm op1, IFuzzyTerm op2)
        {
            _terms.Add(op1.Clone());
            _terms.Add(op2.Clone());
        }

        public FzOr(IFuzzyTerm op1, IFuzzyTerm op2, IFuzzyTerm op3)
        {
            _terms.Add(op1.Clone());
            _terms.Add(op2.Clone());
            _terms.Add(op3.Clone());
        }

        public FzOr(IFuzzyTerm op1, IFuzzyTerm op2, IFuzzyTerm op3, IFuzzyTerm op4)
        {
            _terms.Add(op1.Clone());
            _terms.Add(op2.Clone());
            _terms.Add(op3.Clone());
            _terms.Add(op4.Clone());
        }



        public void ClearDom()
        {
            throw new InvalidOperationException("FzOR.ClearDOM() : invalid context");
        }

        public void ORwithDom(double val)
        {
            throw new InvalidOperationException("FzOR.ORwithDOM() : invalid context");
        }

        public IFuzzyTerm Clone()
        {
            return new FzOr(this);
        }

    }
}
