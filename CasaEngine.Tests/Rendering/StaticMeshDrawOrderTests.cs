using CasaEngine.Framework.Rendering.Draw;
using Xunit;

namespace CasaEngine.Tests.Rendering;

/// <summary>
/// The static mesh draw order is fully determined by what is submitted and in which order: equal sort keys keep their
/// submission order (the sort itself is not stable), and the mesh and material parts of the key come from the order of first
/// appearance in the frame, not from identity hash codes or from the random <see cref="Guid"/> of a generated material.
/// </summary>
public class StaticMeshDrawOrderTests
{
    private static readonly Guid ShaderId = new("5a1d3f0c-0000-4000-8000-000000000001");

    // The item's tag travels in DistanceToCamera, which the comparison never reads.
    private static RenderItem Item(ulong sortKey, int submissionIndex, int tag)
        => new() { SortKey = sortKey, SubmissionIndex = submissionIndex, DistanceToCamera = tag };

    private static List<RenderItem> BuildMixedList(int count, bool withExtraItemFirst)
    {
        // Three distinct keys interleaved, far more items than the insertion-sort threshold of List<T>.Sort.
        ulong[] keys = { 300UL, 100UL, 200UL };
        var items = new List<RenderItem>();
        if (withExtraItemFirst)
        {
            items.Add(Item(150UL, 0, -1));
        }

        for (int i = 0; i < count; i++)
        {
            items.Add(Item(keys[(i * 7 + i / 3) % 3], items.Count, i));
        }

        return items;
    }

