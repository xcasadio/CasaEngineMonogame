using CasaEngine.Framework.Particles;

namespace CasaEngine.Framework.Particles.Rendering;

public static class ParticleRenderPacketSorter
{
    public static void Sort(List<ParticleRenderPacket> packets)
    {
        ArgumentNullException.ThrowIfNull(packets);

        for (int packetIndex = 1; packetIndex < packets.Count; packetIndex++)
        {
            ParticleRenderPacket packet = packets[packetIndex];
            int sortedIndex = packetIndex - 1;

            while (sortedIndex >= 0 && Compare(packets[sortedIndex], packet) > 0)
            {
                packets[sortedIndex + 1] = packets[sortedIndex];
                sortedIndex--;
            }

            packets[sortedIndex + 1] = packet;
        }
    }

    public static int Compare(in ParticleRenderPacket left, in ParticleRenderPacket right)
    {
        int renderQueueComparison = left.RenderQueue.CompareTo(right.RenderQueue);
        if (renderQueueComparison != 0)
        {
            return renderQueueComparison;
        }

        int layerComparison = left.Layer.CompareTo(right.Layer);
        if (layerComparison != 0)
        {
            return layerComparison;
        }

        if (left.SortMode == ParticleSortMode.Distance || right.SortMode == ParticleSortMode.Distance)
        {
            return right.DistanceToCameraSquared.CompareTo(left.DistanceToCameraSquared);
        }

        return 0;
    }
}