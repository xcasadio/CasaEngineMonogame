using CasaEngineCommon.Pool;
using FarseerPhysics.Dynamics;

namespace CasaEngine.Physics2D
{
    public static class PhysicsObjectPool
    {
        //static private ResourcePool<Body> _ResourcePoolBody = new ResourcePool<Body>(CreateBody);
        private static ResourcePool<Fixture> _resourcePoolFixture = new(CreateFixture);

        /*static private Body CreateBody(ResourcePool<Body> pool_)
        {
            return new Body();
        }*/

        private static Fixture CreateFixture(ResourcePool<Fixture> pool)
        {
            return new Fixture();
        }
    }
}
