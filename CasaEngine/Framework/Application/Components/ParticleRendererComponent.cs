using CasaEngine.Framework.Particles.Rendering;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Application.Components;

public sealed class ParticleRendererComponent : DrawableGameComponent, IViewFlushableRenderer
{
    private const int InitialPacketCapacity = 1024;
    private readonly List<ParticleRenderPacket> _packets = new(InitialPacketCapacity);
    private VertexPositionColor[] _vertices = new VertexPositionColor[InitialPacketCapacity * 4];
    private int[] _indices = new int[InitialPacketCapacity * 6];
    private BasicEffect? _effect;

    public int PendingPacketCount => _packets.Count;

    public int FrameFlushedParticleCount { get; private set; }

    public ParticleRendererComponent(Game game) : base(game)
    {
        ArgumentNullException.ThrowIfNull(game);

        game.Components.Add(this);
        UpdateOrder = (int)ComponentUpdateOrder.ParticleComponent;
        DrawOrder = (int)ComponentDrawOrder.ParticleComponent;
    }

    protected override void LoadContent()
    {
        _effect = new BasicEffect(GraphicsDevice)
        {
            VertexColorEnabled = true,
            TextureEnabled = false,
            LightingEnabled = false,
        };
    }

    public override void Update(GameTime gameTime)
    {
        FrameFlushedParticleCount = 0;
        base.Update(gameTime);
    }

    public void Submit(IReadOnlyList<ParticleRenderPacket> packets)
    {
        ArgumentNullException.ThrowIfNull(packets);

        for (int packetIndex = 0; packetIndex < packets.Count; packetIndex++)
        {
            _packets.Add(packets[packetIndex]);
        }
    }

    public void Submit(in ParticleRenderPacket packet)
    {
        _packets.Add(packet);
    }

    public void Flush(in RenderFrame frame, RenderStats? stats = null)
    {
        if (_packets.Count == 0 || _effect == null)
        {
            return;
        }

        EnsureCapacity(_packets.Count);
        BuildBillboardBuffers(in frame);
        DrawPackets(in frame);
        FrameFlushedParticleCount += _packets.Count;
        _packets.Clear();
    }

    private void EnsureCapacity(int packetCount)
    {
        int vertexCount = packetCount * 4;
        if (_vertices.Length < vertexCount)
        {
            _vertices = new VertexPositionColor[vertexCount];
        }

        int indexCount = packetCount * 6;
        if (_indices.Length < indexCount)
        {
            _indices = new int[indexCount];
        }
    }

    private void BuildBillboardBuffers(in RenderFrame frame)
    {
        Vector3 cameraRight = new(frame.View.M11, frame.View.M21, frame.View.M31);
        Vector3 cameraUp = new(frame.View.M12, frame.View.M22, frame.View.M32);

        for (int packetIndex = 0; packetIndex < _packets.Count; packetIndex++)
        {
            ParticleRenderPacket packet = _packets[packetIndex];
            float halfWidth = packet.Size.X * 0.5f;
            float halfHeight = packet.Size.Y * 0.5f;
            float rotationCos = MathF.Cos(packet.Rotation);
            float rotationSin = MathF.Sin(packet.Rotation);
            Vector3 rotatedRight = (cameraRight * rotationCos + cameraUp * rotationSin) * halfWidth;
            Vector3 rotatedUp = (-cameraRight * rotationSin + cameraUp * rotationCos) * halfHeight;

            int vertexOffset = packetIndex * 4;
            _vertices[vertexOffset + 0] = new VertexPositionColor(packet.Position - rotatedRight + rotatedUp, packet.Color);
            _vertices[vertexOffset + 1] = new VertexPositionColor(packet.Position + rotatedRight + rotatedUp, packet.Color);
            _vertices[vertexOffset + 2] = new VertexPositionColor(packet.Position + rotatedRight - rotatedUp, packet.Color);
            _vertices[vertexOffset + 3] = new VertexPositionColor(packet.Position - rotatedRight - rotatedUp, packet.Color);

            int indexOffset = packetIndex * 6;
            _indices[indexOffset + 0] = vertexOffset + 0;
            _indices[indexOffset + 1] = vertexOffset + 1;
            _indices[indexOffset + 2] = vertexOffset + 2;
            _indices[indexOffset + 3] = vertexOffset + 0;
            _indices[indexOffset + 4] = vertexOffset + 2;
            _indices[indexOffset + 5] = vertexOffset + 3;
        }
    }

    private void DrawPackets(in RenderFrame frame)
    {
        if (_effect == null)
        {
            return;
        }

        GraphicsDevice graphicsDevice = GraphicsDevice;
        BlendState previousBlendState = graphicsDevice.BlendState;
        DepthStencilState previousDepthStencilState = graphicsDevice.DepthStencilState;
        RasterizerState previousRasterizerState = graphicsDevice.RasterizerState;
        IndexBuffer? previousIndexBuffer = graphicsDevice.Indices;

        try
        {
            graphicsDevice.BlendState = BlendState.AlphaBlend;
            graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _effect.World = Matrix.Identity;
            _effect.View = frame.View;
            _effect.Projection = frame.Projection;

            int primitiveCount = _packets.Count * 2;
            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                graphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    _vertices,
                    0,
                    _packets.Count * 4,
                    _indices,
                    0,
                    primitiveCount);
            }
        }
        finally
        {
            graphicsDevice.BlendState = previousBlendState;
            graphicsDevice.DepthStencilState = previousDepthStencilState;
            graphicsDevice.RasterizerState = previousRasterizerState;
            graphicsDevice.Indices = previousIndexBuffer;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _effect?.Dispose();
            lock (this)
            {
                Game.RemoveGameComponent<ParticleRendererComponent>();
            }
        }

        base.Dispose(disposing);
    }
}