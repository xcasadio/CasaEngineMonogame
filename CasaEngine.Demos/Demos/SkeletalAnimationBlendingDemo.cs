using System;
using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Port of the three.js "Skeletal Animation Blending" example: an animated kid character
/// blends idle/walk/run on a shared skeleton over a shadow-receiving ground plane, with a
/// gray background and an approximated hemisphere ambient + key directional light.
/// </summary>
public class SkeletalAnimationBlendingDemo : Demo
{
    // Soldier.glb (three.js/mixamo) is loaded via SharpGLTF: Y-up, standing, ~1.83 units (m)
    // tall, feet at model origin (joint AABB min.Y ≈ 0), facing -Z (back toward +Z camera).
    // Rotate 180° around Y so it faces the camera at +Z.
    // The ground is a BoxPrimitive(100,1,100) centred at world Y=-0.5 → top surface at Y=0.
    // Place feet at Y=0.02 so mesh vertices (a few cm below the lowest joint) sit on the surface.
    private const float CharacterScale = 1.0f;

    private static readonly SkeletonDebugDrawOptions SkeletonDebugOptions = new(
        0.15f,
        true,
        new Color(255, 214, 96),
        new Color(255, 110, 110),
        new Color(110, 255, 150),
        new Color(96, 170, 255));

    private const int IdleIndex = 0;
    private const int WalkIndex = 1;
    private const int RunIndex = 2;

    private CasaEngineGame? _game;
    private Entity? _characterEntity;
    private SkinnedMeshComponent? _skinnedMeshComponent;
    private WeightedBlendAnimationNode? _blendNode;
    private readonly AnimationClipNode[] _clipNodes = new AnimationClipNode[3];
    private BlendingControlsScreen? _controlsScreen;

    // three.js default blend weights: idle 0, walk 1, run 0.
    private float _idleWeight;
    private float _walkWeight = 1f;
    private float _runWeight;
    private float _timeScale = 1f;

    // Crossfade state (mirrors three.js prepareCrossFade / executeCrossFade).
    private bool _crossFading;
    private float _crossFadeElapsed;
    private float _crossFadeDuration;
    private Vector3 _crossFadeStart;
    private Vector3 _crossFadeTarget;

    // Pausing / stepping state.
    private bool _paused;
    private float _stepSize = 0.05f;

    // Crossfade duration settings.
    private bool _useDefaultDuration = true;
    private float _customDuration = 3.5f;

    // Visibility state.
    private bool _showSkeleton;

    // Foot lock demo: the soldier walks forward at a constant speed (wrapping back to the
    // start) while its feet stay pinned to the ground via FootLockController, showcasing the
    // fix for in-place-authored locomotion clips played on a moving entity.
    private const float FootLockSpeed = 1.2f;
    private const float FootLockWrapDistance = 6f;
    private const float FootContactHeightThreshold = 0.05f;
    private FootLockController? _footLockController;
    private bool _footLockEnabled;
    private float _footLockDistanceTraveled;

    public override string Title => "Skeletal animation blending";

    public override string Description =>
        "Port of the three.js skeletal animation blending example: blends idle/walk/run on a kid character with a Controls panel for visibility, activation, pausing/stepping, crossfading, blend weights and speed.";

    public override CameraComponent CreateCamera(CasaEngineGame game)
    {
        var entity = new Entity { Name = "SkeletalBlendingCamera" };
        var camera = new CameraLookAtComponent();
        entity.RootComponent = camera;
        entity.Initialize();
        game.GameManager.CurrentWorld.AddEntity(entity);
        return camera;
    }

