
#if UNITTEST

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace CasaEngineCommon.Pool
{
   [TestFixture]
   public class ResourcePoolUnitTest {
      class TestResource {
         public bool IsFree { get; set; }
      }

      const int MaxPoolSize = 3;

      [Test]
      public void GetItem_SingleThread_ReturnsItem() {
         ResourcePool<TestResource> pool = new ResourcePool<TestResource>(CreateResource);

         using (var item = pool.GetItem()) {
            Assert.IsNotNull(item);
         }
      }

      [Test]
      public void GetItem_More_Than_MaxPoolSize() {
         ResourcePool<TestResource> pool = new ResourcePool<TestResource>(CreateResource);

         for (int i = 0; i < 20; i++) {
            using (var item = pool.GetItem()) {
            }
         }

         Assert.AreEqual(1, pool.Count);
      }

      [Test]
      public void GetItem_Resource_Returned_After_Waiting() {
         ResourcePool<TestResource> pool = new ResourcePool<TestResource>(CreateResource);

         List<PoolItem<TestResource>> poolItems = new List<PoolItem<TestResource>>();
         for (int i = 0; i < MaxPoolSize; i++) {
            poolItems.Add(pool.GetItem());
         }

         AutoResetEvent outerWaitLock = new AutoResetEvent(false);

         WaitCallback asyncCall = (o) => {
            outerWaitLock.Set();
            PoolItem<TestResource> item = pool.GetItem();
            Assert.IsNotNull(item);
            Assert.IsNotNull(item.Resource);
            outerWaitLock.Set();
         };
         ThreadPool.QueueUserWorkItem(asyncCall);
         
         outerWaitLock.WaitOne();
         outerWaitLock.Reset();
         poolItems.ForEach(item => item.Dispose());

         outerWaitLock.WaitOne();
      }

      [Test]
      public void GetItem_MultiThreaded_ReturnsItem() {
         ResourcePool<TestResource> pool = new ResourcePool<TestResource>(CreateResource);

         Action work =
             () => {
                using (var item = pool.GetItem()) {
                   TestResource resource = item.Resource;
                   Assert.IsTrue(resource.IsFree);
                   resource.IsFree = false;
                   long ticks = DateTime.Now.Millisecond;
                   int sleepTime = (int)(ticks % 200);
                   Thread.Sleep(sleepTime);
                   resource.IsFree = true;
                }
             };

         const int loopCount = 50;
         const int itemCount = 5;

         for (int i = 0; i < loopCount; i++) {
            List<AutoResetEvent> locks = new List<AutoResetEvent>();

            for (int j = 0; j < itemCount; j++) {
               //Debug.WriteLine("Loop {0} Item {1}", i, j);
               ThreadPool.QueueUserWorkItem((o) => work());
            }
         }
      }

      [Test]
      public void Dispose_WhileInUse_ThrowsException() {
         ResourcePool<TestResource> pool = new ResourcePool<TestResource>(CreateResource);
         PoolItem<TestResource> item = pool.GetItem();

         Assert.Throws<InvalidOperationException>(
            () => pool.Dispose(),
            "Cannot dispose the resource pool while one or more pooled items are in use");
      }

      private static TestResource CreateResource(ResourcePool<TestResource> pool) {
         return pool.Count < MaxPoolSize ? new TestResource { IsFree = true } : null;
      }
   }
}

#endif