using CasaEngine.Core.Helpers;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using EventArgs = System.EventArgs;
using EventHandler = System.EventHandler;

namespace CasaEngine.Framework.Game;

public class GameManager
{
    private readonly CasaEngineGame _game;
    private World.World? _currentWorld;
    private string? _worldToLoad;
    private bool _isNewWorld;

    public World.World? CurrentWorld
    {
        get => _currentWorld;
    }

    /// <summary>Manages active render views for the multi-view render pipeline.</summary>
    public ViewManager ViewManager { get; } = new ViewManager();

    public GameManager(CasaEngineGame game)
    {
        _game = game;
    }

    public void EndLoadContent()
    {
#if !EDITOR
        if (CurrentWorld == null)
        {
            if (string.IsNullOrWhiteSpace(GameSettings.ProjectSettings.FirstWorldLoaded))
            {
                throw new InvalidOperationException("FirstWorldLoaded is undefined");
            }

            SetWorldToLoad(GameSettings.ProjectSettings.FirstWorldLoaded);
        }
#endif
    }

    public void UpdateWorld(GameTime gameTime)
    {
        if (!string.IsNullOrEmpty(_worldToLoad))
        {
            if (_currentWorld != null)
            {
                _currentWorld.Clear();
            }

            var assetInfo = AssetCatalog.GetByFileName(_worldToLoad);
            _currentWorld = _game.AssetContentManager.Load<World.World>(assetInfo.Id, cache: false);
            _worldToLoad = null;
            _isNewWorld = true;
        }

        if (_isNewWorld)
        {
#if !EDITOR
            // Clear views so the incoming world can register a fresh camera view.
            ViewManager.Clear();
#endif
            CurrentWorld.LoadContent(_game);
            CurrentWorld.BeginPlay();

            _isNewWorld = false;
            OnWorldChange();
            WorldLoaded?.Invoke(this, EventArgs.Empty);
        }

        var elapsedTime = GameTimeHelper.ConvertElapsedTimeToSeconds(gameTime);
        //var totalElapsedTime = GameTimeHelper.ConvertTotalTimeToSeconds(gameTime);

        //if (Keyboard.GetState().IsKeyDown(Keys.OemQuotes))
        //    DebugSystem.Instance.DebugCommandUI.Show(); 

        CurrentWorld?.Update(elapsedTime);
    }

    public void SetWorldToLoad(string worldNameToLoad)
    {
        _worldToLoad = worldNameToLoad;
    }

    public void SetWorldToLoad(World.World world)
    {
        _currentWorld = world;
        _isNewWorld = true;
    }

    /// <summary>
    /// Fired (on all configurations) when a world finishes loading and its views are ready.
    /// Subscribe to push UI screens that depend on a live <see cref="UIRoot"/>.
    /// </summary>
    public event EventHandler? WorldLoaded;

    private void OnWorldChange()
    {
#if EDITOR
        WorldChanged?.Invoke(this, EventArgs.Empty);
#endif
    }


#if EDITOR

    public event EventHandler? WorldChanged;

#endif
}