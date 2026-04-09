using CasaEngine.Framework.Application;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using CasaEngine.Framework.Rendering.Models;

namespace CasaEngine.Demos.Demos;

public class SkinnedMeshDemo : Demo
{
    public override string Title => "Skinned mesh demo";
    public override string Description => "Displays an animated skinned mesh model loaded from content. Shows bone-based vertex skinning.";

    public override void Initialize(CasaEngineGame game)
    {
        var world = game.GameManager.CurrentWorld;

        //============ Create skinned mesh ===============
        var entity = new Entity { Name = "Skinned mesh" };
        var skinnedMeshComponent = new SkinnedMeshComponent();
        entity.RootComponent = skinnedMeshComponent;
        entity.RootComponent.LocalPosition = new Vector3(0, 0, 0);
        entity.RootComponent.LocalOrientation = Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(180f));
        entity.RootComponent.LocalScale = new Vector3(0.1f, 0.1f, 0.1f);

        var skinnedMesh = game.AssetContentManager.LoadDirectly<SkinnedMesh>("Content\\SkinnedMesh\\kid_idle.model");
        skinnedMesh.Initialize(game.AssetContentManager);

        skinnedMeshComponent.SkinnedMesh = skinnedMesh;
        skinnedMeshComponent.SkinnedMesh.RiggedModel.BeginAnimation(0);

        world.AddEntity(entity);
    }

    public override void Update(GameTime gameTime)
    {

    }

    public override void Clean()
    {

    }
}