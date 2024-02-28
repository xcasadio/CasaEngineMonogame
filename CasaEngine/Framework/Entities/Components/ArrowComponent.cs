using System.ComponentModel;
using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Entities.Components;

[DisplayName("Arrow")]
public class ArrowComponent : StaticMeshComponent
{
    public ArrowComponent()
    {

    }

    public ArrowComponent(ArrowComponent other) : base(other)
    {
        Mesh = other.Mesh;
    }

#if EDITOR

    protected override void InitializePrivate()
    {
        base.InitializePrivate();

        var cylinderPrimitive = new CylinderPrimitive(1f, 0.5f);
        var conePrimitive = new ConePrimitive();

        var vertices = new List<VertexPositionNormalTexture>(cylinderPrimitive.Vertices.Count + conePrimitive.Vertices.Count);
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

        Mesh = new StaticMesh();
        Mesh.AddVertices(vertices);
        Mesh.AddIndices(cylinderPrimitive.Indices);
        var indicesStart = (uint)cylinderPrimitive.Vertices.Count;

        foreach (var index in conePrimitive.Indices)
        {
            Mesh.AddIndex(index + indicesStart);
        }
    }

    public override void InitializeWithWorld(World.World world)
    {
        Mesh.Texture = world.Game.AssetContentManager.GetAsset<Assets.Textures.Texture>(Assets.Textures.Texture.DefaultTextureName);
        base.InitializeWithWorld(world);
    }
#endif

    public override ArrowComponent Clone()
    {
        return new ArrowComponent(this);
    }
}