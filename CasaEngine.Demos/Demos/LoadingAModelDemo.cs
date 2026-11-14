using System.Collections.Generic;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using Microsoft.Xna.Framework;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// The third demo to read: a model that comes from a file instead of being built in code.
/// <para/>
/// The chain of files, all under <c>CasaEngine.Demos/Content</c>:
/// <list type="bullet">
///   <item><c>SkinnedMesh/kid_idle.glb</c>: the source file exported by a 3D tool (mesh, skeleton, animation).</item>
///   <item><c>SkinnedMesh/kid_idle.model</c>: the engine asset. It is a small JSON file that names the rigged model
///   it uses by asset id.</item>
///   <item><c>AssetInfos.json</c>: the catalogue of the project, which maps every asset id to a file. This is how the
///   <c>.model</c> finds its <c>.glb</c>, and its textures.</item>
/// </list>
/// The <c>.model</c> and the catalogue are project data: a game names the asset and loads it.
/// </summary>
public class LoadingAModelDemo : Demo
{
    public override string Title => "Loading a model";

    public override string Description =>
        "An animated character loaded from an asset file. The demo asks the asset manager for " +
        "SkinnedMesh/kid_idle.model; the asset names its glTF source and its textures by id, and the project " +
        "catalogue (AssetInfos.json) resolves them. One component draws and animates it.";

    public override IReadOnlyList<string> Highlights { get; } = new[]
    {
        "The character plays the first animation clip of its file.",
        "Nothing about the mesh or its textures is written in the demo: only the name of the asset.",
    };

    public override string DocumentationPath => "docs/engine/asset-handles-and-bitmap-fonts.md";

    public override void Initialize(CasaEngineGame game)
    {
        World world = game.GameManager.CurrentWorld;

        // 1. Load the asset. The path is relative to the Content folder of the project.
        SkinnedMesh skinnedMesh = game.AssetContentManager.LoadFromFile<SkinnedMesh>("SkinnedMesh\\kid_idle.model");

        // 2. Resolve what the asset refers to (the rigged model, hence its textures) through the asset manager.
        skinnedMesh.Initialize(game.AssetContentManager);

        // 3. One entity, one component: SkinnedMeshComponent draws the mesh and runs its animation.
        var entity = new Entity { Name = "kid" };
        var character = new SkinnedMeshComponent { SkinnedMesh = skinnedMesh };
        entity.RootComponent = character;

        // The model is large in its own units: scale it down to the size of the scene.
        character.LocalScale = new Vector3(0.1f);

        // 4. Play the first clip of the file, looping.
        character.PlayAnimation(0);

        world.AddEntity(entity);
    }

    public override void InitializeCamera(CameraComponent camera)
    {
        ((ArcBallCameraComponent)camera).SetCamera(new Vector3(6f, 8f, 16f), new Vector3(0f, 5f, 0f), Vector3.Up);
    }

    public override void Update(GameTime gameTime)
    {
        // Nothing to do: the component advances its own animation.
    }

    public override void Clean()
    {
        // The world owns the entity and releases the asset with it.
    }
}
