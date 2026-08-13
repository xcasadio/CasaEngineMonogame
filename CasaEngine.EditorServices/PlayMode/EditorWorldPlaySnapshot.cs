using CasaEngine.Framework.Scene.World;
using Newtonsoft.Json.Linq;

namespace CasaEngine.EditorServices.PlayMode;

/// <summary>
/// Builds the play copy of the world being edited: the edit world is serialized to an
/// in-memory JSON document (same format as a saved .world file) and a fresh World is
/// created from it. The edit world itself is never touched, so stopping the play
/// session simply reinstalls it.
/// </summary>
public static class EditorWorldPlaySnapshot
{
    public static JObject Capture(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        var node = new JObject();
        EditorEntityJsonSerializer.SaveWorld(world, node);
        return node;
    }

    /// <summary>
    /// Creates a world from a snapshot. Entities are only materialized later, when the
    /// game loads the world (<c>World.LoadContent</c>), exactly like a world loaded from disk.
    /// </summary>
    public static World CreatePlayWorld(JObject snapshot, string? fileName = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var world = new World();
        world.Load(snapshot);
        world.FileName = fileName;
        return world;
    }

    public static World CreatePlayWorld(World editWorld)
    {
        ArgumentNullException.ThrowIfNull(editWorld);
        return CreatePlayWorld(Capture(editWorld), editWorld.FileName);
    }
}
