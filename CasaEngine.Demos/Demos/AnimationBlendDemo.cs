using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Demos.Demos;

public class AnimationBlendDemo : Demo
{
    private const string BlendModeEnvironmentVariable = "CASAENGINE_ANIMATION_BLEND_MODE";
    private const string BlendSpace2DXEnvironmentVariable = "CASAENGINE_ANIMATION_BLEND_SPACE_2D_X";
    private const string BlendSpace2DYEnvironmentVariable = "CASAENGINE_ANIMATION_BLEND_SPACE_2D_Y";

    private enum DemoMode
    {
        LinearBlendTree,
        BlendSpace1D,
        BlendSpace2D,
        CrossFade,
        AdvancedCrossFade,
        LayeredUpperBody,
        AdditiveRootMotion,
    }

    private const float CharacterScale = 0.1f;
    private const float OneDimensionalBlendSpeed = 3.5f;
    private const float TwoDimensionalBlendSpeed = 4.5f;
    private const float CrossFadeDurationSeconds = 0.35f;
    private const float UpperBodyActionDurationSeconds = 0.8f;
    private const float AdditiveBreathingDurationSeconds = 1.8f;
    private const float RootMotionBurstDurationSeconds = 0.75f;
    private const float DirectionalStrafeDurationSeconds = 1.0f;
    private const int UpperBodyLayerIndex = 0;
    private const int AdditiveLayerIndex = 0;
    private const int RootMotionLayerIndex = 1;
    private const float RootMotionTrailSpacing = 0.2f;
    private static readonly Color RootMotionTrailColor = new(255, 204, 64);
    private static readonly Color RootMotionVectorColor = new(96, 220, 255);

    private CasaEngineGame _game;
    private SkinnedMeshComponent _skinnedMeshComponent;
    private RiggedModel _riggedModel;
    private LinearBlendAnimationNode _linearBlendTree;
    private BlendSpace1DNode _blendSpace1D;
    private BlendSpace2DNode _blendSpace2D;
    private AnimationClip _upperBodyActionClip;
    private AnimationClip _additiveBreathingClip;
    private AnimationClip _rootMotionBurstClip;
    private BoneMask _upperBodyMask;
    private BoneMask _rootMotionMask;
    private DemoMode _demoMode;
    private KeyboardState _previousKeyboardState;
    private float _blendParameter1D;
    private Vector2 _blendParameter2D;
    private float _upperBodyActionTimeRemaining;
    private float _rootMotionBurstTimeRemaining;
    private float _upperBodyLayerWeight = 1f;
    private float _additiveLayerWeight = 0.45f;
    private bool _applyRootMotionToEntity;
    private Vector2? _configuredBlendSpace2DParameter;
    private AnimationTransitionEasingMode _advancedCrossFadeEasingMode = AnimationTransitionEasingMode.EaseOutCubic;
    private bool _advancedCrossFadePreserveRootVelocity = true;
    private RootMotionDelta _lastObservedRootMotionDelta = RootMotionDelta.Identity;
    private readonly List<Vector3> _rootMotionTrailPoints = new(64);
    private string _latestEventMessage = string.Empty;
    private float _latestEventTimeRemaining;
    private readonly StringBuilder _hudBuilder = new(768);

    public override string Title => "Animation blend demo";

    public override string Description => "Animation showcase for the modern runtime: linear blend trees, 1D/2D blend spaces, baseline and advanced cross-fades, masked upper-body override layers with events, additive breathing, and root-motion observe/apply. Use Tab to cycle the showcase pages.";

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;

        var world = game.GameManager.CurrentWorld;
        var riggedModel = CreateRiggedModel(game);
        var skinnedMesh = new SkinnedMesh();
        skinnedMesh.SetRiggedModel(riggedModel);

        CreateBlendGraphs(riggedModel);
        CreateProceduralShowcaseAssets(riggedModel);

        var entity = new Entity { Name = "Animation Blend Character" };
        _skinnedMeshComponent = new SkinnedMeshComponent();
        _skinnedMeshComponent.AnimationEventTriggered += OnAnimationEventTriggered;
        entity.RootComponent = _skinnedMeshComponent;
        ResetCharacterTransform();
        _skinnedMeshComponent.SkinnedMesh = skinnedMesh;

        world.AddEntity(entity);

