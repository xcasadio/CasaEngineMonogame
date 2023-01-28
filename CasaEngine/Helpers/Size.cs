
/*
Copyright (c) 2008-2012, Laboratorio de Investigación y Desarrollo en Visualización y Computación Gráfica - 
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

using System;


using CasaEngine.CoreSystems;
using Screen = CasaEngine.CoreSystems.Screen;


namespace XNAFinalEngine.Helpers
{

    /// <summary>
    /// Stores an ordered pair of integers, which specify a Height and Width.
    /// </summary>
    public struct Size
    {


        /// <summary>
        /// Some effect works in different sizes but relative to the screen size.
        /// </summary>
        public enum TextureSize
        {
            QuarterSize,
            HalfSize,
            FullSize,
        } // TextureSize



        private int width, height;
        private Screen m_Screen;



        /// <summary>
        /// Gets or sets the horizontal component of this Size structure.
        /// </summary>
        public int Width
        {
            get
            {
                if (this == FullScreen || this == SplitFullScreen)
                    return m_Screen.Width;
                if (this == HalfScreen || this == SplitHalfScreen)
                    return m_Screen.Width / 2;
                if (this == QuarterScreen || this == SplitQuarterScreen)
                    return m_Screen.Width / 4;
                return width;
            }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException("value", "Width has to be greater than or equal to zero.");
                width = value;
            }
        } // Width

        /// <summary>
        /// Gets or sets the vertical component of this Size structure.
        /// </summary>
        public int Height
        {
            get
            {
                if (this == FullScreen)
                    return m_Screen.Height;
                if (this == HalfScreen)
                    return m_Screen.Height / 2;
                if (this == QuarterScreen)
                    return m_Screen.Height / 4;
                if (this == SplitFullScreen)
                    return m_Screen.Height / 2;
                if (this == SplitHalfScreen)
                    return m_Screen.Height / 4;
                if (this == SplitQuarterScreen)
                    return m_Screen.Height / 8;
                return height;
            }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException("value", "Height has to be greater than or equal to zero.");
                height = value;
            }
        } // Height



        /// <summary>Full screen size.</summary>
        /// <remarks>This size will be updated with screen size changes.</remarks>
        public Size FullScreen { get { return new Size { width = -1, height = 0, m_Screen = this.m_Screen }; } }

        /// <summary>Half screen size. E.g. 800x600 becomes 400x300</summary>
        /// <remarks>This size will be updated with screen size changes.</remarks>
        public Size HalfScreen { get { return new Size { width = -2, height = 0, m_Screen = this.m_Screen }; } }

        /// <summary>Quarter of the full screen size. E.g. 800x600 becomes 200x150</summary>
        /// <remarks>This size will be updated with screen size changes.</remarks>
        public Size QuarterScreen { get { return new Size { width = -3, height = 0, m_Screen = this.m_Screen }; } }

        /// <summary>Horizontal split version of the full screen size. E.g. 800x600 becomes 800x300 </summary>
        /// <remarks>This size will be updated with screen size changes.</remarks>
        public Size SplitFullScreen { get { return new Size { width = -4, height = 0, m_Screen = this.m_Screen }; } }

        /// <summary>Horizontal split version of the half screen size. E.g. 800x600 becomes 400x150</summary>
        /// <remarks>This size will be updated with screen size changes.</remarks>
        public Size SplitHalfScreen { get { return new Size { width = -5, height = 0, m_Screen = this.m_Screen }; } }

        /// <summary>Horizontal split version of the quarter of the full screen size. E.g. 800x600 becomes 200x75</summary>
        /// <remarks>This size will be updated with screen size changes.</remarks>
        public Size SplitQuarterScreen { get { return new Size { width = -6, height = 0, m_Screen = this.m_Screen }; } }

        /// <summary>256 x 256</summary>
        public Size Square256X256 { get { return new Size(256, 256, m_Screen); } }

        /// <summary>512 x 512</summary>
        public Size Square512X512 { get { return new Size(512, 512, m_Screen); } }

        /// <summary>1024 x 1024</summary>
        public Size Square1024X1024 { get { return new Size(1024, 1024, m_Screen); } }

        /// <summary>2048 x 2048</summary>
        public Size Square2048X2048 { get { return new Size(2048, 2048, m_Screen); } }



        /// <summary>
        /// Stores an ordered pair of integers, which specify a Height and Width.
        /// </summary>
        public Size(int width, int height, Screen screen_)
        {
            if (width < 0)
                throw new ArgumentOutOfRangeException("width", "Width has to be greater than or equal to zero.");
            if (height < 0)
                throw new ArgumentOutOfRangeException("height", "Height has to be greater than or equal to zero.");
            this.width = width;
            this.height = height;

            m_Screen = screen_;
        } // Size



        public static bool operator ==(Size x, Size y)
        {
            return x.width == y.width && x.height == y.height;
        } // Equal

        public static bool operator !=(Size x, Size y)
        {
            return x.width != y.width || x.height != y.height;
        } // Not Equal

        public override bool Equals(Object obj)
        {
            return obj is Size && this == (Size)obj;
        } // Equals

        public override int GetHashCode()
        {
            return width.GetHashCode() ^ height.GetHashCode();
        } // GetHashCode



        /// <summary>
        /// Returns a size of half dimensions.
        /// It contemplates relative sizes.
        /// </summary>
        /// <returns></returns>
        public Size HalfSize()
        {
            if (this == FullScreen)
                return HalfScreen;
            if (this == HalfScreen)
                return QuarterScreen;
            if (this == SplitFullScreen)
                return SplitHalfScreen;
            if (this == SplitHalfScreen)
                return SplitQuarterScreen;
            return new Size(Width / 2, Height / 2, m_Screen);
        } // HalfSize



        /// <summary>
        /// If this size value correspond to a relative screen size (like full screen) make it relative.
        /// </summary>
        public void MakeRelativeIfPosible()
        {
            if (this == FullScreen)
                this = FullScreen;
            if (this == HalfScreen)
                this = HalfScreen;
            if (this == QuarterScreen)
                this = QuarterScreen;
            if (this == SplitFullScreen)
                this = SplitFullScreen;
            if (this == SplitHalfScreen)
                this = SplitHalfScreen;
            if (this == SplitQuarterScreen)
                this = SplitQuarterScreen;
            // If not stay the same.
        } // MakeRelativeIfPosible


    } // Size 
} // XNAFinalEngine.Helpers
