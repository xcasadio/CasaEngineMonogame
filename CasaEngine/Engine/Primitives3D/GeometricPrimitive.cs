using CasaEngine.Framework.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Engine.Primitives3D;

public abstract class GeometricPrimitive : IDisposable
{
    private GeometricPrimitiveType _type;

    private readonly List<VertexPositionNormalTexture> _vertices = new();
    private readonly List<ushort> _indices = new();

    private VertexBuffer _vertexBuffer;
    private IndexBuffer _indexBuffer;
    private BasicEffect _basicEffect;

    protected int CurrentVertex => _vertices.Count;

    protected GeometricPrimitive(GeometricPrimitiveType type)
    {
        _type = type;
    }

    public StaticMesh CreateMesh()
    {
        var mesh = new StaticMesh();
        mesh.AddVertices(_vertices);
        mesh.AddIndices(_indices);

        return mesh;
    }

    protected void AddVertex(Vector3 position, Vector3 normal, Vector2 uv)
    {
        _vertices.Add(new VertexPositionNormalTexture(position, normal, uv));
    }

    protected void AddIndex(int index)
    {
        if (index > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        _indices.Add((ushort)index);
    }

    protected void InitializePrimitive(GraphicsDevice graphicsDevice)
    {
        _vertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionNormalTexture), _vertices.Count, BufferUsage.None);
        _vertexBuffer.SetData(_vertices.ToArray());

        _indexBuffer = new IndexBuffer(graphicsDevice, typeof(ushort), _indices.Count, BufferUsage.None);
        _indexBuffer.SetData(_indices.ToArray());

        _basicEffect = new BasicEffect(graphicsDevice);

        _basicEffect.EnableDefaultLighting();
        _basicEffect.PreferPerPixelLighting = true;
    }

    ~GeometricPrimitive()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        if (_vertexBuffer != null)
        {
            _vertexBuffer.Dispose();
        }

        if (_indexBuffer != null)
        {
            _indexBuffer.Dispose();
        }

        if (_basicEffect != null)
        {
            _basicEffect.Dispose();
        }
    }

    /// <summary>
    /// Draws the primitive model, using the specified effect. Unlike the other
    /// Draw overload where you just specify the world/view/projection matrices
    /// and color, this method does not set any renderstates, so you must make
    /// sure all states are set to sensible values before you call it.
    /// </summary>
    public void Draw(Effect effect)
    {
        var graphicsDevice = effect.GraphicsDevice;

        graphicsDevice.SetVertexBuffer(_vertexBuffer);
        graphicsDevice.Indices = _indexBuffer;

        //effect.Parameters["WorldViewProj"] = ;

        foreach (var effectPass in effect.CurrentTechnique.Passes)
        {
            effectPass.Apply();
            var primitiveCount = _indices.Count / 3;
            graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, primitiveCount);
        }
    }

    /// <summary>
    /// Draws the primitive model, using a BasicEffect shader with default
    /// lighting. Unlike the other Draw overload where you specify a custom
    /// effect, this method sets important renderstates to sensible values
    /// for 3D model rendering, so you do not need to set these states before
    /// you call it.
    /// </summary>
    public void Draw(Matrix world, Matrix view, Matrix projection, Color color)
    {
        // Set BasicEffect parameters.
        _basicEffect.World = world;
        _basicEffect.View = view;
        _basicEffect.Projection = projection;
        _basicEffect.DiffuseColor = color.ToVector3();
        _basicEffect.Alpha = color.A / 255.0f;

        // Set important renderstates.
        /*RenderState renderState = basicEffect.GraphicsDevice.RenderState;

        renderState.AlphaTestEnable = false;
        renderState.DepthBufferEnable = true;
        renderState.DepthBufferFunction = CompareFunction.LessEqual;

        if (color.A < 255)
        {
            // Set renderstates for alpha blended rendering.
            renderState.AlphaBlendEnable = true;
            renderState.AlphaBlendOperation = BlendFunction.Add;
            renderState.SourceBlend = Blend.SourceAlpha;
            renderState.DestinationBlend = Blend.InverseSourceAlpha;
            renderState.SeparateAlphaBlendEnabled = false;
            renderState.DepthBufferWriteEnable = false;
        }
        else
        {
            // Set renderstates for opaque rendering.
            renderState.AlphaBlendEnable = false;
            renderState.DepthBufferWriteEnable = true;
        }*/

        // Draw the model, using BasicEffect.
        Draw(_basicEffect);
    }

#if EDITOR
    public List<Vector3> Vertex
    {
        get
        {
            return _vertices.Select(v => v.Position).ToList();
        }
    }
#endif
}