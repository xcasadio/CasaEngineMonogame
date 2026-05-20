using CasaEngine.Framework.Particles;
using CasaEngine.Framework.Particles.Rendering;
using Xunit;

namespace CasaEngine.Tests.Particles;

public class ParticleRenderPacketSorterTests
{
    [Fact]
    public void Sort_OrdersByRenderQueueThenLayer()
    {
        var packets = new List<ParticleRenderPacket>
        {
            CreatePacket(3, renderQueue: 3000, layer: 2, distance: 1.0f, ParticleSortMode.None),
            CreatePacket(1, renderQueue: 2000, layer: 2, distance: 1.0f, ParticleSortMode.None),
            CreatePacket(2, renderQueue: 3000, layer: 1, distance: 1.0f, ParticleSortMode.None),
        };

        ParticleRenderPacketSorter.Sort(packets);

        Assert.Equal(1, packets[0].ParticleIndex);
        Assert.Equal(2, packets[1].ParticleIndex);
        Assert.Equal(3, packets[2].ParticleIndex);
    }

    [Fact]
    public void Sort_OrdersDistanceBackToFrontInsideQueueAndLayer()
    {
        var packets = new List<ParticleRenderPacket>
        {
            CreatePacket(1, renderQueue: 3000, layer: 0, distance: 4.0f, ParticleSortMode.Distance),
            CreatePacket(2, renderQueue: 3000, layer: 0, distance: 16.0f, ParticleSortMode.Distance),
            CreatePacket(3, renderQueue: 3000, layer: 0, distance: 9.0f, ParticleSortMode.Distance),
        };

        ParticleRenderPacketSorter.Sort(packets);

        Assert.Equal(2, packets[0].ParticleIndex);
        Assert.Equal(3, packets[1].ParticleIndex);
        Assert.Equal(1, packets[2].ParticleIndex);
    }

    [Fact]
    public void Sort_PreservesInputOrderForEquivalentKeys()
    {
        var packets = new List<ParticleRenderPacket>
        {
            CreatePacket(1, renderQueue: 3000, layer: 0, distance: 4.0f, ParticleSortMode.None),
            CreatePacket(2, renderQueue: 3000, layer: 0, distance: 16.0f, ParticleSortMode.None),
            CreatePacket(3, renderQueue: 3000, layer: 0, distance: 9.0f, ParticleSortMode.None),
        };

        ParticleRenderPacketSorter.Sort(packets);

        Assert.Equal(1, packets[0].ParticleIndex);
        Assert.Equal(2, packets[1].ParticleIndex);
        Assert.Equal(3, packets[2].ParticleIndex);
    }

    private static ParticleRenderPacket CreatePacket(int particleIndex, int renderQueue, int layer, float distance, ParticleSortMode sortMode)
        => new()
        {
            ParticleIndex = particleIndex,
            RenderQueue = renderQueue,
            Layer = layer,
            DistanceToCameraSquared = distance,
            SortMode = sortMode,
        };
}