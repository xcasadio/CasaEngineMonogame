using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Runtime.CompilerServices;
using CasaEngine.Core.Helpers;
using CasaEngine.Framework.Graphics2D;
using BulletSharp.SoftBody;
using SharpDX.Direct2D1.Effects;
using static System.Net.Mime.MediaTypeNames;

namespace CasaEngine.Engine.Renderer;

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
        //Debug Datas
        public Vector2 Origin { get; set; }
        public Rectangle Border { get; set; }
        public Vector2 Scale { get; set; }
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
    private Renderer2dComponent? _renderer2dComponent;

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
    }

    protected override void LoadContent()
    {
        _vertexBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionTexture), NbSprites * 4, BufferUsage.None);
        _indexBuffer = new IndexBuffer(GraphicsDevice, typeof(short), 6, BufferUsage.None);
        _indexBuffer.SetData(new short[] { 0, 1, 2, 0, 2, 3 });
        _effect = _game.Content.Load<Effect>("Shaders\\spritebatch");

        _renderer2dComponent = _game.GetGameComponent<Renderer2dComponent>();
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
        graphicsDevice.DepthStencilState = DepthStencilState.Default;
        graphicsDevice.RasterizerState = RasterizerState.CullNone;
        graphicsDevice.BlendState = BlendState.AlphaBlend;
        graphicsDevice.SamplerStates[0] = SamplerState.PointClamp;

        graphicsDevice.SetVertexBuffer(_vertexBuffer);
        graphicsDevice.Indices = _indexBuffer;

        //RasterizerState rasterizerState = new RasterizerState();
        //rasterizerState.FillMode = FillMode.WireFrame;
        //rasterizerState.CullMode = CullMode.None;
        //rasterizerState.MultiSampleAntiAlias = true;
        //GraphicsDevice.RasterizerState = rasterizerState;

        _effect.Parameters["ViewProj"].SetValue(view * projection);

        for (var i = 0; i < _spriteDatas.Count; i++)
        {
            var spriteDisplayData = _spriteDatas[i];

            DrawDebug(ref spriteDisplayData);

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
    }

    private void DrawDebug(ref SpriteDisplayData spriteDisplayData)
    {
        var position = spriteDisplayData.WorldMatrix.Translation;

        if (IsDrawSpriteOriginEnabled)
        {
            _renderer2dComponent.DrawCross(spriteDisplayData.Origin, 6, Color.Red);
        }

        if (IsDrawSpriteBorderEnabled)
        {
            var temp = new Rectangle
            {
                X = (int)(position.X - (spriteDisplayData.Border.Width / 2f) * spriteDisplayData.Scale.X),
                Y = (int)(position.Y - (spriteDisplayData.Border.Height / 2f) * spriteDisplayData.Scale.Y),
                Width = (int)(spriteDisplayData.Border.Width * spriteDisplayData.Scale.X),
                Height = (int)(spriteDisplayData.Border.Height * spriteDisplayData.Scale.Y)
            };
            _renderer2dComponent.DrawRectangle(ref temp, Color.BlueViolet);
        }

        if (IsDrawSpriteSheetEnabled)
        {
            var position2 = new Vector2(
                position.X - spriteDisplayData.Border.X - spriteDisplayData.Border.Width / 2f,
                position.Y - spriteDisplayData.Border.Y - spriteDisplayData.Border.Height / 2f) * spriteDisplayData.Scale;

            _renderer2dComponent.DrawSprite(spriteDisplayData.Texture, position2, 0.0f, spriteDisplayData.Scale,
                Color.FromNonPremultiplied(255, 255, 255, SpriteSheetTransparency),
                position.Z, SpriteEffects.None);
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

            return xZ < yZ ? -1 : 1;
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
                //DrawCollision(collision2d, new Vector2(pos.X - spriteData.Origin.X * scale.X, pos.Y - spriteData.Origin.Y * scale.Y), scale, zOrder);
            }
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
    public void DrawSprite(Texture2D texture2d, Rectangle uv, Point origin, Vector2 position, float rotation,
        Vector2 scale, Color color, float z, SpriteEffects effects)
    {
        DrawSprite(texture2d, uv, origin, position, rotation, scale, color, z, effects, GraphicsDevice.ScissorRectangle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSprite(Texture2D texture2d, Rectangle uv, Point origin, Vector2 position, float rotation,
        Vector2 scale, Color color, float z, SpriteEffects effects, Rectangle scissorRectangle)
    {
        if (texture2d == null)
        {
            throw new ArgumentNullException(nameof(texture2d));
        }

        if (texture2d.IsDisposed)
        {
            throw new ArgumentException($"{nameof(texture2d)} is disposed");
        }

        var uvTopLeft = new Vector2(effects == SpriteEffects.FlipHorizontally ? uv.Right : uv.Left, effects == SpriteEffects.FlipVertically ? uv.Bottom : uv.Top);
        var uvTopRight = new Vector2(effects == SpriteEffects.FlipHorizontally ? uv.Left : uv.Right, effects == SpriteEffects.FlipHorizontally ? uv.Bottom : uv.Top);
        var uvBottomRight = new Vector2(effects == SpriteEffects.FlipHorizontally ? uv.Left : uv.Right, effects == SpriteEffects.FlipVertically ? uv.Top : uv.Bottom);
        var uvBottomLeft = new Vector2(effects == SpriteEffects.FlipHorizontally ? uv.Right : uv.Left, effects == SpriteEffects.FlipHorizontally ? uv.Top : uv.Bottom);

        var textureSize = new Vector2(texture2d.Width, texture2d.Height);
        uvTopLeft /= textureSize;
        uvTopRight /= textureSize;
        uvBottomRight /= textureSize;
        uvBottomLeft /= textureSize;

        GetSpriteDisplayData(out var spriteDisplayData);
        spriteDisplayData.TopLeft = new(new Vector3(-0.5f, -0.5f, z), uvTopLeft);
        spriteDisplayData.TopRight = new(new Vector3(0.5f, -0.5f, z), uvTopRight);
        spriteDisplayData.BottomRight = new(new Vector3(0.5f, 0.5f, z), uvBottomRight);
        spriteDisplayData.BottomLeft = new(new Vector3(-0.5f, 0.5f, z), uvBottomLeft);
        spriteDisplayData.Texture = texture2d;
        spriteDisplayData.Color = color;
        spriteDisplayData.WorldMatrix = MatrixExtensions.Transformation(
            new Vector3(scale.X * uv.Width, scale.Y * uv.Height, 1.0f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitZ, rotation),
            new Vector3(position.X - origin.X + uv.Width / 2f, position.Y - origin.Y * scale.Y + (uv.Height / 2f) * scale.Y, 0));
        spriteDisplayData.ScissorRectangle = scissorRectangle;
        //Debug Datas
        spriteDisplayData.Origin = new Vector2(position.X - origin.X + uv.Width / 2f, position.Y + origin.Y - uv.Height);
        spriteDisplayData.Border = uv;
        spriteDisplayData.Scale = scale;
        _spriteDatas.Add(spriteDisplayData);
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