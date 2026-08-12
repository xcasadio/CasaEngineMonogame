using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Engine.Geometry;

/// <summary>
/// An authoring shape expressed in its own local space. Its pose belongs to whatever attaches it
/// (Collision2d for sprite and tile collisions), never to the shape itself.
/// </summary>
public abstract class Shape2d
{
    public Shape2dType Type { get; }
    public abstract BoundingBox BoundingBox { get; }

    protected Shape2d(Shape2dType type)
    {
        Type = type;
    }

    public virtual void Load(JObject element)
    {
    }
}
