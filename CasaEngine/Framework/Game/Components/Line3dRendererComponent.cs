using System.Diagnostics;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Game.Components;

public class Line3dRendererComponent : DrawableGameComponent, IViewFlushableRenderer
{
    private class Line3d
    {
        public Vector3 Start { get; private set; }
        public Vector3 End { get; private set; }
        public Color Color { get; private set; }

        public Line3d()
        {
        }

        public Line3d(Vector3 start, Vector3 end, Color color)
        {
            Set(start, end, color);
        }

        public void Set(Vector3 start, Vector3 end, Color color)
        {
            Start = start;
            End = end;
            Color = color;
        }
    }

    private const int NbLines = 5000;
    private readonly List<Line3d> _lines = new(NbLines);
    private readonly Stack<Line3d> _freeLines = new(NbLines);
    private readonly VertexPositionColor[] _vertices = new VertexPositionColor[NbLines * 2];

    private VertexBuffer? _vertexBuffer;
    private Effect? _effect;
    private readonly CasaEngineGame _game;

    public int PendingLineCount => _lines.Count;

    public int FrameFlushedLineCount { get; private set; }

    public double FrameFlushDurationMs { get; private set; }

    public int FramePeakPendingLineCount { get; private set; }

    public Line3dRendererComponent(Microsoft.Xna.Framework.Game game) : base(game)
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
        _effect = Game.Content.Load<Effect>("Shaders\\DebugPrimitiveColor").Clone();
    }

    public override void Update(GameTime gameTime)
    {
        FrameFlushedLineCount = 0;
        FrameFlushDurationMs = 0.0;
        FramePeakPendingLineCount = _lines.Count;
        base.Update(gameTime);
    }

    /// <inheritdoc/>
    public void Flush(in RenderFrame frame, RenderStats? stats = null)
    {
        int lineCount = _lines.Count;
        if (lineCount == 0)
        {
            return;
        }

        long startTimestamp = Stopwatch.GetTimestamp();

        for (var index = 0; index < lineCount && index < NbLines; index++)
        {
            var line = _lines[index];
            _vertices[index * 2 + 0].Position = line.Start;
            _vertices[index * 2 + 0].Color = line.Color;
            _vertices[index * 2 + 1].Position = line.End;
            _vertices[index * 2 + 1].Color = line.Color;
        }

        _vertexBuffer.SetData(_vertices, 0, Math.Min(lineCount * 2, NbLines * 2));
        Draw(Matrix.Identity, frame.View, frame.Projection, stats, lineCount);

        FrameFlushedLineCount += lineCount;
        FrameFlushDurationMs += (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;

        Clear();
    }

    private void Draw(Matrix world, Matrix view, Matrix projection, RenderStats? stats, int lineCount)
    {
        if (_effect == null)
        {
            return;
        }

        _effect.Parameters[ShaderParameterNames.WorldViewProj]?.SetValue(world * view * projection);
        _effect.Parameters[ShaderParameterNames.ColorMultiplier]?.SetValue(Vector4.One);

        Draw(_effect, stats, lineCount);
    }

    private void Draw(Effect effect, RenderStats? stats, int lineCount)
    {
        var graphicsDevice = effect.GraphicsDevice;

        //graphicsDevice.DepthStencilState = DepthStencilState.Default;
        graphicsDevice.DepthStencilState = DepthStencilState.Default;
        graphicsDevice.RasterizerState = RasterizerState.CullNone;
        graphicsDevice.BlendState = BlendState.Opaque;
        graphicsDevice.SetVertexBuffer(_vertexBuffer);
        GraphicsDevice.Indices = null;

        int passCount = effect.CurrentTechnique.Passes.Count;
        foreach (var effectPass in effect.CurrentTechnique.Passes)
        {
            effectPass.Apply();
            graphicsDevice.DrawPrimitives(PrimitiveType.LineList, 0, _lines.Count);
        }

        if (stats != null)
        {
            stats.LineCount += lineCount;
            stats.DrawCalls += passCount;
            stats.EffectBinds += passCount;
            stats.StateChanges += 4;
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
        if (_freeLines.TryPop(out var line3d))
        {
            line3d.Set(start, end, color);
        }
        else
        {
            line3d = new Line3d(start, end, color);
        }

        _lines.Add(line3d);
        FramePeakPendingLineCount = Math.Max(FramePeakPendingLineCount, _lines.Count);
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