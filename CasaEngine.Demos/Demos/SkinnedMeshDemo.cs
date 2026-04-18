using System.Collections.Generic;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using CasaEngine.Framework.Rendering.Models;

namespace CasaEngine.Demos.Demos;

public class SkinnedMeshDemo : Demo
{
    private readonly List<SkinnedMeshComponent> _skinnedMeshComponents = new();
    private float _animationSwitchTimer;
    private int _nextAnimationIndex = 1;

    public override string Title => "Skinned mesh demo";
    public override string Description => "Displays two animated skinned meshes side by side. Left uses linear blend skinning, right uses dual quaternion skinning.";

    public override void Initialize(CasaEngineGame game)
    {
        var world = game.GameManager.CurrentWorld;
        _skinnedMeshComponents.Clear();
        _animationSwitchTimer = 0f;
        _nextAnimationIndex = 1;

        var skinnedMesh = game.AssetContentManager.LoadDirectly<SkinnedMesh>("Content\\SkinnedMesh\\kid_idle.model");
        skinnedMesh.Initialize(game.AssetContentManager);

        CreateSkinnedMeshEntity(world, skinnedMesh, "Linear blend skinned mesh", new Vector3(-1.75f, 0f, 0f), SkinningModeSelection.LinearBlend);
        CreateSkinnedMeshEntity(world, skinnedMesh, "Dual quaternion skinned mesh", new Vector3(1.75f, 0f, 0f), SkinningModeSelection.DualQuaternion);
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

        for (var componentIndex = 0; componentIndex < _skinnedMeshComponents.Count; componentIndex++)
        {
            _skinnedMeshComponents[componentIndex].CrossFadeToAnimation(_nextAnimationIndex, 0.35f);
        }

        _nextAnimationIndex++;
    }

    public override void Clean()
    {
        _skinnedMeshComponents.Clear();
        _animationSwitchTimer = 0f;
        _nextAnimationIndex = 1;
    }

    private void CreateSkinnedMeshEntity(
        CasaEngine.Framework.Scene.World.World world,
        SkinnedMesh skinnedMesh,
        string entityName,
        Vector3 localPosition,
        SkinningModeSelection skinningModeSelection)
    {
        var entity = new Entity { Name = entityName };
        var skinnedMeshComponent = new SkinnedMeshComponent
        {
            SkinnedMesh = skinnedMesh,
            SkinningModeSelection = skinningModeSelection,
        };

        entity.RootComponent = skinnedMeshComponent;
        entity.RootComponent.LocalPosition = localPosition;
        entity.RootComponent.LocalOrientation = Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(180f));
        entity.RootComponent.LocalScale = new Vector3(0.1f, 0.1f, 0.1f);

        skinnedMeshComponent.PlayAnimation(0);
        _skinnedMeshComponents.Add(skinnedMeshComponent);
        world.AddEntity(entity);
    }
}