using System.Collections.Generic;
using CasaEngine.Core.Shapes;
using CasaEngine.Engine.Animations;
using CasaEngine.Engine.Physics;
using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;

namespace DemosGame.Demos;

public class SkinnedMeshDemo : Demo
{
    public override string Title => "Skinned mesh demo";

    public override void Initialize(CasaEngineGame game)
    {
        var world = game.GameManager.CurrentWorld;

        //============ Create skinned mesh ===============
        var entity = new Entity();
        entity.Coordinates.LocalPosition = new Vector3(0, 0, 0);
        var skinnedMeshComponent = new SkinnedMeshComponent(entity);
        entity.ComponentManager.Components.Add(skinnedMeshComponent);

        var skinModelLoader = new RiggedModelLoader(game.Content, game.Content.Load<Effect>("Shaders\\skinEffect"));
        var debugTexture = game.GameManager.AssetContentManager.GetAsset<Texture>(Texture.DefaultTextureName).Resource;
        RiggedModelLoader.DefaultTexture = debugTexture;
        var skinModel = skinModelLoader.LoadAsset("Content/SkinnedMesh/kid_walk.fbx");//dude
        skinnedMeshComponent.SkinnedMesh = skinModel;
        skinModel.BeginAnimation(0);

        //skinnedMeshComponent.Mesh.Initialize(game.GraphicsDevice);
        world.AddEntityImmediately(entity);
    }

    public override void Update(GameTime gameTime)
    {

    }

    protected override void Clean()
    {

    }
}