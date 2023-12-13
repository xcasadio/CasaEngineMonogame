using System.Runtime.CompilerServices;
using System.Windows.Forms;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Shapes;
using CasaEngine.Framework.Assets.Sprites;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Game.Components;

public class SpriteRendererComponent : DrawableGameComponent
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
    }

    private const int NbSprites = 5000;
    private readonly VertexPositionTexture[] _vertices = new VertexPositionTexture[NbSprites * 4];
    private readonly List<SpriteDisplayData> _spriteDatas = new(NbSprites);
    private readonly Stack<SpriteDisplayData> _freeSpriteDatas = new(NbSprites);
    private VertexBuffer? _vertexBuffer;
    private IndexBuffer? _indexBuffer;
    private Effect? _effect;
    private readonly CasaEngineGame _game;

    public bool IsDrawSpriteOriginEnabled = false;
    public bool IsDrawSpriteBorderEnabled = false;
    public bool IsDrawSpriteSheetEnabled = false;
    public bool IsDrawCollisionsEnabled = false;
    public int SpriteSheetTransparency = 124;
    private Line3dRendererComponent? _line3dRendererComponent;
    private readonly DepthStencilState _depthStencilState;
    private readonly BlendState _blendState;
    private readonly Vector3 _verticeTopLeft;
    private readonly Vector3 _verticeTopRight;
    private readonly Vector3 _verticeBottomRight;
    private readonly Vector3 _verticeBottomLeft;

    public SpriteRendererComponent(Microsoft.Xna.Framework.Game game) : base(game)
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

        _verticeTopLeft = new Vector3(-0.5f, 0.5f, 0);
        _verticeTopRight = new Vector3(0.5f, 0.5f, 0);
        _verticeBottomRight = new Vector3(0.5f, -0.5f, 0);
        _verticeBottomLeft = new Vector3(-0.5f, -0.5f, 0);
    }

    protected override void LoadContent()
    {
        _vertexBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionTexture), NbSprites * 4, BufferUsage.None);
        _indexBuffer = new IndexBuffer(GraphicsDevice, typeof(short), 6, BufferUsage.None);
        _indexBuffer.SetData(new short[] { 0, 1, 2, 0, 2, 3 });
        _effect = _game.Content.Load<Effect>("Shaders\\spritebatch");

        _line3dRendererComponent = _game.GetGameComponent<Line3dRendererComponent>();
    }

    public override void Draw(GameTime gameTime)
    {
        if (_spriteDatas.Count == 0)
        {
            return;
        }

        UpdateBuffer();

        var camera = _game.GameManager.ActiveCamera;
        Draw(camera.ViewMatrix, camera.ProjectionMatrix);

        Clear();
    }

    private void Draw(Matrix view, Matrix projection)
    {
        var graphicsDevice = _effect.GraphicsDevice;

        graphicsDevice.DepthStencilState = _depthStencilState;
        graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        graphicsDevice.BlendState = _blendState;
        graphicsDevice.SamplerStates[0] = SamplerState.PointClamp;

        graphicsDevice.SetVertexBuffer(_vertexBuffer);
        graphicsDevice.Indices = _indexBuffer;

        var scissorRectangle = graphicsDevice.ScissorRectangle;

        _effect.Parameters["ViewProj"].SetValue(view * projection);

        for (var i = 0; i < _spriteDatas.Count; i++)
        {
            var spriteDisplayData = _spriteDatas[i];

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

    public void DrawDirectly(Texture2D texture)
    {
        var graphicsDevice = _effect.GraphicsDevice;

        graphicsDevice.DepthStencilState = DepthStencilState.None;
        graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        graphicsDevice.BlendState = BlendState.AlphaBlend; //_blendState
        graphicsDevice.SamplerStates[0] = SamplerState.AnisotropicClamp;

        var camera = _game.GameManager.ActiveCamera;
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

        _spriteDatas.Sort((x, y) =>
        {
            var xZ = x.WorldMatrix.Translation.Z;
            var yZ = y.WorldMatrix.Translation.Z;

            if (xZ == yZ)
            {
                return 0;
            }

            return xZ > yZ ? -1 : 1;
        });

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


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Sprite sprite, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, SpriteEffects effects = SpriteEffects.None)
    {
        DrawSprite(sprite, pos, rot, scale, color, zOrder, effects, GraphicsDevice.ScissorRectangle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Sprite sprite, Vector2 pos, float rot, Vector2 scale, Color color, float zOrder, SpriteEffects effects, Rectangle scissorRectangle)
    {
        DrawSprite(sprite.Texture.Resource, sprite.SpriteData.PositionInTexture, sprite.SpriteData.Origin, pos, rot, scale, color, zOrder, effects, scissorRectangle);

        if (IsDrawCollisionsEnabled)
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
        var color = collision2d.CollisionHitType == CollisionHitType.Attack ? Color.Red : Color.Green;

        switch (collision2d.Shape.Type)
        {
            case Shape2dType.Compound:
                break;
            case Shape2dType.Polygone:
                break;
            case Shape2dType.Rectangle:
                var rectangle = collision2d.Shape as ShapeRectangle;
                _line3dRendererComponent.DrawRectangle(
                    position.X + (rectangle.Position.X - origin.X) * scale.X,
                    position.Y - (rectangle.Position.Y - origin.Y + rectangle.Height) * scale.Y,
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawSprite(Texture2D texture2d, Rectangle sourceInTexture, Point origin, Vector2 position, float rotation,
        Vector2 scale, Color color, float z, SpriteEffects effects, Rectangle scissorRectangle, bool drawDebug)
    {
        if (texture2d == null)
        {
            throw new ArgumentNullException(nameof(texture2d));
        }

        if (texture2d.IsDisposed)
        {
            throw new ArgumentException($"{nameof(texture2d)} is disposed");
        }

        var uvTopLeft = new Vector2(effects == SpriteEffects.FlipHorizontally ? sourceInTexture.Right : sourceInTexture.Left, effects == SpriteEffects.FlipVertically ? sourceInTexture.Bottom : sourceInTexture.Top);
        var uvTopRight = new Vector2(effects == SpriteEffects.FlipHorizontally ? sourceInTexture.Left : sourceInTexture.Right, effects == SpriteEffects.FlipHorizontally ? sourceInTexture.Bottom : sourceInTexture.Top);
        var uvBottomRight = new Vector2(effects == SpriteEffects.FlipHorizontally ? sourceInTexture.Left : sourceInTexture.Right, effects == SpriteEffects.FlipVertically ? sourceInTexture.Top : sourceInTexture.Bottom);
        var uvBottomLeft = new Vector2(effects == SpriteEffects.FlipHorizontally ? sourceInTexture.Right : sourceInTexture.Left, effects == SpriteEffects.FlipHorizontally ? sourceInTexture.Top : sourceInTexture.Bottom);

        var textureSize = new Vector2(texture2d.Width, texture2d.Height);
        uvTopLeft /= textureSize;
        uvTopRight /= textureSize;
        uvBottomRight /= textureSize;
        uvBottomLeft /= textureSize;

        GetSpriteDisplayData(out var spriteDisplayData);
        spriteDisplayData.TopLeft = new(_verticeTopLeft, uvTopLeft);
        spriteDisplayData.TopRight = new(_verticeTopRight, uvTopRight);
        spriteDisplayData.BottomRight = new(_verticeBottomRight, uvBottomRight);
        spriteDisplayData.BottomLeft = new(_verticeBottomLeft, uvBottomLeft);
        spriteDisplayData.Texture = texture2d;
        spriteDisplayData.Color = color;
        spriteDisplayData.WorldMatrix = MatrixExtensions.Transformation(
            new Vector3(scale.X * sourceInTexture.Width, scale.Y * sourceInTexture.Height, 1.0f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitZ, rotation),
            new Vector3(
                position.X - origin.X * scale.X + (sourceInTexture.Width / 2f) * scale.X,
                position.Y + origin.Y * scale.Y - (sourceInTexture.Height / 2f) * scale.Y,
                z));
        spriteDisplayData.ScissorRectangle = scissorRectangle;
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