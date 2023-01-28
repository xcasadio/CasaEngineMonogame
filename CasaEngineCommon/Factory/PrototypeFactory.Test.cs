/*
Nuclex Framework
Copyright (C) 2002-2010 Nuclex Development Labs

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

#if UNITTEST

using System;
using System.IO;

using NUnit.Framework;

namespace CasaEngine.Factory {

  /// <summary>Unit Test for the prototype-based factory class</summary>
  [TestFixture]
  public class PrototypeFactoryTest {


    /// <summary>Interface used for the product in the unit test</summary>
    private interface IProduct {
      /// <summary>Some value associated with the product</summary>
      int Value { get; }
    }



    /// <summary>
    ///   Class derived from the abstract base to serve as concrete product for
    ///   testing the factory employer
    /// </summary>
    private class ConcretePrototype : IProduct, ICloneable, IDisposable {

      /// <summary>Initializes a new instance of the prototype product</summary>
      /// <param name="value">Value that will be associated with this instance</param>
      public ConcretePrototype(int value) {
        this.value = value;
      }

      /// <summary>Immediately releases all resources owned by the instance</summary>
      public void Dispose() {
        this.disposed = true;
      }

      /// <summary>Value the product has been associated with</summary>
      public int Value { get { return this.value; } }

      /// <summary>Whether the prototype instance has been disposed</summary>
      public bool IsDisposed {
        get { return this.disposed; }
      }

      /// <summary>Creates an identical copy of the instance</summary>
      /// <returns>An identical copy of the instance</returns>
      object ICloneable.Clone() {
        return new ConcretePrototype(this.value);
      }

      /// <summary>Value associated with the product</summary>
      private int value;
      /// <summary>Whether the instance has been disposed</summary>
      private bool disposed;

    }


    /// <summary>
    ///   Tests whether the prototype-based factory behaves correctly by creating
    ///   new instances of its product using clones of its assigned prototype.
    /// </summary>
    [Test]
    public void TestGenericInstanceCreation() {
      ConcretePrototype template = new ConcretePrototype(42);

      IAbstractFactory<IProduct> factory = new PrototypeFactory<
        IProduct, ConcretePrototype
      >(template);

      IProduct factoryCreatedProduct = factory.CreateInstance();

      Assert.AreEqual(template.Value, factoryCreatedProduct.Value);
    }

    /// <summary>
    ///   Tests whether the prototype-based factory behaves correctly by creating
    ///   new instances of its product using clones of its assigned prototype.
    /// </summary>
    [Test]
    public void TestInstanceCreation() {
      ConcretePrototype template = new ConcretePrototype(42);

      IAbstractFactory factory = new PrototypeFactory<
        IProduct, ConcretePrototype
      >(template);

      IProduct factoryCreatedProduct = (IProduct)factory.CreateInstance();

      Assert.AreEqual(template.Value, factoryCreatedProduct.Value);
    }

    /// <summary>
    ///   Tests whether the prototype is disposed if it implements the IDisposable
    ///   interface and the factory is explicitely disposed.
    /// </summary>
    [Test]
    public void TestPrototypeDisposal() {
      ConcretePrototype template = new ConcretePrototype(42);

      PrototypeFactory<IProduct, ConcretePrototype> factory = new PrototypeFactory<
        IProduct, ConcretePrototype
      >(template);

      Assert.IsFalse(template.IsDisposed);
      factory.Dispose();
      Assert.IsTrue(template.IsDisposed);
    }

  }

} // namespace CasaEngine.Factory

#endif // UNITTEST
