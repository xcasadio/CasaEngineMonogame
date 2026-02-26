using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering.Draw;

/// <summary>
/// Per-instance data streamed to the GPU via a second vertex buffer.
/// Layout: a 4×4 world matrix stored as four BLENDWEIGHT vertex elements.
/// Used by hardware-instanced draw calls (Phase 9).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct InstanceData : IVertexType
{
    // World matrix rows packed as 4 × Vector4 (16 bytes each = 64 bytes total)
    public Vector4 WorldRow0;
    public Vector4 WorldRow1;
    public Vector4 WorldRow2;
    public Vector4 WorldRow3;

    /// <summary>Vertex declaration that maps the four rows to BLENDWEIGHT semantics 0–3.</summary>
    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement( 0, VertexElementFormat.Vector4, VertexElementUsage.BlendWeight, 0),
        new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.BlendWeight, 1),
        new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.BlendWeight, 2),
        new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.BlendWeight, 3));

    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;

    // -----------------------------------------------------------------------
    //  Helpers
    // -----------------------------------------------------------------------

    /// <summary>Creates an <see cref="InstanceData"/> from a world transform matrix.</summary>
    public static InstanceData FromMatrix(in Matrix world) => new()
    {
        WorldRow0 = new Vector4(world.M11, world.M12, world.M13, world.M14),
        WorldRow1 = new Vector4(world.M21, world.M22, world.M23, world.M24),
        WorldRow2 = new Vector4(world.M31, world.M32, world.M33, world.M34),
        WorldRow3 = new Vector4(world.M41, world.M42, world.M43, world.M44),
    };

    /// <summary>Reconstructs the <see cref="Matrix"/> from stored rows.</summary>
    public readonly Matrix ToMatrix() => new(
        WorldRow0.X, WorldRow0.Y, WorldRow0.Z, WorldRow0.W,
        WorldRow1.X, WorldRow1.Y, WorldRow1.Z, WorldRow1.W,
        WorldRow2.X, WorldRow2.Y, WorldRow2.Z, WorldRow2.W,
        WorldRow3.X, WorldRow3.Y, WorldRow3.Z, WorldRow3.W);
}
