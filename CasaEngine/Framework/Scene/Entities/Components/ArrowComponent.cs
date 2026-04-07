using System.ComponentModel;
using CasaEngine.Engine.Primitives.ThreeD;
using CasaEngine.Framework.Rendering.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Scene.Entities.Components;

[DisplayName("Arrow")]
public class ArrowComponent : StaticModelComponent
{
    public ArrowComponent() { }

    public ArrowComponent(ArrowComponent other) : base(other)
    {
        StaticModel = other.StaticModel;
    }

    protected override void InitializePrivate()
    {
        base.InitializePrivate();

        var cylinderPrimitive = new CylinderPrimitive(1f, 0.5f);
        var conePrimitive = new ConePrimitive();

        var vertices = new List<VertexPositionNormalTexture>(
            cylinderPrimitive.Vertices.Count + conePrimitive.Vertices.Count);

        var rot90 = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.ToRadians(-90f));

        foreach (var vertex in cylinderPrimitive.Vertices)
        {
            vertices.Add(new VertexPositionNormalTexture(
                Vector3.Transform(vertex.Position, rot90),
                Vector3.Transform(vertex.Normal, rot90),
                vertex.TextureCoordinate));
        }

        foreach (var vertex in conePrimitive.Vertices)
        {
            vertices.Add(new VertexPositionNormalTexture(
                Vector3.Transform(vertex.Position, rot90) - Vector3.UnitZ,
                Vector3.Transform(vertex.Normal, rot90),
                vertex.TextureCoordinate));
        }

        var indices = new List<uint>(cylinderPrimitive.Indices);
        var baseOffset = (uint)cylinderPrimitive.Vertices.Count;
        foreach (var index in conePrimitive.Indices)
        {
            indices.Add(index + baseOffset);
        }

        var mesh = new StaticModelMesh { Name = "Arrow" };
        mesh.SetData(vertices.ToArray(), indices.ToArray());

        var rootNode = new StaticModelNode
        {
            Name      = "Root",
            MeshIndex = 0,
            Position  = Vector3.Zero,
            Rotation  = Quaternion.Identity,
            Scale     = Vector3.One
        };

        var model = new StaticModel { Name = "Arrow" };
        model.Meshes.Add(mesh);
        model.RootNode = rootNode;

        StaticModel = model;
    }

    public override void InitializeWithWorld(CasaEngine.Framework.Scene.World.World world)
    {
        if (StaticModel?.Meshes.Count > 0)
        {
            StaticModel.Meshes[0].Texture =
                world.Game.AssetContentManager.GetAsset<Assets.Textures.Texture>(
                    Assets.Textures.Texture.DefaultTextureName);
        }

        base.InitializeWithWorld(world);
    }

    public override ArrowComponent Clone() => new(this);
}
