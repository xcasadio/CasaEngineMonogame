
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

using Screen = CasaEngine.Framework.UserInterface.Screen;

namespace CasaEngine.Core.Helpers;

public struct ScreenSize
{

    public enum TextureSize
    {
        QuarterSize,
        HalfSize,
        FullSize,
    } // TextureSize

    private int _width, _height;
    private Screen _screen;

    public int Width
    {
        get
        {
            if (this == FullScreen || this == SplitFullScreen)
            {
                return _screen.Width;
            }

            if (this == HalfScreen || this == SplitHalfScreen)
            {
                return _screen.Width / 2;
            }

            if (this == QuarterScreen || this == SplitQuarterScreen)
            {
                return _screen.Width / 4;
            }

            return _width;
        }
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Width has to be greater than or equal to zero.");
            }

            _width = value;
        }
    } // Width

    public int Height
    {
        get
        {
            if (this == FullScreen)
            {
                return _screen.Height;
            }

            if (this == HalfScreen)
            {
                return _screen.Height / 2;
            }

            if (this == QuarterScreen)
            {
                return _screen.Height / 4;
            }

            if (this == SplitFullScreen)
            {
                return _screen.Height / 2;
            }

            if (this == SplitHalfScreen)
            {
                return _screen.Height / 4;
            }

            if (this == SplitQuarterScreen)
            {
                return _screen.Height / 8;
            }

            return _height;
        }
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Height has to be greater than or equal to zero.");
            }

            _height = value;
        }
    } // Height

    public ScreenSize FullScreen => new() { _width = -1, _height = 0, _screen = _screen };

    public ScreenSize HalfScreen => new() { _width = -2, _height = 0, _screen = _screen };

    public ScreenSize QuarterScreen => new() { _width = -3, _height = 0, _screen = _screen };

    public ScreenSize SplitFullScreen => new() { _width = -4, _height = 0, _screen = _screen };

    public ScreenSize SplitHalfScreen => new() { _width = -5, _height = 0, _screen = _screen };

    public ScreenSize SplitQuarterScreen => new() { _width = -6, _height = 0, _screen = _screen };

    public ScreenSize Square256X256 => new(256, 256, _screen);

    public ScreenSize Square512X512 => new(512, 512, _screen);

    public ScreenSize Square1024X1024 => new(1024, 1024, _screen);

    public ScreenSize Square2048X2048 => new(2048, 2048, _screen);

    public ScreenSize(int width, int height, Screen screen)
    {
        if (width < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width has to be greater than or equal to zero.");
        }

        if (height < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height has to be greater than or equal to zero.");
        }

        _width = width;
        _height = height;

        _screen = screen;
    } // Size

    public static bool operator ==(ScreenSize x, ScreenSize y)
    {
        return x._width == y._width && x._height == y._height;
    } // Equal

    public static bool operator !=(ScreenSize x, ScreenSize y)
    {
        return x._width != y._width || x._height != y._height;
    } // Not Equal

    public override bool Equals(object obj)
    {
        return obj is ScreenSize && this == (ScreenSize)obj;
    } // Equals

    public override int GetHashCode()
    {
        return _width.GetHashCode() ^ _height.GetHashCode();
    } // GetHashCode

    public ScreenSize HalfSize()
    {
        if (this == FullScreen)
        {
            return HalfScreen;
        }

        if (this == HalfScreen)
        {
            return QuarterScreen;
        }

        if (this == SplitFullScreen)
        {
            return SplitHalfScreen;
        }

        if (this == SplitHalfScreen)
        {
            return SplitQuarterScreen;
        }

        return new ScreenSize(Width / 2, Height / 2, _screen);
    } // HalfSize

    public void MakeRelativeIfPosible()
    {
        if (this == FullScreen)
        {
            this = FullScreen;
        }

        if (this == HalfScreen)
        {
            this = HalfScreen;
        }

        if (this == QuarterScreen)
        {
            this = QuarterScreen;
        }

        if (this == SplitFullScreen)
        {
            this = SplitFullScreen;
        }

        if (this == SplitHalfScreen)
        {
            this = SplitHalfScreen;
        }

        if (this == SplitQuarterScreen)
        {
            this = SplitQuarterScreen;
        }
        // If not stay the same.
    } // MakeRelativeIfPosible

} // Size 
// XNAFinalEngine.Helpers
