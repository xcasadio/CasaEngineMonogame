using CasaEngineCommon.Pool;
using FarseerPhysics.Dynamics;

namespace CasaEngine.Physics2D
{
    static public class PhysicsObjectPool
    {
        //static private ResourcePool<Body> _ResourcePoolBody = new ResourcePool<Body>(CreateBody);
        static private ResourcePool<Fixture> _resourcePoolFixture = new ResourcePool<Fixture>(CreateFixture);

        /*static private Body CreateBody(ResourcePool<Body> pool_)
        {
            return new Body();
        }*/

        static private Fixture CreateFixture(ResourcePool<Fixture> pool)
        {
            return new Fixture();
        }
    }
}
