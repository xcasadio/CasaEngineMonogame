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

namespace CasaEngine.Core.Packing;

/// <summary>Base class for rectangle packing algorithms</summary>
/// <remarks>
///   <para>
///     By uniting all rectangle packers under this common base class, you can
///     easily switch between different algorithms to find the most efficient or
///     performant one for a given job.
///   </para>
///   <para>
///     An almost exhaustive list of packing algorithms can be found here:
///     http://www.csc.liv.ac.uk/~epa/surveyhtml.html
///   </para>
/// </remarks>
public abstract class RectanglePacker
{

    /// <summary>Initializes a new rectangle packer</summary>
    /// <param name="packingAreaWidth">Width of the packing area</param>
    /// <param name="packingAreaHeight">Height of the packing area</param>
    protected RectanglePacker(int packingAreaWidth, int packingAreaHeight)
    {
        this.packingAreaWidth = packingAreaWidth;
        this.packingAreaHeight = packingAreaHeight;
    }

    /// <summary>Allocates space for a rectangle in the packing area</summary>
    /// <param name="rectangleWidth">Width of the rectangle to allocate</param>
    /// <param name="rectangleHeight">Height of the rectangle to allocate</param>
    /// <returns>The location at which the rectangle has been placed</returns>
    public virtual Point Pack(int rectangleWidth, int rectangleHeight)
    {
        if (!TryPack(rectangleWidth, rectangleHeight, out var point))
        {
            throw new OutOfSpaceException("Rectangle does not fit in packing area");
        }

        return point;
    }

    /// <summary>Tries to allocate space for a rectangle in the packing area</summary>
    /// <param name="rectangleWidth">Width of the rectangle to allocate</param>
    /// <param name="rectangleHeight">Height of the rectangle to allocate</param>
    /// <param name="placement">Output parameter receiving the rectangle's placement</param>
    /// <returns>True if space for the rectangle could be allocated</returns>
    public abstract bool TryPack(
        int rectangleWidth, int rectangleHeight, out Point placement
    );

    /// <summary>Maximum width the packing area is allowed to have</summary>
    protected int PackingAreaWidth => packingAreaWidth;

    /// <summary>Maximum height the packing area is allowed to have</summary>
    protected int PackingAreaHeight => packingAreaHeight;

    /// <summary>Maximum allowed width of the packing area</summary>
    private int packingAreaWidth;
    /// <summary>Maximum allowed height of the packing area</summary>
    private int packingAreaHeight;

}

// namespace CasaEngineCommon.Packing
