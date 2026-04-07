using System;
using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Input;

using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Demonstrates rendering a hosted UI runtime into a texture and displaying it on a world-space quad.
/// </summary>
public sealed class WorldSpaceUIDemo : Demo
{
    private const float ScreenWidth = 8.0f;
    private const float ScreenHeight = 4.5f;
    private static readonly Vector3 InitialCameraTarget = new(0.0f, 4.0f, 0.0f);
    private const float InitialCameraDistance = -18.25f;
    private const float InitialCameraYaw = MathHelper.Pi;
    private const float InitialCameraPitch = 0.165f;

    private CasaEngineGame? _game;
    private WorldUIComponent? _worldUi;
    private HudScreen? _hudScreen;
    private PauseMenuScreen? _pauseScreen;
    private StaticModelComponent? _screenMesh;
    private ProjectedMouseInputSource? _worldUiInputSource;

    public override string Title => "World-space UI demo";
    public override string Description => "Renders a UI runtime offscreen and displays the resulting texture on a quad inside the 3D world.";

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        var world = game.GameManager.CurrentWorld;

        var groundEntity = new Entity { Name = "Ground" };
        var groundMesh = new StaticModelComponent();
        groundEntity.RootComponent = groundMesh;
        groundMesh.StaticModel = StaticModel.CreateFromPrimitive(new BoxPrimitive(24, 1, 24));
        groundMesh.StaticModel.Meshes[0].Initialize(game.GraphicsDevice);
        groundMesh.StaticModel.Meshes[0].Material = new LitDiffuseMaterial { DiffuseColor = new Color(160, 150, 135) };
        groundMesh.LocalPosition = new Vector3(0, -0.5f, 0);
        world.AddEntity(groundEntity);

        var screenEntity = new Entity { Name = "World UI Screen" };
        var screenMesh = new StaticModelComponent();
        screenEntity.RootComponent = screenMesh;
        screenMesh.StaticModel = StaticModel.CreateFromPrimitive(new PlanePrimitive(ScreenWidth, ScreenHeight));
        screenMesh.StaticModel.Meshes[0].Initialize(game.GraphicsDevice);

        var screenMaterial = new UnlitTextureMaterial
        {
            Tint = Color.White,
            Alpha = 1.0f,
        };

        screenMesh.StaticModel.Meshes[0].Material = screenMaterial;
        screenMesh.LocalPosition = new Vector3(0, 4.0f, 0);
        screenMesh.LocalOrientation = Quaternion.CreateFromAxisAngle(Vector3.Right, MathHelper.PiOver2);
        world.AddEntity(screenEntity);
        _screenMesh = screenMesh;

        _worldUi = new WorldUIComponent(game.GraphicsDevice, 1024, 576)
        {
            UIView = null,
        };

        var baseInputSource = game.RuntimeContext.WindowInputSource ?? new MonoGameWindowInputSource(() => game.IsActive);
        _worldUiInputSource = new ProjectedMouseInputSource(baseInputSource, TryProjectWorldUiMouse);
        var worldUiRuntimeContext = game.RuntimeContext.CloneWithWindowInputSource(_worldUiInputSource);

        _worldUi.UIView = game.UIViewRuntimeFactory.Create(game, _worldUi.Surface, worldUiRuntimeContext);
        _worldUi.BindToMaterial(screenMaterial);
        world.RegisterWorldUI(_worldUi);

        _hudScreen = new HudScreen(OnOpenPauseMenu);
        _worldUi.UIView?.PushScreen(_hudScreen);
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        if (camera is ArcBallCameraComponent arcBallCamera)
        {
            arcBallCamera.Target = InitialCameraTarget;
            arcBallCamera.Distance = InitialCameraDistance;
            arcBallCamera.Yaw = InitialCameraYaw;
            arcBallCamera.Pitch = InitialCameraPitch;
            return;
        }