    private static List<int> TagsOfKey(List<RenderItem> sorted, ulong key)
    {
        var tags = new List<int>();
        foreach (var item in sorted)
        {
            if (item.SortKey == key)
            {
                tags.Add((int)item.DistanceToCamera);
            }
        }

        return tags;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EqualKeys_KeepTheirSubmissionOrder(bool withExtraItemOfAnotherKey)
    {
        var items = BuildMixedList(300, withExtraItemOfAnotherKey);

        items.Sort(RenderItemOrder.Comparison);

        foreach (ulong key in new[] { 100UL, 200UL, 300UL })
        {
            var tags = TagsOfKey(items, key);
            Assert.NotEmpty(tags);
            Assert.Equal(tags.OrderBy(static t => t).ToList(), tags);
        }
    }

    [Fact]
    public void EqualKeysKeepTheirRelativeOrder_WhateverTheRestOfTheList()
    {
        var alone = BuildMixedList(300, false);
        var withExtra = BuildMixedList(300, true);

        alone.Sort(RenderItemOrder.Comparison);
        withExtra.Sort(RenderItemOrder.Comparison);

        foreach (ulong key in new[] { 100UL, 200UL, 300UL })
        {
            Assert.Equal(TagsOfKey(alone, key), TagsOfKey(withExtra, key));
        }
    }

    [Fact]
    public void DifferentKeys_SortByKey_WhateverTheSubmissionOrder()
    {
        var items = new List<RenderItem>
        {
            Item(30UL, 0, 0),
            Item(10UL, 1, 1),
            Item(20UL, 2, 2),
        };

        items.Sort(RenderItemOrder.Comparison);

        Assert.Equal(new ulong[] { 10UL, 20UL, 30UL }, items.Select(static i => i.SortKey).ToArray());
    }

    [Fact]
    public void SortedList_HasNonDecreasingSortKey()
    {
        var items = BuildMixedList(300, true);

        items.Sort(RenderItemOrder.Comparison);

        for (int i = 1; i < items.Count; i++)
        {
            Assert.True(items[i - 1].SortKey <= items[i].SortKey);
        }
    }

    [Fact]
    public void TransparentItemsAtTheSameDistanceStep_KeepTheirSubmissionOrder()
    {
        var order = new RenderItemOrder();
        var buffer = new object();
        var materialId = Guid.NewGuid();
        var items = new List<RenderItem>();
        for (int i = 0; i < 100; i++)
        {
            // Same material, same buffer, distances closer than the 0.1 quantisation step: one key.
            float distance = 5.0f + (i % 7) * 0.001f;
            var item = Item(0, i, i);
            item.SortKey = order.GenerateKey(RenderQueue.Transparent, ShaderId, materialId, buffer, distance);
            items.Add(item);
        }

        items.Sort(RenderItemOrder.Comparison);

        Assert.Equal(Enumerable.Range(0, 100).ToList(), items.Select(static i => (int)i.DistanceToCamera).ToList());
    }

    [Fact]
    public void MeshPartOfTheKey_IsTheSameForTwoListsSubmittingTheSameBuffersInTheSameOrder()
    {
        var firstBuffers = new[] { new object(), new object(), new object(), new object() };
        var secondBuffers = new[] { new object(), new object(), new object(), new object() };
        int[] submission = { 2, 0, 0, 3, 1, 2, 3 };
        var materialId = Guid.NewGuid();

        var firstOrder = new RenderItemOrder();
        var secondOrder = new RenderItemOrder();
        var firstKeys = submission.Select(b => firstOrder.GenerateKey(RenderQueue.Opaque, ShaderId, materialId, firstBuffers[b], 1.0f)).ToArray();
        var secondKeys = submission.Select(b => secondOrder.GenerateKey(RenderQueue.Opaque, ShaderId, materialId, secondBuffers[b], 1.0f)).ToArray();

        Assert.Equal(firstKeys, secondKeys);
    }

    [Fact]
    public void MeshPartOfTheKey_SeparatesDistinctBuffers_AndGroupsTheSameBuffer()
    {
        var order = new RenderItemOrder();
        var a = new object();
        var b = new object();
        var materialId = Guid.NewGuid();

        ulong keyA1 = order.GenerateKey(RenderQueue.Opaque, ShaderId, materialId, a, 1.0f);
        ulong keyB = order.GenerateKey(RenderQueue.Opaque, ShaderId, materialId, b, 1.0f);
        ulong keyA2 = order.GenerateKey(RenderQueue.Opaque, ShaderId, materialId, a, 9.0f);

        Assert.Equal(keyA1, keyA2);
        Assert.NotEqual(keyA1, keyB);
    }

    [Fact]
    public void MaterialPart_IsTheSameForTwoListsBuiltWithDistinctGeneratedMaterialIds()
    {
        // A generated material gets Guid.NewGuid() as its id: two runs never share ids, but submit in the same order.
        Guid[] firstIds = { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        Guid[] secondIds = { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        int[] submission = { 1, 0, 2, 2, 0, 1 };
        var firstBuffer = new object();
        var secondBuffer = new object();

        var firstOrder = new RenderItemOrder();
        var secondOrder = new RenderItemOrder();
        var firstKeys = submission.Select(m => firstOrder.GenerateKey(RenderQueue.Opaque, ShaderId, firstIds[m], firstBuffer, 1.0f)).ToArray();
        var secondKeys = submission.Select(m => secondOrder.GenerateKey(RenderQueue.Opaque, ShaderId, secondIds[m], secondBuffer, 1.0f)).ToArray();

        Assert.Equal(firstKeys, secondKeys);
        Assert.Equal(3, firstKeys.Distinct().Count());
    }

    [Fact]
    public void Reset_StartsANewFrameWithTheSameIdsForTheSameSubmission()
    {
        var order = new RenderItemOrder();
        var buffers = new[] { new object(), new object() };
        var materialIds = new[] { Guid.NewGuid(), Guid.NewGuid() };

        ulong[] RunFrame() => new[]
        {
            order.GenerateKey(RenderQueue.Opaque, ShaderId, materialIds[1], buffers[1], 1.0f),
            order.GenerateKey(RenderQueue.Opaque, ShaderId, materialIds[0], buffers[0], 1.0f),
        };

        var first = RunFrame();
        order.Reset();
        var second = RunFrame();

        Assert.Equal(first, second);
    }

    [Fact]
    public void SubmissionAndSortOfAWarmList_AllocateNothing()
    {
        var order = new RenderItemOrder();
        var buffers = new object[8];
        var materialIds = new Guid[8];
        for (int i = 0; i < 8; i++)
        {
            buffers[i] = new object();
            materialIds[i] = Guid.NewGuid();
        }

        var items = new List<RenderItem>(256);

        void Frame()
        {
            order.Reset();
            items.Clear();
            for (int i = 0; i < 200; i++)
            {
                var item = Item(0, items.Count, i);
                item.SortKey = order.GenerateKey(RenderQueue.Opaque, ShaderId, materialIds[(i * 5) % 8], buffers[(i * 3) % 8], 1.0f);
                items.Add(item);
            }

            items.Sort(RenderItemOrder.Comparison);
        }

        Frame();
        Frame();

        var before = AllocationWindow.Start();
        for (int i = 0; i < 1000; i++)
        {
            Frame();
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }
}