        _riggedModel = riggedModel;
        _blendParameter1D = 0f;
        _blendParameter2D = Vector2.Zero;
        _configuredBlendSpace2DParameter = ResolveConfiguredBlendSpace2DParameter();
        _demoMode = ResolveInitialDemoMode();
        ApplyDemoMode();
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        base.InitializeCamera(camera);
    }

    public override void Update(GameTime gameTime)
    {
        if (_game == null || _riggedModel == null || _linearBlendTree == null || _blendSpace1D == null || _blendSpace2D == null)
        {
            return;
        }

        var keyboardState = _game.IsActive ? Keyboard.GetState() : new KeyboardState();
        if (IsNewKeyPress(keyboardState, Keys.Tab))
        {
            CycleDemoMode(IsShiftPressed(keyboardState) ? -1 : 1);
        }

        if (IsNewKeyPress(keyboardState, Keys.Back))
        {
            ResetCharacterTransform();
        }

        var elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        UpdateTransientTimers(elapsedSeconds);

        switch (_demoMode)
        {
            case DemoMode.LinearBlendTree:
                UpdateLinearBlendTree(keyboardState, elapsedSeconds);
                break;

            case DemoMode.BlendSpace1D:
                UpdateOneDimensionalBlend(keyboardState, elapsedSeconds);
                break;

            case DemoMode.BlendSpace2D:
                UpdateTwoDimensionalBlend(keyboardState, elapsedSeconds);
                break;

            case DemoMode.CrossFade:
                UpdateCrossFadeMode(keyboardState);
                break;

            case DemoMode.AdvancedCrossFade:
                UpdateAdvancedCrossFadeMode(keyboardState);
                break;

            case DemoMode.LayeredUpperBody:
                UpdateLayeredUpperBodyMode(keyboardState, elapsedSeconds);
                break;

            case DemoMode.AdditiveRootMotion:
                UpdateAdditiveRootMotionMode(keyboardState, elapsedSeconds);
                break;
        }

        if (_demoMode == DemoMode.AdditiveRootMotion)
        {
            ConsumeRootMotionForShowcase();
            DrawRootMotionTrail();
        }

        _previousKeyboardState = keyboardState;
    }

    public override void PostDraw(CasaEngineGame game, GameTime gameTime)
    {
        var spriteBatch = game.Renderer2DComponent?.SpriteBatch;
        if (spriteBatch == null)
        {
            return;
        }

        var font = game.FontSystem.GetFont(14);
        _hudBuilder.Clear();
        _hudBuilder.AppendLine("=== Animation Showcase ===");
        _hudBuilder.AppendLine($"Mode: {GetModeLabel(_demoMode)}");
        _hudBuilder.AppendLine();

        switch (_demoMode)
        {
            case DemoMode.LinearBlendTree:
                _hudBuilder.AppendLine($"Blend: {_blendParameter1D:F2} (0=Idle, 1=Walk, 2=Run)");
                _hudBuilder.AppendLine("[Tab] Next page  [Shift+Tab] Previous page");
                _hudBuilder.AppendLine("[W] Walk  [Shift+W] Run  [Backspace] Reset actor");
                break;

            case DemoMode.BlendSpace1D:
                _hudBuilder.AppendLine($"Blend: {_blendParameter1D:F2} (0=Idle, 1=Walk, 2=Run)");
                _hudBuilder.AppendLine("[Tab] Next page  [Shift+Tab] Previous page");
                _hudBuilder.AppendLine("[W] Walk  [Shift+W] Run  [Backspace] Reset actor");
                break;

            case DemoMode.BlendSpace2D:
                _hudBuilder.AppendLine($"Blend: X={_blendParameter2D.X:F2}  Y={_blendParameter2D.Y:F2}");
                _hudBuilder.AppendLine("[Tab] Next page  [Shift+Tab] Previous page");
                _hudBuilder.AppendLine("[W/A/D] Explore strafe/forward directional locomotion  [Backspace] Reset actor");
                break;

            case DemoMode.CrossFade:
                _hudBuilder.AppendLine("[1] Idle  [2] Walk  [3] Run");
                _hudBuilder.AppendLine($"Cross-fade duration: {CrossFadeDurationSeconds:F2}s");
                _hudBuilder.AppendLine("[Tab] Next page  [Shift+Tab] Previous page");
                break;

            case DemoMode.AdvancedCrossFade:
                _hudBuilder.AppendLine("[1] Idle  [2] Walk  [3] Run");
                _hudBuilder.AppendLine($"Transition easing: {GetTransitionEasingLabel(_advancedCrossFadeEasingMode)}");
                _hudBuilder.AppendLine($"Preserve root velocity: {(_advancedCrossFadePreserveRootVelocity ? "On" : "Off")}");
                _hudBuilder.AppendLine($"Transition active: {(_riggedModel?.AnimationController?.IsCrossFading == true ? "Yes" : "No")}");
                _hudBuilder.AppendLine("[E] Cycle easing  [R] Toggle velocity preservation");
                _hudBuilder.AppendLine("[Tab] Next page  [Shift+Tab] Previous page");
                break;

            case DemoMode.LayeredUpperBody:
                _hudBuilder.AppendLine($"Base locomotion: {_blendParameter1D:F2}");
                _hudBuilder.AppendLine($"Upper-body layer weight: {_upperBodyLayerWeight:F2}");
                _hudBuilder.AppendLine("[W] Walk  [Shift+W] Run  [Space] Trigger upper-body action");
                _hudBuilder.AppendLine("[Q]/[E] Decrease/Increase layer weight");
                break;

            case DemoMode.AdditiveRootMotion:
                _hudBuilder.AppendLine($"Base locomotion: {_blendParameter1D:F2}");
                _hudBuilder.AppendLine($"Additive breathing weight: {_additiveLayerWeight:F2}");
                _hudBuilder.AppendLine($"Root motion mode: {(_applyRootMotionToEntity ? "Apply to entity" : "Observe only")}");
                _hudBuilder.AppendLine($"Last delta: {_lastObservedRootMotionDelta.Translation.X:F2}, {_lastObservedRootMotionDelta.Translation.Y:F2}, {_lastObservedRootMotionDelta.Translation.Z:F2}");
                _hudBuilder.AppendLine("[W] Walk  [Shift+W] Run  [Space] Trigger root-motion burst");
                _hudBuilder.AppendLine("[R] Toggle observe/apply  [Backspace] Reset trail and actor");
                break;
        }

        if (_latestEventTimeRemaining > 0f && !string.IsNullOrEmpty(_latestEventMessage))
        {
            _hudBuilder.AppendLine();
            _hudBuilder.AppendLine($"Event: {_latestEventMessage}");
        }

        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
        font.DrawText(spriteBatch, _hudBuilder.ToString(), new Vector2(10f, 10f), Color.White);
        spriteBatch.End();
    }

    public override void Clean()
    {
        if (_skinnedMeshComponent != null)
        {
            _skinnedMeshComponent.AnimationEventTriggered -= OnAnimationEventTriggered;
        }

        _game = null;
        _skinnedMeshComponent = null;
        _riggedModel = null;
        _linearBlendTree = null;
        _blendSpace1D = null;
        _blendSpace2D = null;
        _upperBodyActionClip = null;
        _additiveBreathingClip = null;
        _rootMotionBurstClip = null;
        _upperBodyMask = null;
        _rootMotionMask = null;
        _blendParameter1D = 0f;
        _blendParameter2D = Vector2.Zero;
        _configuredBlendSpace2DParameter = null;
        _previousKeyboardState = new KeyboardState();
        _rootMotionTrailPoints.Clear();
        _lastObservedRootMotionDelta = RootMotionDelta.Identity;
        _latestEventMessage = string.Empty;
        _latestEventTimeRemaining = 0f;
    }

    private static RiggedModel CreateRiggedModel(CasaEngineGame game)
    {
        var idleModel = game.AssetContentManager.LoadFromFile<RiggedModel>(@"SkinnedMesh\kid_idle.glb");
        var walkModel = game.AssetContentManager.LoadFromFile<RiggedModel>(@"SkinnedMesh\kid_walk.glb");
        var runModel = game.AssetContentManager.LoadFromFile<RiggedModel>(@"SkinnedMesh\kid_run.glb");

        if (idleModel.SkeletonDefinition == null)
        {
            throw new InvalidOperationException("The idle rigged model did not expose a runtime skeleton.");
        }

        var skeleton = idleModel.SkeletonDefinition;
        ValidateSkeletonCompatibility(skeleton, walkModel.SkeletonDefinition, "walk");
        ValidateSkeletonCompatibility(skeleton, runModel.SkeletonDefinition, "run");

        if (idleModel.AnimationClips.Count == 0 || walkModel.AnimationClips.Count == 0 || runModel.AnimationClips.Count == 0)
        {
            throw new InvalidOperationException("The kid demo assets must each provide at least one runtime animation clip.");
        }

        var animationClips = new List<AnimationClip>(3)
        {
            RebindClip(idleModel.AnimationClips[0], skeleton, "Idle"),
            RebindClip(walkModel.AnimationClips[0], skeleton, "Walk"),
            RebindClip(runModel.AnimationClips[0], skeleton, "Run"),
        };

        idleModel.OverrideRuntimeAnimationAssets(skeleton, animationClips);
        return idleModel;
    }

    private void CreateBlendGraphs(RiggedModel riggedModel)
    {
        var clips = riggedModel.AnimationClips;
        if (clips.Count < 3)
        {
            throw new InvalidOperationException("The animation blend demo expects idle, walk, and run clips.");
        }

        var skeleton = riggedModel.SkeletonDefinition ?? throw new InvalidOperationException("The animation blend demo expects a runtime skeleton for its directional showcase.");
        var strafeLeftClip = CreateDirectionalStrafeClip(skeleton, -1f, "StrafeLeft");
        var strafeRightClip = CreateDirectionalStrafeClip(skeleton, 1f, "StrafeRight");

        _linearBlendTree = new LinearBlendAnimationNode(
            new IAnimationGraphNode[]
            {
                new AnimationClipNode(clips[0]),
                new AnimationClipNode(clips[1]),
                new AnimationClipNode(clips[2]),
            });

        _blendSpace1D = new BlendSpace1DNode(
            new[]
            {
                new BlendSpace1DSample(0f, new AnimationClipNode(clips[0])),
                new BlendSpace1DSample(1f, new AnimationClipNode(clips[1])),
                new BlendSpace1DSample(2f, new AnimationClipNode(clips[2])),
            });

        _blendSpace2D = new BlendSpace2DNode(
            new[]
            {
                new BlendSpace2DSample(new Vector2(0f, 0f), new AnimationClipNode(clips[0])),
                new BlendSpace2DSample(new Vector2(-1f, 0f), new AnimationClipNode(strafeLeftClip)),
                new BlendSpace2DSample(new Vector2(0f, 1f), new AnimationClipNode(clips[1])),
                new BlendSpace2DSample(new Vector2(1f, 0f), new AnimationClipNode(strafeRightClip)),
            },
            Vector2.Zero);
    }

    private void CreateProceduralShowcaseAssets(RiggedModel riggedModel)
    {
        var skeleton = riggedModel.SkeletonDefinition ?? throw new InvalidOperationException("The showcase rigged model must expose a runtime skeleton.");
        _upperBodyMask = CreateUpperBodyMask(skeleton);
        _rootMotionMask = CreateRootOnlyMask(skeleton);
        _upperBodyActionClip = CreateUpperBodyActionClip(skeleton);
        _additiveBreathingClip = CreateAdditiveBreathingClip(skeleton);
        _rootMotionBurstClip = CreateRootMotionBurstClip(skeleton);
    }

    private void ApplyDemoMode()
    {
        if (_skinnedMeshComponent == null || _linearBlendTree == null || _blendSpace1D == null || _blendSpace2D == null)
        {
            return;
        }

        _skinnedMeshComponent.ClearAnimationLayer(UpperBodyLayerIndex);
        _skinnedMeshComponent.ClearAnimationLayer(RootMotionLayerIndex);
        _skinnedMeshComponent.RootMotionMode = RootMotionMode.Observe;
        _upperBodyActionTimeRemaining = 0f;
        _rootMotionBurstTimeRemaining = 0f;
        _lastObservedRootMotionDelta = RootMotionDelta.Identity;

        switch (_demoMode)
        {
            case DemoMode.LinearBlendTree:
                _blendParameter1D = 0f;
                _linearBlendTree.Parameter = _blendParameter1D;
                _skinnedMeshComponent.PlayAnimationGraph(_linearBlendTree);
                break;

            case DemoMode.BlendSpace1D:
                _blendParameter1D = 0f;
                _blendSpace1D.Parameter = _blendParameter1D;
                _skinnedMeshComponent.PlayAnimationGraph(_blendSpace1D);
                break;

            case DemoMode.BlendSpace2D:
                _blendParameter2D = _configuredBlendSpace2DParameter ?? Vector2.Zero;
                _blendSpace2D.Parameter = _blendParameter2D;
                _skinnedMeshComponent.PlayAnimationGraph(_blendSpace2D);
                break;

            case DemoMode.CrossFade:
                _skinnedMeshComponent.PlayAnimation(0);
                break;

            case DemoMode.AdvancedCrossFade:
                _skinnedMeshComponent.PlayAnimation(0);
                break;

            case DemoMode.LayeredUpperBody:
                _blendParameter1D = 1f;
                _blendSpace1D.Parameter = _blendParameter1D;
                _skinnedMeshComponent.PlayAnimationGraph(_blendSpace1D);
                break;

            case DemoMode.AdditiveRootMotion:
                _blendParameter1D = 1f;
                _blendSpace1D.Parameter = _blendParameter1D;
                _skinnedMeshComponent.PlayAnimationGraph(_blendSpace1D);
                _skinnedMeshComponent.SetAnimationLayer(AdditiveLayerIndex, _additiveBreathingClip!, _upperBodyMask, _additiveLayerWeight, AnimationLayerBlendMode.Additive, loop: true);
                _skinnedMeshComponent.RootMotionMode = _applyRootMotionToEntity ? RootMotionMode.Apply : RootMotionMode.Observe;
                break;
        }

        SetTransientMessage($"Showcase: {GetModeLabel(_demoMode)}");
    }

    private void UpdateLinearBlendTree(KeyboardState keyboardState, float elapsedSeconds)
    {
        _blendParameter1D = MoveTowards(_blendParameter1D, GetLocomotionBlendTarget(keyboardState), elapsedSeconds * OneDimensionalBlendSpeed);
        _linearBlendTree!.Parameter = _blendParameter1D;
    }

    private void UpdateOneDimensionalBlend(KeyboardState keyboardState, float elapsedSeconds)
    {
        _blendParameter1D = MoveTowards(_blendParameter1D, GetLocomotionBlendTarget(keyboardState), elapsedSeconds * OneDimensionalBlendSpeed);
        _blendSpace1D!.Parameter = _blendParameter1D;
    }

    private void UpdateTwoDimensionalBlend(KeyboardState keyboardState, float elapsedSeconds)
    {
        var target = Vector2.Zero;
        var hasDirectionalInput = false;
        if (keyboardState.IsKeyDown(Keys.A) || keyboardState.IsKeyDown(Keys.Left))
        {
            target.X -= 1f;
            hasDirectionalInput = true;
        }

        if (keyboardState.IsKeyDown(Keys.D) || keyboardState.IsKeyDown(Keys.Right))
        {
            target.X += 1f;
            hasDirectionalInput = true;
        }

        if (keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.Up))
        {
            target.Y = 1f;
            hasDirectionalInput = true;
        }

        if (keyboardState.IsKeyDown(Keys.S) || keyboardState.IsKeyDown(Keys.Down))
        {
            target.Y = 0f;
            hasDirectionalInput = true;
        }

        if (!hasDirectionalInput && _configuredBlendSpace2DParameter.HasValue)
        {
            target = _configuredBlendSpace2DParameter.Value;
        }

        _blendParameter2D = MoveTowards(_blendParameter2D, target, elapsedSeconds * TwoDimensionalBlendSpeed);
        _blendSpace2D!.Parameter = _blendParameter2D;
    }

    private void UpdateCrossFadeMode(KeyboardState keyboardState)
    {
        if (_skinnedMeshComponent == null)
        {
            return;
        }

        if (IsNewKeyPress(keyboardState, Keys.D1) || IsNewKeyPress(keyboardState, Keys.NumPad1))
        {
            _skinnedMeshComponent.CrossFadeToAnimation(0, CrossFadeDurationSeconds);
        }
        else if (IsNewKeyPress(keyboardState, Keys.D2) || IsNewKeyPress(keyboardState, Keys.NumPad2))
        {
            _skinnedMeshComponent.CrossFadeToAnimation(1, CrossFadeDurationSeconds);
        }
        else if (IsNewKeyPress(keyboardState, Keys.D3) || IsNewKeyPress(keyboardState, Keys.NumPad3))
        {
            _skinnedMeshComponent.CrossFadeToAnimation(2, CrossFadeDurationSeconds);
        }
    }

    private void UpdateAdvancedCrossFadeMode(KeyboardState keyboardState)
    {
        if (_riggedModel?.AnimationController == null)
        {
            return;
        }

        if (IsNewKeyPress(keyboardState, Keys.E))
        {
            CycleAdvancedCrossFadeEasingMode();
            SetTransientMessage($"Advanced cross-fade easing: {GetTransitionEasingLabel(_advancedCrossFadeEasingMode)}");
        }

        if (IsNewKeyPress(keyboardState, Keys.R))
        {
            _advancedCrossFadePreserveRootVelocity = !_advancedCrossFadePreserveRootVelocity;
            SetTransientMessage(_advancedCrossFadePreserveRootVelocity
                ? "Advanced cross-fade now preserves root velocity"
                : "Advanced cross-fade now uses pose-only blending");
        }

        if (IsNewKeyPress(keyboardState, Keys.D1) || IsNewKeyPress(keyboardState, Keys.NumPad1))
        {
            TriggerAdvancedCrossFade(0);
        }
        else if (IsNewKeyPress(keyboardState, Keys.D2) || IsNewKeyPress(keyboardState, Keys.NumPad2))
        {
            TriggerAdvancedCrossFade(1);
        }
        else if (IsNewKeyPress(keyboardState, Keys.D3) || IsNewKeyPress(keyboardState, Keys.NumPad3))
        {
            TriggerAdvancedCrossFade(2);
        }
    }

    private void UpdateLayeredUpperBodyMode(KeyboardState keyboardState, float elapsedSeconds)
    {
        UpdateOneDimensionalBlend(keyboardState, elapsedSeconds);

        if (keyboardState.IsKeyDown(Keys.Q))
        {
            _upperBodyLayerWeight = Math.Clamp(_upperBodyLayerWeight - elapsedSeconds, 0f, 1f);
            _skinnedMeshComponent!.SetAnimationLayerWeight(UpperBodyLayerIndex, _upperBodyLayerWeight);
        }
        else if (keyboardState.IsKeyDown(Keys.E))
        {
            _upperBodyLayerWeight = Math.Clamp(_upperBodyLayerWeight + elapsedSeconds, 0f, 1f);
            _skinnedMeshComponent!.SetAnimationLayerWeight(UpperBodyLayerIndex, _upperBodyLayerWeight);
        }

        if (IsNewKeyPress(keyboardState, Keys.Space))
        {
            _skinnedMeshComponent!.SetAnimationLayer(
                UpperBodyLayerIndex,
                _upperBodyActionClip!,
                _upperBodyMask,
                _upperBodyLayerWeight,
                AnimationLayerBlendMode.Override,
                loop: false);
            _upperBodyActionTimeRemaining = _upperBodyActionClip!.DurationSeconds;
            SetTransientMessage("Upper-body override triggered");
        }
    }

    private void UpdateAdditiveRootMotionMode(KeyboardState keyboardState, float elapsedSeconds)
    {
        UpdateOneDimensionalBlend(keyboardState, elapsedSeconds);

        if (IsNewKeyPress(keyboardState, Keys.R))
        {
            _applyRootMotionToEntity = !_applyRootMotionToEntity;
            _skinnedMeshComponent!.RootMotionMode = _applyRootMotionToEntity ? RootMotionMode.Apply : RootMotionMode.Observe;
            SetTransientMessage(_applyRootMotionToEntity ? "Root motion now applies to the entity" : "Root motion now stays in observe-only mode");
        }

        if (IsNewKeyPress(keyboardState, Keys.Space))
        {
            _skinnedMeshComponent!.SetAnimationLayer(
                RootMotionLayerIndex,
                _rootMotionBurstClip!,
                _rootMotionMask,
                1f,
                AnimationLayerBlendMode.Override,
                loop: false);
            _rootMotionBurstTimeRemaining = _rootMotionBurstClip!.DurationSeconds;
            SetTransientMessage("Root-motion burst triggered");
        }
    }

    private void UpdateTransientTimers(float elapsedSeconds)
    {
        if (_latestEventTimeRemaining > 0f)
        {
            _latestEventTimeRemaining = Math.Max(0f, _latestEventTimeRemaining - elapsedSeconds);
        }

        if (_upperBodyActionTimeRemaining > 0f)
        {
            _upperBodyActionTimeRemaining = Math.Max(0f, _upperBodyActionTimeRemaining - elapsedSeconds);
            if (_upperBodyActionTimeRemaining <= 0f)
            {
                _skinnedMeshComponent?.ClearAnimationLayer(UpperBodyLayerIndex);
            }
        }

        if (_rootMotionBurstTimeRemaining > 0f)
        {
            _rootMotionBurstTimeRemaining = Math.Max(0f, _rootMotionBurstTimeRemaining - elapsedSeconds);
            if (_rootMotionBurstTimeRemaining <= 0f)
            {
                _skinnedMeshComponent?.ClearAnimationLayer(RootMotionLayerIndex);
            }
        }
    }

    private void ConsumeRootMotionForShowcase()
    {
        if (_skinnedMeshComponent == null)
        {
            return;
        }

        var rootMotionDelta = _skinnedMeshComponent.ConsumeRootMotionDelta();
        _lastObservedRootMotionDelta = rootMotionDelta;
        if (!HasMeaningfulRootMotion(rootMotionDelta))
        {
            return;
        }

        if (_applyRootMotionToEntity)
        {
            var orientation = _skinnedMeshComponent.LocalOrientation;
            var translatedDelta = Vector3.Transform(rootMotionDelta.Translation * CharacterScale, orientation);
            _skinnedMeshComponent.LocalPosition += translatedDelta;
            _skinnedMeshComponent.LocalOrientation = Quaternion.Normalize(orientation * rootMotionDelta.Rotation);
        }

        AppendTrailPoint(_skinnedMeshComponent.Position);
    }

    private void DrawRootMotionTrail()
    {
        if (_game == null || _skinnedMeshComponent == null)
        {
            return;
        }

        var lineRenderer = _game.Line3dRendererComponent;
        for (var pointIndex = 1; pointIndex < _rootMotionTrailPoints.Count; pointIndex++)
        {
            lineRenderer.AddLine(_rootMotionTrailPoints[pointIndex - 1], _rootMotionTrailPoints[pointIndex], RootMotionTrailColor);
        }

        if (!HasMeaningfulRootMotion(_lastObservedRootMotionDelta))
        {
            return;
        }

        var start = _skinnedMeshComponent.Position + Vector3.Up * 0.1f;
        var deltaVector = Vector3.Transform(_lastObservedRootMotionDelta.Translation * CharacterScale, _skinnedMeshComponent.LocalOrientation);
        var end = start + deltaVector;
        lineRenderer.AddLine(start, end, RootMotionVectorColor);
    }

    private void ResetCharacterTransform()
    {
        if (_skinnedMeshComponent == null)
        {
            return;
        }

        // Identity, not a half turn: the model faces +Z, which is where the camera stands. Root motion
        // accumulates on top of this orientation, so the reset has to clear it explicitly.
        _skinnedMeshComponent.LocalPosition = Vector3.Zero;
        _skinnedMeshComponent.LocalOrientation = Quaternion.Identity;
        _skinnedMeshComponent.LocalScale = new Vector3(CharacterScale);
        _rootMotionTrailPoints.Clear();
        AppendTrailPoint(_skinnedMeshComponent.Position, force: true);
        _lastObservedRootMotionDelta = RootMotionDelta.Identity;
        SetTransientMessage("Character transform reset");
    }

    private void CycleDemoMode(int direction)
    {
        var modeCount = Enum.GetValues<DemoMode>().Length;
        var nextMode = ((int)_demoMode + direction + modeCount) % modeCount;
        _demoMode = (DemoMode)nextMode;
        ApplyDemoMode();
    }

    private void AppendTrailPoint(Vector3 position, bool force = false)
    {
        if (!force && _rootMotionTrailPoints.Count > 0)
        {
            var previousPoint = _rootMotionTrailPoints[_rootMotionTrailPoints.Count - 1];
            if (Vector3.DistanceSquared(previousPoint, position) < RootMotionTrailSpacing * RootMotionTrailSpacing)
            {
                return;
            }
        }

        _rootMotionTrailPoints.Add(position + Vector3.Up * 0.05f);
        if (_rootMotionTrailPoints.Count > 48)
        {
            _rootMotionTrailPoints.RemoveAt(0);
        }
    }

    private void OnAnimationEventTriggered(AnimationEventKeyframe eventKeyframe)
    {
        SetTransientMessage($"Animation event: {eventKeyframe.EventName}");
    }

    private void SetTransientMessage(string message)
    {
        _latestEventMessage = message;
        _latestEventTimeRemaining = 2.5f;
    }

    private static string GetModeLabel(DemoMode mode)
    {
        return mode switch
        {
            DemoMode.LinearBlendTree => "Linear blend tree",
            DemoMode.BlendSpace1D => "Blend space 1D",
            DemoMode.BlendSpace2D => "Blend space 2D",
            DemoMode.CrossFade => "Cross-fade playback",
            DemoMode.AdvancedCrossFade => "Advanced cross-fade playback",
            DemoMode.LayeredUpperBody => "Upper-body override layer",
            DemoMode.AdditiveRootMotion => "Additive + root motion",
            _ => mode.ToString(),
        };
    }

    private static DemoMode ResolveInitialDemoMode()
    {
        var requestedMode = Environment.GetEnvironmentVariable(BlendModeEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(requestedMode)
            && Enum.TryParse<DemoMode>(requestedMode, ignoreCase: true, out var demoMode))
        {
            return demoMode;
        }

        return DemoMode.BlendSpace1D;
    }

    private static Vector2? ResolveConfiguredBlendSpace2DParameter()
    {
        if (!TryParseConfiguredFloat(BlendSpace2DXEnvironmentVariable, out var x)
            || !TryParseConfiguredFloat(BlendSpace2DYEnvironmentVariable, out var y))
        {
            return null;
        }

        return new Vector2(Math.Clamp(x, -1f, 1f), Math.Clamp(y, -1f, 1f));
    }

    private static bool TryParseConfiguredFloat(string environmentVariable, out float value)
    {
        var rawValue = Environment.GetEnvironmentVariable(environmentVariable);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            value = 0f;
            return false;
        }

        return float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static float GetLocomotionBlendTarget(KeyboardState keyboardState)
    {
        if (keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.Up))
        {
            return keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift)
                ? 2f
                : 1f;
        }

        return 0f;
    }

    private void TriggerAdvancedCrossFade(int animationIndex)
    {
        if (_skinnedMeshComponent == null || _riggedModel == null || _riggedModel.AnimationClips.Count == 0)
        {
            return;
        }

        if (animationIndex < 0 || animationIndex >= _riggedModel.AnimationClips.Count)
        {
            return;
        }

        var clip = _riggedModel.AnimationClips[animationIndex];
        _skinnedMeshComponent.CrossFadeToAnimation(
            animationIndex,
            CrossFadeDurationSeconds,
            new AnimationCrossFadeSettings
            {
                EasingMode = _advancedCrossFadeEasingMode,
                PreserveRootTranslationVelocity = _advancedCrossFadePreserveRootVelocity,
            });
        SetTransientMessage($"Advanced cross-fade to {clip.Name}");
    }

    private void CycleAdvancedCrossFadeEasingMode()
    {
        var easingModes = Enum.GetValues<AnimationTransitionEasingMode>();
        var nextIndex = ((int)_advancedCrossFadeEasingMode + 1) % easingModes.Length;
        _advancedCrossFadeEasingMode = easingModes[nextIndex];
    }

    private static string GetTransitionEasingLabel(AnimationTransitionEasingMode easingMode)
    {
        return easingMode switch
        {
            AnimationTransitionEasingMode.SmoothStep => "Smooth step",
            AnimationTransitionEasingMode.EaseOutCubic => "Ease-out cubic",
            _ => "Linear",
        };
    }

    private static bool IsShiftPressed(KeyboardState keyboardState)
    {
        return keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
    }

    private bool IsNewKeyPress(KeyboardState keyboardState, Keys key)
    {
        return keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
    }

    private static bool HasMeaningfulRootMotion(RootMotionDelta rootMotionDelta)
    {
        return rootMotionDelta.Translation.LengthSquared() > 0.000001f
               || Quaternion.Dot(rootMotionDelta.Rotation, Quaternion.Identity) < 0.99999f;
    }

    private static BoneMask CreateUpperBodyMask(SkeletonDefinition skeleton)
    {
        var mask = new BoneMask(skeleton);
        var hasAnyUpperBodyJoint = false;
        SetRecursiveWeightIfPresent(mask, skeleton, ref hasAnyUpperBodyJoint, "spine3", "Chest", "neck", "L_Clav", "R_Clav");

        if (!hasAnyUpperBodyJoint)
        {
            for (var jointIndex = 0; jointIndex < skeleton.Count; jointIndex++)
            {
                mask.SetWeight(jointIndex, 1f);
            }
        }

        return mask;
    }

    private static BoneMask CreateRootOnlyMask(SkeletonDefinition skeleton)
    {
        var mask = new BoneMask(skeleton);
        mask.SetWeight(skeleton.RootIndex, 1f);
        return mask;
    }

    private static void SetRecursiveWeightIfPresent(BoneMask mask, SkeletonDefinition skeleton, ref bool hasAnyUpperBodyJoint, params string[] jointNames)
    {
        for (var candidateIndex = 0; candidateIndex < jointNames.Length; candidateIndex++)
        {
            if (TryFindJointIndex(skeleton, jointNames[candidateIndex], out var jointIndex))
            {
                mask.SetWeightRecursive(jointIndex, 1f);
                hasAnyUpperBodyJoint = true;
            }
        }
    }

    private static AnimationClip CreateUpperBodyActionClip(SkeletonDefinition skeleton)
    {
        var jointTracks = new List<JointAnimationTrack>();
        AddRotationTrack(jointTracks, skeleton, "spine3", UpperBodyActionDurationSeconds,
            (0f, Quaternion.Identity),
            (0.35f, Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(-10f), MathHelper.ToRadians(-4f), MathHelper.ToRadians(8f))),
            (UpperBodyActionDurationSeconds, Quaternion.Identity));
        AddRotationTrack(jointTracks, skeleton, "Chest", UpperBodyActionDurationSeconds,
            (0f, Quaternion.Identity),
            (0.35f, Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(-8f), MathHelper.ToRadians(-6f), MathHelper.ToRadians(10f))),
            (UpperBodyActionDurationSeconds, Quaternion.Identity));
        AddRotationTrack(jointTracks, skeleton, "R_Clav", UpperBodyActionDurationSeconds,
            (0f, Quaternion.Identity),
            (0.35f, Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(-10f), MathHelper.ToRadians(-15f), MathHelper.ToRadians(26f))),
            (UpperBodyActionDurationSeconds, Quaternion.Identity));
        AddRotationTrack(jointTracks, skeleton, "R_Bicep", UpperBodyActionDurationSeconds,
            (0f, Quaternion.Identity),
            (0.35f, Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(-12f), MathHelper.ToRadians(-65f), MathHelper.ToRadians(18f))),
            (UpperBodyActionDurationSeconds, Quaternion.Identity));
        AddRotationTrack(jointTracks, skeleton, "R_Wrist1", UpperBodyActionDurationSeconds,
            (0f, Quaternion.Identity),
            (0.35f, Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(8f), MathHelper.ToRadians(-25f), 0f)),
            (UpperBodyActionDurationSeconds, Quaternion.Identity));
        AddRotationTrack(jointTracks, skeleton, "L_Clav", UpperBodyActionDurationSeconds,
            (0f, Quaternion.Identity),
            (0.35f, Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(5f), MathHelper.ToRadians(5f), MathHelper.ToRadians(-8f))),
            (UpperBodyActionDurationSeconds, Quaternion.Identity));
        AddRotationTrack(jointTracks, skeleton, "L_Bicep", UpperBodyActionDurationSeconds,
            (0f, Quaternion.Identity),
            (0.35f, Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(4f), MathHelper.ToRadians(10f), MathHelper.ToRadians(-12f))),
            (UpperBodyActionDurationSeconds, Quaternion.Identity));
        AddRotationTrack(jointTracks, skeleton, "neck", UpperBodyActionDurationSeconds,
            (0f, Quaternion.Identity),
            (0.35f, Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(-6f), MathHelper.ToRadians(-2f), 0f)),
            (UpperBodyActionDurationSeconds, Quaternion.Identity));

        var eventTrack = new AnimationEventTrack(
            new[]
            {
                new AnimationEventKeyframe(0.35f, "UpperBody/Peak"),
            });

        return new AnimationClip("UpperBodyAction", skeleton, jointTracks, UpperBodyActionDurationSeconds, eventTrack);
    }

    private static AnimationClip CreateAdditiveBreathingClip(SkeletonDefinition skeleton)
    {
        var jointTracks = new List<JointAnimationTrack>();
        AddRotationTrack(jointTracks, skeleton, "spine2", AdditiveBreathingDurationSeconds,
            (0f, Quaternion.Identity),
            (0.9f, Quaternion.CreateFromYawPitchRoll(0f, MathHelper.ToRadians(4f), MathHelper.ToRadians(1f))),
            (AdditiveBreathingDurationSeconds, Quaternion.Identity));
        AddRotationTrack(jointTracks, skeleton, "spine3", AdditiveBreathingDurationSeconds,
            (0f, Quaternion.Identity),
            (0.9f, Quaternion.CreateFromYawPitchRoll(0f, MathHelper.ToRadians(5f), MathHelper.ToRadians(-1f))),
            (AdditiveBreathingDurationSeconds, Quaternion.Identity));
        AddRotationTrack(jointTracks, skeleton, "neck", AdditiveBreathingDurationSeconds,
            (0f, Quaternion.Identity),
            (0.9f, Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(2f), MathHelper.ToRadians(2f), 0f)),
            (AdditiveBreathingDurationSeconds, Quaternion.Identity));
        AddTranslationTrack(jointTracks, skeleton, "Chest", AdditiveBreathingDurationSeconds,
            (0f, Vector3.Zero),
            (0.9f, new Vector3(0f, 0.35f, 0.15f)),
            (AdditiveBreathingDurationSeconds, Vector3.Zero));

        return new AnimationClip("BreathingAdditive", skeleton, jointTracks, AdditiveBreathingDurationSeconds);
    }

    private static AnimationClip CreateRootMotionBurstClip(SkeletonDefinition skeleton)
    {
        var jointTracks = new List<JointAnimationTrack>();
        var rootJointIndex = skeleton.RootIndex;
        var bindTransform = skeleton.GetBindLocalTransform(rootJointIndex);
        var translationTrack = new Vector3AnimationTrack(
            new[]
            {
                new AnimationKeyframe<Vector3>(0f, bindTransform.Translation),
                new AnimationKeyframe<Vector3>(0.35f, bindTransform.Translation + new Vector3(0f, 0f, 10f)),
                new AnimationKeyframe<Vector3>(RootMotionBurstDurationSeconds, bindTransform.Translation + new Vector3(0f, 0f, 16f)),
            });
        var rotationTrack = new QuaternionAnimationTrack(
            new[]
            {
                new AnimationKeyframe<Quaternion>(0f, bindTransform.Rotation),
                new AnimationKeyframe<Quaternion>(0.35f, Quaternion.Normalize(bindTransform.Rotation * Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(6f)))),
                new AnimationKeyframe<Quaternion>(RootMotionBurstDurationSeconds, Quaternion.Normalize(bindTransform.Rotation * Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(10f)))),
            });
        jointTracks.Add(new JointAnimationTrack(rootJointIndex, translationTrack, rotationTrack, null));

        return new AnimationClip("RootMotionBurst", skeleton, jointTracks, RootMotionBurstDurationSeconds);
    }

    private static AnimationClip CreateDirectionalStrafeClip(SkeletonDefinition skeleton, float direction, string clipName)
    {
        var jointTracks = new List<JointAnimationTrack>();

        if (TryFindJointIndex(skeleton, "Root_Pelvis", out var pelvisJointIndex))
        {
            var pelvisBindTransform = skeleton.GetBindLocalTransform(pelvisJointIndex);
            var pelvisTranslationTrack = new Vector3AnimationTrack(
                new[]
                {
                    new AnimationKeyframe<Vector3>(0f, pelvisBindTransform.Translation),
                    new AnimationKeyframe<Vector3>(0.25f, pelvisBindTransform.Translation + new Vector3(direction * 0.35f, 0.08f, 0f)),
                    new AnimationKeyframe<Vector3>(0.5f, pelvisBindTransform.Translation + new Vector3(direction * 0.05f, 0f, 0f)),
                    new AnimationKeyframe<Vector3>(0.75f, pelvisBindTransform.Translation + new Vector3(direction * 0.2f, 0.05f, 0f)),
                    new AnimationKeyframe<Vector3>(DirectionalStrafeDurationSeconds, pelvisBindTransform.Translation),
                });
            var pelvisRotationTrack = new QuaternionAnimationTrack(
                new[]
                {
                    new AnimationKeyframe<Quaternion>(0f, pelvisBindTransform.Rotation),
                    new AnimationKeyframe<Quaternion>(0.25f, Quaternion.Normalize(pelvisBindTransform.Rotation * Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(-direction * 6f), 0f, MathHelper.ToRadians(-direction * 4f)))),
                    new AnimationKeyframe<Quaternion>(0.5f, pelvisBindTransform.Rotation),
                    new AnimationKeyframe<Quaternion>(0.75f, Quaternion.Normalize(pelvisBindTransform.Rotation * Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(direction * 4f), 0f, MathHelper.ToRadians(direction * 2f)))),
                    new AnimationKeyframe<Quaternion>(DirectionalStrafeDurationSeconds, pelvisBindTransform.Rotation),
                });
            jointTracks.Add(new JointAnimationTrack(pelvisJointIndex, pelvisTranslationTrack, pelvisRotationTrack, null));
        }

        AddRotationTrack(jointTracks, skeleton, "spine3", DirectionalStrafeDurationSeconds,
            (0f, Quaternion.Identity),
            (0.25f, Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(direction * 6f), MathHelper.ToRadians(-2f), MathHelper.ToRadians(direction * 3f))),
            (0.5f, Quaternion.Identity),
            (0.75f, Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(-direction * 4f), MathHelper.ToRadians(-1f), MathHelper.ToRadians(-direction * 2f))),
            (DirectionalStrafeDurationSeconds, Quaternion.Identity));
        AddRotationTrack(jointTracks, skeleton, "L_Thigh", DirectionalStrafeDurationSeconds,
            (0f, Quaternion.Identity),
            (0.25f, Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(-direction * 8f), MathHelper.ToRadians(14f), MathHelper.ToRadians(-direction * 18f))),
            (0.5f, Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(-direction * 2f), MathHelper.ToRadians(-4f), MathHelper.ToRadians(-direction * 6f))),
            (0.75f, Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(direction * 10f), MathHelper.ToRadians(-6f), MathHelper.ToRadians(direction * 10f))),
            (DirectionalStrafeDurationSeconds, Quaternion.Identity));
        AddRotationTrack(jointTracks, skeleton, "R_Thigh", DirectionalStrafeDurationSeconds,
            (0f, Quaternion.Identity),
            (0.25f, Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(direction * 6f), MathHelper.ToRadians(-6f), MathHelper.ToRadians(direction * 8f))),
            (0.5f, Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(direction * 2f), 0f, MathHelper.ToRadians(direction * 4f))),
            (0.75f, Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(-direction * 8f), MathHelper.ToRadians(14f), MathHelper.ToRadians(-direction * 18f))),
            (DirectionalStrafeDurationSeconds, Quaternion.Identity));
        AddRotationTrack(jointTracks, skeleton, "L_Calf", DirectionalStrafeDurationSeconds,
            (0f, Quaternion.Identity),
            (0.25f, Quaternion.CreateFromYawPitchRoll(0f, MathHelper.ToRadians(-18f), MathHelper.ToRadians(direction * 5f))),
            (0.5f, Quaternion.Identity),
            (0.75f, Quaternion.CreateFromYawPitchRoll(0f, MathHelper.ToRadians(-8f), MathHelper.ToRadians(-direction * 3f))),
            (DirectionalStrafeDurationSeconds, Quaternion.Identity));
        AddRotationTrack(jointTracks, skeleton, "R_Calf", DirectionalStrafeDurationSeconds,
            (0f, Quaternion.Identity),
            (0.25f, Quaternion.CreateFromYawPitchRoll(0f, MathHelper.ToRadians(-8f), MathHelper.ToRadians(-direction * 3f))),
            (0.5f, Quaternion.Identity),
            (0.75f, Quaternion.CreateFromYawPitchRoll(0f, MathHelper.ToRadians(-18f), MathHelper.ToRadians(direction * 5f))),
            (DirectionalStrafeDurationSeconds, Quaternion.Identity));
        AddRotationTrack(jointTracks, skeleton, "L_Foot", DirectionalStrafeDurationSeconds,
            (0f, Quaternion.Identity),
            (0.25f, Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(direction * 4f), MathHelper.ToRadians(5f), MathHelper.ToRadians(direction * 10f))),
            (0.5f, Quaternion.Identity),
            (0.75f, Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(-direction * 3f), MathHelper.ToRadians(-3f), MathHelper.ToRadians(-direction * 6f))),
            (DirectionalStrafeDurationSeconds, Quaternion.Identity));
        AddRotationTrack(jointTracks, skeleton, "R_Foot", DirectionalStrafeDurationSeconds,
            (0f, Quaternion.Identity),
            (0.25f, Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(-direction * 3f), MathHelper.ToRadians(-3f), MathHelper.ToRadians(-direction * 6f))),
            (0.5f, Quaternion.Identity),
            (0.75f, Quaternion.CreateFromYawPitchRoll(MathHelper.ToRadians(direction * 4f), MathHelper.ToRadians(5f), MathHelper.ToRadians(direction * 10f))),
            (DirectionalStrafeDurationSeconds, Quaternion.Identity));

        return new AnimationClip(clipName, skeleton, jointTracks, DirectionalStrafeDurationSeconds);
    }

    private static void AddRotationTrack(
        List<JointAnimationTrack> jointTracks,
        SkeletonDefinition skeleton,
        string jointName,
        float durationSeconds,
        params (float TimeSeconds, Quaternion DeltaRotation)[] keyframes)
    {
        if (!TryFindJointIndex(skeleton, jointName, out var jointIndex))
        {
            return;
        }

        var bindRotation = skeleton.GetBindLocalTransform(jointIndex).Rotation;
        var rotationKeys = new AnimationKeyframe<Quaternion>[keyframes.Length];
        for (var keyIndex = 0; keyIndex < keyframes.Length; keyIndex++)
        {
            var keyframe = keyframes[keyIndex];
            rotationKeys[keyIndex] = new AnimationKeyframe<Quaternion>(
                keyframe.TimeSeconds,
                Quaternion.Normalize(bindRotation * keyframe.DeltaRotation));
        }

        jointTracks.Add(new JointAnimationTrack(jointIndex, null, new QuaternionAnimationTrack(rotationKeys), null));
    }

    private static void AddTranslationTrack(
        List<JointAnimationTrack> jointTracks,
        SkeletonDefinition skeleton,
        string jointName,
        float durationSeconds,
        params (float TimeSeconds, Vector3 DeltaTranslation)[] keyframes)
    {
        if (!TryFindJointIndex(skeleton, jointName, out var jointIndex))
        {
            return;
        }

        var bindTranslation = skeleton.GetBindLocalTransform(jointIndex).Translation;
        var translationKeys = new AnimationKeyframe<Vector3>[keyframes.Length];
        for (var keyIndex = 0; keyIndex < keyframes.Length; keyIndex++)
        {
            var keyframe = keyframes[keyIndex];
            translationKeys[keyIndex] = new AnimationKeyframe<Vector3>(keyframe.TimeSeconds, bindTranslation + keyframe.DeltaTranslation);
        }

        jointTracks.Add(new JointAnimationTrack(jointIndex, new Vector3AnimationTrack(translationKeys), null, null));
    }

    private static bool TryFindJointIndex(SkeletonDefinition skeleton, string jointName, out int jointIndex)
    {
        jointIndex = -1;
        for (var index = 0; index < skeleton.Count; index++)
        {
            if (string.Equals(skeleton.GetJoint(index).Name, jointName, StringComparison.OrdinalIgnoreCase))
            {
                jointIndex = index;
                return true;
            }
        }

        return false;
    }

    private static void ValidateSkeletonCompatibility(SkeletonDefinition expectedSkeleton, SkeletonDefinition candidateSkeleton, string clipLabel)
    {
        if (candidateSkeleton == null)
        {
            throw new InvalidOperationException($"The {clipLabel} rigged model did not expose a runtime skeleton.");
        }

        if (expectedSkeleton.Count != candidateSkeleton.Count)
        {
            throw new InvalidOperationException($"The {clipLabel} animation skeleton does not match the displayed mesh skeleton.");
        }

        for (var jointIndex = 0; jointIndex < expectedSkeleton.Count; jointIndex++)
        {
            if (!string.Equals(expectedSkeleton.GetJoint(jointIndex).Name, candidateSkeleton.GetJoint(jointIndex).Name, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The {clipLabel} animation skeleton diverges at joint index {jointIndex}.");
            }
        }
    }

    private static AnimationClip RebindClip(AnimationClip sourceClip, SkeletonDefinition targetSkeleton, string clipName)
    {
        var jointTracks = new List<JointAnimationTrack>(targetSkeleton.Count);
        for (var jointIndex = 0; jointIndex < targetSkeleton.Count; jointIndex++)
        {
            if (sourceClip.TryGetJointTrack(jointIndex, out var jointTrack) && jointTrack != null)
            {
                jointTracks.Add(jointTrack);
            }
        }

        return new AnimationClip(clipName, targetSkeleton, jointTracks, sourceClip.DurationSeconds, sourceClip.EventTrack);
    }

    private static float MoveTowards(float current, float target, float maxDelta)
    {
        var delta = target - current;
        if (Math.Abs(delta) <= maxDelta)
        {
            return target;
        }

        return current + Math.Sign(delta) * maxDelta;
    }

    private static Vector2 MoveTowards(Vector2 current, Vector2 target, float maxDelta)
    {
        var delta = target - current;
        var distance = delta.Length();
        if (distance <= maxDelta || distance <= float.Epsilon)
        {
            return target;
        }

        return current + (delta / distance) * maxDelta;
    }
}