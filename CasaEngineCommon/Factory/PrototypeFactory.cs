﻿using System;
using System.Collections.Generic;
using System.Text;

namespace CasaEngineCommon.Factory
{

#if !NO_CLONING

    /// <summary>Factory that creates instances by cloning a prototype</summary>
    /// <typeparam name="ProductType">Type of product created by the factory</typeparam>
    /// <typeparam name="ConcreteType">Type of the prototype that will be cloned</typeparam>
    public class PrototypeFactory<ProductType, ConcreteType> :
      IAbstractFactory<ProductType>, IAbstractFactory, IDisposable
        where ProductType : class
        where ConcreteType : class, ICloneable
    {

        /// <summary>Initializes a new prototype based factory</summary>
        /// <param name="prototype">Prototype instance that will be cloned</param>
        public PrototypeFactory(ConcreteType prototype)
        {
            this.prototype = prototype;
        }

        /// <summary>
        ///   Creates a new instance of the type to which the factory is specialized
        /// </summary>
        /// <returns>The newly created instance</returns>
        public ProductType CreateInstance()
        {
            return (ProductType)prototype.Clone();
        }

        /// <summary>
        ///   Creates a new instance of the type to which the factory is specialized
        /// </summary>
        /// <returns>The newly created instance</returns>
        object IAbstractFactory.CreateInstance()
        {
            return prototype.Clone();
        }

        /// <summary>Immediately releases all resources owned by the instance</summary>
        public void Dispose()
        {
            if (prototype != null)
            {
                IDisposable disposablePrototype = prototype as IDisposable;
                if (disposablePrototype != null)
                {
                    disposablePrototype.Dispose();
                }

                prototype = null;
            }
        }

        /// <summary>The prototype object</summary>
        private ConcreteType prototype;

    }

#endif // !NO_CLONING

} // namespace CasaEngine.Factory
