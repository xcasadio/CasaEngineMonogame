/*
Nuclex Framework
Copyright (C) 2002-2011 Nuclex Development Labs

This library is free software; you can redistribute it and/or
modify it under the terms of the IBM Common Public License as
published by the IBM Corporation; either version 1.0 of the
License, or (at your option) any later version.

This library is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
IBM Common Public License for more details.

You should have received a copy of the IBM Common Public
License along with this library
*/

using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;

namespace CasaEngineCommon.Packing
{

    /// <summary>Simplified packer for rectangles which don't vary greatly in size</summary>
    /// <remarks>
    ///   This is a highly performant packer that sacrifices space efficiency for
    ///   low memory usage and runtime performance. It achieves good results with
    ///   near-uniform sized rectangles but will waste lots of space with rectangles
    ///   of varying dimensions.
    /// </remarks>
    public class SimpleRectanglePacker : RectanglePacker
    {

        /// <summary>Initializes a new rectangle packer</summary>
        /// <param name="packingAreaWidth">Maximum width of the packing area</param>
        /// <param name="packingAreaHeight">Maximum height of the packing area</param>
        public SimpleRectanglePacker(int packingAreaWidth, int packingAreaHeight) :
          base(packingAreaWidth, packingAreaHeight)
        { }

        /// <summary>Tries to allocate space for a rectangle in the packing area</summary>
        /// <param name="rectangleWidth">Width of the rectangle to allocate</param>
        /// <param name="rectangleHeight">Height of the rectangle to allocate</param>
        /// <param name="placement">Output parameter receiving the rectangle's placement</param>
        /// <returns>True if space for the rectangle could be allocated</returns>
        public override bool TryPack(
          int rectangleWidth, int rectangleHeight, out Point placement
        )
        {

            // If the rectangle is larger than the packing area in any dimension,
            // it will never fit!
            if (
              (rectangleWidth > PackingAreaWidth) || (rectangleHeight > PackingAreaHeight)
            )
            {
                placement = Point.Zero;
                return false;
            }

            // Do we have to start a new line ?
            if (column + rectangleWidth > PackingAreaWidth)
            {
                currentLine += lineHeight;
                lineHeight = 0;
                column = 0;
            }

            // If it doesn't fit vertically now, the packing area is considered full
            if (currentLine + rectangleHeight > PackingAreaHeight)
            {
                placement = Point.Zero;
                return false;
            }

            // The rectangle appears to fit at the current location
            placement = new Point(column, currentLine);

            column += rectangleWidth; // Can be larger than cache width till next run
            if (rectangleHeight > lineHeight)
            {
                lineHeight = rectangleHeight;
            }

            return true;

        }

        /// <summary>Current packing line</summary>
        private int currentLine;
        /// <summary>Height of the current packing line</summary>
        private int lineHeight;
        /// <summary>Current column in the current packing line</summary>
        private int column;

    }

} // namespace CasaEngineCommon.Packing
