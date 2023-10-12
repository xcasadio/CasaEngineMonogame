//-----------------------------------------------------------------------------
// FpsCounter.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

using System.Diagnostics;
using System.Text;
using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Graphics2D;
using FontStashSharp;

namespace CasaEngine.Framework.Debugger;

public class FpsCounter : DrawableGameComponent
#if EDITOR
        , Core.Design.IObservable<FpsCounter>
#endif
{
    public float Fps { get; private set; }

    public float FpsMin { get; private set; }

    public float FpsMax { get; private set; }

    public float FpsAvg => _fpsAverage / _numberOfFpsCount;

    public int TotalNumberOfFrames { get; private set; }

    public TimeSpan SampleSpan { get; set; }

    // Reference for debug manager.
    private DebugManager _debugManager;

    // Stopwatch for fps measuring.
    private Stopwatch _stopwatch;

    private int _sampleFrames;

    // stringBuilder for FPS counter draw.
    private readonly StringBuilder _stringBuilder = new(16);

    private Renderer2dComponent _renderer2dComponent;

    private readonly Color _colorBackground = new(0, 0, 0, 128);

    private readonly List<Core.Design.IObserver<FpsCounter>> _listObserver = new();

    private float _fpsAverage;

    private int _numberOfFpsCount;

    private bool _firstCompute = true;
    private SpriteFontBase? _font;
    private SpriteBatch? _spriteBatch;

    public FpsCounter(Microsoft.Xna.Framework.Game game) : base(game)
    {
        SampleSpan = TimeSpan.FromSeconds(1);

        UpdateOrder = (int)ComponentUpdateOrder.DebugManager;
        DrawOrder = (int)ComponentDrawOrder.DebugManager;
    }

    public override void Initialize()
    {
        _debugManager = DebugSystem.Instance.DebugManager;

        if (_debugManager == null)
        {
            throw new InvalidOperationException("DebugManager is not registered.");
        }

        if (Game.Services.GetService(typeof(IDebugCommandHost)) is IDebugCommandHost host)
        {
            host.RegisterCommand("fps", "FPS Counter", CommandExecute);
            Visible = false;
        }

        Reset();

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _font = ((CasaEngineGame)Game).GameManager.FontSystem.GetFont(10);
        _spriteBatch = ((CasaEngineGame)Game).GameManager.SpriteBatch;
        _renderer2dComponent = Game.GetGameComponent<Renderer2dComponent>();

        if (_renderer2dComponent == null)
        {
            throw new InvalidOperationException("FpsCounter.LoadContent() : Renderer2dComponent is null");
        }

        base.LoadContent();
    }

    public void Reset()
    {
        Fps = 0;
        FpsMin = float.MaxValue;
        FpsMax = float.MinValue;
        TotalNumberOfFrames = 0;
        _firstCompute = true;
        _fpsAverage = 0.0f;
        _sampleFrames = 0;
        _stopwatch = Stopwatch.StartNew();
        _stringBuilder.Length = 0;
    }

    private void CommandExecute(IDebugCommandHost host,
        string command, IList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            Visible = !Visible;
        }

        foreach (var arg in arguments)
        {
            switch (arg.ToLower())
            {
                case "on":
                    Visible = true;
                    break;
                case "off":
                    Visible = false;
                    break;
            }
        }
    }

    public override void Update(GameTime gameTime)
    {
        if (_stopwatch.Elapsed > SampleSpan)
        {
            if (_firstCompute == false)
            {
                Fps = _sampleFrames / (float)_stopwatch.Elapsed.TotalSeconds;

                if (FpsMin > Fps)
                {
                    FpsMin = Fps;
                }

                if (FpsMax < Fps)
                {
                    FpsMax = Fps;
                }

                _fpsAverage += Fps;
                _numberOfFpsCount++;
            }

            _firstCompute = false;
            _sampleFrames = 0;

            // Update draw string.
            _stringBuilder.Length = 0;
            _stringBuilder.Append("FPS: ");
            _stringBuilder.AppendNumber(Fps);

            foreach (var ob in _listObserver)
            {
                ob.OnNotify(this);
            }

            _stopwatch.Reset();
            _stopwatch.Start();
        }

        _sampleFrames++;
        TotalNumberOfFrames++;
    }

    public override void Draw(GameTime gameTime)
    {
        // Compute size of border area.
        var size = _font.MeasureString("X");
        var rc = new Rectangle(0, 0, (int)(size.X * 14f), (int)(size.Y * 1.3f));

        var layout = new Layout(_spriteBatch.GraphicsDevice.Viewport);
        rc = layout.Place(rc, 0.01f, 0.01f, Alignment.TopLeft);

        // Place FPS string in border area.
        size = _font.MeasureString(_stringBuilder);
        layout.ClientArea = rc;
        var pos = layout.Place(size, 0, 0.1f, Alignment.Center);

        // Draw
        _renderer2dComponent.DrawRectangle(pos.X, pos.Y, rc.Width, rc.Height, _colorBackground, 0.001f);
        //_renderer2dComponent.DrawText(_font, _stringBuilder.ToString(), pos, 0.0f, Vector2.One, Color.White, 0f);

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
        _spriteBatch.DrawString(_font, _stringBuilder.ToString(), pos, Color.White);
        _spriteBatch.End();

        base.Draw(gameTime);
    }

    public void RegisterObserver(Core.Design.IObserver<FpsCounter> arg)
    {
        _listObserver.Add(arg);
    }

    public void UnRegisterObserver(Core.Design.IObserver<FpsCounter> arg)
    {
        _listObserver.Remove(arg);
        arg.OnUnregister(this);
    }

    public void NotifyObservers()
    {
        foreach (var ob in _listObserver)
        {
            ob.OnNotify(this);
        }
    }
}