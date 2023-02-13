namespace CasaEngine.AI.FuzzyLogic
{
    public class FuzzySetTriangle
        : FuzzySet
    {
        readonly double _dPeakPoint;
        readonly double _dLeftOffset;
        readonly double _dRightOffset;





        public FuzzySetTriangle(double mid, double lft, double rgt)
            : base(mid)
        {
            _dPeakPoint = mid;
            _dLeftOffset = lft;
            _dRightOffset = rgt;
        }



        public override double CalculateDom(double val)
        {
            //test for the case where the triangle's left or right offsets are zero
            //(to prevent divide by zero errors below)
            if ((_dRightOffset == 0.0 && _dPeakPoint == val) ||
                 (_dLeftOffset == 0.0 && _dPeakPoint == val))
            {
                return 1.0;
            }

            //find DOM if left of center
            if ((val <= _dPeakPoint) && (val >= (_dPeakPoint - _dLeftOffset)))
            {
                var grad = 1.0 / _dLeftOffset;

                return grad * (val - (_dPeakPoint - _dLeftOffset));
            }
            //find DOM if right of center
            else if ((val > _dPeakPoint) && (val < (_dPeakPoint + _dRightOffset)))
            {
                var grad = 1.0 / -_dRightOffset;

                return grad * (val - _dPeakPoint) + 1.0;
            }
            //out of range of this FLV, return zero
            else
            {
                return 0.0;
            }
        }

    }
}
