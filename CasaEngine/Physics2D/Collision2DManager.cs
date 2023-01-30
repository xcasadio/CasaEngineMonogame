using CasaEngine.Math.Shape2D;
using CasaEngine.Math.Collision;
using Microsoft.Xna.Framework;
using CasaEngine.AI.Messaging;
using CasaEngine.Gameplay.Actor;
using CasaEngine.Gameplay;


namespace CasaEngine.Physics2D
{
    public class Collision2DManager
    {

        static private Collision2DManager m_Instance = null;

        private readonly List<IAttackable> m_Objects = new List<IAttackable>();
        private readonly Message m_Message1 = new Message(0, 0, (int)MessageType.Hit, 0, null);
        private readonly Message m_Message2 = new Message(0, 0, (int)MessageType.Hit, 0, null);
        private HitInfo m_HitInfo = new HitInfo();

        //to avoid GC
        private Vector2
            v1 = new Vector2(),
            v2 = new Vector2(),
            contactPoint = new Vector2();



        static public Collision2DManager Instance
        {
            get
            {
                if (m_Instance == null)
                {
                    m_Instance = new Collision2DManager();
                }

                return m_Instance;
            }
        }






        public void RegisterObject(IAttackable object_)
        {
            m_Objects.Add(object_);
        }

        public void UnregisterObject(IAttackable object_)
        {
            m_Objects.Remove(object_);
        }

