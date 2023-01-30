using Microsoft.Xna.Framework;

namespace CasaEngine.AI.Navigation
{
    public interface IPhysicEngine
    {
        bool NearBodyWorldRayCast(ref Vector3 position_, ref Vector3 feelers_, out Vector3 contactPoint_, out Vector3 ContactNormal_);

        bool WorldRayCast(ref Vector3 start, ref Vector3 end, Vector3 dir_);
    }

    public static class PhysicEngine
    {
        static public IPhysicEngine Physic = null;
    }
}
