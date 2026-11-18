using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.World;
using Newtonsoft.Json.Linq;

namespace CasaEngine.EditorServices;

public static class EditorWorldWriter
{
    public static void SaveWorld(World world)
    {
        JObject rootObject = new();
        EditorEntityJsonSerializer.SaveWorld(world, rootObject);
        EditorAssetWriterService.SaveDocument(world.FileName, rootObject);
    }

    /// <summary>
    /// Writes <paramref name="world"/> to its <see cref="World.FileName"/> with one inline entity reference per entity of
    /// <paramref name="entities"/>, in list order. The file has the format <see cref="SaveWorld(World)"/> writes, so it loads with
    /// <c>World.Load</c> and the editor saves it back unchanged. The entities are not initialized and not added to the world,
    /// so a tool can write a world without a running game, including entities whose components need the game to initialize.
    /// </summary>
    /// <exception cref="ArgumentNullException">The world, the list or one entity is null.</exception>
    /// <exception cref="InvalidOperationException">The world already holds entity references: the two sources are not mixed.</exception>
    public static void SaveWorld(World world, IReadOnlyList<Entity> entities)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(entities);

        if (EditorEntityJsonSerializer.GetEntityReferences(world).Count > 0)
        {
            throw new InvalidOperationException("The world already holds entity references; write it with SaveWorld(World) instead.");
        }

        var entityReferences = new List<EntityReference>(entities.Count);
        foreach (var entity in entities)
        {
            ArgumentNullException.ThrowIfNull(entity);
            entityReferences.Add(new EntityReference { AssetId = Guid.Empty, Entity = entity });
        }

        JObject rootObject = new();
        EditorEntityJsonSerializer.SaveWorld(world, entityReferences, rootObject);
        EditorAssetWriterService.SaveDocument(world.FileName, rootObject);
    }
}