        public void Update()
        {
            Shape2DObject g1, g2;

            for (int i = 0; i < m_Objects.Count; i++)
            {
                Shape2DObject[] g1List = m_Objects[i].Shape2DObjectList;

                if (g1List == null)
                {
                    continue;
                }

                for (int a = 0; a < g1List.Length; a++)
                {
                    g1 = g1List[a];

                    for (int j = i + 1; j < m_Objects.Count; j++)
                    {
                        Shape2DObject[] g2List = m_Objects[j].Shape2DObjectList;

                        if (g2List == null)
                        {
                            continue;
                        }

                        for (int b = 0; b < g2List.Length; b++)
                        {
                            g2 = g2List[b];

                            if (g1.Flag != g2.Flag)
                            {
                                v1.X = g1.Location.X;
                                v1.Y = g1.Location.Y;

                                v2.X = g2.Location.X;
                                v2.Y = g2.Location.Y;

                                switch (g1.Shape2DType)
                                {
                                    case Shape2DType.Circle:

                                        switch (g2.Shape2DType)
                                        {
                                            case Shape2DType.Circle:

                                                if (Collision2D.CollideCircles(ref contactPoint, (ShapeCircle)g1, ref v1, (ShapeCircle)g2, ref v2) == true)
                                                {
                                                    if (g1.Flag == 0 // defense
                                                        && g2.Flag == 1 // attack
                                                        && m_Objects[j].CanAttackHim(m_Objects[i]) == true)
                                                    {
                                                        SendMessage(m_Objects[j], m_Objects[i], ref contactPoint);
                                                    }
                                                    else if (g1.Flag == 1 // attack
                                                        && g2.Flag == 0
                                                        && m_Objects[i].CanAttackHim(m_Objects[j])) // defense
                                                    {
                                                        SendMessage(m_Objects[i], m_Objects[j], ref contactPoint);
                                                    }
                                                }
                                                break;

                                            case Shape2DType.Polygone:

                                                if (Collision2D.CollidePolygonAndCircle(ref contactPoint, (ShapePolygone)g2, ref v2, (ShapeCircle)g1, ref v1) == true)
                                                {
                                                    if (g1.Flag == 0 // defense
                                                        && g2.Flag == 1 // attack
                                                        && m_Objects[j].CanAttackHim(m_Objects[i]) == true)
                                                    {
                                                        SendMessage(m_Objects[j], m_Objects[i], ref contactPoint);
                                                    }
                                                    else if (g1.Flag == 1 // attack
                                                        && g2.Flag == 0
                                                        && m_Objects[i].CanAttackHim(m_Objects[j])) // defense
                                                    {
                                                        SendMessage(m_Objects[i], m_Objects[j], ref contactPoint);
                                                    }
                                                }
                                                break;

                                            default:
                                                throw new InvalidOperationException();
                                        }
                                        break;

                                    case Shape2DType.Polygone:

                                        switch (g2.Shape2DType)
                                        {
                                            case Shape2DType.Circle:

                                                if (Collision2D.CollidePolygonAndCircle(ref contactPoint, (ShapePolygone)g1, ref v1, (ShapeCircle)g2, ref v2) == true)
                                                {
                                                    if (g1.Flag == 0 // defense
                                                        && g2.Flag == 1 // attack
                                                        && m_Objects[j].CanAttackHim(m_Objects[i]) == true)
                                                    {
                                                        SendMessage(m_Objects[j], m_Objects[i], ref contactPoint);
                                                    }
                                                    else if (g1.Flag == 1 // attack
                                                        && g2.Flag == 0
                                                        && m_Objects[i].CanAttackHim(m_Objects[j])) // defense
                                                    {
                                                        SendMessage(m_Objects[i], m_Objects[j], ref contactPoint);
                                                    }
                                                }
                                                break;

                                            case Shape2DType.Polygone:

                                                if (Collision2D.CollidePolygons((ShapePolygone)g2, ref v2, (ShapePolygone)g1, ref v1) == true)
                                                {
                                                    if (g1.Flag == 0 // defense
                                                        && g2.Flag == 1 // attack
                                                        && m_Objects[j].CanAttackHim(m_Objects[i]) == true)
                                                    {
                                                        SendMessage(m_Objects[j], m_Objects[i], ref contactPoint);
                                                    }
                                                    else if (g1.Flag == 1 // attack
                                                        && g2.Flag == 0
                                                        && m_Objects[i].CanAttackHim(m_Objects[j])) // defense
                                                    {
                                                        SendMessage(m_Objects[i], m_Objects[j], ref contactPoint);
                                                    }
                                                }
                                                break;

                                            case Shape2DType.Rectangle:

                                                if (Collision2D.CollidePolygonAndRectangle((ShapePolygone)g1, ref v1, (ShapeRectangle)g2, ref v2) == true)
                                                {
                                                    if (g1.Flag == 0 // defense
                                                        && g2.Flag == 1 // attack
                                                        && m_Objects[j].CanAttackHim(m_Objects[i]) == true)
                                                    {
                                                        SendMessage(m_Objects[j], m_Objects[i], ref contactPoint);
                                                    }
                                                    else if (g1.Flag == 1 // attack
                                                        && g2.Flag == 0
                                                        && m_Objects[i].CanAttackHim(m_Objects[j])) // defense
                                                    {
                                                        SendMessage(m_Objects[i], m_Objects[j], ref contactPoint);
                                                    }
                                                }
                                                break;

                                            default:
                                                throw new InvalidOperationException();
                                        }
                                        break;

                                    default:
                                        throw new InvalidOperationException();
                                }
                            }
                        }
                    }
                }
            }
        }

        private void SendMessage(IAttackable attacker_, IAttackable hit_, ref Vector2 contactPoint)
        {
            m_HitInfo.ActorHit = (Actor2D)hit_;
            m_HitInfo.ActorAttacking = (Actor2D)attacker_;
            m_HitInfo.Direction = Vector2.Subtract(m_HitInfo.ActorHit.Position, m_HitInfo.ActorAttacking.Position);
            m_HitInfo.Direction.Normalize();
            m_HitInfo.ContactPoint = contactPoint;

            m_Message1.SenderID = -1;
            m_Message1.RecieverID = -1; //hit_.ID
            m_Message1.Type = (int)MessageType.Hit;
            m_Message1.ExtraInfo = m_HitInfo;

            m_Message2.SenderID = -1;
            m_Message2.RecieverID = -1; //attacker_.ID
            m_Message2.Type = (int)MessageType.IHitSomeone;
            m_Message2.ExtraInfo = m_HitInfo;

            hit_.HandleMessage(m_Message1);
            attacker_.HandleMessage(m_Message2);
        }

    }
}
