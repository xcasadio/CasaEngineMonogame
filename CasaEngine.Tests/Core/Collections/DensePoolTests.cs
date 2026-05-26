using CasaEngine.Core.Collections;
using Xunit;

namespace CasaEngine.Tests.Core.Collections;

public sealed class DensePoolTests
{
    [Fact]
    public void Fetch_AddsElementsToDenseActivePrefix()
    {
        var pool = new DensePool<PooledItem>(2);

        DensePool<PooledItem>.Handle first = pool.Fetch();
        DensePool<PooledItem>.Handle second = pool.Fetch();
        pool[first].Value = 1;
        pool[second].Value = 2;

        Assert.Equal(2, pool.Count);
        Assert.Equal(1, pool.Elements[0].Value);
        Assert.Equal(2, pool.Elements[1].Value);
    }

    [Fact]
    public void Release_MovesLastActiveElementAndInvalidatesReleasedHandle()
    {
        var pool = new DensePool<PooledItem>(2);
        DensePool<PooledItem>.Handle first = pool.Fetch();
        DensePool<PooledItem>.Handle second = pool.Fetch();
        pool[first].Value = 1;
        pool[second].Value = 2;

        pool.Release(first);

        Assert.Equal(1, pool.Count);
        Assert.False(pool.TryGet(first, out _));
        Assert.True(pool.TryGet(second, out PooledItem item));
        Assert.Equal(2, item.Value);
        Assert.Equal(0, pool.GetIndex(second));
    }

    [Fact]
    public void Fetch_AfterRelease_ReusesSlotWithNewGeneration()
    {
        var pool = new DensePool<PooledItem>(1);
        DensePool<PooledItem>.Handle released = pool.Fetch();

        Assert.True(pool.TryRelease(released));
        DensePool<PooledItem>.Handle reused = pool.Fetch();

        Assert.NotEqual(released, reused);
        Assert.False(pool.TryGet(released, out _));
        Assert.True(pool.TryGet(reused, out _));
    }

    [Fact]
    public void TryRelease_WhenHandleIsReleasedTwice_ReturnsFalse()
    {
        var pool = new DensePool<PooledItem>(1);
        DensePool<PooledItem>.Handle handle = pool.Fetch();

        Assert.True(pool.TryRelease(handle));
        Assert.False(pool.TryRelease(handle));
    }

    [Fact]
    public void TryGet_WhenHandleBelongsToAnotherPool_ReturnsFalse()
    {
        var firstPool = new DensePool<PooledItem>(1);
        var secondPool = new DensePool<PooledItem>(1);
        DensePool<PooledItem>.Handle foreignHandle = firstPool.Fetch();

        Assert.False(secondPool.TryGet(foreignHandle, out _));
        Assert.False(secondPool.TryRelease(foreignHandle));
    }

    [Fact]
    public void Swap_UpdatesHandleDenseIndexes()
    {
        var pool = new DensePool<PooledItem>(2);
        DensePool<PooledItem>.Handle first = pool.Fetch();
        DensePool<PooledItem>.Handle second = pool.Fetch();
        pool[first].Value = 1;
        pool[second].Value = 2;

        pool.Swap(0, 1);

        Assert.Equal(1, pool[first].Value);
        Assert.Equal(2, pool[second].Value);
        Assert.Equal(1, pool.GetIndex(first));
        Assert.Equal(0, pool.GetIndex(second));
    }

    private sealed class PooledItem
    {
        public int Value { get; set; }
    }
}