
#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

#endregion

namespace CasaEngineCommon.Extension
{
	/// <summary>
	/// Fonction utile pour l'ecriture/lecture binaire
	/// Only read for game project
	/// </summary>
	static public class BinaryIOExtension
    {
        #region Write

		/// <summary>
		/// 
		/// </summary>
		/// <param name="binW_"></param>
		/// <param name="vec_"></param>
		static public void Write(this BinaryWriter binW_, Vector4 vec_)
		{
			binW_.Write(vec_.X);
			binW_.Write(vec_.Y);
			binW_.Write(vec_.Z);
			binW_.Write(vec_.W);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="binW_"></param>
		/// <param name="vec_"></param>
		static public void Write(this BinaryWriter binW_, Vector3 vec_)
		{
			binW_.Write(vec_.X);
			binW_.Write(vec_.Y);
			binW_.Write(vec_.Z);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="binW_"></param>
		/// <param name="vec_"></param>
		static public void Write(this BinaryWriter binW_, Vector2 vec_)
		{
			binW_.Write(vec_.X);
			binW_.Write(vec_.Y);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="binW_"></param>
		/// <param name="q_"></param>
		static public void Write(this BinaryWriter binW_, Quaternion q_)
		{
			binW_.Write(q_.W);
			binW_.Write(q_.X);
			binW_.Write(q_.Y);
			binW_.Write(q_.Z);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="binW_"></param>
		/// <param name="mat_"></param>
		static public void Write(this BinaryWriter binW_, Matrix mat_)
		{
			binW_.Write(mat_.M11);
			binW_.Write(mat_.M12);
			binW_.Write(mat_.M13);
			binW_.Write(mat_.M14);
			binW_.Write(mat_.M21);
			binW_.Write(mat_.M22);
			binW_.Write(mat_.M23);
			binW_.Write(mat_.M24);
			binW_.Write(mat_.M31);
			binW_.Write(mat_.M32);
			binW_.Write(mat_.M33);
			binW_.Write(mat_.M34);
			binW_.Write(mat_.M41);
			binW_.Write(mat_.M42);
			binW_.Write(mat_.M43);
			binW_.Write(mat_.M44);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="binWriter_"></param>
		/// <param name="b_"></param>
		static public void Write(this BinaryWriter binWriter_, BoundingBox b_)
		{
			binWriter_.Write(b_.Max);
			binWriter_.Write(b_.Min);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="binWriter_"></param>
		/// <param name="c_"></param>
		static public void Write(this BinaryWriter binWriter_, Color c_)
		{
			binWriter_.Write(c_.ToVector4());
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="binWriter_"></param>
		/// <param name="r_"></param>
		static public void Write(this BinaryWriter binWriter_, Rectangle r_)
		{
			binWriter_.Write(r_.X);
			binWriter_.Write(r_.Y);
			binWriter_.Write(r_.Width);
			binWriter_.Write(r_.Height);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="binWriter_"></param>
		/// <param name="p_"></param>
		static public void Write(this BinaryWriter binWriter_, Point p_)
		{
			binWriter_.Write(p_.X);
			binWriter_.Write(p_.Y);
		}

        #endregion

        #region Read

		/// <summary>
		/// 
		/// </summary>
		/// <param name="binR_"></param>
		/// <returns></returns>
		static public Vector4 ReadVector4(this BinaryReader binR_)
		{
			Vector4 vec_ = new Vector4();

			vec_.X = binR_.ReadSingle();
			vec_.Y = binR_.ReadSingle();
			vec_.Z = binR_.ReadSingle();
			vec_.W = binR_.ReadSingle();

			return vec_;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="binR_"></param>
		/// <returns></returns>
		static public Vector3 ReadVector3(this BinaryReader binR_)
		{
			Vector3 vec_ = new Vector3();

			vec_.X = binR_.ReadSingle();
			vec_.Y = binR_.ReadSingle();
			vec_.Z = binR_.ReadSingle();

			return vec_;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="binR_"></param>
		/// <returns></returns>
		static public Vector2 ReadVector2(this BinaryReader binR_)
		{
			Vector2 vec_ = new Vector2();

			vec_.X = binR_.ReadSingle();
			vec_.Y = binR_.ReadSingle();

			return vec_;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="binR_"></param>
		/// <returns></returns>
		static public Quaternion ReadQuaternion(this BinaryReader binR_)
		{
			Quaternion q_ = new Quaternion();

			q_.W = binR_.ReadSingle();
			q_.X = binR_.ReadSingle();
			q_.Y = binR_.ReadSingle();
			q_.Z = binR_.ReadSingle();

			return q_;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="binR_"></param>
		/// <returns></returns>
		static public Matrix ReadMatrix(this BinaryReader binR_)
		{
			Matrix mat_ = new Matrix();
			mat_.M11 = binR_.ReadSingle();
			mat_.M12 = binR_.ReadSingle();
			mat_.M13 = binR_.ReadSingle();
			mat_.M14 = binR_.ReadSingle();
			mat_.M21 = binR_.ReadSingle();
			mat_.M22 = binR_.ReadSingle();
			mat_.M23 = binR_.ReadSingle();
			mat_.M24 = binR_.ReadSingle();
			mat_.M31 = binR_.ReadSingle();
			mat_.M32 = binR_.ReadSingle();
			mat_.M33 = binR_.ReadSingle();
			mat_.M34 = binR_.ReadSingle();
			mat_.M41 = binR_.ReadSingle();
			mat_.M42 = binR_.ReadSingle();
			mat_.M43 = binR_.ReadSingle();
			mat_.M44 = binR_.ReadSingle();

			return mat_;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="binReader_"></param>
		/// <returns></returns>
		static public BoundingBox ReadBoundingBox(this BinaryReader binReader_)
		{
			BoundingBox b = new BoundingBox();

			b.Max = binReader_.ReadVector3();
			b.Min = binReader_.ReadVector3();

			return b;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="binReader_"></param>
		/// <returns></returns>
		static public Color ReadColor(this BinaryReader binReader_)
		{
			return new Color(binReader_.ReadVector4());
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="binReader_"></param>
		/// <returns></returns>
		static public Rectangle ReadRectangle(this BinaryReader binReader_)
		{
			return new Rectangle(binReader_.ReadInt32(), binReader_.ReadInt32(), binReader_.ReadInt32(), binReader_.ReadInt32());
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="binR_"></param>
		/// <returns></returns>
		static public Point ReadPoint(this BinaryReader binR_)
		{
			Point p = new Point();

			p.X = binR_.ReadInt32();
			p.Y = binR_.ReadInt32();

			return p;
        }

        #endregion
    }
}
