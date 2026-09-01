using System.Runtime.CompilerServices;

using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Engine.Geometry;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Application.Components;

public class SpriteRendererComponent : DrawableGameComponent, IViewFlushableRenderer
{
    private struct SpriteDisplayData
    {
        public VertexPositionTexture TopLeft;
        public VertexPositionTexture TopRight;
        public VertexPositionTexture BottomLeft;
        public VertexPositionTexture BottomRight;
        public Color Color;
        public Texture2D Texture;
        public Matrix WorldMatrix;
        public Rectangle ScissorRectangle;
        public RenderSortKey2D SortKey;
        public bool HasSortKey;
        public SpriteBlendMode BlendMode;
    }

    private const int NbSprites = 10000;
    private readonly VertexPositionTexture[] _vertices = new VertexPositionTexture[NbSprites * 4];
    private readonly List<SpriteDisplayData> _spriteDatas = new(NbSprites);
    private readonly Stack<SpriteDisplayData> _freeSpriteDatas = new(NbSprites);
    private VertexBuffer _vertexBuffer;
    private IndexBuffer _indexBuffer;
    private Effect _effect;
    private readonly CasaEngineGame _game;

    public bool IsDrawSpriteOriginEnabled = false;
    public bool IsDrawSpriteBorderEnabled = false;
    public bool IsDrawSpriteSheetEnabled = false;
    public bool IsDrawCollisionsEnabled = false;
    public int SpriteSheetTransparency = 124;
    private Line3dRendererComponent _line3dRendererComponent;
    private readonly DepthStencilState _depthStencilState;
    private readonly BlendState _blendState;
    private readonly Vector3 _vertexTopLeft;
    private readonly Vector3 _vertexTopRight;
    private readonly Vector3 _vertexBottomRight;
    private readonly Vector3 _vertexBottomLeft;
    private static readonly Comparison<SpriteDisplayData> SpriteDisplayDataComparison = CompareSpriteDisplayData;

    // Cached once, never allocated per run/frame - see GetBlendState's doc. Exact PSX fusion formulas
    // (screen fade/tint overlay, additive/subtractive backdrop layers): color channel saturates via
    // One/One, alpha uses Add/Zero/One so the destination alpha is never touched.
    private static readonly BlendState AdditiveBlendState = new()
    {
        ColorBlendFunction = BlendFunction.Add,
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One
    };

    // ReverseSubtract computes dst - src, which is what the PSX GPU's subtractive mode does.
    // BlendFunction.Subtract would instead compute src - dst - wrong channel order.
    private static readonly BlendState SubtractiveBlendState = new()
    {
        ColorBlendFunction = BlendFunction.ReverseSubtract,
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One
    };

    public SpriteRendererComponent(Game game) : base(game)
    {
        if (game == null)
        {
            throw new ArgumentNullException(nameof(game));
        }

        _game = Game as CasaEngineGame;
        game.Components.Add(this);

        UpdateOrder = (int)ComponentUpdateOrder.Line3dComponent;
        DrawOrder = (int)ComponentDrawOrder.Line3dComponent;

        _depthStencilState = new DepthStencilState
        {
            DepthBufferEnable = true,
            DepthBufferWriteEnable = true,
            DepthBufferFunction = CompareFunction.LessEqual,
        };

        _blendState = new BlendState
        {
            ColorBlendFunction = BlendFunction.Add,
            AlphaBlendFunction = BlendFunction.Max,
            ColorSourceBlend = Blend.One,
            AlphaSourceBlend = Blend.SourceColor,
            ColorDestinationBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.DestinationAlpha
        };

        _vertexTopLeft = new Vector3(-0.5f, 0.5f, 0);
        _vertexTopRight = new Vector3(0.5f, 0.5f, 0);
        _vertexBottomRight = new Vector3(0.5f, -0.5f, 0);
        _vertexBottomLeft = new Vector3(-0.5f, -0.5f, 0);
    }

