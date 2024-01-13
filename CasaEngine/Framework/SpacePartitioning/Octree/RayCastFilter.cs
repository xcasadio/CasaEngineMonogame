using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.SpacePartitioning.Octree;

public delegate int RayCastFilter<T>(Ray ray, T item, List<RayCastHit<T>> hits);