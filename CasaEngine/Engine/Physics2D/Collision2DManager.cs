using Microsoft.Xna.Framework;
using CasaEngine.Core.Maths.Shape2D;
using CasaEngine.Framework.AI.Messaging;
using CasaEngine.Framework.Gameplay.Actor;
using CasaEngine.Framework.Gameplay;

namespace CasaEngine.Engine.Physics2D
{
    public class Collision2DManager
    {

        private static Collision2DManager _instance;

        private readonly List<IAttackable> _objects = new();
        private readonly Message _message1 = new(0, 0, (int)MessageType.Hit, 0, null);
        private readonly Message _message2 = new(0, 0, (int)MessageType.Hit, 0, null);
        private HitInfo _hitInfo;

        //to avoid GC
        private Vector2
            _v1,
            _v2,
            _contactPoint;



        public static Collision2DManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Collision2DManager();
                }

                return _instance;
            }
        }






        public void RegisterObject(IAttackable @object)
        {
            _objects.Add(@object);
        }

        public void UnregisterObject(IAttackable @object)
        {
            _objects.Remove(@object);
        }

        public void Update()
        {
            Shape2DObject g1, g2;

            for (var i = 0; i < _objects.Count; i++)
            {
                var g1List = _objects[i].Shape2DObjectList;

                if (g1List == null)
                {
                    continue;
                }

                for (var a = 0; a < g1List.Length; a++)
                {
                    g1 = g1List[a];

                    for (var j = i + 1; j < _objects.Count; j++)
                    {
                        var g2List = _objects[j].Shape2DObjectList;

                        if (g2List == null)
                        {
                            continue;
                        }

                        for (var b = 0; b < g2List.Length; b++)
                        {
                            g2 = g2List[b];

                            if (g1.Flag != g2.Flag)
                            {
                                _v1.X = g1.Location.X;
                                _v1.Y = g1.Location.Y;

                                _v2.X = g2.Location.X;
                                _v2.Y = g2.Location.Y;

                                switch (g1.Shape2DType)
                                {
                                    case Shape2DType.Circle:

                                        switch (g2.Shape2DType)
                                        {
                                            case Shape2DType.Circle:

                                                if (Collision2D.CollideCircles(ref _contactPoint, (ShapeCircle)g1, ref _v1, (ShapeCircle)g2, ref _v2))
                                                {
                                                    if (g1.Flag == 0 // defense
                                                        && g2.Flag == 1 // attack
                                                        && _objects[j].CanAttackHim(_objects[i]))
                                                    {
                                                        SendMessage(_objects[j], _objects[i], ref _contactPoint);
                                                    }
                                                    else if (g1.Flag == 1 // attack
                                                        && g2.Flag == 0
                                                        && _objects[i].CanAttackHim(_objects[j])) // defense
                                                    {
                                                        SendMessage(_objects[i], _objects[j], ref _contactPoint);
                                                    }
                                                }
                                                break;

                                            case Shape2DType.Polygone:

                                                if (Collision2D.CollidePolygonAndCircle(ref _contactPoint, (ShapePolygone)g2, ref _v2, (ShapeCircle)g1, ref _v1))
                                                {
                                                    if (g1.Flag == 0 // defense
                                                        && g2.Flag == 1 // attack
                                                        && _objects[j].CanAttackHim(_objects[i]))
                                                    {
                                                        SendMessage(_objects[j], _objects[i], ref _contactPoint);
                                                    }
                                                    else if (g1.Flag == 1 // attack
                                                        && g2.Flag == 0
                                                        && _objects[i].CanAttackHim(_objects[j])) // defense
                                                    {
                                                        SendMessage(_objects[i], _objects[j], ref _contactPoint);
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

                                                if (Collision2D.CollidePolygonAndCircle(ref _contactPoint, (ShapePolygone)g1, ref _v1, (ShapeCircle)g2, ref _v2))
                                                {
                                                    if (g1.Flag == 0 // defense
                                                        && g2.Flag == 1 // attack
                                                        && _objects[j].CanAttackHim(_objects[i]))
                                                    {
                                                        SendMessage(_objects[j], _objects[i], ref _contactPoint);
                                                    }
                                                    else if (g1.Flag == 1 // attack
                                                        && g2.Flag == 0
                                                        && _objects[i].CanAttackHim(_objects[j])) // defense
                                                    {
                                                        SendMessage(_objects[i], _objects[j], ref _contactPoint);
                                                    }
                                                }
                                                break;

                                            case Shape2DType.Polygone:

                                                if (Collision2D.CollidePolygons((ShapePolygone)g2, ref _v2, (ShapePolygone)g1, ref _v1))
                                                {
                                                    if (g1.Flag == 0 // defense
                                                        && g2.Flag == 1 // attack
                                                        && _objects[j].CanAttackHim(_objects[i]))
                                                    {
                                                        SendMessage(_objects[j], _objects[i], ref _contactPoint);
                                                    }
                                                    else if (g1.Flag == 1 // attack
                                                        && g2.Flag == 0
                                                        && _objects[i].CanAttackHim(_objects[j])) // defense
                                                    {
                                                        SendMessage(_objects[i], _objects[j], ref _contactPoint);
                                                    }
                                                }
                                                break;

                                            case Shape2DType.Rectangle:

                                                if (Collision2D.CollidePolygonAndRectangle((ShapePolygone)g1, ref _v1, (ShapeRectangle)g2, ref _v2))
                                                {
                                                    if (g1.Flag == 0 // defense
                                                        && g2.Flag == 1 // attack
                                                        && _objects[j].CanAttackHim(_objects[i]))
                                                    {
                                                        SendMessage(_objects[j], _objects[i], ref _contactPoint);
                                                    }
                                                    else if (g1.Flag == 1 // attack
                                                        && g2.Flag == 0
                                                        && _objects[i].CanAttackHim(_objects[j])) // defense
                                                    {
                                                        SendMessage(_objects[i], _objects[j], ref _contactPoint);
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

        private void SendMessage(IAttackable attacker, IAttackable hit, ref Vector2 contactPoint)
        {
            _hitInfo.ActorHit = (Actor2D)hit;
            _hitInfo.ActorAttacking = (Actor2D)attacker;
            _hitInfo.Direction = Vector2.Subtract(_hitInfo.ActorHit.Position, _hitInfo.ActorAttacking.Position);
            _hitInfo.Direction.Normalize();
            _hitInfo.ContactPoint = contactPoint;

            _message1.SenderID = -1;
            _message1.RecieverID = -1; //hit_.Id
            _message1.Type = (int)MessageType.Hit;
            _message1.ExtraInfo = _hitInfo;

            _message2.SenderID = -1;
            _message2.RecieverID = -1; //attacker_.Id
            _message2.Type = (int)MessageType.HitSomeone;
            _message2.ExtraInfo = _hitInfo;

            hit.HandleMessage(_message1);
            attacker.HandleMessage(_message2);
        }

    }
}
