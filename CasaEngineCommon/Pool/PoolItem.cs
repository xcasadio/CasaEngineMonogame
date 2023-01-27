using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngineCommon.Pool 
{
   /// <summary>
   /// Represents an item in the <see cref="ResourcePool{T}"/>
   /// </summary>
   /// <typeparam name="T">The type of the resource to be hold</typeparam>
   public sealed class PoolItem<T> : IDisposable where T : class 
   {
      internal PoolItem(ResourcePool<T> pool, T resource) 
      {
         _pool = pool;
         _resource = resource;
      }

      private T _resource;
      private readonly ResourcePool<T> _pool;

      public static implicit operator T(PoolItem<T> item) 
      {
         return item.Resource;
      }

      /// <summary>
      /// Gets the resource hold by this resource pool item
      /// </summary>
      public T Resource { get { return _resource; } }

      /// <summary>
      /// Disposes this instance of an resource pool item and sends the resource back into pool
      /// </summary>
      public void Dispose() 
      {
         _pool.SendBackToPool(_resource);
         _resource = null;
      }
   }
}
