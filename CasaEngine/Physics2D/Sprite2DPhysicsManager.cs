using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Graphics2D;
using FarseerPhysics.Common;
using CasaEngineCommon.Pool;
using CasaEngine.Game;
using Microsoft.Xna.Framework;
using CasaEngine.Math.Shape2D;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Collision.Shapes;
using CasaEngine.Assets.Graphics2D;

namespace CasaEngine.Physics2D
{
    /// <summary>
    /// 
    /// </summary>
    public sealed class Sprite2DPhysicsManager
    {

        private static Sprite2DPhysicsManager m_Instance;
        private HashSet<Sprite2D> m_PhysicToAddInWorld = new HashSet<Sprite2D>();

        private Vector2 m_Vector2Tmp = new Vector2();



        /// <summary>
        /// Gets
        /// </summary>
        static public Sprite2DPhysicsManager Instance
        {
            get
            {
                if (m_Instance == null)
                {
                    m_Instance = new Sprite2DPhysicsManager();
                }

                return m_Instance;
            }
        }






        /// <summary>
        /// 
        /// </summary>
        /// <param name="g_"></param>
        /// <param name="body_"></param>
        /// <returns></returns>
        /*public Pool<Fixture>.Accessor CreateFixtureFromShape2DObject(Shape2DObject g_, Body body_)
        {
            //release
            //componentPool.Release(itemAccesor);

            Pool<Fixture>.Accessor itemAccesor = componentPool.Fetch();
            Fixture item = componentPool[itemAccesor];            
            Shape shape = null;
            
            switch (g_.Shape2DType)
            {
                case Shape2DType.Rectangle:
                    ShapeRectangle r = (ShapeRectangle)g_;                    
                    Vertices rect = PolygonTools.CreateRectangle(r.Width, r.Height);
                    PolygonShape p = new PolygonShape(1f);
                    p.SetAsBox(r.Width, r.Height);
                    shape = p;
                    break;

                case Shape2DType.Circle:
                    ShapeCircle c = (ShapeCircle)g_;
                    shape = new CircleShape(c.Radius, 1f);
                    break;

                default:
                    throw new NotImplementedException();
            }

            m_Vector2Tmp.X = g_.Location.X;
            m_Vector2Tmp.Y = g_.Location.Y;
            body_.Position = m_Vector2Tmp;
            body_.UserData = g_;
            item.AttachToBody(body_, shape);

            return itemAccesor;
        }*/


        // Pool for this type of components.
        /*private static readonly Pool<Fixture> componentPool = new Pool<Fixture>(20);

        /// <summary>
        /// Pool for this type of components.
        /// </summary>
        internal static Pool<Fixture> ComponentPool { get { return componentPool; } }*/


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sprite2D_"></param>
        /*public void AddSprite2DPhysicsToWorldByID(uint id_)
        {
            m_PhysicToAddInWorld.Add(GameInfo.Instance.Asset2DManager.GetSprite2DByID(id_));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="world_"></param>
        public void AddPhysicsToWorld(FarseerPhysics.Dynamics.World world_)
        {
            foreach (Sprite2D sprite2D in m_PhysicToAddInWorld)
            {
                AddSprite2DPhysicsToWorld(world_, sprite2D);
            }

            m_PhysicToAddInWorld.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="world_"></param>
        /// <param name="sprite2D_"></param>
        private void AddSprite2DPhysicsToWorld(FarseerPhysics.Dynamics.World world_, Sprite2D sprite2D_)
        {
            foreach (Shape2DObject ob in sprite2D_.Collisions)
            {
                PoolItem<BodyDef> bodyDef = CreateBodyDefFromShape2DObject(ob);
                Body body = world_.CreateBody(bodyDef.Resource);
                bodyDef.Dispose();
                //sprite2D_.AddBody(body);
            } 
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sprite2D_"></param>
        /// <returns></returns>
        private PoolItem<BodyDef> CreateBodyDefFromShape2DObject(Shape2DObject g_)
        {
            PoolItem<BodyDef> p = m_ResourcePool_BodyDef.GetItem();
            BodyDef bd = p.Resource;

            SetBodyDefFromShape2DObject(ref bd, g_);

            return p;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bodyDef_"></param>
        /// <param name="sprite2D_"></param>
        private void SetBodyDefFromShape2DObject(ref BodyDef bodyDef_, Shape2DObject g_)
        {
            bodyDef_.active = false;
            bodyDef_.allowSleep = true;
            bodyDef_.angle = MathHelper.ToRadians(g_.Rotation);
            bodyDef_.angularDamping = 0.0f;
            bodyDef_.angularVelocity = 0.0f;
            bodyDef_.awake = true;
            bodyDef_.bullet = false;
            bodyDef_.fixedRotation = true;
            bodyDef_.inertiaScale = 1.0f;
            bodyDef_.linearDamping = 0.0f;
            bodyDef_.linearVelocity = Vector2.Zero;
            bodyDef_.position = new Vector2(g_.Location.X, g_.Location.Y);
            bodyDef_.type = BodyType.Static;
            bodyDef_.userData = g_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pool"></param>
        /// <returns></returns>
        private static BodyDef CreateBodyDef(ResourcePool<BodyDef> pool)
        {
            return new BodyDef();
        }*/
    }
}
