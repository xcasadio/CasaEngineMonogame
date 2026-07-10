

namespace CasaEngine.Framework.Rendering.Draw;

/// <summary>
/// Generates a 64-bit sort key used to order <see cref="RenderItem"/>s
/// in a way that minimises GPU state changes.
///
/// Bit layout (MSB → LSB):
///
/// Common prefix:
/// <code>
/// [63..60]  queue (4 bits) — compact mapping: 0=Opaque, 1=AlphaTest, 2=Transparent, 3=Overlay
/// </code>
///
/// Opaque / alpha-test (queue &lt; Transparent) — pure state grouping, depth is not encoded:
/// <code>
/// [59..44]  shaderHash   (16 bits) — groups items by shader
/// [43..28]  materialHash (16 bits) — groups items by material instance
/// [27..12]  meshHash     (16 bits) — groups items by mesh (vertex buffer)
/// [11..0]   unused
/// </code>
///
/// Transparent / overlay (queue &gt;= Transparent) — distance dominates so items sort
/// back-to-front across different materials; state hashes only tie-break items at the
/// same quantised depth:
/// <code>
/// [59..48]  distBits     (12 bits) — reversed distance (far first)
/// [47..32]  shaderHash   (16 bits) — groups items by shader
/// [31..16]  materialHash (16 bits) — groups items by material instance
/// [15..0]   meshHash     (16 bits) — groups items by mesh (vertex buffer)
/// </code>
/// </summary>
public static class SortKeyGenerator
{
    /// <param name="queue">Render queue (opaque, alpha-test, transparent, overlay).</param>
    /// <param name="shaderHash">Hash derived from the shader asset id.</param>
    /// <param name="materialHash">Hash derived from the material instance id.</param>
    /// <param name="meshHash">Hash derived from the mesh / vertex-buffer id.</param>
    /// <param name="distance">Camera-space depth for transparent back-to-front sorting.</param>
    public static ulong Generate(
        RenderQueue queue,
        int shaderHash,
        int materialHash,
        int meshHash,
        float distance = 0f)
    {
        ulong key = QueueBits(queue) << 60;

        if (queue >= RenderQueue.Transparent)
        {
            // Distance dominates for transparent/overlay: back-to-front ordering across
            // different materials. Reversed so that farther objects sort first, state
            // hashes only tie-break items at the same quantised depth.
            var distBits = (uint)Math.Clamp((int)(distance * 10f), 0, 0xFFF);
            key |= ((ulong)(0xFFF - distBits)) << 48;
            key |= ((ulong)(shaderHash   & 0xFFFF)) << 32;
            key |= ((ulong)(materialHash & 0xFFFF)) << 16;
            key |=  (ulong)(meshHash     & 0xFFFF);
        }
        else
        {
            // Opaque/alpha-test: pure state grouping, depth is not encoded.
            key |= ((ulong)(shaderHash   & 0xFFFF)) << 44;
            key |= ((ulong)(materialHash & 0xFFFF)) << 28;
            key |= ((ulong)(meshHash     & 0xFFFF)) << 12;
        }

        return key;
    }

    private static ulong QueueBits(RenderQueue queue) => queue switch
    {
        RenderQueue.Opaque      => 0,
        RenderQueue.AlphaTest   => 1,
        RenderQueue.Transparent => 2,
        RenderQueue.Overlay     => 3,
        _ => queue >= RenderQueue.Transparent ? 3UL : 1UL,
    };
}
