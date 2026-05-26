using CasaEngine.Framework.Particles;
using CasaEngine.Framework.Particles.Rendering;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;
using TextureAsset = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Framework.Application.Components;

public sealed class ParticleRendererComponent : DrawableGameComponent, IViewFlushableRenderer
{
    private const int InitialPacketCapacity = 1024;
    private const int FallbackTextureSize = 2;
    private static readonly BlendState MultiplyBlendState = new()
    {
        ColorSourceBlend = Blend.DestinationColor,
        ColorDestinationBlend = Blend.Zero,
        AlphaSourceBlend = Blend.DestinationAlpha,
        AlphaDestinationBlend = Blend.Zero,
        ColorBlendFunction = BlendFunction.Add,
        AlphaBlendFunction = BlendFunction.Add,
    };

    private readonly List<ParticleRenderPacket> _packets = new(InitialPacketCapacity);
    private readonly Dictionary<Guid, Texture2D> _textureCache = new();
    private readonly CasaEngineGame _casaEngineGame;
    private VertexPositionColorTexture[] _vertices = new VertexPositionColorTexture[InitialPacketCapacity * 4];
    private int[] _indices = new int[InitialPacketCapacity * 6];
    private BasicEffect _effect;
    private Texture2D _fallbackTexture;

    public int PendingPacketCount => _packets.Count;

    public int FrameFlushedParticleCount { get; private set; }

    public int FrameDrawCallCount { get; private set; }

    public int FrameTextureBindCount { get; private set; }

    public int FrameStateChangeCount { get; private set; }

    public double FrameFlushCpuMilliseconds { get; private set; }

    public ParticleRendererComponent(Game game) : base(game)
    {
        ArgumentNullException.ThrowIfNull(game);

        _casaEngineGame = game as CasaEngineGame;
        game.Components.Add(this);
        UpdateOrder = (int)ComponentUpdateOrder.ParticleComponent;
        DrawOrder = (int)ComponentDrawOrder.ParticleComponent;
    }

    protected override void LoadContent()
    {
        _effect = new BasicEffect(GraphicsDevice)
        {
            VertexColorEnabled = true,
            TextureEnabled = true,
            LightingEnabled = false,
        };
        _fallbackTexture = CreateFallbackTexture(GraphicsDevice);
    }

