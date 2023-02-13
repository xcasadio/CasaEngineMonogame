using Microsoft.Xna.Framework;

namespace CasaEngine.Core_Systems.Curves
{
    public class Bezier3DCurve
    {

        private readonly List<Vector3> _controlPoints = new();
        private readonly List<Vector3> _curvePoints = new();

        private float _resolution = 0.1f;



        public float Resolution
        {
            get => _resolution;
            set => _resolution = value;
        }

        public List<Vector3> CurvePoints => _curvePoints;

        public List<Vector3> ControlPoints => _controlPoints;


        public void Compute()
        {
            if (_controlPoints.Count == 0)
            {
                return;
            }

            _curvePoints.Clear();

            var nbPoint = (int)(1.0f / (float)_resolution);

            //Algo
            for (var u = 0; u <= nbPoint; u++)
            {
                _curvePoints.Add(Casteljau(_controlPoints, (float)u / (float)nbPoint));
            }
        }

        public static Vector3 Casteljau(List<Vector3> controlPoints, float t)
        {
            if (t > 1.0f || t < 0.0f)
            {
                throw new ArgumentOutOfRangeException("Bezier2D.Casteljau() : t must be contains in [0;1]");
            }

            //Algo init
            var pointTemp = new List<Vector3>();
            int max;

            pointTemp.AddRange(controlPoints);

            //calcul des points avec reduction de l'arbre a chaque etape
            Vector3 a, b, u;

            for (max = controlPoints.Count; max > 1; max--)
            {
                for (var i = 0; i < max - 1; i++)
                {
                    a = pointTemp[i];
                    b = pointTemp[i + 1];

                    u.X = (1.0f - t) * a.X + t * b.X;
                    u.Y = (1.0f - t) * a.Y + t * b.Y;
                    u.Z = (1.0f - t) * a.Z + t * b.Z;

                    pointTemp[i] = u;
                }
            }

            return pointTemp[0];
        }

        public Vector3 GetPointFast(float percent)
        {
            if (percent < 0.0f || percent > 1.0f)
            {
                throw new ArgumentOutOfRangeException("percent_ has to be between 0 and 1");
            }

            if (_curvePoints.Count == 0)
            {
                throw new InvalidOperationException("Bezier3DCurve.GetPointFast() : please compute curve before call this function");
            }

            return _curvePoints[(int)(percent * (float)_curvePoints.Count)];
        }

        public Vector3 GetPoint(float percent)
        {
            if (percent < 0.0f || percent > 1.0f)
            {
                throw new ArgumentOutOfRangeException("percent_ has to be between 0 and 1");
            }

            return Casteljau(_controlPoints, percent);
        }

    }
}
