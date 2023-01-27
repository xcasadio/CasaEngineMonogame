#region File Description
//-----------------------------------------------------------------------------
// Vector3Helper.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

#region Using directives
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
#endregion

namespace CasaEngineCommon.Helper
{
    /// <summary>
    /// Vector 3 helper
    /// </summary>
    public static class Vector3Helper
    {
        #region GetAngleBetweenVectors
        /// <summary>
        /// Return angle between two vectors. Used for visibility testing and
        /// for checking angles between vectors for the road sign generation.
        /// </summary>
        /// <param name="vec1">Vector 1</param>
        /// <param name="vec2">Vector 2</param>
        /// <returns>Float</returns>
        public static float GetAngleBetweenVectors(Vector3 vec1, Vector3 vec2)
        {
            // See http://en.wikipedia.org/wiki/Vector_(spatial)
            // for help and check out the Dot Product section ^^
            // Both vectors are normalized so we can save deviding through the
            // lengths.
            return MathHelper.Acos(Vector3.Dot(vec1, vec2));
        }

		/// <summary>
		/// Return angle between two vectors. Used for visibility testing and
		/// for checking angles between vectors for the road sign generation.
		/// </summary>
		/// <param name="vec1">Vector 1</param>
		/// <param name="vec2">Vector 2</param>
		/// <returns>Float</returns>
		public static float GetAngleBetweenVectors( Vector3 axis_, Vector3 vec1_, Vector3 vec2_ )
		{
			Matrix mat = new Matrix( vec1_.X, vec1_.Y, vec1_.Z, 0.0f, vec2_.X, vec2_.Y, vec2_.Z, 0.0f, axis_.X, axis_.Y, axis_.Z, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f );

			float coeff = 1.0f;

			if ( mat.Determinant() < 0.0f )
			{
				coeff = -1.0f;
			}

            return coeff * MathHelper.Acos(Vector3.Dot(vec1_, vec2_));
		}

		/// <summary>
		/// Return matrix rotation between two vectors
		/// </summary>
		/// <param name="vec1">Vector 1</param>
		/// <param name="vec2">Vector 2</param>
		/// <returns></returns>
		public static Matrix GetRotationMatrixBetweenVectors(Vector3 vec1_, Vector3 vec2_ )
		{
			Vector3 axis = Vector3.Cross( vec1_, vec2_ );
			return GetRotationMatrixBetweenVectors( axis, vec1_, vec2_ );
		}

		/// <summary>
		/// Return matrix rotation between two vectors
		/// </summary>
		/// <param name="vec1">Vector 1</param>
		/// <param name="vec2">Vector 2</param>
		/// <returns></returns>
		public static Matrix GetRotationMatrixBetweenVectors( Vector3 axis_, Vector3 vec1_, Vector3 vec2_ )
		{
			/*Matrix mat = new Matrix( vec1_.X, vec1_.Y, vec1_.Z, 0.0f, vec2_.X, vec2_.Y, vec2_.Z, 0.0f, axis_.X, axis_.Y, axis_.Z, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f );

			float coeff = 1.0f;

			if ( mat.Determinant() < 0.0f )
			{
				coeff = -1.0f;
			}*/

			return Matrix.CreateFromAxisAngle( axis_,/* coeff * */GetAngleBetweenVectors(axis_, vec1_, vec2_ ) );
		}

		/// <summary>
		/// Return quaternion between two vectors
		/// </summary>
		/// <param name="vec1">Vector 1</param>
		/// <param name="vec2">Vector 2</param>
		/// <returns></returns>
		public static Quaternion GetQuaternionBetweenVectors(Vector3 vec1_, Vector3 vec2_)
		{
			Vector3 axis = Vector3.Cross(vec1_, vec2_);
			return GetQuaternionBetweenVectors(axis, vec1_, vec2_);
		}

		/// <summary>
		/// Return quaternion between two vectors
		/// </summary>
		/// <param name="vec1">Vector 1</param>
		/// <param name="vec2">Vector 2</param>
		/// <returns></returns>
		public static Quaternion GetQuaternionBetweenVectors(Vector3 axis_, Vector3 vec1_, Vector3 vec2_)
		{
			/*Matrix mat = new Matrix(vec1_.X, vec1_.Y, vec1_.Z, 0.0f, vec2_.X, vec2_.Y, vec2_.Z, 0.0f, axis_.X, axis_.Y, axis_.Z, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);

			float coeff = 1.0f;

			if (mat.Determinant() < 0.0f)
			{
				coeff = -1.0f;
			}
			
			return Quaternion.CreateFromAxisAngle(axis_, coeff * GetAngleBetweenVectors(vec1_, vec2_));
			*/

			//float fDot = Vector3.Dot(vec1_, vec2_);
			//return new Quaternion(axis_.X, axis_.Y, axis_.Z, fDot);
			return Quaternion.CreateFromAxisAngle(axis_, GetAngleBetweenVectors(axis_, vec1_, vec2_));
		}

        #endregion

        #region DistanceToLine
        /// <summary>
        /// Distance from our point to the line described by linePos1 and linePos2.
        /// </summary>
        /// <param name="point">Point</param>
        /// <param name="linePos1">Line position 1</param>
        /// <param name="linePos2">Line position 2</param>
        /// <returns>Float</returns>
        public static float DistanceToLine(Vector3 point,
            Vector3 linePos1, Vector3 linePos2)
        {
            // For help check out this article:
            // http://mathworld.wolfram.com/Point-LineDistance3-Dimensional.html
            Vector3 lineVec = linePos2 - linePos1;
            Vector3 pointVec = linePos1 - point;
            return Vector3.Cross(lineVec, pointVec).Length() / lineVec.Length();
        }
        #endregion

        #region SignedDistanceToPlane
        /// <summary>
        /// Signed distance to plane
        /// </summary>
        /// <param name="point">Point</param>
        /// <param name="planePosition">Plane position</param>
        /// <param name="planeNormal">Plane normal</param>
        /// <returns>Float</returns>
        public static float SignedDistanceToPlane(Vector3 point,
            Vector3 planePosition, Vector3 planeNormal)
        {
            Vector3 pointVec = planePosition - point;
            return Vector3.Dot(planeNormal, pointVec);
        }
        #endregion

		#region Truncate

		/// <summary>
		/// Truncates a vector
		/// </summary>
		/// <param name="vector">Vector to truncate</param>
		/// <param name="max">Maximum value of the length of the vector</param>
		/// <returns>The new vector truncated</returns>
		public static Vector3 Truncate(this Vector3 vector, float max)
		{
			float len = vector.Length();
			if (len > max)
			{
				// Do it this way so we're only computing length once, instead of forcing Vector3 to do it too.
				vector *= max / len;
			}
			return vector;
		}

		/// <summary>
		/// Truncates a vector
		/// </summary>
		/// <param name="vector">Vector to truncate</param>
		/// <param name="max">Maximum value of the length of the vector</param>
		public static void Truncate(ref Vector3 vector, float max)
		{
			float len = vector.Length();
			if (len > max)
			{
				// Do it this way so we're only computing length once, instead of forcing Vector3 to do it too.
				vector *= max / len;
			}
		}

		#endregion
	}
}
