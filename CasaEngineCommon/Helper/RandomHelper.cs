#region File Description
//-----------------------------------------------------------------------------
// RandomHelper.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

#region Using directives
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Text;
#endregion

namespace CasaEngineCommon.Helper
{
    /// <summary>
    /// Random helper
    /// </summary>
    public static class RandomHelper
    {
        #region Variables
        /// <summary>
        /// Global random generator
        /// </summary>
        public static Random globalRandomGenerator =
            GenerateNewRandomGenerator();
        #endregion

        #region Generate a new random generator
        /// <summary>
        /// Generate a new random generator with help of
        /// WindowsHelper.GetPerformanceCounter.
        /// Also used for all GetRandom methods here.
        /// </summary>
        /// <returns>Random</returns>
        public static Random GenerateNewRandomGenerator()
        {
            globalRandomGenerator =
                new Random((int)DateTime.Now.Ticks);
            //needs Interop: (int)WindowsHelper.GetPerformanceCounter());
            return globalRandomGenerator;
        }
        #endregion

        #region Get random float and byte methods

		/// <summary>
		/// Get random int
		/// </summary>
		/// <returns>Int</returns>
		public static int GetRandomInt()
		{
			return globalRandomGenerator.Next();
		}

        /// <summary>
        /// Get random int
        /// </summary>
        /// <param name="max">Maximum</param>
        /// <returns>Int</returns>
        public static int GetRandomInt(int max)
        {
            return globalRandomGenerator.Next(max);
        }

		/// <summary>
		/// Get random int
		/// </summary>
		/// <param name="min">Minimum</param>
		/// <param name="max">Maximum</param>
		/// <returns>Int</returns>
		public static int GetRandomInt(int min, int max)
		{
			return globalRandomGenerator.Next(min, max);
		}

        /// <summary>
        /// Get random float between min and max
        /// </summary>
        /// <param name="min">Min</param>
        /// <param name="max">Max</param>
        /// <returns>Float</returns>
        public static float GetRandomFloat(float min, float max)
        {
            return (float)globalRandomGenerator.NextDouble() * (max - min) + min;
        }

		/// <summary>
		/// Returns a random boolean value.
		/// </summary>
		static public bool NextBool()
		{
			return GetRandomInt(2) == 1;
		}

        /// <summary>
        /// Get random byte between min and max
        /// </summary>
        /// <param name="min">Min</param>
        /// <param name="max">Max</param>
        /// <returns>Byte</returns>
        public static byte GetRandomByte(byte min, byte max)
        {
            return (byte)(globalRandomGenerator.Next(min, max));
        }

        /// <summary>
        /// Get random Vector2
        /// </summary>
        /// <param name="min">Minimum for each component</param>
        /// <param name="max">Maximum for each component</param>
        /// <returns>Vector2</returns>
        public static Vector2 GetRandomVector2(float min, float max)
        {
            return new Vector2(
                GetRandomFloat(min, max),
                GetRandomFloat(min, max));
        }

        /// <summary>
        /// Get random Vector3
        /// </summary>
        /// <param name="min">Minimum for each component</param>
        /// <param name="max">Maximum for each component</param>
        /// <returns>Vector3</returns>
        public static Vector3 GetRandomVector3(float min, float max)
        {
            return new Vector3(
                GetRandomFloat(min, max),
                GetRandomFloat(min, max),
                GetRandomFloat(min, max));
        }

        /// <summary>
        /// Get random color
        /// </summary>
        /// <returns>Color</returns>
        public static Color RandomColor
        {
            get
            {
                return new Color(new Vector3(
                    GetRandomFloat(0f, 1.0f),
                    GetRandomFloat(0f, 1.0f),
                    GetRandomFloat(0f, 1.0f)));
            }
        }

        /// <summary>
        /// Get random normal Vector3
        /// </summary>
        /// <returns>Vector3</returns>
        public static Vector3 RandomNormalVector3
        {
            get
            {
                Vector3 randomNormalVector = new Vector3(
                    GetRandomFloat(-1.0f, 1.0f),
                    GetRandomFloat(-1.0f, 1.0f),
                    GetRandomFloat(-1.0f, 1.0f));
                randomNormalVector.Normalize();
                return randomNormalVector;
            }
        }
        #endregion

		/// <summary>
		/// Returns a random variation of the specified value.
		/// </summary>
		/// <param name="value">The value.</param>
		/// <param name="variation">The variation multiple of the value.</param>
		/// <example>a value of 10 with a variation of 0.5 will result in a random number between 5.0 and 15.</example>
		static public float Variation(float value, float variation)
		{
			float min = (value - variation),
				  max = (value + variation);

			return GetRandomFloat(min, max);
		}

		/// <summary>
		/// Chooses a random item from the specified parameters and returns it.
		/// </summary>
		static public int Choose(params int[] values)
		{
			int index = GetRandomInt(values.Length);

			return values[index];
		}

		/// <summary>
		/// Chooses a random item from the specified parameters and returns it.
		/// </summary>
		static public float Choose(params float[] values)
		{
			int index = GetRandomInt(values.Length);

			return values[index];
		}

		/// <summary>
		/// Chooses a random item from the specified parameters and returns it.
		/// </summary>
		static public T Choose<T>(params T[] values)
		{
			int index = GetRandomInt(values.Length);

			return values[index];
		}
	}
}