        camera.SetPositionAndTarget(new Vector3(0, 7, 18), InitialCameraTarget);
    }

    public override void Update(GameTime gameTime)
    {
    }

    public override void Clean()
    {
        if (_game?.GameManager.CurrentWorld != null && _worldUi != null)
        {
            _game.GameManager.CurrentWorld.UnregisterWorldUI(_worldUi);
            if (_pauseScreen != null)
            {
                _worldUi.UIView?.RemoveScreen(_pauseScreen);
            }
            if (_hudScreen != null)
            {
                _worldUi.UIView?.RemoveScreen(_hudScreen);
            }
            _worldUi.Dispose();
        }

        _hudScreen = null;
        _pauseScreen = null;
        _screenMesh = null;
        _worldUi = null;
        _worldUiInputSource = null;
        _game = null;
    }

    private void OnOpenPauseMenu()
    {
        if (_worldUi?.UIView == null || _pauseScreen != null)
        {
            return;
        }

        _pauseScreen = new PauseMenuScreen(OnResume);
        _worldUi.UIView.PushScreen(_pauseScreen);
    }

    private void OnResume()
    {
        if (_pauseScreen == null)
        {
            return;
        }

        _worldUi?.UIView?.RemoveScreen(_pauseScreen);
        _pauseScreen = null;
    }

    private MouseState? TryProjectWorldUiMouse(WindowInputSnapshot snapshot)
    {
        if (_game == null || _worldUi == null || _screenMesh == null)
        {
            return null;
        }

        var interactionView = ResolveInteractionView(_game);
        if (interactionView == null)
        {
            return null;
        }

        var screenBounds = GetScreenBounds(interactionView);
        var screenMouseState = snapshot.MouseState;
        if (!screenBounds.Contains(screenMouseState.Position))
        {
            return null;
        }

        var viewLocalPoint = new Vector2(
            screenMouseState.X - screenBounds.X,
            screenMouseState.Y - screenBounds.Y);

        var ray = _game.GameManager.ViewManager.ViewToWorldRay(interactionView, viewLocalPoint);
        if (!TryProjectRayToWorldUi(ray, out var uiPoint))
        {
            return null;
        }

        return new MouseState(
            uiPoint.X,
            uiPoint.Y,
            screenMouseState.ScrollWheelValue,
            screenMouseState.LeftButton,
            screenMouseState.MiddleButton,
            screenMouseState.RightButton,
            screenMouseState.XButton1,
            screenMouseState.XButton2,
            screenMouseState.HorizontalScrollWheelValue);
    }

    private bool TryProjectRayToWorldUi(Ray ray, out Point uiPoint)
    {
        uiPoint = Point.Zero;

        if (_worldUi == null || _screenMesh == null)
        {
            return false;
        }

        var inverseWorld = Matrix.Invert(_screenMesh.WorldMatrixWithScale);
        var localRayPosition = Vector3.Transform(ray.Position, inverseWorld);
        var localRayDirection = Vector3.TransformNormal(ray.Direction, inverseWorld);

        if (localRayDirection.LengthSquared() <= float.Epsilon)
        {
            return false;
        }

        localRayDirection.Normalize();

        if (MathF.Abs(localRayDirection.Y) <= 0.0001f)
        {
            return false;
        }

        var distance = -localRayPosition.Y / localRayDirection.Y;
        if (distance < 0.0f)
        {
            return false;
        }

        var localHit = localRayPosition + localRayDirection * distance;
        var halfWidth = ScreenWidth * 0.5f;
        var halfHeight = ScreenHeight * 0.5f;

        if (localHit.X < -halfWidth || localHit.X > halfWidth || localHit.Z < -halfHeight || localHit.Z > halfHeight)
        {
            return false;
        }

        var u = (localHit.X + halfWidth) / ScreenWidth;
        var v = (localHit.Z + halfHeight) / ScreenHeight;

        uiPoint = new Point(
            (int)Math.Clamp(u * (_worldUi.Resolution.X - 1), 0.0f, _worldUi.Resolution.X - 1),
            (int)Math.Clamp(v * (_worldUi.Resolution.Y - 1), 0.0f, _worldUi.Resolution.Y - 1));
        return true;
    }

    private RenderView? ResolveInteractionView(CasaEngineGame game)
    {
        var viewManager = game.GameManager.ViewManager;
        var activeView = viewManager.ActiveView;
        if (activeView != null
            && activeView.World == game.GameManager.CurrentWorld
            && activeView.Enabled
            && activeView.IsVisible)
        {
            return activeView;
        }

        foreach (var view in viewManager.Views)
        {
            if (view.World == game.GameManager.CurrentWorld && view.Enabled && view.IsVisible)
            {
                return view;
            }
        }

        return null;
    }

    private static Rectangle GetScreenBounds(RenderView view)
    {
        return view.Host is IViewScreenBoundsHost screenBoundsHost
            ? screenBoundsHost.ScreenBounds
            : view.Surface.ViewportRect;
    }
}