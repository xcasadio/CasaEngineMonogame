using CasaEngine.Engine.Animations;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.SceneManagement;
using CasaEngine.Framework.SceneManagement.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.Demos.Demos;

public class SkinnedMeshDemo : Demo
{
    public override string Title => "Skinned mesh demo";

    public override void Initialize(CasaEngineGame game)
    {
        var world = game.GameManager.CurrentWorld;

        //============ Create skinned mesh ===============
        var entity = new AActor();
        var skinnedMeshComponent = new SkinnedMeshComponent();
        entity.RootComponent = skinnedMeshComponent;
        entity.RootComponent.Position = new Vector3(0, 0, 0);
        entity.RootComponent.Orientation = Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(180f));
        entity.RootComponent.Scale = new Vector3(0.1f, 0.1f, 0.1f);

        var skinModelLoader = new RiggedModelLoader(game.Content, game.Content.Load<Effect>("Shaders\\skinEffect"));
        var debugTexture = game.GameManager.AssetContentManager.GetAsset<Texture>(Texture.DefaultTextureName).Resource;
        RiggedModelLoader.DefaultTexture = debugTexture;
        var skinModel = skinModelLoader.LoadAsset("Content/SkinnedMesh/kid_idle.fbx");//dude kid_idle
        skinnedMeshComponent.SkinnedMesh = skinModel;
        skinModel.BeginAnimation(0);

        world.AddEntity(entity);
    }

    public override void Update(GameTime gameTime)
    {

    }

    public override void Clean()
    {

    }
}