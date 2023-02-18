using CasaEngine.Framework.Assets.Graphics2D;
using Microsoft.Xna.Framework;

namespace CasaEngine.Engine.Physics2D
{
    public sealed class Sprite2DPhysicsManager
    {
        private static Sprite2DPhysicsManager _instance;

        private HashSet<Sprite2D> _physicToAddInWorld = new();
        private Vector2 _vector2Tmp = new();

        public static Sprite2DPhysicsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Sprite2DPhysicsManager();
                }

                return _instance;
            }
        }

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

            _Vector2Tmp.X = g_.Location.X;
            _Vector2Tmp.Y = g_.Location.Y;
            body_.position = _Vector2Tmp;
            body_.UserData = g_;
            item.AttachToBody(body_, shape);

            return itemAccesor;
        }*/

        // Pool for this type of components.
        /*private static readonly Pool<Fixture> componentPool = new Pool<Fixture>(20);

        internal static Pool<Fixture> ComponentPool { get { return componentPool; } }*/

        /*public void AddSprite2DPhysicsToWorldByID(uint id_)
        {
            _PhysicToAddInWorld.Add(GameInfo.Instance.Asset2DManager.GetSprite2DByID(id_));
        }

        public void AddPhysicsToWorld(FarseerPhysics.Dynamics.World world_)
        {
            foreach (Sprite2D sprite2D in _PhysicToAddInWorld)
            {
                AddSprite2DPhysicsToWorld(world_, sprite2D);
            }

            _PhysicToAddInWorld.Clear();
        }

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

        private PoolItem<BodyDef> CreateBodyDefFromShape2DObject(Shape2DObject g_)
        {
            PoolItem<BodyDef> p = _ResourcePool_BodyDef.GetItem();
            BodyDef bd = p.Resource;

            SetBodyDefFromShape2DObject(ref bd, g_);

            return p;
        }

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

        private static BodyDef CreateBodyDef(ResourcePool<BodyDef> pool)
        {
            return new BodyDef();
        }*/
    }
}
