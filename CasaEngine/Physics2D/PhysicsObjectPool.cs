using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngineCommon.Pool;
using FarseerPhysics.Dynamics;
using CasaEngine.Game;

namespace CasaEngine.Physics2D
{
    /// <summary>
    /// 
    /// </summary>
    static public class PhysicsObjectPool
    {
        //static private ResourcePool<Body> m_ResourcePoolBody = new ResourcePool<Body>(CreateBody);
        static private ResourcePool<Fixture> m_ResourcePoolFixture = new ResourcePool<Fixture>(CreateFixture);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pool_"></param>
        /// <returns></returns>
        /*static private Body CreateBody(ResourcePool<Body> pool_)
        {
            return new Body();
        }*/

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pool_"></param>
        /// <returns></returns>
        static private Fixture CreateFixture(ResourcePool<Fixture> pool_)
        {
            return new Fixture();
        }
    }
}
