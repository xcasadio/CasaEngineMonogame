using CasaEngine.Core.Logging;
﻿using System;
using System.Collections.Generic;
using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Animations;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Materials.Runtime;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Environment;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Animated skinned characters under directional shadows. One <see cref="SkinnedMesh"/> is loaded once and shared by six
/// <see cref="SkinnedMeshComponent"/>s, laid out in three columns that differ only in their shadow flags (the same
/// layout as the static shadow reference scene).
/// <para/>
/// Types to look at: <see cref="SkinnedMeshComponent"/> (plays and cross-fades animations, has per-component
/// <c>CastShadows</c>, <c>ReceiveShadows</c>, <c>SkinningModeSelection</c> and a <c>Material</c> override),
/// <see cref="SkinningModeSelection"/> and <see cref="AnimationCrossFadeSettings"/> with
/// <see cref="AnimationTransitionMode"/>.
/// </summary>
public class SkinnedMeshDemo : Demo
{
    private CasaEngineGame? _game;
    private readonly List<SkinnedMeshComponent> _skinnedMeshComponents = new();
    // Where the two characters of a column stand, from the centre of the column. The receiver is behind the caster and
    // to its right, on the path of the key light, so that the shadow of the caster falls on it.
    private static readonly Vector3 CasterOffset = new(-1.6f, 0f, 3.0f);
    private static readonly Vector3 ReceiverOffset = new(1.0f, 0f, -0.2f);
    private const float GroundWidth = 8.0f;
    private const float GroundDepth = 14.0f;
    private const float GroundCentreZ = 0.5f;

    private float _animationSwitchTimer;
    private int _nextAnimationIndex = 1;
    private bool _useInertializeTransition;

    public override string Title => "Skinned mesh demo";
    public override string Description =>
        "Animated skinned characters, in three pairs: a large one in front (linear blend skinning) and a small one " +
        "behind (dual quaternion skinning). Every 2.5 seconds all of them change animation, alternating a " +
        "cross-fade and an inertialized transition. The key light casts shadows, and each pair sets CastShadows and " +
        "ReceiveShadows differently: compare the ground and the small character of the three pairs.";

    public override IReadOnlyList<string> Highlights { get; } = new[]
    {
        "All six characters change clip together, with a smooth transition instead of a pop.",
        "The left character is tinted orange by a per-component material override.",
        "Left: the large character casts a shadow on the ground and on the small one, and the shadow follows the animation.",
        "Centre: ReceiveShadows is off on the ground and on the small character, so no shadow shows on them.",
        "Right: CastShadows is off on the large character, so it has no shadow at all.",
    };

    public override string DocumentationPath => "docs/engine/light-component.md";

    // The base Demo creates an arc-ball camera and its InitializeCamera casts to that type, so a demo that wants another
    // camera overrides both methods together. A fixed look-at camera keeps the view the same from run to run.
    public override CameraComponent CreateCamera(CasaEngineGame game)
    {
        var entity = new Entity { Name = "SkinnedShadowValidationCamera" };
        var camera = new CameraLookAtComponent();
        entity.RootComponent = camera;
        entity.Initialize();
        game.GameManager.CurrentWorld.AddEntity(entity);
        return camera;
    }

    // Called by the demos app right after Initialize. It replaces the default light rig: the scene is lit by the two
    // lights below. No environment cubemap, a plain background and a fixed ambient, so only the lights shape the
    // shading.
    public override void ConfigureSceneLighting(CasaEngine.Framework.Scene.World.World world)
    {
        var environmentSettings = world.EnvironmentSettings;
        environmentSettings.Type = EnvironmentType.None;
        environmentSettings.BackgroundMode = EnvironmentBackgroundMode.SolidColor;
        environmentSettings.BackgroundColor = new Color(28, 34, 42);
        environmentSettings.BackgroundCubemap = null;
        environmentSettings.SpecularEnvironmentCubemap = null;
        environmentSettings.AmbientColor = new Vector3(0.24f, 0.26f, 0.3f);
        environmentSettings.AmbientIntensity = 1.2f;
        environmentSettings.SpecularIntensity = 0.2f;

        // Shadows are off by default in the engine: the world turns them on. Without this line the CastShadows and
        // ReceiveShadows flags below have no effect. Resolution is the size of the shadow map; the two biases offset the
        // depth comparison so that a lit surface does not shadow itself; MaxDistance is how far from the camera shadows
        // are drawn (the camera stands about 40 units from the characters).
        environmentSettings.Shadows.Enabled = true;
        environmentSettings.Shadows.Resolution = 2048;
        environmentSettings.Shadows.DepthBias = 0.001f;
        environmentSettings.Shadows.NormalBias = 0.002f;
        environmentSettings.Shadows.MaxDistance = 64.0f;
        environmentSettings.MarkDirty();

        // The key light is the one that casts shadows; the fill light from another direction does not and only keeps the
        // unlit sides from going black. The direction of a directional light is its component's forward vector, so the
        // orientation is built from the direction wanted (see CreateOrientationFromForward).
        var keyLight = new LightComponent
        {
            Type = LightType.Directional,
            LocalOrientation = CreateOrientationFromForward(new Vector3(0.42f, -0.76f, -0.5f)),
            Color = new Color(255, 242, 214),
            SpecularColor = Color.White,
            Intensity = 1.85f,
            CastShadows = true,
        };

        var keyLightEntity = new Entity { Name = "SkinnedShadowDirectionalLight" };
        keyLightEntity.RootComponent = keyLight;
        world.AddEntity(keyLightEntity);

        var fillLight = new LightComponent
        {
            Type = LightType.Directional,
            LocalOrientation = CreateOrientationFromForward(new Vector3(0.24f, -0.38f, -0.9f)),
            Color = new Color(122, 148, 188),
            SpecularColor = new Color(92, 110, 136),
            Intensity = 0.45f,
            CastShadows = false,
        };

        var fillLightEntity = new Entity { Name = "SkinnedShadowFillLight" };
        fillLightEntity.RootComponent = fillLight;
        world.AddEntity(fillLightEntity);
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        camera.SetPositionAndTarget(new Vector3(0f, 14.0f, 31.0f), new Vector3(0f, 1.5f, 0f));
    }

