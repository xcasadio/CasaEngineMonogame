
#region License
/*
Copyright (c) 2008-2011, Laboratorio de Investigación y Desarrollo en Visualización y Computación Gráfica - 
                         Departamento de Ciencias e Ingeniería de la Computación - Universidad Nacional del Sur.
All rights reserved.
Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

•	Redistributions of source code must retain the above copyright, this list of conditions and the following disclaimer.

•	Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer
    in the documentation and/or other materials provided with the distribution.

•	Neither the name of the Universidad Nacional del Sur nor the names of its contributors may be used to endorse or promote products derived
    from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS ''AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR
CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE,
EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

-----------------------------------------------------------------------------------------------------------------------------------------------
Author: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/
#endregion

#region Using directives
using Microsoft.Xna.Framework;
#endregion

namespace XNAFinalEngine.Helpers
{
    public class ArrayHelper
    {

        #region Matrix

        /// <summary>
        /// Test if two matrix arrays have the same values.
        /// </summary>
        public static bool Equals(Matrix[] a1, Matrix[] a2)
        {
            if (a1 == null && a2 == null)
                return true;
            if (a1 == null || a2 == null)
                return false;

            if (a1.Length != a2.Length)
                return false;

            //return !a1.Where((t, i) => t != a2[i]).Any(); // Produces garbage
            for (int i = 0; i < a1.Length; i++)
            {
                if (a1[i] != a2[i])
                    return false;
            }
            return true;
        } // Equals

        #endregion

        #region Vector2

        /// <summary>
        /// Test if two Vector2 arrays have the same values.
        /// </summary>
        public static bool Equals(Vector2[] a1, Vector2[] a2)
        {
            if (a1 == null && a2 == null)
                return true;
            if (a1 == null || a2 == null)
                return false;

            if (a1.Length != a2.Length)
                return false;
            
            //return !a1.Where((t, i) => t != a2[i]).Any(); // Produces garbage
            for (int i = 0; i < a1.Length; i++)
            {
                if (a1[i] != a2[i])
                    return false;
            }
            return true;
        } // Equals

        #endregion

        #region Vector3

        /// <summary>
        /// Test if two Vector3 arrays have the same values.
        /// </summary>
        public static bool Equals(Vector3[] a1, Vector3[] a2)
        {
            if (a1 == null && a2 == null)
                return true;
            if (a1 == null || a2 == null)
                return false;

            if (a1.Length != a2.Length)
                return false;

            //return !a1.Where((t, i) => t != a2[i]).Any(); // Produces garbage
            for (int i = 0; i < a1.Length; i++)
            {
                if (a1[i] != a2[i])
                    return false;
            }
            return true;
        } // Equals

        #endregion

        #region Vector4

        /// <summary>
        /// Test if two Vector4 arrays have the same values.
        /// </summary>
        public static bool Equals(Vector4[] a1, Vector4[] a2)
        {
            if (a1 == null && a2 == null)
                return true;
            if (a1 == null || a2 == null)
                return false;

            if (a1.Length != a2.Length)
                return false;

            //return !a1.Where((t, i) => t != a2[i]).Any(); // Produces garbage
            for (int i = 0; i < a1.Length; i++)
            {
                if (a1[i] != a2[i])
                    return false;
            }
            return true;
        } // Equals

        #endregion

    } // ArrayHelper
} // XNAFinalEngine.Helpers