    protected override void LoadContent()
    {
        _vertexBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionTexture), NbSprites * 4, BufferUsage.None);
        _indexBuffer = new IndexBuffer(GraphicsDevice, typeof(short), 6, BufferUsage.None);
        _indexBuffer.SetData(new short[] { 0, 1, 2, 0, 2, 3 });
        _effect = _game.Content.Load<Effect>(BuiltInShaderCatalog.SpriteBatchContentName);

        _line3dRendererComponent = _game.GetGameComponent<Line3dRendererComponent>();
    }

    public bool TryReloadBuiltInShader(string contentName, Effect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);

        if (!string.Equals(
                BuiltInShaderCatalog.NormalizeContentName(contentName),
                BuiltInShaderCatalog.SpriteBatchContentName,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _effect.Dispose();
        _effect = effect;
        return true;
    }

    /// <inheritdoc/>
    public void Flush(in RenderFrame frame, RenderStats stats = null)
    {
        if (_spriteDatas.Count == 0)
        {
            return;
        }

        UpdateBuffer();
        Draw(frame.View, frame.Projection);
        Clear();
    }

    private void Draw(Matrix view, Matrix projection)
    {
        var graphicsDevice = _effect.GraphicsDevice;

        // DepthStencilState is fixed for the whole sorted-sprite pass (by design, this slice). It is
        // safe to keep fixed even though sprites may now blend rather than draw opaque: every sorted
        // participant (TileMap sorted overlay + Y-sorted entities) shares a coplanar Z and draws in
        // painter order under LessEqual, so an alpha sprite writing depth cannot clip a later same-Z
        // draw. A future per-sprite depth flag (most likely just DepthWrite on/off) would ride the
        // exact same per-run mechanism used below for BlendState.
        graphicsDevice.DepthStencilState = _depthStencilState;
        graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        graphicsDevice.SamplerStates[0] = SamplerState.PointClamp;

        graphicsDevice.SetVertexBuffer(_vertexBuffer);
        graphicsDevice.Indices = _indexBuffer;

        var scissorRectangle = graphicsDevice.ScissorRectangle;

        _effect.Parameters["ViewProj"].SetValue(view * projection);

        // The sorted list is ordered by SortKey/Z only and must never be reordered for blend state:
        // BlendState is applied per contiguous RUN of sprites sharing the same blend mode, so a state
        // change never costs more than one BlendState set per run, and correctness of the key order
        // always prevails over minimizing state changes.
        var currentBlendMode = SpriteBlendMode.Opaque;
        graphicsDevice.BlendState = _blendState;

        for (var i = 0; i < _spriteDatas.Count; i++)
        {
            var spriteDisplayData = _spriteDatas[i];

            if (spriteDisplayData.BlendMode != currentBlendMode)
            {
                currentBlendMode = spriteDisplayData.BlendMode;
                graphicsDevice.BlendState = GetBlendState(currentBlendMode);
            }

            _effect.Parameters["Texture"].SetValue(spriteDisplayData.Texture);
            _effect.Parameters["Color"].SetValue(spriteDisplayData.Color.ToVector4());
            _effect.Parameters["World"].SetValue(spriteDisplayData.WorldMatrix);
            graphicsDevice.ScissorRectangle = spriteDisplayData.ScissorRectangle;

            for (var j = 0; j < _effect.CurrentTechnique.Passes.Count; j++)
            {
                _effect.CurrentTechnique.Passes[j].Apply();
                graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, i * 4, 0, 2);
            }
        }

        graphicsDevice.ScissorRectangle = scissorRectangle;
    }

    /// <summary>
    /// Resolves a <see cref="SpriteBlendMode"/> to the <see cref="BlendState"/> instance to apply.
    /// Both branches return a cached instance (the component's own fixed opaque state, or MonoGame's
    /// static <see cref="BlendState.NonPremultiplied"/>) so the sorted draw loop never allocates a
    /// <see cref="BlendState"/> per sprite or per run.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BlendState GetBlendState(SpriteBlendMode blendMode)
    {
        return blendMode switch
        {
            SpriteBlendMode.AlphaBlend => BlendState.NonPremultiplied,
            SpriteBlendMode.Additive => AdditiveBlendState,
            SpriteBlendMode.Subtractive => SubtractiveBlendState,
            _ => _blendState
        };
    }

    // DrawDirectly reads the active view camera for ViewProjection. It is used for
    // full-screen texture display and always applies to the primary (active) view.
    public void DrawDirectly(Texture2D texture)
    {
        var graphicsDevice = _effect.GraphicsDevice;

        graphicsDevice.DepthStencilState = DepthStencilState.None;
        graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        graphicsDevice.BlendState = BlendState.AlphaBlend; //BlendState.AlphaBlend; //_blendState

        var camera = _game.GameManager.ViewManager.ActiveView?.Camera;
        if (camera == null)
        {
            return;
        }

        _effect.Parameters["ViewProj"].SetValue(camera.ViewMatrix * camera.ProjectionMatrix);
        _effect.Parameters["Texture"].SetValue(texture);
        _effect.Parameters["Color"].SetValue(Color.White.ToVector4());
        _effect.Parameters["World"].SetValue(Matrix.Identity);
        var z = 0.0f;

        var vertices = new VertexPositionColorTexture[]
        {
            new(new Vector3(0, 0, z), Color.White, Vector2.UnitY),
            new(new Vector3(texture.Width, 0, z), Color.White, Vector2.One),
            new(new Vector3(texture.Width, texture.Height, z), Color.White, Vector2.UnitX),
            new(new Vector3(0, texture.Height, z), Color.White, Vector2.Zero)
        };

        for (var j = 0; j < _effect.CurrentTechnique.Passes.Count; j++)
        {
            _effect.CurrentTechnique.Passes[j].Apply();
            graphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                vertices, 0, 4,
                new short[] { 0, 2, 1, 0, 3, 2 }, 0,
                2);
        }
    }

    public bool DrawStaticBatch(
        Texture2D texture,
        VertexBuffer vertexBuffer,
        IndexBuffer indexBuffer,
        int primitiveCount,
        in Matrix world,
        in RenderFrame frame)
    {
        if (_effect == null
            || texture == null
            || vertexBuffer == null
            || indexBuffer == null
            || primitiveCount <= 0)
        {
            return false;
        }

        var graphicsDevice = _effect.GraphicsDevice;
        var previousDepthStencilState = graphicsDevice.DepthStencilState;
        var previousRasterizerState = graphicsDevice.RasterizerState;
        var previousBlendState = graphicsDevice.BlendState;
        var previousSamplerState = graphicsDevice.SamplerStates[0];
        var previousScissorRectangle = graphicsDevice.ScissorRectangle;
        var previousIndexBuffer = graphicsDevice.Indices;

        try
        {
            graphicsDevice.DepthStencilState = _depthStencilState;
            graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
            graphicsDevice.BlendState = _blendState;
            graphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
            graphicsDevice.SetVertexBuffer(vertexBuffer);
            graphicsDevice.Indices = indexBuffer;

            _effect.Parameters["ViewProj"].SetValue(frame.ViewProjection);
            _effect.Parameters["Texture"].SetValue(texture);
            _effect.Parameters["Color"].SetValue(Color.White.ToVector4());
            _effect.Parameters["World"].SetValue(world);

            for (var passIndex = 0; passIndex < _effect.CurrentTechnique.Passes.Count; passIndex++)
            {
                _effect.CurrentTechnique.Passes[passIndex].Apply();
                graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, primitiveCount);
            }
        }
        finally
        {
            graphicsDevice.SetVertexBuffer(null);
            graphicsDevice.Indices = previousIndexBuffer;
            graphicsDevice.DepthStencilState = previousDepthStencilState;
            graphicsDevice.RasterizerState = previousRasterizerState;
            graphicsDevice.BlendState = previousBlendState;
            graphicsDevice.SamplerStates[0] = previousSamplerState;
            graphicsDevice.ScissorRectangle = previousScissorRectangle;
        }

        return true;
    }

    private void DrawDebug(Vector3 position, Vector2 scale, Vector2 origin, Texture2D texture2d, Rectangle sourceInTexture)
    {
        if (IsDrawSpriteOriginEnabled)
        {
            _line3dRendererComponent.DrawCross(
                new Vector2(position.X + (origin.X - sourceInTexture.Width / 2f) * scale.X,
                    position.Y - (origin.Y - sourceInTexture.Height / 2f) * scale.Y),
                position.Z, 6, Color.Red);
        }

        if (IsDrawSpriteBorderEnabled)
        {
            var temp = new Rectangle
            {
                X = (int)(position.X - (sourceInTexture.Width / 2f) * scale.X),
                Y = (int)(position.Y - (sourceInTexture.Height / 2f) * scale.Y),
                Width = (int)(sourceInTexture.Width * scale.X),
                Height = (int)(sourceInTexture.Height * scale.Y)
            };
            _line3dRendererComponent.DrawRectangle(ref temp, Color.BlueViolet, position.Z);
        }

        if (IsDrawSpriteSheetEnabled)
        {
            var texturePosition = new Vector2(
                position.X - (sourceInTexture.X + sourceInTexture.Width / 2f) * scale.X,
                position.Y + (sourceInTexture.Y + sourceInTexture.Height / 2f) * scale.Y);

            DrawSprite(texture2d, texture2d.Bounds, Point.Zero, texturePosition, 0.0f, scale,
                Color.FromNonPremultiplied(255, 255, 255, SpriteSheetTransparency),
                position.Z, SpriteEffects.None, GraphicsDevice.ScissorRectangle, false);
        }
    }

    private void UpdateBuffer()
    {
        var nbVertices = 4;

        _spriteDatas.Sort(SpriteDisplayDataComparison);

        for (var i = 0; i < _spriteDatas.Count; i++)
        {
            var index = i * nbVertices;
            var spriteDisplayData = _spriteDatas[i];

            _vertices[index + 0].Position = spriteDisplayData.TopLeft.Position;
            _vertices[index + 0].TextureCoordinate = spriteDisplayData.TopLeft.TextureCoordinate;
            _vertices[index + 1].Position = spriteDisplayData.TopRight.Position;
            _vertices[index + 1].TextureCoordinate = spriteDisplayData.TopRight.TextureCoordinate;
            _vertices[index + 2].Position = spriteDisplayData.BottomRight.Position;
            _vertices[index + 2].TextureCoordinate = spriteDisplayData.BottomRight.TextureCoordinate;
            _vertices[index + 3].Position = spriteDisplayData.BottomLeft.Position;
            _vertices[index + 3].TextureCoordinate = spriteDisplayData.BottomLeft.TextureCoordinate;
        }

        _vertexBuffer.SetData(_vertices, 0, Math.Min(_spriteDatas.Count * 4, NbSprites * 4));
    }

    private static int CompareSpriteDisplayData(SpriteDisplayData x, SpriteDisplayData y)
    {
        if (x.HasSortKey || y.HasSortKey)
        {
            return x.SortKey.CompareTo(y.SortKey);
        }

        var xZ = x.WorldMatrix.Translation.Z;
        var yZ = y.WorldMatrix.Translation.Z;

        if (xZ == yZ)
        {
            return 0;
        }

        return xZ > yZ ? -1 : 1;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Sprite sprite, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, SpriteEffects effects = SpriteEffects.None)
    {
        DrawSprite(sprite, pos, rot, scale, color, zOrder, drawDebug: true, effects, GraphicsDevice.ScissorRectangle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Sprite sprite, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, bool drawDebug, SpriteEffects effects = SpriteEffects.None)
    {
        DrawSprite(sprite, pos, rot, scale, color, zOrder, drawDebug, effects, GraphicsDevice.ScissorRectangle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Sprite sprite, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, in RenderSortKey2D sortKey, SpriteEffects effects = SpriteEffects.None)
    {
        DrawSprite(sprite, pos, rot, scale, color, zOrder, sortKey, drawDebug: true, effects, GraphicsDevice.ScissorRectangle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Sprite sprite, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, in RenderSortKey2D sortKey, bool drawDebug, SpriteEffects effects = SpriteEffects.None)
    {
        DrawSprite(sprite, pos, rot, scale, color, zOrder, sortKey, drawDebug, effects, GraphicsDevice.ScissorRectangle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Sprite sprite, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, SpriteEffects effects, Rectangle scissorRectangle)
    {
        DrawSprite(sprite, pos, rot, scale, color, zOrder, drawDebug: true, effects, scissorRectangle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Sprite sprite, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, bool drawDebug, SpriteEffects effects, Rectangle scissorRectangle)
    {
        DrawSprite(sprite.Texture.Resource, sprite.SpriteData.PositionInTexture, sprite.SpriteData.Origin, pos, rot, scale, color, zOrder, effects, scissorRectangle, drawDebug);

        if (drawDebug && IsDrawCollisionsEnabled)
        {
            foreach (var collision2d in sprite.SpriteData.CollisionShapes)
            {
                DrawCollision(collision2d, pos, zOrder, sprite.SpriteData.Origin, scale);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Sprite sprite, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, in RenderSortKey2D sortKey, SpriteEffects effects, Rectangle scissorRectangle)
    {
        DrawSprite(sprite, pos, rot, scale, color, zOrder, sortKey, drawDebug: true, effects, scissorRectangle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Sprite sprite, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, in RenderSortKey2D sortKey, bool drawDebug, SpriteEffects effects, Rectangle scissorRectangle)
    {
        DrawSprite(sprite.Texture.Resource, sprite.SpriteData.PositionInTexture, sprite.SpriteData.Origin, pos, rot, scale, color, zOrder, effects, scissorRectangle, drawDebug, true, sortKey);

        if (drawDebug && IsDrawCollisionsEnabled)
        {
            foreach (var collision2d in sprite.SpriteData.CollisionShapes)
            {
                DrawCollision(collision2d, pos, zOrder, sprite.SpriteData.Origin, scale);
            }
        }
    }

    /// <summary>
    /// Same as <see cref="DrawSprite(Sprite,Vector2,float,Vector2,Color,float,in RenderSortKey2D,bool,SpriteEffects,Rectangle)"/>
    /// but with an explicit <see cref="SpriteBlendMode"/> for the sorted draw loop's per-run blend state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Sprite sprite, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, in RenderSortKey2D sortKey, bool drawDebug, SpriteEffects effects, Rectangle scissorRectangle, SpriteBlendMode blendMode)
    {
        DrawSprite(sprite.Texture.Resource, sprite.SpriteData.PositionInTexture, sprite.SpriteData.Origin, pos, rot, scale, color, zOrder, effects,
            scissorRectangle, drawDebug, true, sortKey, hasWorldTransform: false, worldTransform: default, blendMode: blendMode);

        if (drawDebug && IsDrawCollisionsEnabled)
        {
            foreach (var collision2d in sprite.SpriteData.CollisionShapes)
            {
                DrawCollision(collision2d, pos, zOrder, sprite.SpriteData.Origin, scale);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawCollision(Collision2d collision2d, Vector2 position, float z, Point origin, Vector2 scale)
    {
        var color = SpriteCollisionHelper.GetDebugColor(collision2d);

        switch (collision2d.Shape.Type)
        {
            case Shape2dType.Compound:
                break;
            case Shape2dType.Polygone:
                break;
            case Shape2dType.Rectangle:
                var rectangle = collision2d.Shape as ShapeRectangle;
                _line3dRendererComponent.DrawRectangle(
                    position.X + (collision2d.LocalPosition.X - origin.X) * scale.X,
                    position.Y - (collision2d.LocalPosition.Y - origin.Y + rectangle.Height) * scale.Y,
                    rectangle.Width * scale.X,
                    rectangle.Height * scale.Y,
                    color,
                    z - 0.001f);
                break;
            case Shape2dType.Circle:
                break;
            case Shape2dType.Line:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Texture2D texture2d, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, SpriteEffects effects)
    {
        DrawSprite(texture2d, texture2d.Bounds, Point.Zero, pos, rot, scale, color, zOrder, effects, GraphicsDevice.ScissorRectangle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Texture2D texture2d, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, SpriteEffects effects, Rectangle scissorRectangle)
    {
        DrawSprite(texture2d, texture2d.Bounds, Point.Zero, pos, rot, scale, color, zOrder, effects, scissorRectangle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Texture2D texture2d, Rectangle sourceInTexture, Point origin, Vector2 position, float rotation,
        Vector2 scale, Color color, float z, SpriteEffects effects)
    {
        DrawSprite(texture2d, sourceInTexture, origin, position, rotation, scale, color, z, effects, GraphicsDevice.ScissorRectangle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Texture2D texture2d, Rectangle sourceInTexture, Point origin, Vector2 position, float rotation,
        Vector2 scale, Color color, float z, SpriteEffects effects, Rectangle scissorRectangle)
    {
        DrawSprite(texture2d, sourceInTexture, origin, position, rotation, scale, color, z, effects, scissorRectangle, true);
    }

    /// <summary>
    /// Draws a raw texture sprite with an explicit <see cref="RenderSortKey2D"/>, sorting it against
    /// every other keyed sprite in the scene instead of by depth. Used by non-<see cref="Sprite"/> asset
    /// sources (e.g. the tile map sorted overlay) that need the keyed sprite path.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Texture2D texture2d, Rectangle sourceInTexture, Point origin, Vector2 position, float rotation,
        Vector2 scale, Color color, float z, in RenderSortKey2D sortKey, SpriteEffects effects)
    {
        DrawSprite(texture2d, sourceInTexture, origin, position, rotation, scale, color, z, effects,
            GraphicsDevice.ScissorRectangle, drawDebug: false, hasSortKey: true, in sortKey);
    }

    /// <summary>
    /// Same as <see cref="DrawSprite(Texture2D,Rectangle,Point,Vector2,float,Vector2,Color,float,in RenderSortKey2D,SpriteEffects)"/>
    /// but with an explicit scissor rectangle instead of the device's current one, so callers that
    /// submit many sprites per frame (e.g. the tile map sorted overlay) do not have to read
    /// <see cref="GraphicsDevice"/> once per sprite.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Texture2D texture2d, Rectangle sourceInTexture, Point origin, Vector2 position, float rotation,
        Vector2 scale, Color color, float z, in RenderSortKey2D sortKey, SpriteEffects effects, Rectangle scissorRectangle)
    {
        DrawSprite(texture2d, sourceInTexture, origin, position, rotation, scale, color, z, effects,
            scissorRectangle, drawDebug: false, hasSortKey: true, in sortKey);
    }

    /// <summary>
    /// Same as <see cref="DrawSprite(Texture2D,Rectangle,Point,Vector2,float,Vector2,Color,float,in RenderSortKey2D,SpriteEffects)"/>
    /// but with an explicit <see cref="SpriteBlendMode"/> for the sorted draw loop's per-run blend state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Texture2D texture2d, Rectangle sourceInTexture, Point origin, Vector2 position, float rotation,
        Vector2 scale, Color color, float z, in RenderSortKey2D sortKey, SpriteEffects effects, SpriteBlendMode blendMode)
    {
        DrawSprite(texture2d, sourceInTexture, origin, position, rotation, scale, color, z, effects,
            GraphicsDevice.ScissorRectangle, drawDebug: false, hasSortKey: true, in sortKey,
            hasWorldTransform: false, worldTransform: default, blendMode: blendMode);
    }

    /// <summary>
    /// Same as <see cref="DrawSprite(Texture2D,Rectangle,Point,Vector2,float,Vector2,Color,float,in RenderSortKey2D,SpriteEffects,Rectangle)"/>
    /// but with an explicit <see cref="SpriteBlendMode"/> for the sorted draw loop's per-run blend state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Texture2D texture2d, Rectangle sourceInTexture, Point origin, Vector2 position, float rotation,
        Vector2 scale, Color color, float z, in RenderSortKey2D sortKey, SpriteEffects effects, Rectangle scissorRectangle, SpriteBlendMode blendMode)
    {
        DrawSprite(texture2d, sourceInTexture, origin, position, rotation, scale, color, z, effects,
            scissorRectangle, drawDebug: false, hasSortKey: true, in sortKey,
            hasWorldTransform: false, worldTransform: default, blendMode: blendMode);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawSprite(Texture2D texture2d, Rectangle sourceInTexture, Point origin, Vector2 position, float rotation,
        Vector2 scale, Color color, float z, SpriteEffects effects, Rectangle scissorRectangle, bool drawDebug)
    {
        DrawSprite(texture2d, sourceInTexture, origin, position, rotation, scale, color, z, effects, scissorRectangle, drawDebug, false, RenderSortKey2D.Default);
    }

    /// <summary>
    /// Draws a sprite whose quad is expressed in a local space, then transformed by <paramref name="worldTransform"/>.
    /// Used by world-space objects (tile maps) that carry a full world matrix including rotation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Texture2D texture2d, Rectangle sourceInTexture, Point origin, Vector2 position, float rotation,
        Vector2 scale, Color color, float z, SpriteEffects effects, in Matrix worldTransform)
    {
        DrawSprite(texture2d, sourceInTexture, origin, position, rotation, scale, color, z, effects,
            GraphicsDevice.ScissorRectangle, drawDebug: false, hasSortKey: false, RenderSortKey2D.Default,
            hasWorldTransform: true, in worldTransform);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawSprite(Texture2D texture2d, Rectangle sourceInTexture, Point origin, Vector2 position, float rotation,
        Vector2 scale, Color color, float z, SpriteEffects effects, Rectangle scissorRectangle, bool drawDebug, bool hasSortKey, in RenderSortKey2D sortKey,
        bool hasWorldTransform = false, in Matrix worldTransform = default, SpriteBlendMode blendMode = SpriteBlendMode.Opaque)
    {
        if (texture2d == null)
        {
            throw new ArgumentNullException(nameof(texture2d));
        }

        if (texture2d.IsDisposed)
        {
            throw new ArgumentException($"{nameof(texture2d)} is disposed");
        }

        bool flipHorizontally = (effects & SpriteEffects.FlipHorizontally) != 0;
        bool flipVertically = (effects & SpriteEffects.FlipVertically) != 0;

        var uvTopLeft = new Vector2(flipHorizontally ? sourceInTexture.Right : sourceInTexture.Left, flipVertically ? sourceInTexture.Bottom : sourceInTexture.Top);
        var uvTopRight = new Vector2(flipHorizontally ? sourceInTexture.Left : sourceInTexture.Right, flipVertically ? sourceInTexture.Bottom : sourceInTexture.Top);
        var uvBottomRight = new Vector2(flipHorizontally ? sourceInTexture.Left : sourceInTexture.Right, flipVertically ? sourceInTexture.Top : sourceInTexture.Bottom);
        var uvBottomLeft = new Vector2(flipHorizontally ? sourceInTexture.Right : sourceInTexture.Left, flipVertically ? sourceInTexture.Top : sourceInTexture.Bottom);

        var textureSize = new Vector2(texture2d.Width, texture2d.Height);
        uvTopLeft /= textureSize;
        uvTopRight /= textureSize;
        uvBottomRight /= textureSize;
        uvBottomLeft /= textureSize;

        GetSpriteDisplayData(out var spriteDisplayData);
        spriteDisplayData.TopLeft = new(_vertexTopLeft, uvTopLeft);
        spriteDisplayData.TopRight = new(_vertexTopRight, uvTopRight);
        spriteDisplayData.BottomRight = new(_vertexBottomRight, uvBottomRight);
        spriteDisplayData.BottomLeft = new(_vertexBottomLeft, uvBottomLeft);
        spriteDisplayData.Texture = texture2d;
        spriteDisplayData.Color = color;
        spriteDisplayData.WorldMatrix = MatrixExtensions.Transformation(
            new Vector3(scale.X * sourceInTexture.Width, scale.Y * sourceInTexture.Height, 1.0f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitZ, rotation),
            new Vector3(
                position.X - origin.X * scale.X + (sourceInTexture.Width / 2f) * scale.X,
                position.Y + origin.Y * scale.Y - (sourceInTexture.Height / 2f) * scale.Y,
                z));
        if (hasWorldTransform)
        {
            spriteDisplayData.WorldMatrix *= worldTransform;
        }

        spriteDisplayData.ScissorRectangle = scissorRectangle;
            spriteDisplayData.SortKey = sortKey;
            spriteDisplayData.HasSortKey = hasSortKey;
            spriteDisplayData.BlendMode = blendMode;
        _spriteDatas.Add(spriteDisplayData);

        if (drawDebug)
        {
            DrawDebug(spriteDisplayData.WorldMatrix.Translation, scale, origin.ToVector2(), texture2d, sourceInTexture);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void GetSpriteDisplayData(out SpriteDisplayData spriteDisplayData)
    {
        spriteDisplayData = _freeSpriteDatas.Count > 0 ? _freeSpriteDatas.Pop() : new SpriteDisplayData();
    }

    private void Clear()
    {
        foreach (var line in _spriteDatas)
        {
            _freeSpriteDatas.Push(line);
        }

        _spriteDatas.Clear();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (this)
            {
                Game.RemoveGameComponent<SpriteRendererComponent>();
            }
        }

        base.Dispose(disposing);
    }
}