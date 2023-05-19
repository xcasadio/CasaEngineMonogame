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

    private const int NbLines = 1024;
    private readonly List<Line3d> _lines = new(NbLines);
    private readonly VertexPositionColor[] _vertices = new VertexPositionColor[NbLines];
    private readonly Stack<Line3d> _freeLines = new(1024);

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

    public override void Update(GameTime gameTime)
    {
        for (var index = 0; index < _lines.Count; index++)
        {
            var line = _lines[index];
            _vertices[index * 2].Position = line.Start;
            _vertices[index * 2].Color = line.Color;
            _vertices[index * 2 + 1].Position = line.End;
            _vertices[index * 2 + 1].Color = line.Color;
        }
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

        _vertexBuffer.SetData(_vertices, 0, _lines.Count * 2);
        //Game.GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        Game.GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        var camera = _game.GameManager.ActiveCamera;
        Draw(Matrix.Identity, camera.ViewMatrix, camera.ProjectionMatrix);

        Clear();
    }

    private void Draw(Matrix world, Matrix view, Matrix projection)
    {
        _basicEffect.World = world;
        _basicEffect.View = view;
        _basicEffect.Projection = projection;
        _basicEffect.DiffuseColor = Color.White.ToVector3();
        _basicEffect.Alpha = 1.0f;

        Draw(_basicEffect);
    }

    private void Draw(Effect effect)
    {
        var graphicsDevice = effect.GraphicsDevice;

        graphicsDevice.SetVertexBuffer(_vertexBuffer);

        //effect.Parameters["WorldViewProj"] = ;

        foreach (var effectPass in effect.CurrentTechnique.Passes)
        {
            effectPass.Apply();
            graphicsDevice.DrawPrimitives(PrimitiveType.LineList, 0, _lines.Count);
        }
    }

    public void AddLine(Vector3 start, Vector3 end, Color color)
    {
        _lines.Add(new Line3d(start, end, color));
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