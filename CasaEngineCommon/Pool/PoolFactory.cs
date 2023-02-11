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
using CasaEngineCommon.Factory;

namespace CasaEngineCommon.Pool
{

    /// <summary>Factory that allows created instance to be reused</summary>
    /// <typeparam name="ProductType">Type of objects the factory produces</typeparam>
    /// <remarks>
    ///   <para>
    ///     Object pooling in the traditional sense doesn't make much sense in the .NET
    ///     environment because memory allocation is instantaneous (return address and
    ///     increment pointer) and memory fragmentation will be fixed automatically.
    ///   </para>
    ///   <para>
    ///     This object pool tries to avoid 
    ///   </para>
    /// </remarks>
    internal class PoolFactory<ProductType> :
      IAbstractFactory<ProductType>, IAbstractFactory
      where ProductType : class, new()
    {

        /// <summary>Initializes a new pool factory</summary>
        public PoolFactory() : this(64) { }

        /// <summary>
        ///   Initializes a new pool factory with an explicit unused object limit
        /// </summary>
        /// <param name="maximumUnusedObjectCount">
        ///   Maximum number of unused objects that will be kept in the pool
        /// </param>
        public PoolFactory(int maximumUnusedObjectCount)
        {
            this.maximumUnusedObjectCount = maximumUnusedObjectCount;

            unusedItems = new List<ProductType>(maximumUnusedObjectCount);
        }

        /// <summary>Takes an instance from the pool</summary>
        /// <returns>A new or reused instance of the pool factory's product type</returns>
        public ProductType Take()
        {
            if (unusedItems.Count > 0)
            {
                ProductType item = unusedItems[unusedItems.Count - 1];
                unusedItems.RemoveAt(unusedItems.Count - 1);
                return item;
            }

            return new ProductType();
        }

        /// <summary>Returns a node to the factory for reuse</summary>
        /// <param name="item">Item that will be returned to the factory</param>
        /// <remarks>
        ///   The caller promises that the item is no longer being used and that no
        ///   other references to it exist anywhere else in the code. Any cleanup
        ///   should be done before returning the unused item to the pool factory.
        /// </remarks>
        public void Redeem(ProductType item)
        {
            if (unusedItems.Count < maximumUnusedObjectCount)
            {
                unusedItems.Add(item);
            }
        }

        /// <summary>
        ///   Creates a new instance of the type to which the factory is specialized
        /// </summary>
        /// <returns>The newly created instance</returns>
        ProductType IAbstractFactory<ProductType>.CreateInstance()
        {
            return Take();
        }

        /// <summary>
        ///   Creates a new instance of the type to which the factory is specialized
        /// </summary>
        /// <returns>The newly created instance</returns>
        object IAbstractFactory.CreateInstance()
        {
            return Take();
        }

        /// <summary>Unused items available for resuse by the pool factory</summary>
        private List<ProductType> unusedItems;
        /// <summary>Maximum number of unused objects that will be kept in the pool</summary>
        private int maximumUnusedObjectCount;

    }

} // namespace CasaEngine.Pool
