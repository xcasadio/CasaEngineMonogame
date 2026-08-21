using CasaEngine.Core.Logging;
﻿using System;
using System.Collections.Generic;
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

public class SkinnedMeshDemo : Demo
{
    private CasaEngineGame? _game;
    private readonly List<SkinnedMeshComponent> _skinnedMeshComponents = new();
    private float _animationSwitchTimer;
    private int _nextAnimationIndex = 1;
    private bool _useInertializeTransition;

    public override string Title => "Skinned mesh demo";
    public override string Description => "Displays animated skinned meshes with forward shadows. Left column shows a skinned caster shadowing a receiver, center keeps the caster but disables ReceiveShadows on the receiver, and right disables CastShadows on the caster. The left caster also uses a per-component Material override (tinted orange) to exercise SkinnedMeshComponent.Material instead of the renderer's default material.";

    public override CameraComponent CreateCamera(CasaEngineGame game)
    {
        var entity = new Entity { Name = "SkinnedShadowValidationCamera" };
        var camera = new CameraLookAtComponent();
        entity.RootComponent = camera;
        entity.Initialize();
        game.GameManager.CurrentWorld.AddEntity(entity);
        return camera;
    }

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
        environmentSettings.MarkDirty();

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
        camera.SetPositionAndTarget(new Vector3(0f, 8.5f, 38.0f), new Vector3(0f, 2.0f, 0f));
    }

    public override void Initialize(CasaEngineGame game)
    {
        _game = game;
        var world = game.GameManager.CurrentWorld;
        _skinnedMeshComponents.Clear();
        _animationSwitchTimer = 0f;
        _nextAnimationIndex = 1;
        _useInertializeTransition = false;

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

        CreateShadowValidationColumn(world, skinnedMesh, "ReceiveShadow", -9.0f, casterCastShadows: true, receiverReceiveShadows: true, casterMaterialOverride: tintedCasterMaterial);
        CreateShadowValidationColumn(world, skinnedMesh, "NoReceiveShadow", 0.0f, casterCastShadows: true, receiverReceiveShadows: false);
        CreateShadowValidationColumn(world, skinnedMesh, "NoCastShadow", 9.0f, casterCastShadows: false, receiverReceiveShadows: true);
    }

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

    private void CreateShadowValidationColumn(
        CasaEngine.Framework.Scene.World.World world,
        SkinnedMesh skinnedMesh,
        string columnName,
        float centerX,
        bool casterCastShadows,
        bool receiverReceiveShadows,
        LitDiffuseMaterial casterMaterialOverride = null)
    {
        CreateSkinnedMeshEntity(
            world,
            skinnedMesh,
            $"{columnName}Caster",
            new Vector3(centerX - 2.4f, 0f, 5.2f),
            SkinningModeSelection.LinearBlend,
            castShadows: casterCastShadows,
            receiveShadows: true,
            uniformScale: 0.11f,
            materialOverride: casterMaterialOverride);

        CreateSkinnedMeshEntity(
            world,
            skinnedMesh,
            $"{columnName}Receiver",
            new Vector3(centerX + 2.4f, 0f, -3.5f),
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

        skinnedMeshComponent.PlayAnimation(0);
        _skinnedMeshComponents.Add(skinnedMeshComponent);
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