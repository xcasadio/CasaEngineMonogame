#region CPL License
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
#endregion

using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;

#if UNITTEST

using NUnit.Framework;

namespace CasaEngineCommon.Packing {

  /// <summary>Unit test for the cygon rectangle packer class</summary>
  [TestFixture]
  internal class CygonRectanglePackerTest : RectanglePackerTest {

    /// <summary>Tests the packer's efficiency using a deterministic benchmark</summary>
    [Test]
    public void TestSpaceEfficiency() {
      float efficiency = CalculateEfficiency(new CygonRectanglePacker(70, 70));

      Assert.GreaterOrEqual(efficiency, 0.75f, "Packer achieves 75% efficiency");
    }

    /// <summary>Verifies that the packer rejects a rectangle that is too large</summary>
    [Test]
    public void TestTooLargeRectangle() {
      CygonRectanglePacker packer = new CygonRectanglePacker(128, 128);
      Point placement;

      bool result = packer.TryPack(129, 10, out placement);
      Assert.IsFalse(result);

      result = packer.TryPack(10, 129, out placement);
      Assert.IsFalse(result);
    }

    /// <summary>
    ///   Tests whether the packer throws the appropriate exception if a rectangle
    ///   is too large to fit in the packing area
    /// </summary>
    [Test]
    public void TestThrowOnTooLargeRectangle() {
      CygonRectanglePacker packer = new CygonRectanglePacker(128, 128);
      Assert.Throws<OutOfSpaceException>(
        delegate() { packer.Pack(129, 129); }
      );
    }

    /// <summary>
    ///   Verifies that the packer can pack a rectangle that barely fits in the packing area
    /// </summary>
    [Test]
    public void TestBarelyFittingRectangle() {
      CygonRectanglePacker packer = new CygonRectanglePacker(128, 128);

      Point placement = packer.Pack(128, 128);

      Assert.AreEqual(new Point(0, 0), placement);
    }

    /// <summary>Tests the packer's stability by running a complete benchmark</summary>
    [Test]
    public void TestStability() {
      float score = Benchmark(
        delegate() { return new CygonRectanglePacker(1024, 1024); }
      );

      // This is mainly a stability and performance test. It fails when the
      // packer crashes on its own and is otherwise only there to tell how long
      // it takes to complete the benchmark.
    }

  }

} // namespace CasaEngineCommon.Packing

#endif // UNITTEST
