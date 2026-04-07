using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Framework.Rendering.Shaders;

namespace CasaEngine.Framework.Game.Components;

public class PrimitiveBatch
    : IDisposable
{
    private const int DefaultBufferSize = 500;

    private Effect? _effect;

    // the device that we will issue draw calls to.
    private readonly GraphicsDevice _device;

    // hasBegun is flipped to true once Begin is called, and is used to make
    // sure users don't call End before Begin is called.
    private bool _hasBegun;

    private bool _isDisposed;
    private readonly VertexPositionColor[] _lineVertices;
    private int _lineVertsCount;
    private readonly VertexPositionColor[] _triangleVertices;
    private int _triangleVertsCount;


    public PrimitiveBatch(GraphicsDevice graphicsDevice)
        : this(graphicsDevice, DefaultBufferSize)
    {
    }

    public PrimitiveBatch(GraphicsDevice graphicsDevice, int bufferSize)
        : this(graphicsDevice, null, bufferSize)
    {
    }

    public PrimitiveBatch(GraphicsDevice graphicsDevice, Effect? effect, int bufferSize)
    {
        if (graphicsDevice == null)
        {
            throw new ArgumentNullException(nameof(graphicsDevice));
        }
        _device = graphicsDevice;

        _triangleVertices = new VertexPositionColor[bufferSize - bufferSize % 3];
        _lineVertices = new VertexPositionColor[bufferSize - bufferSize % 2];
        _effect = effect;
    }


    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }


    public void SetProjection(ref Matrix projection)
    {
        if (_effect != null)
        {
            _effect.Parameters[ShaderParameterNames.WorldViewProj]?.SetValue(projection);
        }
    }

    public Effect? Effect
    {
        get => _effect;
        set => _effect = value;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing && !_isDisposed)
        {
            if (_effect != null)
            {
                _effect.Dispose();
            }

            _isDisposed = true;
        }
    }


    public void Begin(ref Matrix projection, ref Matrix view, ref Matrix world)
    {
        if (_hasBegun)
        {
            throw new InvalidOperationException("End must be called before Begin can be called again.");
        }

        if (_effect == null)
        {
            throw new InvalidOperationException("PrimitiveBatch requires a CasaEngine Effect before Begin is called.");
        }

        _effect.Parameters[ShaderParameterNames.WorldViewProj]?.SetValue(world * view * projection);
        _effect.Parameters[ShaderParameterNames.ColorMultiplier]?.SetValue(Vector4.One);

        // flip the error checking boolean. It's now ok to call AddVertex, Flush,
        // and End.
        _hasBegun = true;
    }

    public bool IsReady()
    {
        return _hasBegun;
    }

    public void AddVertex(Vector2 vertex, Color color, PrimitiveType primitiveType)
    {
        if (!_hasBegun)
        {
            throw new InvalidOperationException("Begin must be called before AddVertex can be called.");
        }
        if (primitiveType == PrimitiveType.LineStrip ||
            primitiveType == PrimitiveType.TriangleStrip)
        {
            throw new NotSupportedException("The specified primitiveType is not supported by PrimitiveBatch.");
        }

        if (primitiveType == PrimitiveType.TriangleList)
        {
            if (_triangleVertsCount >= _triangleVertices.Length)
            {
                FlushTriangles();
            }
            _triangleVertices[_triangleVertsCount].Position = new Vector3(vertex, -0.1f);
            _triangleVertices[_triangleVertsCount].Color = color;
            _triangleVertsCount++;
        }
        if (primitiveType == PrimitiveType.LineList)
        {
            if (_lineVertsCount >= _lineVertices.Length)
            {
                FlushLines();
            }
            _lineVertices[_lineVertsCount].Position = new Vector3(vertex, 0f);
            _lineVertices[_lineVertsCount].Color = color;
            _lineVertsCount++;
        }
    }


    public void End()
    {
        if (!_hasBegun)
        {
            throw new InvalidOperationException("Begin must be called before End can be called.");
        }

        // Draw whatever the user wanted us to draw
        FlushTriangles();
        FlushLines();

        _hasBegun = false;
    }

    private void FlushTriangles()
    {
        if (!_hasBegun)
        {
            throw new InvalidOperationException("Begin must be called before Flush can be called.");
        }
        if (_triangleVertsCount >= 3)
        {
            var primitiveCount = _triangleVertsCount / 3;
            // submit the draw call to the graphics card
            _device.SamplerStates[0] = SamplerState.AnisotropicClamp;
            foreach (var pass in _effect!.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserPrimitives(PrimitiveType.TriangleList, _triangleVertices, 0, primitiveCount);
            }
            _triangleVertsCount -= primitiveCount * 3;
        }
    }

    private void FlushLines()
    {
        if (!_hasBegun)
        {
            throw new InvalidOperationException("Begin must be called before Flush can be called.");
        }
        if (_lineVertsCount >= 2)
        {
            var primitiveCount = _lineVertsCount / 2;
            // submit the draw call to the graphics card
            _device.SamplerStates[0] = SamplerState.AnisotropicClamp;
            foreach (var pass in _effect!.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserPrimitives(PrimitiveType.LineList, _lineVertices, 0, primitiveCount);
            }
            _lineVertsCount -= primitiveCount * 2;
        }
    }
}