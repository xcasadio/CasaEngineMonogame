
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.ScreenEffects;
using CasaEngine.Framework.UI.MGUI;
using Microsoft.Xna.Framework;
using EventArgs = System.EventArgs;
using EventHandler = System.EventHandler;

namespace CasaEngine.Framework.Application;

public class GameManager
{
    private const string ScreenAssetType = "uiscreen";
    private const string WorldAssetType = "world";
    private const string SoundAssetType = "sound";
    private const string FontAssetType = "fnt";
    private const string ButtonsMappingAssetType = "buttonsmapping";

    private readonly CasaEngineGame _game;
    private readonly AssetContentManager _assetContentManager;
    private Scene.World.World _currentWorld;
    private string _worldToLoad;
    private bool _isNewWorld;
    private bool _firstScreenOpened;
    private float _scaledTotalTime;
    private float _timeScale = 1f;
    private long _frameIndex;

    public Scene.World.World CurrentWorld
    {
        get => _currentWorld;
    }

    /// <summary>Manages active render views for the multi-view render pipeline.</summary>
    public ViewManager ViewManager { get; } = new();

    public GameScreenManager ScreenManager { get; }

    public float TimeScale
    {
        get => _timeScale;
        set
        {
            if (value < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "TimeScale cannot be negative.");
            }

            _timeScale = value;
        }
    }

    public GameManager(CasaEngineGame game)
        : this(game, game?.AssetContentManager)
    {
    }

    /// <param name="game">The game, or null in tests.</param>
    /// <param name="assetContentManager">The manager whose unreferenced assets are freed when a world change
    /// starts (ADR-0036); the game's own for the public constructor.</param>
    internal GameManager(CasaEngineGame game, AssetContentManager assetContentManager)
    {
        _game = game;
        _assetContentManager = assetContentManager;
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
        if (HasPendingWorldLoad)
        {
            // ADR-0036: a world change starts. Free what nobody holds any more, before the old world is
            // cleared: its holders still hold what they use, so nothing that crosses the change is freed,
            // and whatever they give back while clearing stays pending until the next world change.
            int freedAssetCount = _assetContentManager?.CollectUnreferenced() ?? 0;

            // ADR-0037 (P7): visible proof, at Info level, that unreferenced assets are actually freed
            // across a world change.
            Logs.WriteInfo($"World change: {freedAssetCount} unreferenced asset(s) freed.");
        }

        if (!string.IsNullOrEmpty(_worldToLoad))
        {
            if (_currentWorld != null)
            {
                _currentWorld.Clear();
            }

            var assetInfo = AssetCatalog.GetByFileName(_worldToLoad);

            // GetByFileName returns null for an unknown file name (AssetCatalog.GetByFileName), so a
            // world path that is not in the catalog used to surface as a NullReferenceException on
            // assetInfo.Id, one frame after whoever asked for the load - with nothing naming the path.
            // Gameplay code can request a world by name (SetWorldToLoad(string)), so the caller needs
            // to be told which path failed. Same exception shape EndLoadContent already uses for an
            // undefined FirstWorldLoaded.
            if (assetInfo == null)
            {
                throw new InvalidOperationException($"World not found in the asset catalog: '{_worldToLoad}'");
            }

            _currentWorld = _game.AssetContentManager.LoadCopy<Scene.World.World>(assetInfo.Id);
            _worldToLoad = null;
            _isNewWorld = true;
        }

        if (_isNewWorld)
        {
            // ADR-0087: the world's own virtual resolution is in force before its views and screens are built.
            _game.UseWorldVirtualResolution(CurrentWorld);

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
            OpenFirstScreen(_game.ExecutionPolicy, GameSettings.ProjectSettings.FirstScreenName, CreateDataDrivenScreen);
            DiscardLoadTime(_game.ExecutionPolicy, _game.LoadTimeDiscard);
        }

        float unscaledElapsedTime = GameTimeHelper.ConvertElapsedTimeToSeconds(gameTime);
        _scaledTotalTime += unscaledElapsedTime * TimeScale;
        _frameIndex++;

        FrameTime frameTime = GameTimeHelper.CreateFrameTime(gameTime, TimeScale, _scaledTotalTime, _frameIndex);
        CurrentWorld?.Update(frameTime);
    }

    /// <summary>
    /// Opens the project's first screen on the active view, the first time it is called with a name (ADR-0078). The
    /// policy and the factory are parameters because <c>_game</c> is null in tests.
    /// </summary>
    /// <param name="policy">The running policy: under external view management the editor owns the views, so nothing opens.</param>
    /// <param name="firstScreenName">The screen asset's id or name; empty opens nothing.</param>
    /// <param name="createScreen">Builds the screen for a view.</param>
    /// <exception cref="InvalidOperationException">A screen is named but no active view hosts a UI.</exception>
    internal void OpenFirstScreen(GameplayExecutionPolicy policy, string firstScreenName, Func<string, IUIViewRuntime, IUIScreen> createScreen)
    {
        if (_firstScreenOpened || string.IsNullOrWhiteSpace(firstScreenName) || policy.UseExternalViewManagement)
        {
            return;
        }

        var view = ViewManager.GetActiveUIView()
            ?? throw new InvalidOperationException($"First screen '{firstScreenName}' needs an active view that hosts a UI, and there is none.");

        _firstScreenOpened = true;
        PushNewScreen(view, createScreen(firstScreenName, view));
    }

    /// <summary>
    /// Opens a screen for the <c>OpenScreen</c> cutscene action. Unlike <see cref="OpenFirstScreen"/> it opens on every
    /// call and leaves the one-time opening of <c>FirstScreenName</c> alone. The policy and the factory are parameters
    /// because <c>_game</c> is null in tests.
    /// </summary>
    /// <returns>Null when the screen is open, or when nothing has to open under external view management; otherwise why it cannot open.</returns>
    internal string OpenScreenFromCutscene(GameplayExecutionPolicy policy, string screenName, Func<string, IUIViewRuntime, IUIScreen> createScreen)
    {
        if (policy.UseExternalViewManagement)
        {
            Logs.WriteInfo($"OpenScreen '{screenName}' skipped: the editor manages the views.");
            return null;
        }

        if (!IsScreenAsset(screenName))
        {
            return $"OpenScreen: no screen asset '{screenName}' is registered in this project.";
        }

        IUIViewRuntime view = ViewManager.GetActiveUIView();
        if (view == null)
        {
            return $"OpenScreen: screen '{screenName}' needs an active view that hosts a UI, and there is none.";
        }

        PushNewScreen(view, createScreen(screenName, view));
        return null;
    }

    /// <summary>Opens a screen for the <c>OpenScreen</c> cutscene action on the running game.</summary>
    internal string OpenScreenFromCutscene(string screenName)
        => _game == null
            ? "OpenScreen needs a running game, and this manager has none."
            : OpenScreenFromCutscene(_game.ExecutionPolicy, screenName, CreateDataDrivenScreen);

    /// <summary>Asks for a world for the <c>ChangeWorld</c> cutscene action; the world loads at the next update.</summary>
    /// <returns>Null when the world is requested; otherwise why it cannot be.</returns>
    internal string RequestWorldFromCutscene(string worldFileName)
    {
        if (!IsWorldAsset(worldFileName))
        {
            return $"ChangeWorld: no world asset '{worldFileName}' is registered in this project.";
        }

        SetWorldToLoad(worldFileName);
        return null;
    }

    /// <summary>The world name waiting for the next <see cref="UpdateWorld"/>, or null.</summary>
    internal string PendingWorldToLoad => _worldToLoad;

    /// <summary>
    /// Starts the discard of what the new world took to start. Under external view management the editor runs the loop:
    /// this game is never run, so it has no catch-up to stop and no frame that would end the discard.
    /// </summary>
    internal static void DiscardLoadTime(GameplayExecutionPolicy policy, LoadTimeDiscard loadTimeDiscard)
    {
        if (!policy.UseExternalViewManagement)
        {
            loadTimeDiscard.WorldReady();
        }
    }

    /// <summary>A screen that never reached the stack has no one to dispose it.</summary>
    private static void PushNewScreen(IUIViewRuntime view, IUIScreen screen)
    {
        try
        {
            view.PushScreen(screen);
        }
        catch
        {
            (screen as IDisposable)?.Dispose();
            throw;
        }
    }

    private DataDrivenScreen CreateDataDrivenScreen(string assetIdOrName, IUIViewRuntime view)
        => new(_game.AssetContentManager, assetIdOrName, CreateScreenActionContext(view, ReadScreenGameServices()));

    private ScreenGameServices ReadScreenGameServices()
        => new(
            () => _game.ScreenEffectComponent.Service,
            _game.ActiveVirtualResolution,
            fontId => _game.UIFonts.Acquire(fontId),
            _game.UIInputMapping != null,
            _game.Exit);

    /// <summary>What a data-driven screen asks of the game. <paramref name="game"/> is a parameter because <c>_game</c> is null in tests.</summary>
    internal UIScreenActionContext CreateScreenActionContext(IUIViewRuntime view, ScreenGameServices game)
        => new()
        {
            ScreenExists = IsScreenAsset,
            WorldExists = IsWorldAsset,
            AcquireSound = AcquireSound,
            IsTopScreen = screen => view is UIRoot root && ReferenceEquals(root.ScreenStack.Top, screen),
            OpenScreen = assetIdOrName => PushNewScreen(view, CreateDataDrivenScreen(assetIdOrName, view)),
            CloseScreen = view.RemoveScreen,
            ChangeWorld = SetWorldToLoad,
            PlaySound = PlaySound,
            Quit = game.Quit,
            StartFade = action => StartScreenFade(game.ScreenEffects(), action),
            IsFadeRunning = () => game.ScreenEffects().IsFading,
            DesignResolution = DesignResolutionOf(game.VirtualResolution),
            AcquireFont = assetIdOrName => game.HoldFont(FindFontAsset(assetIdOrName).Id),
            HasUIInputMapping = game.HasUIInputMapping,
        };

    /// <summary>
    /// Starts the fade a checked <c>fade_screen</c> action describes (ADR-0081). The layer goes first: it stays on the
    /// service, and the fade must not draw a frame on the previous one.
    /// </summary>
    internal static void StartScreenFade(ScreenEffectService effects, UIScreenActionData action)
    {
        var from = action.From.Value;
        var to = action.To.Value;

        effects.Layer = action.Layer.Value;
        effects.StartFade(from.R, from.G, from.B, from.A, to.R, to.G, to.B, to.A, action.DurationSeconds.Value, action.BlendMode.Value);
    }

    /// <summary>
    /// The resolution screens are written at (ADR-0079). Only a project that sets <c>ScaleUI</c> has one, and the
    /// editor never does: it manages its own views, so it has no active virtual resolution.
    /// </summary>
    internal static Point? DesignResolutionOf(VirtualResolutionSettings virtualResolution)
        => virtualResolution is { ScaleUI: true } ? new Point(virtualResolution.Width, virtualResolution.Height) : null;

    internal static bool IsScreenAsset(string assetIdOrName) => IsCataloguedAs(FindAsset(assetIdOrName), ScreenAssetType);

    internal static bool IsWorldAsset(string fileName) => IsCataloguedAs(AssetCatalog.GetByFileName(fileName), WorldAssetType);

    /// <summary>
    /// The sound the name or id designates. The type is checked here because <c>SoundAssetLoader</c> would read any
    /// catalogued JSON as a silent sound, and leave that instance in the cache under the id of another asset.
    /// </summary>
    /// <exception cref="InvalidOperationException">The project has no such sound.</exception>
    internal static AssetInfo FindSoundAsset(string assetIdOrName)
    {
        var assetInfo = FindAsset(assetIdOrName);

        return IsCataloguedAs(assetInfo, SoundAssetType)
            ? assetInfo
            : throw new InvalidOperationException($"No sound asset '{assetIdOrName}' is registered in this project.");
    }

    /// <summary>
    /// The bitmap font the name or id designates. The type is checked here because <c>BitmapFontLoader</c> would read
    /// any catalogued text file, and fail on a message that does not name what the screen asked for. A name that lands
    /// on something else is told apart: the catalogue keeps the last asset of a name, so a page PNG named like its
    /// <c>.fnt</c> hides a font that is registered.
    /// </summary>
    /// <exception cref="InvalidOperationException">The project has no such font.</exception>
    internal static AssetInfo FindFontAsset(string assetIdOrName)
    {
        var assetInfo = FindAsset(assetIdOrName);

        if (IsCataloguedAs(assetInfo, FontAssetType))
        {
            return assetInfo;
        }

        throw new InvalidOperationException(assetInfo == null
            ? $"No bitmap font asset '{assetIdOrName}' is registered in this project."
            : $"'{assetIdOrName}' designates '{assetInfo.FileName}', of type '{assetInfo.AssetType}', and not a bitmap font. The catalogue keeps one asset per name, the last it reads: if a font has this name too, a page PNG for instance has to be renamed.");
    }

    /// <summary>The buttons mapping the name or id designates (ADR-0080).</summary>
    /// <exception cref="InvalidOperationException">The project has no such mapping.</exception>
    internal static AssetInfo FindButtonsMappingAsset(string assetIdOrName)
    {
        var assetInfo = FindAsset(assetIdOrName);

        return IsCataloguedAs(assetInfo, ButtonsMappingAssetType)
            ? assetInfo
            : throw new InvalidOperationException($"No buttons mapping asset '{assetIdOrName}' is registered in this project.");
    }

    private static AssetInfo FindAsset(string assetIdOrName)
        => Guid.TryParse(assetIdOrName, out var assetId) ? AssetCatalog.Get(assetId) : AssetCatalog.Get(assetIdOrName);

    private static bool IsCataloguedAs(AssetInfo assetInfo, string assetType)
        => assetInfo != null && string.Equals(assetInfo.AssetType, assetType, StringComparison.OrdinalIgnoreCase);

    private AssetHandle<SoundAsset> AcquireSound(string assetIdOrName)
        => _game.AssetContentManager.Acquire<SoundAsset>(FindSoundAsset(assetIdOrName).Id);

    private void PlaySound(SoundAsset sound)
    {
        _game.AudioSystemComponent?.Service?.PlaySound(sound);
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
    /// Reinstalls an already-loaded world as the current world without running
    /// LoadContent/BeginPlay again. Used by the editor to bring the edit world back
    /// after a play-in-editor session: that world was set aside fully loaded, so the
    /// normal load path would duplicate its entities.
    /// </summary>
    public void RestoreWorld(Scene.World.World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        _worldToLoad = null;
        _isNewWorld = false;
        _currentWorld = world;
        OnWorldChange();
    }

    internal bool HasPendingWorldLoad => _isNewWorld || !string.IsNullOrEmpty(_worldToLoad);

    /// <summary>
    /// Fired (on all configurations) when a world finishes loading and its views are ready.
    /// Subscribe to push UI screens that depend on a live hosted UI runtime.
    /// </summary>
    public event EventHandler WorldLoaded;

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

    public IUIViewRuntime GetPlayerUIView(PlayerController playerController)
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
        return playerController.PlayerIndex;
    }

    private RenderView ResolveAssignedView(
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

    public event EventHandler WorldChanged;
}