
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
Authors: Schneider, José Ignacio (jis@cs.uns.edu.ar)         
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

namespace XNAFinalEngine.Helpers
{

    /// <summary>
    /// Stores a set of four floating-point numbers that represent the location and size of a rectangle.
    /// </summary>
    public struct RectangleF
    {


        private float x, y, width, height;



        /// <summary>
        /// Gets or sets the x-coordinate of the upper-left corner of this RectangleF structure.
        /// </summary>
        public float X { get { return x; } set { x = value; } }

        /// <summary>
        /// Gets or sets the y-coordinate of the upper-left corner of this RectangleF structure.
        /// </summary>
        public float Y { get { return y; } set { y = value; } }
                
        /// <summary>
        /// Gets or sets the width of this RectangleF structure.
        /// </summary>
        public float Width { get { return width; } set { width = value; } }

        /// <summary>
        /// Gets or sets the height of this RectangleF structure.
        /// </summary>
        public float Height { get { return height; } set { height = value; } }



        /// <summary>
        /// Stores a set of four floating-point numbers that represent the location and size of a rectangle.
        /// </summary>
        public RectangleF(float x, float y, float width, float height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        } // RectangleF



        public static bool operator ==(RectangleF x, RectangleF y)
        {
            return x.x == y.x && x.y == y.y && x.width == y.width && x.height == y.height;
        } // Equal

        public static bool operator !=(RectangleF x, RectangleF y)
        {
            return !(x == y);
        } // Not Equal

        public override bool Equals(System.Object obj)
        {
            return obj is RectangleF && this == (RectangleF)obj;
        } // Equals

        public override int GetHashCode()
        {
            return width.GetHashCode() ^ height.GetHashCode();
        } // GetHashCode


    } // RectangleF 
} // XNAFinalEngine.Helpers