    public override void ConfigureSceneLighting(World world)
    {
        var environmentSettings = world.EnvironmentSettings;
        environmentSettings.Type = EnvironmentType.None;
        environmentSettings.BackgroundMode = EnvironmentBackgroundMode.SolidColor;
        // three.js: scene.background = 0xa0a0a0
        environmentSettings.BackgroundColor = new Color(160, 160, 160);
        environmentSettings.BackgroundCubemap = null;
        environmentSettings.SpecularEnvironmentCubemap = null;
        // Approximate three.js HemisphereLight( 0xffffff, 0x8d8d8d, 3 ) with bright ambient.
        environmentSettings.AmbientColor = new Vector3(0.85f, 0.86f, 0.88f);
        environmentSettings.AmbientIntensity = 1.7f;
        environmentSettings.SpecularIntensity = 0.15f;
        environmentSettings.Shadows.Enabled = true;
        // Tight shadow frustum: camera is ~3.8 units from the soldier, MaxDistance=8 gives
        // a 16x16 world-unit ortho projection → ~0.016 u/texel at 1024 resolution.
        // The default MaxDistance=100 would produce a 200x200 frustum and blocky ~5-texel shadows.
        environmentSettings.Shadows.MaxDistance = 8.0f;
        environmentSettings.MarkDirty();

        // Key light from the camera/front side: source above and at +Z, direction toward -Z.
        // (Soldier faces +Z so a negative-Z direction component lights the front.)
        AddDirectionalLight(
            world,
            "SkeletalBlendingKeyLight",
            new Vector3(0.29f, -0.81f, -0.51f),
            new Color(255, 250, 244),
            Color.White,
            2.4f,
            castShadows: true);

        // Soft fill from behind/above to separate the silhouette.
        AddDirectionalLight(
            world,
            "SkeletalBlendingFillLight",
            new Vector3(-0.2f, -0.35f, 0.92f),
            new Color(150, 158, 170),
            new Color(80, 86, 96),
            0.3f);
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        // Soldier faces +Z (after 180° Y rotation). Camera on the +Z side, closer framing.
        camera.SetPositionAndTarget(new Vector3(0f, 1.4f, 3.5f), new Vector3(0f, 0.8f, 0f));

        var uiView = GetUIView();
        if (uiView != null && _controlsScreen == null)
        {
            _controlsScreen = new BlendingControlsScreen();
            _controlsScreen.IdleWeightChanged += weight => _idleWeight = weight;
            _controlsScreen.WalkWeightChanged += weight => _walkWeight = weight;
            _controlsScreen.RunWeightChanged += weight => _runWeight = weight;
            _controlsScreen.TimeScaleChanged += speed => _timeScale = speed;

            _controlsScreen.ShowModelChanged += ShowModel;
            _controlsScreen.ShowSkeletonChanged += visible => _showSkeleton = visible;
            _controlsScreen.FootLockChanged += SetFootLockEnabled;

            _controlsScreen.DeactivateAllRequested += DeactivateAll;
            _controlsScreen.ActivateAllRequested += ActivateAll;
            _controlsScreen.PauseContinueRequested += PauseContinue;
            _controlsScreen.SingleStepRequested += MakeSingleStep;
            _controlsScreen.StepSizeChanged += size => _stepSize = size;

            _controlsScreen.UseDefaultDurationChanged += useDefault => _useDefaultDuration = useDefault;
            _controlsScreen.CustomDurationChanged += duration => _customDuration = duration;
            _controlsScreen.WalkToIdleRequested += () => PrepareCrossFade(new Vector3(1f, 0f, 0f), 1.0f);
            _controlsScreen.IdleToWalkRequested += () => PrepareCrossFade(new Vector3(0f, 1f, 0f), 0.5f);
            _controlsScreen.WalkToRunRequested += () => PrepareCrossFade(new Vector3(0f, 0f, 1f), 2.5f);
            _controlsScreen.RunToWalkRequested += () => PrepareCrossFade(new Vector3(0f, 1f, 0f), 5.0f);

            uiView.PushScreen(_controlsScreen);
        }
    }

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        var world = game.GameManager.CurrentWorld;

        CreateGround(world, game.GraphicsDevice);

        var riggedModel = SoldierLocomotionModelFactory.Create(game);
        var skinnedMesh = new SkinnedMesh();
        skinnedMesh.SetRiggedModel(riggedModel);