    public override void Update(GameTime gameTime)
    {
        FrameFlushedParticleCount = 0;
        FrameDrawCallCount = 0;
        FrameTextureBindCount = 0;
        FrameStateChangeCount = 0;
        FrameFlushCpuMilliseconds = 0.0;
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

    public void Flush(in RenderFrame frame, RenderStats stats = null)
    {
        if (_packets.Count == 0 || _effect == null)
        {
            return;
        }

        long flushStartTimestamp = Stopwatch.GetTimestamp();
        EnsureCapacity(_packets.Count);
        ParticleRenderPacketSorter.Sort(_packets);
        BuildBillboardBuffers(in frame);
        int particleCount = _packets.Count;
        DrawPackets(in frame, stats);
        FrameFlushedParticleCount += particleCount;
        if (stats != null)
        {
            stats.ParticleCount += particleCount;
            stats.TransparentItems += particleCount;
        }

        double flushCpuMilliseconds = GetElapsedMilliseconds(flushStartTimestamp);
        FrameFlushCpuMilliseconds += flushCpuMilliseconds;
        if (stats != null)
        {
            stats.ParticleRenderCpuMilliseconds += flushCpuMilliseconds;
        }

        _packets.Clear();
    }

    private void EnsureCapacity(int packetCount)
    {
        int vertexCount = packetCount * 4;
        if (_vertices.Length < vertexCount)
        {
            _vertices = new VertexPositionColorTexture[vertexCount];
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
            GetFlipbookTextureCoordinates(packet, out Vector2 uvTopLeft, out Vector2 uvTopRight, out Vector2 uvBottomRight, out Vector2 uvBottomLeft);

            int vertexOffset = packetIndex * 4;
            _vertices[vertexOffset + 0] = new VertexPositionColorTexture(packet.Position - rotatedRight + rotatedUp, packet.Color, uvTopLeft);
            _vertices[vertexOffset + 1] = new VertexPositionColorTexture(packet.Position + rotatedRight + rotatedUp, packet.Color, uvTopRight);
            _vertices[vertexOffset + 2] = new VertexPositionColorTexture(packet.Position + rotatedRight - rotatedUp, packet.Color, uvBottomRight);
            _vertices[vertexOffset + 3] = new VertexPositionColorTexture(packet.Position - rotatedRight - rotatedUp, packet.Color, uvBottomLeft);

            int indexOffset = packetIndex * 6;
            _indices[indexOffset + 0] = vertexOffset + 0;
            _indices[indexOffset + 1] = vertexOffset + 1;
            _indices[indexOffset + 2] = vertexOffset + 2;
            _indices[indexOffset + 3] = vertexOffset + 0;
            _indices[indexOffset + 4] = vertexOffset + 2;
            _indices[indexOffset + 5] = vertexOffset + 3;
        }
    }

    private void DrawPackets(in RenderFrame frame, RenderStats stats)
    {
        if (_effect == null)
        {
            return;
        }

        GraphicsDevice graphicsDevice = GraphicsDevice;
        BlendState previousBlendState = graphicsDevice.BlendState;
        DepthStencilState previousDepthStencilState = graphicsDevice.DepthStencilState;
        RasterizerState previousRasterizerState = graphicsDevice.RasterizerState;
        SamplerState previousSamplerState = graphicsDevice.SamplerStates[0];
        IndexBuffer previousIndexBuffer = graphicsDevice.Indices;

        try
        {
            graphicsDevice.RasterizerState = RasterizerState.CullNone;
            graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
            AddStateChanges(stats, 2);

            _effect.World = Matrix.Identity;
            _effect.View = frame.View;
            _effect.Projection = frame.Projection;

            int segmentStartPacketIndex = 0;
            while (segmentStartPacketIndex < _packets.Count)
            {
                ParticleRenderPacket segmentPacket = _packets[segmentStartPacketIndex];
                int segmentEndPacketIndex = segmentStartPacketIndex + 1;
                while (segmentEndPacketIndex < _packets.Count && HasSameRenderState(segmentPacket, _packets[segmentEndPacketIndex]))
                {
                    segmentEndPacketIndex++;
                }

                graphicsDevice.BlendState = GetBlendState(segmentPacket.BlendMode);
                graphicsDevice.DepthStencilState = GetDepthStencilState(segmentPacket.DepthTest, segmentPacket.DepthWrite);
                _effect.Texture = ResolveTexture(segmentPacket.TextureAssetId);
                AddStateChanges(stats, 2);
                AddTextureBind(stats);

                int primitiveCount = (segmentEndPacketIndex - segmentStartPacketIndex) * 2;
                int startIndex = segmentStartPacketIndex * 6;
                int passCount = _effect.CurrentTechnique.Passes.Count;
                foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    graphicsDevice.DrawUserIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        _vertices,
                        0,
                        _packets.Count * 4,
                        _indices,
                        startIndex,
                        primitiveCount);
                }

                    AddDrawCalls(stats, passCount);

                segmentStartPacketIndex = segmentEndPacketIndex;
            }
        }
        finally
        {
            graphicsDevice.BlendState = previousBlendState;
            graphicsDevice.DepthStencilState = previousDepthStencilState;
            graphicsDevice.RasterizerState = previousRasterizerState;
            graphicsDevice.SamplerStates[0] = previousSamplerState;
            graphicsDevice.Indices = previousIndexBuffer;
        }
    }

    private void AddDrawCalls(RenderStats stats, int count)
    {
        FrameDrawCallCount += count;
        if (stats != null)
        {
            stats.DrawCalls += count;
            stats.EffectBinds += count;
        }
    }

    private void AddTextureBind(RenderStats stats)
    {
        FrameTextureBindCount++;
        if (stats != null)
        {
            stats.TextureBinds++;
        }
    }

    private void AddStateChanges(RenderStats stats, int count)
    {
        FrameStateChangeCount += count;
        if (stats != null)
        {
            stats.StateChanges += count;
        }
    }

    private static bool HasSameRenderState(in ParticleRenderPacket left, in ParticleRenderPacket right)
        => left.BlendMode == right.BlendMode
            && left.DepthTest == right.DepthTest
            && left.DepthWrite == right.DepthWrite
            && left.TextureAssetId == right.TextureAssetId;

    internal static void GetFlipbookTextureCoordinates(
        in ParticleRenderPacket packet,
        out Vector2 uvTopLeft,
        out Vector2 uvTopRight,
        out Vector2 uvBottomRight,
        out Vector2 uvBottomLeft)
    {
        int columns = Math.Max(1, packet.FlipbookColumns);
        int rows = Math.Max(1, packet.FlipbookRows);
        long atlasFrameCountLong = (long)columns * rows;
        int atlasFrameCount = atlasFrameCountLong > int.MaxValue ? int.MaxValue : (int)atlasFrameCountLong;
        int frameIndex = Math.Clamp(packet.FlipbookFrameIndex, 0, atlasFrameCount - 1);
        int column = frameIndex % columns;
        int row = frameIndex / columns;
        float inverseColumns = 1.0f / columns;
        float inverseRows = 1.0f / rows;
        float left = column * inverseColumns;
        float top = row * inverseRows;
        float right = left + inverseColumns;
        float bottom = top + inverseRows;

        uvTopLeft = new Vector2(left, top);
        uvTopRight = new Vector2(right, top);
        uvBottomRight = new Vector2(right, bottom);
        uvBottomLeft = new Vector2(left, bottom);
    }

    private Texture2D ResolveTexture(Guid textureAssetId)
    {
        if (textureAssetId == Guid.Empty || _casaEngineGame == null)
        {
            return _fallbackTexture!;
        }

        if (!_textureCache.TryGetValue(textureAssetId, out Texture2D texture))
        {
            texture = TryLoadTexture(textureAssetId);
            _textureCache[textureAssetId] = texture;
        }

        return texture ?? _fallbackTexture!;
    }

    private Texture2D TryLoadTexture(Guid textureAssetId)
    {
        try
        {
            TextureAsset textureAsset = _casaEngineGame!.AssetContentManager.Load<TextureAsset>(textureAssetId);
            textureAsset.Load(_casaEngineGame.AssetContentManager);
            return textureAsset.Resource;
        }
        catch
        {
            return null;
        }
    }

    private static Texture2D CreateFallbackTexture(GraphicsDevice graphicsDevice)
    {
        var texture = new Texture2D(graphicsDevice, FallbackTextureSize, FallbackTextureSize, false, SurfaceFormat.Color);
        texture.SetData(new[]
        {
            Color.Magenta,
            Color.White,
            Color.White,
            Color.Magenta,
        });
        return texture;
    }

    private static BlendState GetBlendState(ParticleBlendMode blendMode)
        => blendMode switch
        {
            ParticleBlendMode.Additive => BlendState.Additive,
            ParticleBlendMode.Multiply => MultiplyBlendState,
            _ => BlendState.AlphaBlend,
        };

    private static DepthStencilState GetDepthStencilState(bool depthTest, bool depthWrite)
    {
        if (!depthTest)
        {
            return DepthStencilState.None;
        }

        return depthWrite ? DepthStencilState.Default : DepthStencilState.DepthRead;
    }

    private static double GetElapsedMilliseconds(long startTimestamp)
        => (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _effect?.Dispose();
            _fallbackTexture?.Dispose();
            lock (this)
            {
                Game.RemoveGameComponent<ParticleRendererComponent>();
            }
        }

        base.Dispose(disposing);
    }
}