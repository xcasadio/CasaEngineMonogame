using System;
using System.Collections.Generic;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.World;

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