    public override void Initialize(CasaEngineGame game)
    {
        // Reset the demo's state in case the same demo object is entered again.
        _game = game;
        var world = game.GameManager.CurrentWorld;
        _skinnedMeshComponents.Clear();
        _animationSwitchTimer = 0f;
        _nextAnimationIndex = 1;
        _useInertializeTransition = false;

        // The character comes straight from a model file (relative to the project root) rather than from the asset
        // catalogue. Initialize then resolves the rigged model and its animation clips through the content manager; it is
        // done once, and the six components below share the result.
        var skinnedMesh = game.AssetContentManager.LoadFromFile<SkinnedMesh>("SkinnedMesh\\kid_idle.model");
        skinnedMesh.Initialize(game.AssetContentManager);

        // Exercises the per-component material override path (SkinnedMeshComponent.Material):
        // a tinted material replaces the renderer's default skinned material for this one caster.
        var tintedCasterMaterial = new LitDiffuseMaterial
        {
            DiffuseColor = new Color(255, 150, 60),
            AmbientColor = Vector3.One,
            SpecularColor = new Vector3(0.3f, 0.3f, 0.3f),
            SpecularPower = 16.0f,
        };

        // Three columns 12 units apart on X, far enough for a shadow not to reach the next column, each with a caster and
        // a receiver: left is the reference (both flags on, the caster tinted), centre turns receiving off on the receiver, right turns casting off on the caster.
        CreateShadowValidationColumn(game, world, skinnedMesh, "ReceiveShadow", -12.0f, casterCastShadows: true, receiverReceiveShadows: true, casterMaterialOverride: tintedCasterMaterial);
        CreateShadowValidationColumn(game, world, skinnedMesh, "NoReceiveShadow", 0.0f, casterCastShadows: true, receiverReceiveShadows: false);
        CreateShadowValidationColumn(game, world, skinnedMesh, "NoCastShadow", 12.0f, casterCastShadows: false, receiverReceiveShadows: true);
    }

    // Every 2.5 seconds all the characters cross-fade to the next animation clip of the model, cycling through its
    // clips. Nothing happens when the model has fewer than two clips. All components share one mesh, so the first one
    // stands for the clip count of all.
    public override void Update(GameTime gameTime)
    {
        if (_skinnedMeshComponents.Count == 0)
        {
            return;
        }

        if (_skinnedMeshComponents[0].AnimationClips.Count < 2)
        {
            return;
        }

        _animationSwitchTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_animationSwitchTimer < 2.5f)
        {
            return;
        }

        _animationSwitchTimer = 0f;
        if (_nextAnimationIndex >= _skinnedMeshComponents[0].AnimationClips.Count)
        {
            _nextAnimationIndex = 0;
        }

        // Alternates transition modes every cycle so both the smooth pose cross-fade and the
        // pop-free inertialization ("offset decay") transition are exercised in this demo.
        _useInertializeTransition = !_useInertializeTransition;
        // The 0.35 passed below is the transition duration in seconds (the durationSeconds parameter).
        var transitionSettings = new AnimationCrossFadeSettings
        {
            TransitionMode = _useInertializeTransition ? AnimationTransitionMode.Inertialize : AnimationTransitionMode.CrossFade,
        };
        Logs.WriteInfo($"SkinnedMeshDemo: transitioning to animation {_nextAnimationIndex} using {transitionSettings.TransitionMode} mode.");

