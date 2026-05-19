
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering.Draw;

/// <summary>
/// Groups <see cref="RenderItem"/>s that share the same mesh and material variant,
/// then issues a single <c>DrawInstancedPrimitives</c> call for the whole group.
///
/// Requires the shader to read per-instance world matrices from a second vertex stream
/// using BLENDWEIGHT 0–3 semantics (see <see cref="InstanceData"/>).
///
/// Usage:
/// <code>
/// batcher.BeginFrame();
/// foreach (var group in renderItems.GroupBy(i => (i.Mesh.VertexBuffer, i.Material)))
///     batcher.DrawGroup(...);
/// batcher.Flush(graphicsDevice, shader, context);
/// </code>
///
/// The batcher is optional — the forward renderer falls back to individual draw calls
/// when the group size is below <see cref="MinInstanceThreshold"/>.
/// </summary>
public sealed class InstanceBatcher : IDisposable
{
    // -----------------------------------------------------------------------
    //  Constants
    // -----------------------------------------------------------------------

    /// <summary>
    /// Minimum number of instances in a group to justify the instanced path.
    /// Groups below this threshold use individual draw calls instead.
    /// </summary>
    public int MinInstanceThreshold { get; set; } = 4;

    private const int InitialCapacity = 256;

    // -----------------------------------------------------------------------
    //  Fields
    // -----------------------------------------------------------------------

    private readonly GraphicsDevice _device;
    private InstanceData[]     _stagingBuffer;
    private DynamicVertexBuffer? _instanceVB;
    private bool _disposed;

    // -----------------------------------------------------------------------
    //  Constructor / Dispose
    // -----------------------------------------------------------------------

    public InstanceBatcher(GraphicsDevice device)
    {
        _device        = device ?? throw new ArgumentNullException(nameof(device));
        _stagingBuffer = new InstanceData[InitialCapacity];
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _instanceVB?.Dispose();
    }

    // -----------------------------------------------------------------------
    //  Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Draws a group of instances sharing the same mesh and shader via hardware instancing.
    /// Each element in <paramref name="items"/> must reference the same
    /// <see cref="RenderItem.Mesh"/> vertex/index buffers.
    /// </summary>
    /// <param name="items">Render items to batch (same mesh + material).</param>
    /// <param name="shader">Bound and configured shader wrapper.</param>
    /// <param name="context">Current render context.</param>
    public void DrawInstancedGroup(
        IReadOnlyList<RenderItem> items,
        ShaderWrapper shader,
        in RenderContext context,
        bool techniqueSelectedBySelector = false)
    {
        if (items.Count == 0)
        {
            return;
        }

        EnsureBufferCapacity(items.Count);

        // Fill staging data
        for (int i = 0; i < items.Count; i++)
            _stagingBuffer[i] = InstanceData.FromMatrix(items[i].World);

        // Upload to GPU
        EnsureInstanceVB(items.Count);
        _instanceVB!.SetData(_stagingBuffer, 0, items.Count, SetDataOptions.Discard);

        var firstItem = items[0];
        var mesh = firstItem.Mesh;
        _device.SetVertexBuffers(
            new VertexBufferBinding(mesh.VertexBuffer!),
            new VertexBufferBinding(_instanceVB, 0, 1)); // frequency = 1 (per-instance)
        _device.Indices = mesh.IndexBuffer;

        // Bind material params (world will come from the instance stream)
        if (firstItem.Material.RequiresMaterialTechniqueSelection(
            techniqueSelectedBySelector,
            in context,
            firstItem.Features))
        {
            firstItem.Material.SelectTechnique(shader, in context, firstItem.Features);
        }
        firstItem.Material.Bind(shader, in context, Microsoft.Xna.Framework.Matrix.Identity);
        shader.SetParameter(ShaderParameterNames.ReceiveShadows, firstItem.EffectiveReceiveShadows ? 1.0f : 0.0f);

        int primCount = mesh.IndexBuffer!.IndexCount / 3;

        for (int p = 0; p < shader.PassCount; p++)
        {
            shader.ApplyPass(p);
            _device.DrawInstancedPrimitives(
                mesh.PrimitiveType,
                baseVertex: 0,
                startIndex: 0,
                primitiveCount: primCount,
                instanceCount: items.Count);
        }
    }

    // -----------------------------------------------------------------------
    //  Helpers
    // -----------------------------------------------------------------------

    private void EnsureBufferCapacity(int required)
    {
        if (_stagingBuffer.Length >= required)
        {
            return;
        }

        int newSize = Math.Max(required, _stagingBuffer.Length * 2);
        _stagingBuffer = new InstanceData[newSize];
    }

    private void EnsureInstanceVB(int required)
    {
        if (_instanceVB is not null && _instanceVB.VertexCount >= required)
        {
            return;
        }

        _instanceVB?.Dispose();
        int capacity = Math.Max(required, InitialCapacity);
        _instanceVB = new DynamicVertexBuffer(_device, InstanceData.VertexDeclaration,
            capacity, BufferUsage.WriteOnly);
    }
}
