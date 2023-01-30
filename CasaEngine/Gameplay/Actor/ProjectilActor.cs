using CasaEngine.Math.Shape2D;
using Microsoft.Xna.Framework;
using FarseerPhysics.Dynamics;
using CasaEngine.AI.Messaging;
using CasaEngine.Helper;
using CasaEngine.Gameplay.Actor.Object;
using CasaEngineCommon.Helper;

namespace CasaEngine.Gameplay.Actor
{
    public class ProjectilActor
        : AnimatedSpriteActor, IAttackable
    {

        private Body m_Body;
        private List<Shape2DObject> m_Shape2DObjectList = new List<Shape2DObject>();
        private TeamInfo m_TeamInfo;

#if !FINAL
        private ShapeRendererComponent m_ShapeRendererComponent;
#endif

        //life?
        //distance Max
        Vector3 m_Start;



        public Shape2DObject[] Shape2DObjectList => m_Shape2DObjectList.ToArray();

        public new Vector2 Position => m_Body.Position;

        public TeamInfo TeamInfo
        {
            get => m_TeamInfo;
            set => m_TeamInfo = value;
        }

        public Actor2D Owner
        {
            get;
            set;
        }

        public Vector2 Velocity
        {
            get;
            set;
        }



        public ProjectilActor()
            : base()
        {
        }

        public ProjectilActor(ProjectilActor src_)
        {
            CopyFrom(src_);
        }



        public override BaseObject Clone()
        {
            return new ProjectilActor(this);
        }

        protected override void CopyFrom(BaseObject ob_)
        {
            base.CopyFrom(ob_);

            ProjectilActor src = ob_ as ProjectilActor;

            //m_Body
            //m_Shape2DObjectList
            m_TeamInfo = src.TeamInfo;
            m_ShapeRendererComponent = src.m_ShapeRendererComponent;
            m_Start = src.m_Start;
            Owner = src.Owner;
            Velocity = src.Velocity;
        }

        public virtual void Initialize(FarseerPhysics.Dynamics.World world_)
        {
            base.Initialize();

            m_Body = new Body(world_);
            m_Body.IsBullet = true;

            m_Body.Restitution = 0.0f;
            m_Body.SleepingAllowed = false;
            m_Body.IgnoreGravity = true;
            m_Body.Friction = 0.0f;
            m_Body.IsStatic = false;
            m_Body.FixedRotation = true;
            m_Body.UserData = world_;
        }

        public void DoANewAttack()
        {

        }

        public bool CanAttackHim(IAttackable other_)
        {
            return true;
        }

        public bool HandleMessage(Message message)
        {
            switch (message.Type)
            {
                case (int)MessageType.Hit:
                    //Hit((HitInfo)message.ExtraInfo);
                    Remove = true;
                    break;

                case (int)MessageType.IHitSomeone:
                    HitInfo hitInfo = (HitInfo)message.ExtraInfo;
                    Remove = true;
                    break;
            }

            return true;
        }

        public override void Update(float elapsedTime_)
        {
            base.Update(elapsedTime_);
            base.Position = m_Body.Position;
            m_Body.LinearVelocity = Velocity;
        }

        public override void Draw(float elapsedTime_)
        {
            base.Draw(elapsedTime_);

            if (ShapeRendererComponent.DisplayCollisions == true)
            {
                Shape2DObject[] geometry2DObjectList = Shape2DObjectList;
                if (geometry2DObjectList != null)
                {
                    foreach (Shape2DObject g in geometry2DObjectList)
                    {
                        m_ShapeRendererComponent.AddShape2DObject(g, g.Flag == 0 ? Color.Green : Color.Red);
                    }
                }
            }
        }

        public void SetTransform(Vector2 position, Vector2 direction_)
        {
            float rot = Vector2Helper.GetAngleBetweenVectors(Vector2.UnitX, direction_);
            m_Body.SetTransform(ref position, rot);
        }

    }
}
