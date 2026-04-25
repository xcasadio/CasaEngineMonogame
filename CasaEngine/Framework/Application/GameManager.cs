
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using EventArgs = System.EventArgs;
using EventHandler = System.EventHandler;

namespace CasaEngine.Framework.Application;

public class GameManager
{
    private readonly CasaEngineGame _game;
    private Scene.World.World? _currentWorld;
    private string? _worldToLoad;
    private bool _isNewWorld;

    public Scene.World.World? CurrentWorld
    {
        get => _currentWorld;
    }

    /// <summary>Manages active render views for the multi-view render pipeline.</summary>
    public ViewManager ViewManager { get; } = new();

    public GameScreenManager ScreenManager { get; }

    public GameManager(CasaEngineGame game)
    {
        _game = game;
        ScreenManager = new GameScreenManager(ViewManager);
        ViewManager.ViewAdded += _ => SyncPlayerViewAssignments();
        ViewManager.ViewRemoved += _ => SyncPlayerViewAssignments();
    }

    public void EndLoadContent()
    {
        if (_game.ExecutionPolicy.UseExternalViewManagement)
        {
            return;
        }

        if (CurrentWorld == null)
        {
            if (string.IsNullOrWhiteSpace(GameSettings.ProjectSettings.FirstWorldLoaded))
            {
                throw new InvalidOperationException("FirstWorldLoaded is undefined");
            }

            SetWorldToLoad(GameSettings.ProjectSettings.FirstWorldLoaded);
        }
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
            _currentWorld = _game.AssetContentManager.Load<Scene.World.World>(assetInfo.Id, cache: false);
            _worldToLoad = null;
            _isNewWorld = true;
        }

        if (_isNewWorld)
        {
            if (!_game.ExecutionPolicy.UseExternalViewManagement)
            {
                // Clear views so the incoming world can register a fresh camera view.
                ViewManager.Clear();
            }

            CurrentWorld.LoadContent(_game);

            if (!_game.ExecutionPolicy.UseExternalViewManagement)
            {
                _game.RuntimeViewBootstrapper?.BootstrapViews(_game, CurrentWorld, ViewManager);
            }

            SyncPlayerViewAssignments();
            CurrentWorld.BeginPlay();

            _isNewWorld = false;
            OnWorldChange();
            WorldLoaded?.Invoke(this, EventArgs.Empty);
        }

        var elapsedTime = GameTimeHelper.ConvertElapsedTimeToSeconds(gameTime);
        CurrentWorld?.Update(elapsedTime);
    }

    public void SetWorldToLoad(string worldNameToLoad)
    {
        _worldToLoad = worldNameToLoad;
    }

    public void SetWorldToLoad(Scene.World.World world)
    {
        _currentWorld = world;
        _isNewWorld = true;
    }

    /// <summary>
    /// Fired (on all configurations) when a world finishes loading and its views are ready.
    /// Subscribe to push UI screens that depend on a live hosted UI runtime.
    /// </summary>
    public event EventHandler? WorldLoaded;

    public void SyncPlayerViewAssignments()
    {
        if (CurrentWorld == null)
        {
            return;
        }

        var inputRouter = _game.InputComponent.InputRouter;
        if (inputRouter == null)
        {
            return;
        }

        var views = ViewManager.Views;
        foreach (var playerController in CurrentWorld.PlayerControllers)
        {
            var playerIndex = ResolvePlayerIndex(playerController);
            var view = ResolveAssignedView(playerController, playerIndex, views, inputRouter);

            if (view == null)
            {
                inputRouter.UnassignPlayer(playerIndex);
                playerController.AssignedViewId = ViewId.Empty;
                playerController.UIView = null;
                continue;
            }

            inputRouter.AssignPlayer(playerIndex, view.Id);
            playerController.AssignedViewId = view.Id;
            playerController.UIView = view.UIView;
        }
    }

    public IUIViewRuntime? GetPlayerUIView(PlayerController playerController)
    {
        ArgumentNullException.ThrowIfNull(playerController);

        if (!playerController.AssignedViewId.IsEmpty
            && ViewManager.TryGetView(playerController.AssignedViewId, out var assignedView))
        {
            return assignedView.UIView;
        }

        return playerController.UIView;
    }

    private static PlayerIndex ResolvePlayerIndex(PlayerController playerController)
    {
        if (playerController.Player is LocalPlayer localPlayer)
        {
            return localPlayer.ControllerId;
        }

        return PlayerIndex.One;
    }

    private RenderView? ResolveAssignedView(
        PlayerController playerController,
        PlayerIndex playerIndex,
        IReadOnlyList<RenderView> views,
        InputRouter inputRouter)
    {
        if (!playerController.AssignedViewId.IsEmpty
            && ViewManager.TryGetView(playerController.AssignedViewId, out var existingAssignedView)
            && existingAssignedView.Enabled
            && existingAssignedView.IsVisible)
        {
            return existingAssignedView;
        }

        var routerViewId = inputRouter.GetViewForPlayer(playerIndex);
        if (!routerViewId.IsEmpty
            && ViewManager.TryGetView(routerViewId, out var routerAssignedView)
            && routerAssignedView.Enabled
            && routerAssignedView.IsVisible)
        {
            return routerAssignedView;
        }

        if (views.Count == 0)
        {
            return null;
        }

        var preferredIndex = Math.Clamp((int)playerIndex, 0, views.Count - 1);
        return views[preferredIndex];
    }

    private void OnWorldChange()
    {
        WorldChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? WorldChanged;
}