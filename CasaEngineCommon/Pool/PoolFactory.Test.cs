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

#if UNITTEST

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using Microsoft.Xna.Framework;

using NUnit.Framework;

using CasaEngine.Factory;

namespace CasaEngineCommon.Pool {

  /// <summary>Unit tests for the pool factory</summary>
  [TestFixture]
  internal class PoolFactoryTest {

    /// <summary>
    ///   Verifies that the pool factory's default constructor is working
    /// </summary>
    [Test]
    public void TestDefaultConstructor() {
      PoolFactory<object> factory = new PoolFactory<object>();
      Assert.IsNotNull(factory); // Nonsense; prevents compiler warning
    }

    /// <summary>
    ///   Verifies that the pool factory's explicit constructor is working
    /// </summary>
    [Test]
    public void TestExplicitConstructor() {
      PoolFactory<object> factory = new PoolFactory<object>(100);
      Assert.IsNotNull(factory); // Nonsense; prevents compiler warning
    }

    /// <summary>
    ///   Tests whether the pool factory actually pools objects
    /// </summary>
    [Test]
    public void TestPooling() {
      PoolFactory<object> factory = new PoolFactory<object>(10);

      // We allocate 10 objects ahead because a valid pool factory implementation could
      // decide to only start returning reused objects after the pool is full to quickly
      // grow the pool at the beginning.
      object[] objects = new object[10];
      for(int index = 0; index < objects.Length; ++index) {
        objects[index] = factory.Take();
      }

      for(int index = objects.Length - 1; index >= 0; --index) {
        factory.Redeem(objects[index]);
      }

      for(int index = 0; index < objects.Length; ++index) {
        Assert.Contains(factory.Take(), objects);
      }
    }

    /// <summary>
    ///   Verifies that the pool factory implements the IAbstractFactory&lt;&gt; interface
    /// </summary>
    [Test]
    public void TestViaGenericFactoryInterface() {
      IAbstractFactory<object> factory = new PoolFactory<object>(10);
      Assert.IsNotNull(factory.CreateInstance());
    }

    /// <summary>
    ///   Verifies that the pool factory implements the IAbstractFactory interface
    /// </summary>
    [Test]
    public void TestViaObjectFactoryInterface() {
      IAbstractFactory factory = new PoolFactory<object>(10);
      Assert.IsNotNull(factory.CreateInstance());
    }

  }

} // namespace CasaEngine.Pool

#endif // UNITTEST
