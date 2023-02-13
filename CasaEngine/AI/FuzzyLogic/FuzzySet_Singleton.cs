namespace CasaEngine.AI.FuzzyLogic
{
    public class FuzzySetSingleton
        : FuzzySet
    {

        //the values that define the shape of this FLV
        readonly double _dMidPoint;
        readonly double _dLeftOffset;
        readonly double _dRightOffset;





        public FuzzySetSingleton(double mid, double lft, double rgt)
            : base(mid)
        {
            _dMidPoint = mid;
            _dLeftOffset = lft;
            _dRightOffset = rgt;
        }



        public override double CalculateDom(double val)
        {
            if ((val >= _dMidPoint - _dLeftOffset) &&
                 (val <= _dMidPoint + _dRightOffset))
            {
                return 1.0;
            }

            //out of range of this FLV, return zero
            else
            {
                return 0.0;
            }
        }

    }
}
