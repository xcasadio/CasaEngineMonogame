using FarseerPhysics.Common;
using CasaEngine.Math.Shape2D;
using Microsoft.Xna.Framework;
using FarseerPhysics.Collision.Shapes;
using FarseerPhysics.Collision;

namespace CasaEngine.Math.Collision
{
    static public class Collision2D
    {

        //to avoid GC
        static Manifold manifold = new Manifold();
        static Transform transformA = new Transform(), transformB = new Transform();
        static readonly CircleShape circleShape1 = new CircleShape(1.0f, 1.0f);
        static readonly CircleShape circleShape2 = new CircleShape(1.0f, 1.0f);
        static readonly PolygonShape polygonShape1 = new PolygonShape(1.0f);
        static readonly PolygonShape polygonShape2 = new PolygonShape(1.0f);


        static public bool CollidePolygons(ShapePolygone p1, ref Vector2 pos1, ShapePolygone p2, ref Vector2 pos2)
        {
            if (p1.IsABox == true)
            {
                polygonShape1.SetAsBox(
                    System.Math.Abs((p1.Points[0].X - p1.Points[1].X) / 2.0f),
                    System.Math.Abs((p1.Points[0].Y - p1.Points[1].Y) / 2.0f));
            }
            else
            {
                polygonShape1.Set(new Vertices(p1.Points));
            }

            if (p1.IsABox == true)
            {
                polygonShape2.SetAsBox(
                    System.Math.Abs((p2.Points[0].X - p2.Points[1].X) / 2.0f),
                    System.Math.Abs((p2.Points[0].Y - p2.Points[1].Y) / 2.0f));
            }
            else
            {
                polygonShape2.Set(new Vertices(p2.Points));
            }

            transformA.Set(pos1, 0.0f);
            transformB.Set(pos2, 0.0f);

            FarseerPhysics.Collision.Collision.CollidePolygonAndCircle(ref manifold, polygonShape1, ref transformA, circleShape2, ref transformB);

            if (manifold.PointCount > 0)
            {
                //contactPoint_ = manifold.Points[0].LocalPoint;
                return true;
            }

            return false;
        }

        static public bool CollidePolygonAndRectangle(ShapePolygone p1, ref Vector2 pos1, ShapeRectangle r2, ref Vector2 pos2)
        {
            Vertices v = new Vertices(p1.Points);
            //v.Translate(ref pos1);
            polygonShape1.Set(v);
            transformA.Set(pos1, 0.0f);

            polygonShape2.SetAsBox(r2.Width / 2, r2.Height / 2);
            transformB.Set(pos2, 0.0f);

            FarseerPhysics.Collision.Collision.CollidePolygonAndCircle(ref manifold, polygonShape1, ref transformA, circleShape2, ref transformB);

            if (manifold.PointCount > 0)
            {
                //contactPoint_ = manifold.Points[0].LocalPoint;
                return true;
            }

            return false;
        }

        static public bool CollidePolygonAndCircle(ref Vector2 contactPoint_, ShapePolygone p1, ref Vector2 pos1, ShapeCircle c2, ref Vector2 pos2)
        {
            polygonShape1.Set(new Vertices(p1.Points));
            circleShape2.Radius = c2.Radius;
            transformA.Set(pos1, 0.0f);
            transformB.Set(pos2, 0.0f);

            FarseerPhysics.Collision.Collision.CollidePolygonAndCircle(ref manifold, polygonShape1, ref transformA, circleShape2, ref transformB);

            if (manifold.PointCount > 0)
            {
                contactPoint_ = manifold.Points[0].LocalPoint;
                return true;
            }

            return false;
        }

        static public bool CollideEdgeAndCircle(ref ShapeLine l1, ref Vector2 pos1, ref ShapeCircle c2, ref Vector2 pos2)
        {
            throw new NotImplementedException();
            return true;
        }

        static public bool CollideEdgeAndPolygon(ref ShapeLine l1, ref Vector2 pos1, ref ShapePolygone p2, ref Vector2 pos2)
        {
            throw new NotImplementedException();
            return true;
        }

        static public bool CollideCircles(ref Vector2 contactPoint_, ShapeCircle c1, ref Vector2 pos1, ShapeCircle c2, ref Vector2 pos2)
        {
            circleShape1.Radius = c1.Radius;
            circleShape2.Radius = c2.Radius;
            transformA.Set(pos1, 0.0f);
            transformB.Set(pos2, 0.0f);

            FarseerPhysics.Collision.Collision.CollideCircles(ref manifold, circleShape1, ref transformA, circleShape2, ref transformB);

            if (manifold.PointCount > 0)
            {
                contactPoint_ = manifold.Points[0].LocalPoint;
                return true;
            }

            return false;
        }

    }
}
