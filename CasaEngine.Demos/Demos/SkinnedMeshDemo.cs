using CasaEngine.Engine.Animations;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.SceneManagement.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;
using CasaEngine.Framework.Graphics;

namespace CasaEngine.Demos.Demos;

public class SkinnedMeshDemo : Demo
{
    public override string Title => "Skinned mesh demo";

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

        //var skinModelLoader = new RiggedModelLoader(game.AssetContentManager, game.Content.Load<Effect>("Shaders\\skinEffect"));
        //var debugTexture = game.AssetContentManager.GetAsset<Texture>(Texture.DefaultTextureName).Resource;
        //RiggedModelLoader.DefaultTexture = debugTexture;
        //var skinModel = skinModelLoader.LoadAsset("Content/SkinnedMesh/kid_idle.fbx");//dude kid_idle

        var skinnedMesh = game.AssetContentManager.LoadDirectly<SkinnedMesh>("SkinnedMesh\\kid_idle.model");
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