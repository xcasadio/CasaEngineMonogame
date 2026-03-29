using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Transform;
using CasaEngine.Framework.World;

namespace CasaEngine.EditorServices;

public static class EditorWorldEditingService
{
    public static IEnumerable<ITransformableObject> GetSelectableComponents(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.GetTransformableObjects();
    }

    public static void AddEntity(World world, Entity entity)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(entity);

        var entityReference = new EntityReference
        {
            Name = entity.Name,
            Entity = entity,
        };

        world.AddEntityReferenceImmediate(entityReference, entityReference.Entity);
    }

    public static void AddEntityReference(World world, EntityReference entityReference)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(entityReference);
        ArgumentNullException.ThrowIfNull(entityReference.Entity);

        world.AddEntityReferenceImmediate(entityReference, entityReference.Entity);
    }

    public static void RemoveEntity(World world, Entity entity)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(entity);

        world.RemoveEntityImmediate(entity);
    }
}