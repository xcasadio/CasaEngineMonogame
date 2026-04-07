using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering.Models;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using CasaEngine.Engine.Environment;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// Demonstrates loading a multi-mesh <see cref="StaticModel"/> and displaying it
/// via a <see cref="StaticModelComponent"/> with an auto-built sub-mesh hierarchy.
///
/// The demo constructs the model programmatically from box and sphere primitives
/// (simulating what the importer produces from an FBX file):
///
/// Root
///  ├── "Ground"  (box, y = 0)
///  ├── "Cube A"  (box, x = -2)
///  └── "Sphere"  (sphere, x =  2)
/// </summary>
public class StaticModelDemo : Demo
{
    public override string Title => "Static model demo";
    public override string Description =>
        "Builds a StaticModel programmatically from box/sphere primitives and renders it " +
        "through a StaticModelComponent with an auto-generated sub-mesh hierarchy.";

    public override void Initialize(CasaEngineGame game)
    {
        var world = game.GameManager.CurrentWorld;
        var gd = game.GraphicsDevice;

        // ------------------------------------------------------------------
        // Build mesh list from geometric primitives
        // ------------------------------------------------------------------
        var groundPrimitive = new BoxPrimitive(10, 0.5f, 10);
        var boxPrimitive    = new BoxPrimitive(1, 1, 1);
        var spherePrimitive = new SpherePrimitive(0.75f, 16);

        var groundMesh = CreateMesh("Ground", groundPrimitive);
        var boxMesh    = CreateMesh("Box",    boxPrimitive);
        var sphereMesh = CreateMesh("Sphere", spherePrimitive);

        // Tint meshes so they are visually distinct even without textures.
        // (The renderer uses the texture if present, otherwise the default white.)

        // ------------------------------------------------------------------
        // Assemble StaticModel
        // ------------------------------------------------------------------
        var model = new StaticModel { Name = "DemoModel" };
        model.Meshes.Add(groundMesh);   // index 0
        model.Meshes.Add(boxMesh);      // index 1
        model.Meshes.Add(sphereMesh);   // index 2

        // Node hierarchy
        var rootNode = new StaticModelNode
        {
            Name     = "Root",
            MeshIndex = -1,            // structural — no mesh on root
            Position = Vector3.Zero,
            Rotation = Quaternion.Identity,
            Scale    = Vector3.One
        };

        var groundNode = new StaticModelNode
        {
            Name      = "Ground",
            MeshIndex = 0,
            Position  = new Vector3(0, -0.25f, 0),
            Rotation  = Quaternion.Identity,
            Scale     = Vector3.One
        };

        var boxNodeA = new StaticModelNode
        {
            Name      = "Cube A",
            MeshIndex = 1,
            Position  = new Vector3(-2f, 0.5f, 0f),
            Rotation  = Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.ToRadians(30f)),
            Scale     = Vector3.One
        };

        var sphereNode = new StaticModelNode
        {
            Name      = "Sphere",
            MeshIndex = 2,
            Position  = new Vector3(2f, 0.75f, 0f),
            Rotation  = Quaternion.Identity,
            Scale     = Vector3.One
        };

        rootNode.Children.Add(groundNode);
        rootNode.Children.Add(boxNodeA);
        rootNode.Children.Add(sphereNode);

        model.RootNode = rootNode;

        // Upload vertex/index buffers  (no asset manager needed for programmatic data)
        foreach (var mesh in model.Meshes)
        {
            mesh.Initialize(gd);
        }

        // Assign materials so meshes render correctly through the material pipeline.
        var texturePath = Path.Combine(EngineEnvironment.ProjectPath, "checkboard.png");
        if (File.Exists(texturePath))
        {
            groundMesh.Material = new LitDiffuseMaterial
            {
                BasColor     = Texture2D.FromFile(gd, texturePath),
                DiffuseColor = Color.White,
            };
        }
        else
        {
            groundMesh.Material = new LitDiffuseMaterial { DiffuseColor = new Color(200, 190, 160) };
        }
        boxMesh.Material    = new LitDiffuseMaterial { DiffuseColor = new Color(160, 120, 80) };
        sphereMesh.Material = new LitDiffuseMaterial { DiffuseColor = new Color(80, 110, 170) };

        // ------------------------------------------------------------------
        // Create entity + component
        // ------------------------------------------------------------------
        var entity = new Entity { Name = "StaticModelEntity" };
        var component = new StaticModelComponent();
        // Assign the programmatic model directly (bypasses asset-ID loading).
        component.StaticModel = model;
        entity.RootComponent = component;

        world.AddEntity(entity);
    }

    public override void Update(GameTime gameTime) { }

    public override void Clean() { }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static StaticModelMesh CreateMesh(string name, GeometricPrimitive primitive)
    {
        var mesh = new StaticModelMesh { Name = name };
        mesh.SetData(
            primitive.Vertices.ToArray(),
            primitive.Indices.ToArray());
        return mesh;
    }
}
