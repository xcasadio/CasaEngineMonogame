namespace CasaEngine.AI.FuzzyLogic
{
    public class FuzzySetLeftShoulder
        : FuzzySet
    {
        readonly double _dPeakPoint;
        readonly double _dRightOffset;
        readonly double _dLeftOffset;





        public FuzzySetLeftShoulder(double peak, double leftOffset, double rightOffset) :
            base(((peak - leftOffset) + peak) / 2)
        {
            _dPeakPoint = peak;
            _dLeftOffset = leftOffset;
            _dRightOffset = rightOffset;
        }



        public override double CalculateDom(double val)
        {
            //test for the case where the left or right offsets are zero
            //(to prevent divide by zero errors below)
            if ((_dRightOffset == 0.0 && _dPeakPoint == val) ||
                 (_dLeftOffset == 0.0 && _dPeakPoint == val))
            {
                return 1.0;
            }
            //find DOM if right of center
            else if ((val >= _dPeakPoint) && (val < (_dPeakPoint + _dRightOffset)))
            {
                var grad = 1.0 / -_dRightOffset;

                return grad * (val - _dPeakPoint) + 1.0;
            }
            //find DOM if left of center
            else if ((val < _dPeakPoint) && (val >= _dPeakPoint - _dLeftOffset))
            {
                return 1.0;
            }
            else //out of range of this FLV, return zero
            {
                return 0.0;
            }
        }

    }
}
