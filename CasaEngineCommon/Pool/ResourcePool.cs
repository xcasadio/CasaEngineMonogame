using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace CasaEngineCommon.Pool
{
    /// <summary>
    /// Generic pool class
    /// </summary>
    /// <typeparam name="T">The type of items to be stored in the pool</typeparam>
    public class ResourcePool<T>
        : IDisposable where T : class
    {
        /// <summary>
        /// Creates a new pool
        /// </summary>
        /// <param name="factory">The factory method to create new items to be stored in the pool</param>
        public ResourcePool(Func<ResourcePool<T>, T> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException("factory");
            }

            _factoryMethod = factory;
        }

        private readonly Func<ResourcePool<T>, T> _factoryMethod;
        private ConcurrentQueue<PoolItem<T>> _freeItems = new();
        private ConcurrentQueue<AutoResetEvent> _waitLocks = new();

        private ConcurrentDictionary<AutoResetEvent, PoolItem<T>> _syncContext = new();

        public Action<T> CleanupPoolItem { get; set; }

        /// <summary>
        /// Gets the current count of items in the pool
        /// </summary>
        public int Count { get; private set; }

        public void Dispose()
        {
            lock (this)
            {
                if (Count != _freeItems.Count)
                {
                    throw new InvalidOperationException(
                        "Cannot dispose the resource pool while one or more pooled items are in use");
                }

                foreach (var poolItem in _freeItems)
                {
                    Action<T> cleanMethod = CleanupPoolItem;
                    if (cleanMethod != null)
                    {
                        CleanupPoolItem(poolItem.Resource);
                    }
                }

                Count = 0;
                _freeItems = null;
                _waitLocks = null;
                _syncContext = null;
            }
        }

        /// <summary>
        /// Gets a free resource from the pool. If no free items available this method tries to 
        /// create a new item. If no new item could be created this method waits until another thread
        /// frees one resource.
        /// </summary>
        /// <returns>A resource item</returns>
        public PoolItem<T> GetItem()
        {
            PoolItem<T> item;

            // try to get an item
            if (!TryGetItem(out item))
            {
                AutoResetEvent waitLock = null;

                lock (this)
                {
                    // try to get an entry in exclusive mode
                    if (!TryGetItem(out item))
                    {
                        // no item available, create a wait lock and enqueue it
                        waitLock = new AutoResetEvent(false);
                        _waitLocks.Enqueue(waitLock);
                    }
                }

                if (waitLock != null)
                {
                    // wait until a new item is available
                    waitLock.WaitOne();
                    _syncContext.TryRemove(waitLock, out item);
                    waitLock.Dispose();
                }
            }

            return item;
        }

        private bool TryGetItem(out PoolItem<T> item)
        {
            // try to get an already pooled resource
            if (_freeItems.TryDequeue(out item))
            {
                return true;
            }

            lock (this)
            {
                // try to create a new resource
                T resource = _factoryMethod(this);
                if (resource == null && Count == 0)
                {
                    throw new InvalidOperationException("Pool empty and no item created");
                }

                if (resource != null)
                {
                    // a new resource was created and can be returned
                    Count++;
                    item = new PoolItem<T>(this, resource);
                }
                else
                {
                    // no items available to return at the moment
                    item = null;
                }

                return item != null;
            }
        }

        /// <summary>
        /// Called from <see cref="PoolItem{T}"/> to free previously taked resources
        /// </summary>
        /// <param name="resource">The resource to send back into the pool.</param>
        internal void SendBackToPool(T resource)
        {
            lock (this)
            {
                PoolItem<T> item = new PoolItem<T>(this, resource);
                AutoResetEvent waitLock;

                if (_waitLocks.TryDequeue(out waitLock))
                {
                    _syncContext.TryAdd(waitLock, item);
                    waitLock.Set();
                }
                else
                {
                    _freeItems.Enqueue(item);
                }
            }
        }
    }
}