        _characterEntity = new Entity { Name = "SkeletalBlendingCharacter" };
        _skinnedMeshComponent = new SkinnedMeshComponent
        {
            SkinnedMesh = skinnedMesh,
            CastShadows = true,
            ReceiveShadows = true,
        };
        _characterEntity.RootComponent = _skinnedMeshComponent;
        // Ground top surface = Y 0 (BoxPrimitive 1u tall centred at Y=-0.5).
        // Tiny upward offset so foot mesh vertices (slightly below the lowest joint) don't clip.
        // Left at the identity orientation: the soldier faces +Z, which is both where the camera
        // stands and what the key light setup above assumes.
        _skinnedMeshComponent.LocalPosition = new Vector3(0f, 0.02f, 0f);
        _skinnedMeshComponent.LocalScale = new Vector3(CharacterScale);

        BuildBlendGraph();
        TryCreateFootLockController();

        world.AddEntity(_characterEntity);
    }

    private void TryCreateFootLockController()
    {
        var skeleton = _skinnedMeshComponent?.SkeletonDefinition;
        if (skeleton == null
            || !skeleton.TryGetJointIndex("mixamorig:LeftFoot", out _)
            || !skeleton.TryGetJointIndex("mixamorig:RightFoot", out _))
        {
            return;
        }

        _footLockController = new FootLockController(
            skeleton,
            new FootLockSettings(),
            FootLockFoot.FromAnkleName(skeleton, "mixamorig:LeftFoot"),
            FootLockFoot.FromAnkleName(skeleton, "mixamorig:RightFoot"));
    }

    private void SetFootLockEnabled(bool enabled)
    {
        _footLockEnabled = enabled;
        if (!enabled)
        {
            _footLockDistanceTraveled = 0f;
            _skinnedMeshComponent?.ClearTwoBoneIkConstraints();
            if (_skinnedMeshComponent != null)
            {
                _skinnedMeshComponent.LocalPosition = new Vector3(0f, 0.02f, 0f);
            }
        }
    }

    private void UpdateFootLock(float dt)
    {
        if (_footLockController == null || _skinnedMeshComponent?.CurrentModelPose == null)
        {
            return;
        }

        _footLockDistanceTraveled += FootLockSpeed * dt;
        if (_footLockDistanceTraveled > FootLockWrapDistance)
        {
            _footLockDistanceTraveled -= FootLockWrapDistance;
        }

        _skinnedMeshComponent.LocalPosition = new Vector3(0f, 0.02f, _footLockDistanceTraveled);

        var pose = _skinnedMeshComponent.CurrentModelPose;
        var entityWorld = _skinnedMeshComponent.WorldMatrixWithScale;
        Span<bool> contacts = stackalloc bool[2];
        for (var footIndex = 0; footIndex < _footLockController.FeetCount; footIndex++)
        {
            var ankleWorldY = Vector3.Transform(
                pose.GetTransform(_footLockController.Feet[footIndex].EndJointIndex).Translation,
                entityWorld).Y;
            contacts[footIndex] = ankleWorldY <= FootContactHeightThreshold;
        }

        _footLockController.Update(dt, pose, entityWorld, contacts);
        _skinnedMeshComponent.ApplyFootLock(_footLockController);
    }

    private void BuildBlendGraph()
    {
        if (_skinnedMeshComponent == null)
        {
            return;
        }

        var clips = _skinnedMeshComponent.AnimationClips;
        if (clips.Count < 3)
        {
            throw new InvalidOperationException("The skeletal blending demo expects idle, walk and run clips.");
        }

        _clipNodes[IdleIndex] = new AnimationClipNode(clips[IdleIndex], loop: true);
        _clipNodes[WalkIndex] = new AnimationClipNode(clips[WalkIndex], loop: true);
        _clipNodes[RunIndex] = new AnimationClipNode(clips[RunIndex], loop: true);

        _blendNode = new WeightedBlendAnimationNode(
            new IAnimationGraphNode[] { _clipNodes[IdleIndex], _clipNodes[WalkIndex], _clipNodes[RunIndex] },
            new[] { _idleWeight, _walkWeight, _runWeight });

        _skinnedMeshComponent.PlayAnimationGraph(_blendNode);
    }

    public override void Update(GameTime gameTime)
    {
        if (_blendNode == null)
        {
            return;
        }

        if (_crossFading)
        {
            _crossFadeElapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;
            float t = _crossFadeDuration <= 0f
                ? 1f
                : MathHelper.Clamp(_crossFadeElapsed / _crossFadeDuration, 0f, 1f);
            var weights = Vector3.Lerp(_crossFadeStart, _crossFadeTarget, t);
            _idleWeight = weights.X;
            _walkWeight = weights.Y;
            _runWeight = weights.Z;

            _controlsScreen?.SetWeightDisplays(_idleWeight, _walkWeight, _runWeight);

            if (t >= 1f)
            {
                _crossFading = false;
            }
        }

        ApplyWeightsAndSpeed();
        UpdateCrossFadeControls();

        if (_footLockEnabled)
        {
            UpdateFootLock((float)gameTime.ElapsedGameTime.TotalSeconds);
        }

        if (_showSkeleton)
        {
            DrawSkeleton();
        }
    }

    private void ShowModel(bool visible)
    {
        if (_characterEntity != null)
        {
            _characterEntity.IsVisible = visible;
        }
    }

    private void DrawSkeleton()
    {
        if (_game == null || _skinnedMeshComponent?.CurrentModelPose == null)
        {
            return;
        }

        SkeletonDebugVisualizer.Draw(
            _game.Line3dRendererComponent,
            _skinnedMeshComponent.CurrentModelPose,
            _skinnedMeshComponent.WorldMatrixWithScale,
            SkeletonDebugOptions);
    }

    private void ApplyWeightsAndSpeed()
    {
        if (_blendNode == null)
        {
            return;
        }

        _blendNode.SetWeight(IdleIndex, _idleWeight);
        _blendNode.SetWeight(WalkIndex, _walkWeight);
        _blendNode.SetWeight(RunIndex, _runWeight);

        for (var clipIndex = 0; clipIndex < _clipNodes.Length; clipIndex++)
        {
            _clipNodes[clipIndex].Speed = _timeScale;
        }
    }

    // Enables/disables the crossfade buttons based on the current pure-weight state
    // (mirrors three.js updateCrossFadeControls).
    private void UpdateCrossFadeControls()
    {
        if (_controlsScreen == null)
        {
            return;
        }

        if (_idleWeight == 1f && _walkWeight == 0f && _runWeight == 0f)
        {
            _controlsScreen.SetCrossFadeButtonsEnabled(false, true, false, false);
        }
        else if (_idleWeight == 0f && _walkWeight == 1f && _runWeight == 0f)
        {
            _controlsScreen.SetCrossFadeButtonsEnabled(true, false, true, false);
        }
        else if (_idleWeight == 0f && _walkWeight == 0f && _runWeight == 1f)
        {
            _controlsScreen.SetCrossFadeButtonsEnabled(false, false, false, true);
        }
        else
        {
            _controlsScreen.SetCrossFadeButtonsEnabled(true, true, true, true);
        }
    }

    private void PrepareCrossFade(Vector3 targetWeights, float defaultDuration)
    {
        // Leaving single-step / paused mode, like three.js prepareCrossFade.
        SetPaused(false);

        _crossFadeDuration = _useDefaultDuration ? defaultDuration : _customDuration;
        _crossFadeStart = new Vector3(_idleWeight, _walkWeight, _runWeight);
        _crossFadeTarget = targetWeights;
        _crossFadeElapsed = 0f;
        _crossFading = true;
    }

    private void DeactivateAll()
    {
        _crossFading = false;
        _idleWeight = 0f;
        _walkWeight = 0f;
        _runWeight = 0f;
        _controlsScreen?.SetWeightDisplays(0f, 0f, 0f);
    }

    private void ActivateAll()
    {
        _crossFading = false;
        _idleWeight = 0f;
        _walkWeight = 1f;
        _runWeight = 0f;
        _controlsScreen?.SetWeightDisplays(0f, 1f, 0f);
    }

    private void PauseContinue()
    {
        SetPaused(!_paused);
    }

    private void SetPaused(bool paused)
    {
        if (_paused == paused)
        {
            return;
        }

        _paused = paused;
        if (_paused)
        {
            _skinnedMeshComponent?.PauseAnimation();
        }
        else
        {
            _skinnedMeshComponent?.ResumeAnimation();
        }
    }

    private void MakeSingleStep()
    {
        // Freeze continuous playback and advance exactly one step (three.js single step).
        SetPaused(true);
        ApplyWeightsAndSpeed();
        _skinnedMeshComponent?.AdvanceAnimation(_stepSize);
    }

    public override void Clean()
    {
        if (_controlsScreen != null)
        {
            GetUIView()?.RemoveScreen(_controlsScreen);
            _controlsScreen = null;
        }

        if (_game?.GameManager.CurrentWorld is { } world)
        {
            world.EnvironmentSettings.ResetToDefaults();
            world.EnvironmentSettings.MarkDirty();
        }

        _game = null;
        _characterEntity = null;
        _skinnedMeshComponent = null;
        _blendNode = null;
        Array.Clear(_clipNodes);
        _crossFading = false;
        _paused = false;
        _showSkeleton = false;
        _footLockController = null;
        _footLockEnabled = false;
        _footLockDistanceTraveled = 0f;
    }

    private CasaEngine.Framework.UI.IUIViewRuntime? GetUIView()
        => _game?.GameManager.ViewManager.GetActiveUIView();

    private static void CreateGround(World world, GraphicsDevice graphicsDevice)
    {
        var groundMaterial = new LitDiffuseMaterial
        {
            Name = "SkeletalBlendingGround",
            // three.js: MeshPhongMaterial( { color: 0xcbcbcb } )
            DiffuseColor = new Color(203, 203, 203),
            AmbientColor = new Vector3(0.22f, 0.22f, 0.22f),
            SpecularColor = Vector3.Zero,
        };

        var mesh = new StaticModelMesh
        {
            Name = "SkeletalBlendingGroundMesh",
            Material = groundMaterial,
        };
        var primitive = new BoxPrimitive(100.0f, 1.0f, 100.0f);
        mesh.SetData(primitive.Vertices.ToArray(), primitive.Indices.ToArray());
        mesh.Initialize(graphicsDevice);

        var model = new StaticModel { Name = "SkeletalBlendingGroundModel" };
        model.Meshes.Add(mesh);
        model.RootNode = new StaticModelNode
        {
            Name = "SkeletalBlendingGroundRoot",
            MeshIndex = 0,
            Position = Vector3.Zero,
            Rotation = Quaternion.Identity,
            Scale = Vector3.One,
        };

        var component = new StaticModelComponent
        {
            StaticModel = model,
            CastShadows = false,
            ReceiveShadows = true,
        };
        component.LocalPosition = new Vector3(0f, -0.5f, 0f);

        var entity = new Entity { Name = "SkeletalBlendingGround", RootComponent = component };
        world.AddEntity(entity);
    }

    private static void AddDirectionalLight(
        World world,
        string entityName,
        Vector3 direction,
        Color diffuseColor,
        Color specularColor,
        float intensity,
        bool castShadows = false)
    {
        var entity = new Entity { Name = entityName };
        entity.RootComponent = new LightComponent
        {
            Type = LightType.Directional,
            LocalOrientation = CreateOrientationFromForward(direction),
            Color = diffuseColor,
            SpecularColor = specularColor,
            Intensity = intensity,
            CastShadows = castShadows,
        };

        world.AddEntity(entity);
    }

    private static Quaternion CreateOrientationFromForward(Vector3 forward)
    {
        if (forward.LengthSquared() <= 0.0001f)
        {
            return Quaternion.Identity;
        }

        forward = Vector3.Normalize(forward);
        float dot = MathHelper.Clamp(Vector3.Dot(Vector3.Forward, forward), -1.0f, 1.0f);

        if (dot >= 0.9999f)
        {
            return Quaternion.Identity;
        }

        if (dot <= -0.9999f)
        {
            return Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.Pi);
        }

        var axis = Vector3.Normalize(Vector3.Cross(Vector3.Forward, forward));
        float angle = MathF.Acos(dot);
        return Quaternion.CreateFromAxisAngle(axis, angle);
    }
}
