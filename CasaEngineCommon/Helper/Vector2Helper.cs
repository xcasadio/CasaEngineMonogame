using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace CasaEngineCommon.Helper
{
    /// <summary>
    /// 
    /// </summary>
    public static class Vector2Helper
    {
        /// <summary>
        /// Return angle between two vectors. Used for visibility testing and
        /// for checking angles between vectors for the road sign generation.
        /// </summary>
        /// <param name="vec1">Vector 1</param>
        /// <param name="vec2">Vector 2</param>
        /// <returns>Float</returns>
        public static float GetAngleBetweenVectors(Vector2 vec1, Vector2 vec2)
        {
            // See http://en.wikipedia.org/wiki/Vector_(spatial)
            // for help and check out the Dot Product section ^^
            // Both vectors are normalized so we can save deviding through the
            // lengths.
            return MathHelper.Acos(Vector2.Dot(vec1, vec2));
        }
    }
}
