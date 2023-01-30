namespace CasaEngine.AI.Fuzzy
{
    public class FuzzySet_LeftShoulder
        : FuzzySet
    {
        readonly double m_dPeakPoint;
        readonly double m_dRightOffset;
        readonly double m_dLeftOffset;





        public FuzzySet_LeftShoulder(double peak, double LeftOffset, double RightOffset) :
            base(((peak - LeftOffset) + peak) / 2)
        {
            m_dPeakPoint = peak;
            m_dLeftOffset = LeftOffset;
            m_dRightOffset = RightOffset;
        }



        public override double CalculateDOM(double val)
        {
            //test for the case where the left or right offsets are zero
            //(to prevent divide by zero errors below)
            if ((m_dRightOffset == 0.0 && m_dPeakPoint == val) ||
                 (m_dLeftOffset == 0.0 && m_dPeakPoint == val))
            {
                return 1.0;
            }
            //find DOM if right of center
            else if ((val >= m_dPeakPoint) && (val < (m_dPeakPoint + m_dRightOffset)))
            {
                double grad = 1.0 / -m_dRightOffset;

                return grad * (val - m_dPeakPoint) + 1.0;
            }
            //find DOM if left of center
            else if ((val < m_dPeakPoint) && (val >= m_dPeakPoint - m_dLeftOffset))
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
