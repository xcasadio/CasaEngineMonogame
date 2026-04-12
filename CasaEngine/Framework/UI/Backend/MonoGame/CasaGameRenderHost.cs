using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MGUI.Shared.Rendering;

namespace CasaEngine.Framework.UI.Backend.MonoGame;

public sealed class CasaGameRenderHost<TObservableGame> : IRenderHost, IDisposable
    where TObservableGame : Game, IObservableUpdate
{
    public TObservableGame Game { get; }

    public Rectangle GetBounds() => new(0, 0, Game.Window.ClientBounds.Width, Game.Window.ClientBounds.Height);

    public GraphicsDevice GraphicsDevice => Game.GraphicsDevice;

    public object GetService(Type serviceType) => Game.Services.GetService(serviceType)!;

    public event EventHandler<TimeSpan>? PreviewUpdate;
    public event EventHandler<EventArgs>? EndUpdate;

    private Rectangle _previousClientBounds;
    private readonly EventHandler<TimeSpan> _onGamePreviewUpdate;
    private readonly EventHandler<EventArgs> _onGameEndUpdate;
    private readonly EventHandler<EventArgs> _onClientSizeChanged;

    public CasaGameRenderHost(TObservableGame game)
    {
        ArgumentNullException.ThrowIfNull(game);
        Game = game;

        _onGamePreviewUpdate = (_, elapsed) => PreviewUpdate?.Invoke(Game, elapsed);
        _onGameEndUpdate = (_, args) => EndUpdate?.Invoke(Game, args);
        _onClientSizeChanged = (_, _) =>
        {
            if (GraphicsDevice.ScissorRectangle == _previousClientBounds)
            {
                GraphicsDevice.ScissorRectangle = GetBounds();
            }

            _previousClientBounds = GetBounds();
        };

        Game.PreviewUpdate += _onGamePreviewUpdate;
        Game.EndUpdate += _onGameEndUpdate;

        _previousClientBounds = GetBounds();
        Game.Window.ClientSizeChanged += _onClientSizeChanged;
    }

    public void Dispose()
    {
        Game.PreviewUpdate -= _onGamePreviewUpdate;
        Game.EndUpdate -= _onGameEndUpdate;
        Game.Window.ClientSizeChanged -= _onClientSizeChanged;
        System.Diagnostics.Debug.WriteLine($"[Dispose] {GetType().Name} unsubscribed 3 event handlers");
    }
}