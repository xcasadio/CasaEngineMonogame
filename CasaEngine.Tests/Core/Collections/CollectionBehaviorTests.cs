using CasaEngine.Core.Collections;
using CasaEngine.Framework.AI.Messaging;
using Xunit;

namespace CasaEngine.Tests.Core.Collections;

public sealed class CollectionBehaviorTests
{
    [Fact]
    public void IndexedPriorityQueue_Dequeue_ReturnsIndexesByIndexedPriority()
    {
        List<double> costs = [3.0, 1.0, 2.0];
        var queue = new IndexedPriorityQueue<double>(costs);

        queue.Enqueue(0);
        queue.Enqueue(1);
        queue.Enqueue(2);

        Assert.Equal(1, queue.Dequeue());
        Assert.Equal(2, queue.Dequeue());
        Assert.Equal(0, queue.Dequeue());
        Assert.Empty(queue);
    }

    [Fact]
    public void IndexedPriorityQueue_ChangePriority_WhenPriorityDecreases_MovesIndexToFront()
    {
        List<double> costs = [10.0, 20.0, 30.0];
        var queue = new IndexedPriorityQueue<double>(costs);

        queue.Enqueue(0);
        queue.Enqueue(1);
        queue.Enqueue(2);
        costs[2] = 1.0;

        queue.ChangePriority(2);

        Assert.Equal(2, queue.Dequeue());
    }

    [Fact]
    public void IndexedPriorityQueue_ChangePriority_WhenPriorityIncreases_MovesIndexDown()
    {
        List<double> costs = [1.0, 2.0, 3.0];
        var queue = new IndexedPriorityQueue<double>(costs);

        queue.Enqueue(0);
        queue.Enqueue(1);
        queue.Enqueue(2);
        costs[0] = 4.0;

        queue.ChangePriority(0);

        Assert.Equal(1, queue.Dequeue());
    }

    [Fact]
    public void UniquePriorityQueue_Enqueue_SkipsMessagesEqualWithinComparerPrecision()
    {
        object extraInfo = new();
        Guid senderId = Guid.NewGuid();
        Guid receiverId = Guid.NewGuid();
        var queue = new UniquePriorityQueue<Message>(new MessageComparer(1000.0));

        int firstIndex = queue.Enqueue(new Message(senderId, receiverId, 7, 10000.0, extraInfo));
        int repeatedIndex = queue.Enqueue(new Message(senderId, receiverId, 7, 10500.0, extraInfo));

        Assert.NotEqual(-1, firstIndex);
        Assert.Equal(-1, repeatedIndex);
        Assert.Single(queue);
    }

    [Fact]
    public void UniquePriorityQueue_Dequeue_ReturnsMessagesByDispatchTime()
    {
        Guid senderId = Guid.NewGuid();
        Guid receiverId = Guid.NewGuid();
        var queue = new UniquePriorityQueue<Message>(new MessageComparer(0.0));
        Message lateMessage = new(senderId, receiverId, 1, 20000.0, new object());
        Message earlyMessage = new(senderId, receiverId, 1, 10000.0, new object());

        queue.Enqueue(lateMessage);
        queue.Enqueue(earlyMessage);

        Assert.Same(earlyMessage, queue.Dequeue());
        Assert.Same(lateMessage, queue.Dequeue());
    }

    [Fact]
    public void ScheduledMessageQueue_Enqueue_SkipsMessagesEqualWithinComparerPrecision()
    {
        object extraInfo = new();
        Guid senderId = Guid.NewGuid();
        Guid receiverId = Guid.NewGuid();
        var queue = new ScheduledMessageQueue(1000.0);

        bool firstEnqueued = queue.Enqueue(new Message(senderId, receiverId, 7, 10000.0, extraInfo));
        bool repeatedEnqueued = queue.Enqueue(new Message(senderId, receiverId, 7, 10500.0, extraInfo));

        Assert.True(firstEnqueued);
        Assert.False(repeatedEnqueued);
        Assert.Equal(1, queue.Count);
    }

    [Fact]
    public void ScheduledMessageQueue_Dequeue_ReturnsMessagesByDispatchTimeThenInsertionOrder()
    {
        Guid senderId = Guid.NewGuid();
        Guid receiverId = Guid.NewGuid();
        var queue = new ScheduledMessageQueue(0.0);
        Message firstSameTimeMessage = new(senderId, receiverId, 1, 10000.0, new object());
        Message secondSameTimeMessage = new(senderId, receiverId, 2, 10000.0, new object());
        Message earlyMessage = new(senderId, receiverId, 3, 5000.0, new object());

        queue.Enqueue(firstSameTimeMessage);
        queue.Enqueue(secondSameTimeMessage);
        queue.Enqueue(earlyMessage);

        Assert.Same(earlyMessage, queue.Dequeue());
        Assert.Same(firstSameTimeMessage, queue.Dequeue());
        Assert.Same(secondSameTimeMessage, queue.Dequeue());
    }

    [Fact]
    public void Pool_Release_MovesLastActiveElementIntoReleasedSlot()
    {
        var pool = new Pool<PooledItem>(2);
        Pool<PooledItem>.Accessor first = pool.Fetch();
        Pool<PooledItem>.Accessor second = pool.Fetch();
        pool[first].Value = 1;
        pool[second].Value = 2;

        pool.Release(first);

        Assert.Equal(1, pool.Count);
        Assert.Equal(2, pool.Elements[0].Value);
        Assert.Equal(0, second.Index);
    }

    [Fact]
    public void Pool_Fetch_ReusesReleasedAccessorWithoutGenerationGuard()
    {
        var pool = new Pool<PooledItem>(2);
        Pool<PooledItem>.Accessor first = pool.Fetch();
        _ = pool.Fetch();

        pool.Release(first);
        Pool<PooledItem>.Accessor reused = pool.Fetch();
        pool[reused].Value = 42;

        Assert.Same(first, reused);
        Assert.Equal(42, pool[first].Value);
    }

    private sealed class PooledItem
    {
        public int Value { get; set; }
    }
}