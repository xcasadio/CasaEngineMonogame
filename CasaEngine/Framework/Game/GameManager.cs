using CasaEngine.Core.Helpers;
using CasaEngine.Engine;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;
using EventArgs = System.EventArgs;
using EventHandler = System.EventHandler;

namespace CasaEngine.Framework.Game;

public class GameManager
{
    private readonly CasaEngineGame _game;
    private CameraComponent? _activeCamera;
    private World.World? _currentWorld;
    private string? _worldToLoad;
    private bool _isNewWorld;

    public World.World? CurrentWorld
    {
        get => _currentWorld;
    }

    /// <summary>Manages active render views for the multi-view render pipeline.</summary>
    public ViewManager ViewManager { get; } = new ViewManager();

    /// <summary>
    /// The currently active camera. Used as a backward-compatible fallback when
    /// <see cref="UseRenderPipeline"/> is false or when <see cref="ViewManager"/> has no views.
    /// <para/>
    /// TODO: Deprecate once multi-view pipeline is the default path.
    /// Prefer adding views to <see cref="ViewManager"/> and letting each
    /// <see cref="RenderView"/> carry its own camera.
    /// </summary>
    public CameraComponent? ActiveCamera
    {
        get => _activeCamera;
        set
        {
            _activeCamera = value;
            if (_activeCamera != null)
            {
                if (_activeCamera.Owner.IsInitialized == false)
                {
                    throw new InvalidOperationException("LoadContent the camera before activate it");
                }

                //TODO: why change min an max depth create bugs ?
                _game.SetViewport(_activeCamera.Viewport.Bounds);
            }
        }
    }

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
            CurrentWorld.LoadContent(_game);
            CurrentWorld.BeginPlay();

#if EDITOR
            //in editor the active camera is debug camera and it isn't belong to the world
            SetCameraWithEditor(CurrentWorld);
#endif

            _isNewWorld = false;
            OnWorldChange();
        }

        var elapsedTime = GameTimeHelper.ConvertElapsedTimeToSeconds(gameTime);
        //var totalElapsedTime = GameTimeHelper.ConvertTotalTimeToSeconds(gameTime);

#if EDITOR
        _cameraEditorEntity.Update(elapsedTime);
        _cameraEditorEntity.GameplayProxy?.Update(elapsedTime);
#endif

        //if (Keyboard.GetState().IsKeyDown(Keys.OemQuotes))
        //    DebugSystem.Instance.DebugCommandUI.Show(); 

        CurrentWorld?.Update(elapsedTime);
    }

    /// <summary>
    /// Draws the current world using the legacy single-camera path.
    /// Called only when <see cref="CasaEngineGame.UseRenderPipeline"/> is false.
    /// <para/>
    /// TODO: This method becomes a no-op when multi-view pipeline is the default.
    /// </summary>
    public void DrawWorld(GameTime gameTime)
    {
        CurrentWorld?.Draw(ActiveCamera.ViewMatrix * ActiveCamera.ProjectionMatrix);
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

    private void OnWorldChange()
    {
#if EDITOR
        WorldChanged?.Invoke(this, EventArgs.Empty);
#endif
    }


#if EDITOR

    public event EventHandler? WorldChanged;

    private Entity _cameraEditorEntity;

    private void SetCameraWithEditor(World.World world)
    {
        _cameraEditorEntity = new Entity { Name = "Camera editor" };
        _cameraEditorEntity.IsVisible = false;

        var cameraEditor = CreateCameraComponentCallback != null ?
            CreateCameraComponentCallback(_cameraEditorEntity) : CreateCameraComponent(_cameraEditorEntity);

        _cameraEditorEntity.AddComponent(cameraEditor);
        _cameraEditorEntity.Initialize();
        _cameraEditorEntity.GameplayProxy?.Initialize(_cameraEditorEntity);
        _cameraEditorEntity.InitializeWithWorld(world);
        _cameraEditorEntity.GameplayProxy?.InitializeWithWorld(world);

        ActiveCamera = cameraEditor;
    }

    public delegate CameraComponent CameraComponentCallback(Entity cameraEntity);
    public CameraComponentCallback? CreateCameraComponentCallback;

    private ArcBallCameraComponent CreateCameraComponent(Entity cameraEntity)
    {
        var cameraEditor = new ArcBallCameraComponent();
        cameraEditor.SetCamera(Vector3.Backward * 10 + Vector3.Up * 10, Vector3.Zero, Vector3.Up);
        cameraEntity.GameplayProxyClassName = nameof(ScriptArcBallCamera);
        return cameraEditor;
    }

#endif
}