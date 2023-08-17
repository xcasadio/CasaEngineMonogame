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

        var skinModelLoader = new SkinModelLoader(game.Content, game.GraphicsDevice);
        // pad the animation a bit for smooth looping, set a debug texture (if no texture on a mesh)
        skinModelLoader.SetDefaultOptions(0.1f, game.GameManager.AssetContentManager.GetAsset<Texture>(Texture.DefaultTextureName).Resource);

        var skinFx = new SkinFx(game.GameManager.AssetContentManager, game.Content.Load<Effect>("Shaders\\skinEffect"));
        var skinModel = skinModelLoader.Load("Content/SkinnedMesh/kid_walk.fbx",
            "SkinnedMesh", false, 3, skinFx, rescale: 0.35f);
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