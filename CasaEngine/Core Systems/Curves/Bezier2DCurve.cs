using Microsoft.Xna.Framework;

namespace CasaEngine.Math.Curves
{
    public class Bezier2DCurve
    {

        private readonly List<Vector2> m_ControlPoints = new List<Vector2>();
        private readonly List<Vector2> m_CurvePoints = new List<Vector2>();

        private float m_Resolution = 0.1f;



        public float Resolution
        {
            get => m_Resolution;
            set => m_Resolution = value;
        }

        public List<Vector2> CurvePoints => m_CurvePoints;

        public List<Vector2> ControlPoints => m_ControlPoints;


        public void Compute()
        {
            if (m_ControlPoints.Count == 0)
            {
                return;
            }

            m_CurvePoints.Clear();

            int nbPoint = (int)(1.0f / (float)m_Resolution);

            //Algo
            for (int u = 0; u <= nbPoint; u++)
            {
                m_CurvePoints.Add(Bezier2DCurve.Casteljau(m_ControlPoints, (float)u / (float)nbPoint));
            }
        }

        static public Vector2 Casteljau(List<Vector2> ControlPoints_, float t_)
        {
            if (t_ > 1.0f || t_ < 0.0f)
            {
                throw new ArgumentOutOfRangeException("Bezier2D.Casteljau() : t must be contains in [0;1]");
            }

            //Algo init
            List<Vector2> pointTemp = new List<Vector2>();
            int max;

            pointTemp.AddRange(ControlPoints_);

            //calcul des points avec reduction de l'arbre a chaque etape
            Vector2 A, B, U;

            for (max = ControlPoints_.Count; max > 1; max--)
            {
                for (int i = 0; i < max - 1; i++)
                {
                    A = pointTemp[i];
                    B = pointTemp[i + 1];

                    U.X = (1.0f - t_) * A.X + t_ * B.X;
                    U.Y = (1.0f - t_) * A.Y + t_ * B.Y;
                    //U.Z = (1.0f - t_)*A.Z + t_*B.Z;

                    pointTemp[i] = U;
                }
            }

            return pointTemp[0];
        }

        public Vector2 GetPointFast(float percent_)
        {
            if (percent_ < 0.0f || percent_ > 1.0f)
            {
                throw new ArgumentOutOfRangeException("percent_ has to be between 0 and 1");
            }

            if (m_CurvePoints.Count == 0)
            {
                throw new InvalidOperationException("Bezier2DCurve.GetPointFast() : please compute curve before call this function");
            }

            return m_CurvePoints[(int)(percent_ * (float)m_CurvePoints.Count)];
        }

        public Vector2 GetPoint(float percent_)
        {
            if (percent_ < 0.0f || percent_ > 1.0f)
            {
                throw new ArgumentOutOfRangeException("percent_ has to be between 0 and 1");
            }

            return Bezier2DCurve.Casteljau(m_ControlPoints, percent_);
        }

    }
}
