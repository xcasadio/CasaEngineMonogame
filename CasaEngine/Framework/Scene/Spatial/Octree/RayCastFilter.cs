using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Scene.Spatial.Octree;

public delegate int RayCastFilter<T>(Ray ray, T item, List<RayCastHit<T>> hits);