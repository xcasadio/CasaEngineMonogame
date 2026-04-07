

namespace CasaEngine.Framework.Rendering.Draw;

/// <summary>
/// Generates a 64-bit sort key used to order <see cref="RenderItem"/>s
/// in a way that minimises GPU state changes.
///
/// Bit layout (MSB → LSB):
/// <code>
/// [63..60]  queue        (4 bits)  — RenderQueue value
/// [59..44]  shaderHash   (16 bits) — groups items by shader
/// [43..28]  materialHash (16 bits) — groups items by material instance
/// [27..12]  meshHash     (16 bits) — groups items by mesh (vertex buffer)
/// [11..0]   distBits     (12 bits) — for transparent: reversed distance (far first)
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
        ulong key = 0;
        key |= ((ulong)queue   & 0xF)      << 60;
        key |= ((ulong)(shaderHash   & 0xFFFF)) << 44;
        key |= ((ulong)(materialHash & 0xFFFF)) << 28;
        key |= ((ulong)(meshHash     & 0xFFFF)) << 12;

        if (queue >= RenderQueue.Transparent)
        {
            // Invert distance so that farther objects sort first (back-to-front blending).
            var distBits = (uint)Math.Clamp((int)(distance * 10f), 0, 0xFFF);
            key |= (ulong)(0xFFF - distBits);
        }

        return key;
    }
}
