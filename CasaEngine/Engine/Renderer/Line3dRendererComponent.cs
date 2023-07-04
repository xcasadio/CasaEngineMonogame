using System.Runtime.CompilerServices;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Engine.Renderer;

public class Line3dRendererComponent : DrawableGameComponent
{
    private class Line3d
    {
        public Vector3 Start { get; }
        public Vector3 End { get; }
        public Color Color { get; }

        public Line3d(Vector3 start, Vector3 end, Color color)
        {
            Start = start;
            End = end;
            Color = color;
        }
    }

    private const int NbLines = 50000;
    private readonly List<Line3d> _lines = new(NbLines);
    private readonly VertexPositionColor[] _vertices = new VertexPositionColor[NbLines * 2];
    private readonly Stack<Line3d> _freeLines = new(NbLines);

    private VertexBuffer? _vertexBuffer;
    private BasicEffect? _basicEffect;
    private readonly CasaEngineGame _game;

    public Line3dRendererComponent(Game game) : base(game)
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
        _vertexBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionColor), NbLines * 2, BufferUsage.None);
        _basicEffect = new BasicEffect(GraphicsDevice);
    }

    public override void Draw(GameTime gameTime)
    {
        if (_lines.Count == 0)
        {
            return;
        }

        for (var index = 0; index < _lines.Count && index < NbLines; index++)
        {
            var line = _lines[index];
            _vertices[index * 2 + 0].Position = line.Start;
            _vertices[index * 2 + 0].Color = line.Color;
            _vertices[index * 2 + 1].Position = line.End;
            _vertices[index * 2 + 1].Color = line.Color;
        }

        _vertexBuffer.SetData(_vertices, 0, Math.Min(_lines.Count * 2, NbLines * 2));
        var camera = _game.GameManager.ActiveCamera;
        Draw(Matrix.Identity, camera.ViewMatrix, camera.ProjectionMatrix);
        //Draw(Matrix.Identity, Matrix.CreateTranslation(-_game.Window.ClientBounds.Width / 2f, 0, 0.0f),
        //    Matrix.CreateOrthographic(_game.Window.ClientBounds.Width, _game.Window.ClientBounds.Height,
        //    //Matrix.CreateOrthographicOffCenter(-_game.Window.ClientBounds.Width, _game.Window.ClientBounds.Width, -_game.Window.ClientBounds.Height, _game.Window.ClientBounds.Height,
        //    0.0f, 1.0f));

        Clear();
    }

    private void Draw(Matrix world, Matrix view, Matrix projection)
    {
        _basicEffect.World = world;
        _basicEffect.View = view;
        _basicEffect.Projection = projection;
        _basicEffect.VertexColorEnabled = true;
        _basicEffect.Alpha = 1.0f;

        Draw(_basicEffect);
    }

    private void Draw(Effect effect)
    {
        var graphicsDevice = effect.GraphicsDevice;

        //graphicsDevice.DepthStencilState = DepthStencilState.Default;
        graphicsDevice.DepthStencilState = DepthStencilState.None;
        graphicsDevice.RasterizerState = RasterizerState.CullNone;
        graphicsDevice.BlendState = BlendState.AlphaBlend;
        graphicsDevice.SetVertexBuffer(_vertexBuffer);

        foreach (var effectPass in effect.CurrentTechnique.Passes)
        {
            effectPass.Apply();
            graphicsDevice.DrawPrimitives(PrimitiveType.LineList, 0, _lines.Count);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddLine(Vector3 start, Vector3 end, Color color)
    {
        AddLine(ref start, ref end, ref color);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddLine(ref Vector3 start, ref Vector3 end, ref Color color)
    {
        _lines.Add(new Line3d(start, end, color));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawCross(Vector2 pos, float z, int size, Color color)
    {
        AddLine(new Vector3(pos.X - size, pos.Y, z), new Vector3(pos.X + size, pos.Y, z), color);
        AddLine(new Vector3(pos.X, pos.Y - size, z), new Vector3(pos.X, pos.Y + size, z), color);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawRectangle(Rectangle rectangle, Color color, float z)
    {
        DrawRectangle(ref rectangle, color, z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawRectangle(ref Rectangle rectangle, Color color, float z)
    {
        DrawRectangle(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, color, z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawRectangle(float x, float y, float width, float height, Color color, float z)
    {
        var topLeft = new Vector3(x, y, z);
        var topRight = new Vector3(x + width, y, z);
        var bottomLeft = new Vector3(x, y + height, z);
        var bottomRight = new Vector3(x + width, y + height, z);

        AddLine(topLeft, topRight, color);
        AddLine(topLeft, bottomLeft, color);
        AddLine(topRight, bottomRight, color);
        AddLine(bottomLeft, bottomRight, color);
    }

    private void Clear()
    {
        foreach (var line in _lines)
        {
            _freeLines.Push(line);
        }

        _lines.Clear();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (this)
            {
                Game.RemoveGameComponent<Line3dRendererComponent>();
            }
        }

        base.Dispose(disposing);
    }
}