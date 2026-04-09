using System;
using System.Collections.Generic;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Scene.Entities;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Scene.World;

public interface IWorldSpatialIndex3D
{
    void Clear();

    void Add(Entity entity, BoundingBox bounds);

    bool Remove(Entity entity);

    bool Move(Entity entity, BoundingBox newBounds);

    void ApplyPendingMoves();

    void Query(BoundingFrustum frustum, List<Entity> results, Func<Entity, bool>? filter = null);

    void Query(BoundingBox bounds, List<Entity> results, Func<Entity, bool>? filter = null);

    void DebugDraw(Line3dRendererComponent line3dRendererComponent);
}