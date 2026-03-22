using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Graphics;

/// <summary>
/// A node in the hierarchy of a <see cref="StaticModel"/>.
/// Each node has a local transform and may optionally reference a mesh
/// (via <see cref="MeshIndex"/>).  Structural nodes with no geometry have
/// MeshIndex == -1.
/// </summary>
public class StaticModelNode : ISerializable
{
    public string Name { get; set; } = string.Empty;

    public Vector3 Position { get; set; } = Vector3.Zero;
    public Quaternion Rotation { get; set; } = Quaternion.Identity;
    public Vector3 Scale { get; set; } = Vector3.One;

    /// <summary>Index into the parent <see cref="StaticModel.Meshes"/> list. -1 means no mesh.</summary>
    public int MeshIndex { get; set; } = -1;

    public List<StaticModelNode> Children { get; } = new();

    /// <summary>Local-space transform matrix built from Position, Rotation and Scale.</summary>
    public Matrix LocalTransform =>
        Matrix.CreateScale(Scale)
        * Matrix.CreateFromQuaternion(Rotation)
        * Matrix.CreateTranslation(Position);

    public void Load(JObject element)
    {
        Name = element["name"].GetString();
        MeshIndex = element["mesh_index"].GetInt32();
        Position = element["position"].GetVector3();
        Rotation = element["rotation"].GetQuaternion();
        Scale = element["scale"].GetVector3();

        Children.Clear();
        foreach (JObject childNode in element["children"])
        {
            var child = new StaticModelNode();
            child.Load(childNode);
            Children.Add(child);
        }
    }

    public void Save(JObject jObject)
    {
        throw new NotSupportedException("StaticModelNode authoring serialization lives in CasaEngine.EditorServices.");
    }
}
