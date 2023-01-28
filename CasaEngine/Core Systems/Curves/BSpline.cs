using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace CasaEngine.Math.Curves
{
    /// <summary>
    /// BSpline curve, Cox de Boor algorithm
    /// </summary>
    public class BSpline
    {
        /// <summary>
        /// vector modal parametrization method
        /// </summary>
        public enum VectorModalParametrization
        {
            Uniform,
            ArcLength,
            Centripetal
        }


        private List<Vector2> m_ControlPoints = new List<Vector2>();
        private List<float> m_ModalNodes = new List<float>();
        private List<Vector2> m_CurvePoints = new List<Vector2>();

        private int m_Degree = 3;
        private bool m_Closed = false;
        private bool m_BeginToBounds = true;
        private float m_Resolution = 0.1f;
        private VectorModalParametrization m_VectorModalParametrization = VectorModalParametrization.Uniform;



        /// <summary>
        /// Gets/Sets
        /// </summary>
        public float Resolution
        {
            get { return m_Resolution; }
            set { m_Resolution = value; }
        }

        /// <summary>
        /// Gets
        /// </summary>
        public List<Vector2> CurvePoints
        {
            get { return m_CurvePoints; }
        }

        /// <summary>
        /// Gets
        /// </summary>
        public List<Vector2> ControlPoints
        {
            get { return m_ControlPoints; }
        }



        /// <summary>
        /// Cox de Boor Algorithm
        /// </summary>
        public void Compute()
        {
            if (m_ControlPoints.Count == 0)
            {
                return;
            }

            List<Vector2> ptControl = new List<Vector2>();

            m_CurvePoints.Clear();

            //Algo
            if ((m_ControlPoints.Count >= m_Degree && m_Closed == false)
                || (m_Closed && m_ControlPoints.Count > m_Degree))
            {
                if (m_BeginToBounds)
                {
                    for (int i = 0; i < m_Degree; i++)
                    {
                        ptControl.Add(m_ControlPoints.First());
                    }
                }

                //copie temporaire
                for (int i = 0; i < m_ControlPoints.Count; i++)
                {
                    ptControl.Add(m_ControlPoints[i]);
                }

                //le cas de la spline partant des extremité
                if (m_BeginToBounds)
                {
                    for (int i = 0; i < m_Degree; i++)
                    {
                        ptControl.Add(ptControl.Last());
                    }
                }
                else if (m_BeginToBounds) // bspline fermée
                {
                    for (int i = 0; i <= m_Degree; i++)
                    {
                        ptControl.Add(m_ControlPoints[i]);
                    }
                }

                ModalVectorParametrization(ptControl);

                // Pour chaque sous courbes Bezier de la spline de degré m_Degree
                int r;
                float t;

                for (r = m_Degree; r < ptControl.Count; r++)
                {
                    // discrétisation de la sous spline
                    // ayant pour paramètre t : appartient [ t[r], t[r+1] ]
                    for (t = m_ModalNodes[r]; t <= m_ModalNodes[r + 1]; t += m_Resolution)
                    {
                        /*
						 *  Composée de B-Spline définie par:
						 *  S(t) = Somme( 0, n){ Pi * B(n, i)(t) }
						 *  avec k le degré, n le nombre de point de contrôle
						 */
                        m_CurvePoints.Add(Cox_de_Boor(r, t, ptControl));
                    }
                }
            }
        }

        /// <summary>
        /// Cox de Boor algorithm
        /// </summary>
        /// <param name="r"></param>
        /// <param name="t"></param>
        /// <param name="ptControl_"></param>
        /// <returns></returns>
        Vector2 Cox_de_Boor(int r, float t, List<Vector2> ptControl_)
        {
            List<Vector2> Pt = new List<Vector2>(m_Degree * (r + 1));
            float x, y;

            for (int i = 0; i <= m_Degree * (r + 1); i++)
            {
                Pt.Add(Vector2.Zero);
            }

            // Initialisation
            for (int i = r - m_Degree; i <= r; i++)
            {
                Pt[0 * m_Degree + i] = ptControl_[i];
            }

            for (int j = 1; j <= m_Degree; j++)
            {
                for (int i = r - m_Degree + j; i <= r; i++)
                {
                    x = ((Pt[(j - 1) * m_Degree + i].X * (t - m_ModalNodes[i])) + (Pt[(j - 1) * m_Degree + i - 1].X * (m_ModalNodes[i - j + m_Degree + 1] - t))) / (m_ModalNodes[i - j + m_Degree + 1] - m_ModalNodes[i]);
                    y = ((Pt[(j - 1) * m_Degree + i].Y * (t - m_ModalNodes[i])) + (Pt[(j - 1) * m_Degree + i - 1].Y * (m_ModalNodes[i - j + m_Degree + 1] - t))) / (m_ModalNodes[i - j + m_Degree + 1] - m_ModalNodes[i]);
                    Pt[j * m_Degree + i] = new Vector2(x, y);
                }
            }

            return Pt[m_Degree * m_Degree + r];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ptControl_"></param>
        void ModalVectorParametrization(List<Vector2> ptControl_)
        {
            int nbVectorModal = ptControl_.Count;
            int offset = 0;
            float t;

            //init
            m_ModalNodes.Clear();

            if (m_BeginToBounds)
            {
                offset = m_Degree;
                nbVectorModal -= m_Degree - 1;

                /*for( int i = 0; i < m_Degree; i++)
				{
					//m_ModalNodes.Add( 0.0f );
				}*/
            }

            /*if ( m_Closed )
			{
				//nbVectorModal += m_Degree;
			}*/

            switch (m_VectorModalParametrization)
            {
                case VectorModalParametrization.Uniform:
                    for (int i = 0; i <= ptControl_.Count + m_Degree; i++)
                    {
                        m_ModalNodes.Add((float)i);
                    }
                    break;

                case VectorModalParametrization.ArcLength: //bug
                                                           //for( int i = 0; i < nbVectorModal; i++)
                                                           //{
                    m_ModalNodes.Add(0.0f);
                    m_ModalNodes.Add(1.0f);

                    for (int i = 2; i < nbVectorModal; i++)
                    {
                        float t1 = m_ModalNodes[i - 1];
                        float t2 = (m_ModalNodes[i - 1] - m_ModalNodes[i - 2]);
                        t = t1 + t2;
                        float xx = Vector2.Distance(ptControl_[i + offset], ptControl_[i - 1 + offset]);
                        float yy = Vector2.Distance(ptControl_[i - 1 + offset], ptControl_[i - 2 + offset]);

                        if (Vector2.Distance(ptControl_[i - 1 + offset], ptControl_[i - 2 + offset]) != 0.0f)
                        {
                            t *= Vector2.Distance(ptControl_[i + offset], ptControl_[i - 1 + offset]) / Vector2.Distance(ptControl_[i - 1 + offset], ptControl_[i - 2 + offset]);
                        }
                        else
                        {
                            t = 0.0f;
                        }

                        m_ModalNodes.Add(t);
                    }

                    for (int i = m_ModalNodes.Count; i < ptControl_.Count + m_Degree; i++)
                    {
                        m_ModalNodes.Add(m_ModalNodes.Last());
                    }
                    //}
                    break;

                case VectorModalParametrization.Centripetal: //bug
                    m_ModalNodes.Add(0.0f);
                    m_ModalNodes.Add(1.0f);

                    for (int i = 2; i < nbVectorModal; i++)
                    {
                        if (Vector2.Distance(ptControl_[i - 1], ptControl_[i - 2]) == 0.0f)
                        {
                            t = 0.0f;
                        }
                        else
                        {
                            t = m_ModalNodes[i - 1] + (m_ModalNodes[i - 1] - m_ModalNodes[i - 2]);
                            t *= CasaEngineCommon.Helper.MathHelper.Sqrt(Vector2.Distance(ptControl_[i], ptControl_[i - 1]) / Vector2.Distance(ptControl_[i - 1], ptControl_[i - 2]));
                        }

                        m_ModalNodes.Add(t);
                    }

                    for (int i = 0; i <= m_Degree; i++)
                    {
                        m_ModalNodes.Add(nbVectorModal + i);
                    }

                    break;
            }

            /*if ( m_BeginToBounds )
			{
				for( int i = 0; i < m_Degree; i++)
				{
					//m_ModalNodes.Add( m_ModalNodes.Last() );
				}
			}*/
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="percent_">Between 0 and 1</param>
        /// <returns></returns>
        public Vector2 GetPoint(float percent_)
        {
            if (percent_ < 0.0f || percent_ > 1.0f)
            {
                throw new ArgumentOutOfRangeException("percent_ has to be between 0 and 1");
            }

            return m_CurvePoints[(int)(percent_ * (float)m_CurvePoints.Count)];
        }

    }
}
