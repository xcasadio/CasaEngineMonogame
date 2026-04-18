using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using CasaEngine.Framework.Rendering.Models;

namespace CasaEngine.Demos.Demos;

public class SkinnedMeshDemo : Demo
{
    private SkinnedMeshComponent? _skinnedMeshComponent;
    private float _animationSwitchTimer;
    private int _nextAnimationIndex = 1;
    private SkinningMode _skinningMode = SkinningMode.DualQuaternion;

    public override string Title => "Skinned mesh demo";
    public override string Description => "Displays an animated skinned mesh model loaded from content. Defaults to dual quaternion skinning to validate twist preservation.";

    public override void Initialize(CasaEngineGame game)
    {
        var world = game.GameManager.CurrentWorld;

        //============ Create skinned mesh ===============
        var entity = new Entity { Name = "Skinned mesh" };
        _skinnedMeshComponent = new SkinnedMeshComponent();
        entity.RootComponent = _skinnedMeshComponent;
        entity.RootComponent.LocalPosition = new Vector3(0, 0, 0);
        entity.RootComponent.LocalOrientation = Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(180f));
        entity.RootComponent.LocalScale = new Vector3(0.1f, 0.1f, 0.1f);

        var skinnedMesh = game.AssetContentManager.LoadDirectly<SkinnedMesh>("Content\\SkinnedMesh\\kid_idle.model");
        skinnedMesh.Initialize(game.AssetContentManager);
        if (skinnedMesh.RiggedModel != null)
        {
            skinnedMesh.RiggedModel.SkinningMode = _skinningMode;
        }

        _skinnedMeshComponent.SkinnedMesh = skinnedMesh;
        _skinnedMeshComponent.PlayAnimation(0);

        world.AddEntity(entity);
    }

    public override void Update(GameTime gameTime)
    {
        if (_skinnedMeshComponent == null)
        {
            return;
        }

        if (_skinnedMeshComponent.AnimationClips.Count < 2)
        {
            return;
        }

        _animationSwitchTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_animationSwitchTimer < 2.5f)
        {
            return;
        }

        _animationSwitchTimer = 0f;
        if (_nextAnimationIndex >= _skinnedMeshComponent.AnimationClips.Count)
        {
            _nextAnimationIndex = 0;
        }

        _skinnedMeshComponent.CrossFadeToAnimation(_nextAnimationIndex, 0.35f);
        _nextAnimationIndex++;
    }

    public override void Clean()
    {

    }
}