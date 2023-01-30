using CasaEngineCommon.Pool;
using FarseerPhysics.Dynamics;

namespace CasaEngine.Physics2D
{
    static public class PhysicsObjectPool
    {
        //static private ResourcePool<Body> m_ResourcePoolBody = new ResourcePool<Body>(CreateBody);
        static private ResourcePool<Fixture> m_ResourcePoolFixture = new ResourcePool<Fixture>(CreateFixture);

        /*static private Body CreateBody(ResourcePool<Body> pool_)
        {
            return new Body();
        }*/

        static private Fixture CreateFixture(ResourcePool<Fixture> pool_)
        {
            return new Fixture();
        }
    }
}
