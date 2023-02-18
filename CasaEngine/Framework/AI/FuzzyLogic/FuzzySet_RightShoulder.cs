namespace CasaEngine.Framework.AI.FuzzyLogic
{
    public class FuzzySetRightShoulder
        : FuzzySet
    {
        readonly double _dPeakPoint;
        readonly double _dLeftOffset;
        readonly double _dRightOffset;





        public FuzzySetRightShoulder(double peak, double leftOffset, double rightOffset)
            : base((peak + rightOffset + peak) / 2)
        {
            _dPeakPoint = peak;
            _dLeftOffset = leftOffset;
            _dRightOffset = rightOffset;
        }



        public override double CalculateDom(double val)
        {
            //test for the case where the left or right offsets are zero
            //(to prevent divide by zero errors below)
            if (_dRightOffset == 0.0 && _dPeakPoint == val ||
                 _dLeftOffset == 0.0 && _dPeakPoint == val)
            {
                return 1.0;
            }

            //find DOM if left of center
            else if (val <= _dPeakPoint && val > _dPeakPoint - _dLeftOffset)
            {
                var grad = 1.0 / _dLeftOffset;

                return grad * (val - (_dPeakPoint - _dLeftOffset));
            }
            //find DOM if right of center and less than center + right offset
            else if (val > _dPeakPoint && val <= _dPeakPoint + _dRightOffset)
            {
                return 1.0;
            }

            else
            {
                return 0;
            }
        }

    }
}
