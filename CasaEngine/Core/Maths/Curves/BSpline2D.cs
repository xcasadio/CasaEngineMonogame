using Microsoft.Xna.Framework;
using MathHelper = CasaEngine.Core.Helper.MathHelper;

namespace CasaEngine.Core.Maths.Curves;

public class BSpline2D
{
    public enum VectorModalParametrization
    {
        Uniform,
        ArcLength,
        Centripetal
    }


    private readonly List<Vector2> _controlPoints = new();
    private readonly List<float> _modalNodes = new();
    private readonly List<Vector2> _curvePoints = new();

    private readonly int _degree = 3;
    private readonly bool _closed = false;
    private readonly bool _beginToBounds = true;
    private float _resolution = 0.1f;
    private readonly VectorModalParametrization _vectorModalParametrization = VectorModalParametrization.Uniform;



    public float Resolution
    {
        get => _resolution;
        set => _resolution = value;
    }

    public List<Vector2> CurvePoints => _curvePoints;

    public List<Vector2> ControlPoints => _controlPoints;


    public void Compute()
    {
        if (_controlPoints.Count == 0)
        {
            return;
        }

        var ptControl = new List<Vector2>();

        _curvePoints.Clear();

        //Algo
        if (_controlPoints.Count >= _degree && _closed == false
            || _closed && _controlPoints.Count > _degree)
        {
            if (_beginToBounds)
            {
                for (var i = 0; i < _degree; i++)
                {
                    ptControl.Add(_controlPoints.First());
                }
            }

            //copie temporaire
            for (var i = 0; i < _controlPoints.Count; i++)
            {
                ptControl.Add(_controlPoints[i]);
            }

            //le cas de la spline partant des extremitées
            if (_beginToBounds)
            {
                for (var i = 0; i < _degree; i++)
                {
                    ptControl.Add(ptControl.Last());
                }
            }
            else if (_beginToBounds) // bspline fermée
            {
                for (var i = 0; i <= _degree; i++)
                {
                    ptControl.Add(_controlPoints[i]);
                }
            }

            ModalVectorParametrization(ptControl);

            // Pour chaque sous courbes Bezier de la spline de degrée _Degree
            int r;
            float t;

            for (r = _degree; r < ptControl.Count; r++)
            {
                // discrétisation de la sous spline
                // ayant pour paramètre t : appartient [ t[r], t[r+1] ]
                for (t = _modalNodes[r]; t <= _modalNodes[r + 1]; t += _resolution)
                {
                    /*
                     *  Composée de B-Spline définie par:
                     *  S(t) = Somme( 0, n){ Pi * B(n, i)(t) }
                     *  avec k le degrée, n le nombre de point de contrôle
                     */
                    _curvePoints.Add(Cox_de_Boor(r, t, ptControl));
                }
            }
        }
    }

    private Vector2 Cox_de_Boor(int r, float t, List<Vector2> ptControl)
    {
        var pt = new List<Vector2>(_degree * (r + 1));
        float x, y;

        for (var i = 0; i <= _degree * (r + 1); i++)
        {
            pt.Add(Vector2.Zero);
        }

        // Initialisation
        for (var i = r - _degree; i <= r; i++)
        {
            pt[0 * _degree + i] = ptControl[i];
        }

        for (var j = 1; j <= _degree; j++)
        {
            for (var i = r - _degree + j; i <= r; i++)
            {
                x = (pt[(j - 1) * _degree + i].X * (t - _modalNodes[i]) + pt[(j - 1) * _degree + i - 1].X * (_modalNodes[i - j + _degree + 1] - t)) / (_modalNodes[i - j + _degree + 1] - _modalNodes[i]);
                y = (pt[(j - 1) * _degree + i].Y * (t - _modalNodes[i]) + pt[(j - 1) * _degree + i - 1].Y * (_modalNodes[i - j + _degree + 1] - t)) / (_modalNodes[i - j + _degree + 1] - _modalNodes[i]);
                pt[j * _degree + i] = new Vector2(x, y);
            }
        }

        return pt[_degree * _degree + r];
    }

    private void ModalVectorParametrization(List<Vector2> ptControl)
    {
        var nbVectorModal = ptControl.Count;
        var offset = 0;
        float t;

        //init
        _modalNodes.Clear();

        if (_beginToBounds)
        {
            offset = _degree;
            nbVectorModal -= _degree - 1;

            /*for( int i = 0; i < _Degree; i++)
            {
                //_ModalNodes.Add( 0.0f );
            }*/
        }

        /*if ( _Closed )
        {
            //nbVectorModal += _Degree;
        }*/

        switch (_vectorModalParametrization)
        {
            case VectorModalParametrization.Uniform:
                for (var i = 0; i <= ptControl.Count + _degree; i++)
                {
                    _modalNodes.Add(i);
                }
                break;

            case VectorModalParametrization.ArcLength: //bug
                //for( int i = 0; i < nbVectorModal; i++)
                //{
                _modalNodes.Add(0.0f);
                _modalNodes.Add(1.0f);

                for (var i = 2; i < nbVectorModal; i++)
                {
                    var t1 = _modalNodes[i - 1];
                    var t2 = _modalNodes[i - 1] - _modalNodes[i - 2];
                    t = t1 + t2;
                    var xx = Vector2.Distance(ptControl[i + offset], ptControl[i - 1 + offset]);
                    var yy = Vector2.Distance(ptControl[i - 1 + offset], ptControl[i - 2 + offset]);

                    if (Vector2.Distance(ptControl[i - 1 + offset], ptControl[i - 2 + offset]) != 0.0f)
                    {
                        t *= Vector2.Distance(ptControl[i + offset], ptControl[i - 1 + offset]) / Vector2.Distance(ptControl[i - 1 + offset], ptControl[i - 2 + offset]);
                    }
                    else
                    {
                        t = 0.0f;
                    }

                    _modalNodes.Add(t);
                }

                for (var i = _modalNodes.Count; i < ptControl.Count + _degree; i++)
                {
                    _modalNodes.Add(_modalNodes.Last());
                }
                //}
                break;

            case VectorModalParametrization.Centripetal: //bug
                _modalNodes.Add(0.0f);
                _modalNodes.Add(1.0f);

                for (var i = 2; i < nbVectorModal; i++)
                {
                    if (Vector2.Distance(ptControl[i - 1], ptControl[i - 2]) == 0.0f)
                    {
                        t = 0.0f;
                    }
                    else
                    {
                        t = _modalNodes[i - 1] + (_modalNodes[i - 1] - _modalNodes[i - 2]);
                        t *= MathHelper.Sqrt(Vector2.Distance(ptControl[i], ptControl[i - 1]) / Vector2.Distance(ptControl[i - 1], ptControl[i - 2]));
                    }

                    _modalNodes.Add(t);
                }

                for (var i = 0; i <= _degree; i++)
                {
                    _modalNodes.Add(nbVectorModal + i);
                }

                break;
        }

        /*if ( _BeginToBounds )
        {
            for( int i = 0; i < _Degree; i++)
            {
                //_ModalNodes.Add( _ModalNodes.Last() );
            }
        }*/
    }

    public Vector2 GetPoint(float percent)
    {
        if (percent < 0.0f || percent > 1.0f)
        {
            throw new ArgumentOutOfRangeException("percent_ has to be between 0 and 1");
        }

        return _curvePoints[(int)(percent * _curvePoints.Count)];
    }

}