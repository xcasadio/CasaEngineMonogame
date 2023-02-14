using CasaEngine.Core_Systems.Math.Shape2D;
using FarseerPhysics.Collision;
using FarseerPhysics.Collision.Shapes;
using FarseerPhysics.Common;
using Microsoft.Xna.Framework;

namespace CasaEngine.Physics2D
{
    public static class Collision2D
    {

        //to avoid GC
        static Manifold _manifold = new();
        static Transform _transformA = new(), _transformB = new();
        static readonly CircleShape CircleShape1 = new(1.0f, 1.0f);
        static readonly CircleShape CircleShape2 = new(1.0f, 1.0f);
        static readonly PolygonShape PolygonShape1 = new(1.0f);
        static readonly PolygonShape PolygonShape2 = new(1.0f);


        public static bool CollidePolygons(ShapePolygone p1, ref Vector2 pos1, ShapePolygone p2, ref Vector2 pos2)
        {
            if (p1.IsABox)
            {
                PolygonShape1.SetAsBox(
                    Math.Abs((p1.Points[0].X - p1.Points[1].X) / 2.0f),
                    Math.Abs((p1.Points[0].Y - p1.Points[1].Y) / 2.0f));
            }
            else
            {
                PolygonShape1.Set(new Vertices(p1.Points));
            }

            if (p1.IsABox)
            {
                PolygonShape2.SetAsBox(
                    Math.Abs((p2.Points[0].X - p2.Points[1].X) / 2.0f),
                    Math.Abs((p2.Points[0].Y - p2.Points[1].Y) / 2.0f));
            }
            else
            {
                PolygonShape2.Set(new Vertices(p2.Points));
            }

            _transformA.Set(pos1, 0.0f);
            _transformB.Set(pos2, 0.0f);

            Collision.CollidePolygonAndCircle(ref _manifold, PolygonShape1, ref _transformA, CircleShape2, ref _transformB);

            if (_manifold.PointCount > 0)
            {
                //contactPoint_ = manifold.Points[0].LocalPoint;
                return true;
            }

            return false;
        }

        public static bool CollidePolygonAndRectangle(ShapePolygone p1, ref Vector2 pos1, ShapeRectangle r2, ref Vector2 pos2)
        {
            var v = new Vertices(p1.Points);
            //v.Translate(ref pos1);
            PolygonShape1.Set(v);
            _transformA.Set(pos1, 0.0f);

            PolygonShape2.SetAsBox(r2.Width / 2, r2.Height / 2);
            _transformB.Set(pos2, 0.0f);

            Collision.CollidePolygonAndCircle(ref _manifold, PolygonShape1, ref _transformA, CircleShape2, ref _transformB);

            if (_manifold.PointCount > 0)
            {
                //contactPoint_ = manifold.Points[0].LocalPoint;
                return true;
            }

            return false;
        }

        public static bool CollidePolygonAndCircle(ref Vector2 contactPoint, ShapePolygone p1, ref Vector2 pos1, ShapeCircle c2, ref Vector2 pos2)
        {
            PolygonShape1.Set(new Vertices(p1.Points));
            CircleShape2.Radius = c2.Radius;
            _transformA.Set(pos1, 0.0f);
            _transformB.Set(pos2, 0.0f);

            Collision.CollidePolygonAndCircle(ref _manifold, PolygonShape1, ref _transformA, CircleShape2, ref _transformB);

            if (_manifold.PointCount > 0)
            {
                contactPoint = _manifold.Points[0].LocalPoint;
                return true;
            }

            return false;
        }

        public static bool CollideEdgeAndCircle(ref ShapeLine l1, ref Vector2 pos1, ref ShapeCircle c2, ref Vector2 pos2)
        {
            throw new NotImplementedException();
            return true;
        }

        public static bool CollideEdgeAndPolygon(ref ShapeLine l1, ref Vector2 pos1, ref ShapePolygone p2, ref Vector2 pos2)
        {
            throw new NotImplementedException();
            return true;
        }

        public static bool CollideCircles(ref Vector2 contactPoint, ShapeCircle c1, ref Vector2 pos1, ShapeCircle c2, ref Vector2 pos2)
        {
            CircleShape1.Radius = c1.Radius;
            CircleShape2.Radius = c2.Radius;
            _transformA.Set(pos1, 0.0f);
            _transformB.Set(pos2, 0.0f);

            Collision.CollideCircles(ref _manifold, CircleShape1, ref _transformA, CircleShape2, ref _transformB);

            if (_manifold.PointCount > 0)
            {
                contactPoint = _manifold.Points[0].LocalPoint;
                return true;
            }

            return false;
        }

    }
}
