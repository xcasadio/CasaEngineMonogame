using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Engine.Primitives.TwoD;

public abstract class Primitive2D : IDisposable
{
    private List<VertexPositionColor> vertices = new();
    private List<ushort> indices = new();

    private VertexDeclaration vertexDeclaration;
    private VertexBuffer vertexBuffer;
    private IndexBuffer indexBuffer;
    private Effect? effect;

    /// <summary>
    /// Finalizer.
    /// </summary>
    ~Primitive2D()
    {
        Dispose(false);
    }

    /// <summary>
    /// Frees resources used by this object.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Frees resources used by this object.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (vertexDeclaration != null)
            {
                vertexDeclaration.Dispose();
            }

            if (vertexBuffer != null)
            {
                vertexBuffer.Dispose();
            }

            if (indexBuffer != null)
            {
                indexBuffer.Dispose();
            }

            if (effect != null)
            {
                effect.Dispose();
            }
        }
    }

    /// <summary>
    /// Adds a new vertex to the primitive model. This should only be called
    /// during the initialization process, before InitializePrimitive.
    /// </summary>
    protected void AddVertex(Vector3 position, Color color)
    {
        vertices.Add(new VertexPositionColor(position, color));
    }

    /// <summary>
    /// Adds a new index to the primitive model. This should only be called
    /// during the initialization process, before InitializePrimitive.
    /// </summary>
    protected void AddIndex(int index)
    {
        if (index > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        indices.Add((ushort)index);
    }

    /// <summary>
    /// Queries the index of the current vertex. This starts at
    /// zero, and increments every time AddVertex is called.
    /// </summary>
    protected int CurrentVertex => vertices.Count;

    /// <summary>
    /// Once all the geometry has been specified by calling AddVertex and AddIndex,
    /// this method copies the vertex and index data into GPU format buffers, ready
    /// for efficient rendering.
    protected void InitializePrimitive(GraphicsDevice graphicsDevice)
    {
        // Create a vertex declaration, describing the format of our vertex data.
        vertexDeclaration = VertexPositionColor.VertexDeclaration;

        // Create a vertex buffer, and copy our vertex data into it.
        vertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionColor), vertices.Count, BufferUsage.None);

        vertexBuffer.SetData(vertices.ToArray());

        // Create an index buffer, and copy our index data into it.
        indexBuffer = new IndexBuffer(graphicsDevice, typeof(ushort), indices.Count, BufferUsage.None);

        indexBuffer.SetData(indices.ToArray());
    }

    protected void SetEffect(Effect primitiveEffect)
    {
        effect = primitiveEffect;
    }

    /// <summary>
    /// Draws the primitive model, using the specified effect. Unlike the other
    /// Draw overload where you just specify the world/view/projection matrices
    /// and color, this method does not set any renderstates, so you must make
    /// sure all states are set to sensible values before you call it.
    /// </summary>
    public void Draw(Effect effect)
    {
        GraphicsDevice graphicsDevice = effect.GraphicsDevice;
        graphicsDevice.SetVertexBuffer(vertexBuffer);
        graphicsDevice.Indices = indexBuffer;

        //effect. Begin(SaveStateMode.SaveState);
        //
        //foreach (EffectPass effectPass in effect.CurrentTechnique.Passes)
        //{
        //    effectPass.Begin();
        //
        //    int primitiveCount = indices.Count / 3;
        //    
        //    graphicsDevice.DrawIndexedPrimitives(PrimitiveType.LineList, 0, 0, primitiveCount);
        //
        //    effectPass.End();
        //}
        //
        //effect.End();
    }

    /// <summary>
    /// Draws the primitive model using the currently assigned CasaEngine effect.
    /// Unlike the other Draw overload where you specify a custom effect,
    /// this method applies the world-view-projection and color parameters when
    /// the assigned effect exposes them.
    /// </summary>
    public void Draw(Matrix world, Matrix view, Matrix projection, Color color)
    {
        if (effect == null)
        {
            return;
        }

        effect.Parameters[PrimitiveEffectParameterNames.WorldViewProj]?.SetValue(world * view * projection);
        effect.Parameters[PrimitiveEffectParameterNames.ColorMultiplier]?.SetValue(color.ToVector4());
        effect.Parameters[PrimitiveEffectParameterNames.SolidColor]?.SetValue(color.ToVector4());

        // Set important renderstates.
        //RenderState renderState = effect.GraphicsDevice.RenderState;
        //
        //renderState.AlphaTestEnable = false;
        //renderState.DepthBufferEnable = false;// true;
        //renderState.DepthBufferFunction = CompareFunction.LessEqual;
        //
        //if (color.A < 255)
        //{
        //    renderState.AlphaBlendEnable = true;
        //    renderState.AlphaBlendOperation = BlendFunction.Add;
        //    renderState.SourceBlend = Blend.SourceAlpha;
        //    renderState.DestinationBlend = Blend.InverseSourceAlpha;
        //    renderState.SeparateAlphaBlendEnabled = false;
        //    renderState.DepthBufferWriteEnable = false;
        //}
        //else
        //{
        //    renderState.AlphaBlendEnable = false;
        //    renderState.DepthBufferWriteEnable = true;
        //}

        Draw(effect);
    }

}