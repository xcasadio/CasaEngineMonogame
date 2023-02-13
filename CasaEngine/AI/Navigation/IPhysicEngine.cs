using Microsoft.Xna.Framework;

namespace CasaEngine.AI.Navigation
{
    public interface IPhysicEngine
    {
        bool NearBodyWorldRayCast(ref Vector3 position, ref Vector3 feelers, out Vector3 contactPoint, out Vector3 contactNormal);

        bool WorldRayCast(ref Vector3 start, ref Vector3 end, Vector3 dir);
    }

    public static class PhysicEngine
    {
        public static IPhysicEngine Physic = null;
    }
}
