using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace CasaEngine.Math.Curves
{
	/// <summary>
	/// Bezier curve, Causteljau algorithm
	/// </summary>
	public class Bezier2DCurve
	{
		#region Fields

		private List<Vector2> m_ControlPoints = new List<Vector2>();
		private List<Vector2> m_CurvePoints = new List<Vector2>();

		private float m_Resolution = 0.1f;

		#endregion // Fields

		#region Properties

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

		#endregion

		#region Constructors



		#endregion

		#region Methods

		/// <summary>
		/// Casteljau Algorithm
		/// </summary>
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

		/// <summary>
		/// 
		/// </summary>
		/// <param name="ControlPoints_"></param>
		/// <param name="t_">Between 0 and 1</param>
		/// <returns></returns>
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

					U.X = (1.0f - t_)*A.X + t_*B.X;
					U.Y = (1.0f - t_)*A.Y + t_*B.Y;
					//U.Z = (1.0f - t_)*A.Z + t_*B.Z;

					pointTemp[i] = U;
				}
			}

			return pointTemp[0];
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="percent_">Between 0 and 1</param>
		/// <returns></returns>
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

			return Bezier2DCurve.Casteljau(m_ControlPoints, percent_);
		}

		#endregion
	}
}