        for (var componentIndex = 0; componentIndex < _skinnedMeshComponents.Count; componentIndex++)
        {
            _skinnedMeshComponents[componentIndex].CrossFadeToAnimation(_nextAnimationIndex, 0.35f, transitionSettings);
        }

        _nextAnimationIndex++;
    }

    // The environment belongs to the world: reset it to the engine defaults so the next demo does not inherit this one's.
    public override void Clean()
    {
        if (_game?.GameManager.CurrentWorld is { } world)
        {
            world.EnvironmentSettings.ResetToDefaults();
            world.EnvironmentSettings.MarkDirty();
        }

        _game = null;
        _skinnedMeshComponents.Clear();
        _animationSwitchTimer = 0f;
        _nextAnimationIndex = 1;
        _useInertializeTransition = false;
    }

    // One column: a caster (larger scale) and a receiver (smaller) placed apart in depth. The two use different skinning
    // modes (LinearBlend and DualQuaternion), so both are on screen. The caster always receives shadows; the receiver
    // never casts. The column's two flags are applied to the caster's CastShadows and the receiver's ReceiveShadows.
    private void CreateShadowValidationColumn(
        CasaEngine.Framework.Application.CasaEngineGame game,
        CasaEngine.Framework.Scene.World.World world,
        SkinnedMesh skinnedMesh,
        string columnName,
        float centerX,
        bool casterCastShadows,
        bool receiverReceiveShadows,
        LitDiffuseMaterial casterMaterialOverride = null)
    {
        // A ground under the pair: a static mesh that never casts and takes the column's receive flag, like the receiver.
        // It is what the shadow of the caster falls on.
        StaticModel groundModel = StaticModel.CreateFromPrimitive(new BoxPrimitive(GroundWidth, 0.25f, GroundDepth));
        groundModel.Meshes[0].Initialize(game.GraphicsDevice);
        groundModel.Meshes[0].Material = new LitDiffuseMaterial
        {
            DiffuseColor = new Color(120, 124, 118),
            AmbientColor = new Vector3(0.35f),
        };

        var groundEntity = new Entity { Name = $"{columnName}Ground" };
        var ground = new StaticModelComponent
        {
            StaticModel = groundModel,
            CastShadows = false,
            ReceiveShadows = receiverReceiveShadows,
        };
        groundEntity.RootComponent = ground;
        ground.LocalPosition = new Vector3(centerX, -0.125f, GroundCentreZ);
        world.AddEntity(groundEntity);

        CreateSkinnedMeshEntity(
            world,
            skinnedMesh,
            $"{columnName}Caster",
            CasterOffset + new Vector3(centerX, 0f, 0f),
            SkinningModeSelection.LinearBlend,
            castShadows: casterCastShadows,
            receiveShadows: true,
            uniformScale: 0.11f,
            materialOverride: casterMaterialOverride);

        CreateSkinnedMeshEntity(
            world,
            skinnedMesh,
            $"{columnName}Receiver",
            ReceiverOffset + new Vector3(centerX, 0f, 0f),
            SkinningModeSelection.DualQuaternion,
            castShadows: false,
            receiveShadows: receiverReceiveShadows,
            uniformScale: 0.085f);
    }

    private void CreateSkinnedMeshEntity(
        CasaEngine.Framework.Scene.World.World world,
        SkinnedMesh skinnedMesh,
        string entityName,
        Vector3 localPosition,
        SkinningModeSelection skinningModeSelection,
        bool castShadows,
        bool receiveShadows,
        float uniformScale = 0.1f,
        LitDiffuseMaterial materialOverride = null)
    {
        // The component is the entity's root and draws the shared mesh. Material, when not null, replaces the renderer's
        // default skinned material for this component only. The uniform scale sets the size on screen; the caster is drawn a little larger than the receiver.
        var entity = new Entity { Name = entityName };
        var skinnedMeshComponent = new SkinnedMeshComponent
        {
            SkinnedMesh = skinnedMesh,
            SkinningModeSelection = skinningModeSelection,
            CastShadows = castShadows,
            ReceiveShadows = receiveShadows,
            Material = materialOverride,
        };

        entity.RootComponent = skinnedMeshComponent;
        entity.RootComponent.LocalPosition = localPosition;
    entity.RootComponent.LocalOrientation = Quaternion.Identity;
    entity.RootComponent.LocalScale = new Vector3(uniformScale, uniformScale, uniformScale);

        // Start on the first clip; Update moves on to the next ones. The list lets Update reach every component.
        skinnedMeshComponent.PlayAnimation(0);
        _skinnedMeshComponents.Add(skinnedMeshComponent);
        world.AddEntity(entity);
    }

    // Turns a direction into the rotation that takes Vector3.Forward onto it. The two special cases are the ones where the
    // cross product used for the axis is zero: already aligned, or exactly opposite (a half turn around Up).
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