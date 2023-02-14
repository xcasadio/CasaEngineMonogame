using System.Diagnostics;
using CasaEngine.Game;
using FarseerPhysics;
using FarseerPhysics.Collision;
using FarseerPhysics.Collision.Shapes;
using FarseerPhysics.Common;
using FarseerPhysics.Controllers;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Dynamics.Contacts;
using FarseerPhysics.Dynamics.Joints;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Helpers
{
    public class ShapeRenderer
        : DebugView, IDisposable
    {

        //Drawing
        private PrimitiveBatch _primitiveBatch;
        private readonly Vector2[] _tempVertices = new Vector2[Settings.MaxPolygonVertices];
        private List<StringData> _stringData;

        private Matrix _localProjection;
        private Matrix _localView;

        GraphicsDevice _device;
        SpriteBatch _batch;
        SpriteFont _font;

        //Shapes
        public Color DefaultShapeColor = new(0.9f, 0.7f, 0.7f);
        public Color InactiveShapeColor = new(0.5f, 0.5f, 0.3f);
        public Color KinematicShapeColor = new(0.5f, 0.5f, 0.9f);
        public Color SleepingShapeColor = new(0.6f, 0.6f, 0.6f);
        public Color StaticShapeColor = new(0.5f, 0.9f, 0.5f);
        public Color TextColor = Color.White;

        //Contacts
        private int _pointCount;
        private const int MaxContactPoints = 2048;
        private readonly ContactPoint[] _points = new ContactPoint[MaxContactPoints];

        //Debug panel
#if XBOX
        public Vector2 DebugPanelPosition = new Vector2(55, 100);
#else
        public Vector2 DebugPanelPosition = new(40, 100);
#endif
        private int _max;
        private int _avg;
        private int _min;

        //Performance graph
        public bool AdaptiveLimits = true;
        public int ValuesToGraph = 500;
        public int MinimumValue;
        public int MaximumValue = 1000;
        private readonly List<float> _graphValues = new();

#if XBOX
        public Rectangle PerformancePanelBounds = new Rectangle(265, 100, 200, 100);
#else
        public Rectangle PerformancePanelBounds = new(250, 100, 200, 100);
#endif
        private readonly Vector2[] _background = new Vector2[4];
        public bool Enabled = true;

#if XBOX || WINDOWS_PHONE
        public const int CircleSegments = 16;
#else
        public const int CircleSegments = 32;
#endif





        public ShapeRenderer(FarseerPhysics.Dynamics.World world)
            : base(world)
        {
            if (world != null)
            {
                world.ContactManager.PreSolve += PreSolve;

                EnableOrDisableFlag(DebugViewFlags.Shape);
                EnableOrDisableFlag(DebugViewFlags.Joint);
                EnableOrDisableFlag(DebugViewFlags.PolygonPoints);
                /*EnableOrDisableFlag(DebugViewFlags.DebugPanel);
                EnableOrDisableFlag(DebugViewFlags.PerformanceGraph);            
                EnableOrDisableFlag(DebugViewFlags.ContactPoints);
                EnableOrDisableFlag(DebugViewFlags.ContactNormals);
                EnableOrDisableFlag(DebugViewFlags.Controllers);            
                EnableOrDisableFlag(DebugViewFlags.CenterOfMass);
                EnableOrDisableFlag(DebugViewFlags.AABB);*/
            }

            DefaultShapeColor = Color.White;
            SleepingShapeColor = Color.LightGray;
            LoadContent(Engine.Instance.Game.GraphicsDevice, Engine.Instance.SpriteBatch, Engine.Instance.DefaultSpriteFont);

            Settings.EnableDiagnostics = true;
        }



        private void EnableOrDisableFlag(DebugViewFlags flag)
        {
            if ((Flags & flag) == flag)
            {
                RemoveFlags(flag);
            }
            else
            {
                AppendFlags(flag);
            }
        }

        public void BeginCustomDraw(ref Matrix projection, ref Matrix view, ref Matrix world)
        {
            _primitiveBatch.Begin(ref projection, ref view, ref world);
        }

        public void EndCustomDraw()
        {
            _primitiveBatch.End();
        }


        public void Dispose()
        {
            World.ContactManager.PreSolve -= PreSolve;
        }


        private void PreSolve(Contact contact, ref Manifold oldManifold)
        {
            if ((Flags & DebugViewFlags.ContactPoints) == DebugViewFlags.ContactPoints)
            {
                var manifold = contact.Manifold;

                if (manifold.PointCount == 0)
                {
                    return;
                }

                var fixtureA = contact.FixtureA;

                FixedArray2<PointState> state1, state2;
                Collision.GetPointStates(out state1, out state2, ref oldManifold, ref manifold);

                FixedArray2<Vector2> points;
                Vector2 normal;
                contact.GetWorldManifold(out normal, out points);

                for (var i = 0; i < manifold.PointCount && _pointCount < MaxContactPoints; ++i)
                {
                    if (fixtureA == null)
                    {
                        _points[i] = new ContactPoint();
                    }
                    var cp = _points[_pointCount];
                    cp.Position = points[i];
                    cp.Normal = normal;
                    cp.State = state2[i];
                    _points[_pointCount] = cp;
                    ++_pointCount;
                }
            }
        }

        private void DrawDebugData()
        {
            if ((Flags & DebugViewFlags.Shape) == DebugViewFlags.Shape)
            {
                foreach (var b in World.BodyList)
                {
                    Transform xf;
                    b.GetTransform(out xf);
                    foreach (var f in b.FixtureList)
                    {
                        if (b.Enabled == false)
                        {
                            DrawShape(f, xf, InactiveShapeColor);
                        }
                        else if (b.BodyType == BodyType.Static)
                        {
                            DrawShape(f, xf, StaticShapeColor);
                        }
                        else if (b.BodyType == BodyType.Kinematic)
                        {
                            DrawShape(f, xf, KinematicShapeColor);
                        }
                        else if (b.Awake == false)
                        {
                            DrawShape(f, xf, SleepingShapeColor);
                        }
                        else
                        {
                            DrawShape(f, xf, DefaultShapeColor);
                        }
                    }
                }
            }
            if ((Flags & DebugViewFlags.ContactPoints) == DebugViewFlags.ContactPoints)
            {
                const float axisScale = 0.3f;

                for (var i = 0; i < _pointCount; ++i)
                {
                    var point = _points[i];

                    if (point.State == PointState.Add)
                    {
                        // Add
                        DrawPoint(point.Position, 0.1f, new Color(0.3f, 0.95f, 0.3f));
                    }
                    else if (point.State == PointState.Persist)
                    {
                        // Persist
                        DrawPoint(point.Position, 0.1f, new Color(0.3f, 0.3f, 0.95f));
                    }

                    if ((Flags & DebugViewFlags.ContactNormals) == DebugViewFlags.ContactNormals)
                    {
                        var p1 = point.Position;
                        var p2 = p1 + axisScale * point.Normal;
                        DrawSegment(p1, p2, new Color(0.4f, 0.9f, 0.4f));
                    }
                }
                _pointCount = 0;
            }
            if ((Flags & DebugViewFlags.PolygonPoints) == DebugViewFlags.PolygonPoints)
            {
                foreach (var body in World.BodyList)
                {
                    foreach (var f in body.FixtureList)
                    {
                        var polygon = f.Shape as PolygonShape;
                        if (polygon != null)
                        {
                            Transform xf;
                            body.GetTransform(out xf);

                            for (var i = 0; i < polygon.Vertices.Count; i++)
                            {
                                var tmp = MathUtils.Multiply(ref xf, polygon.Vertices[i]);
                                DrawPoint(tmp, 0.1f, Color.Red);
                            }
                        }
                    }
                }
            }
            if ((Flags & DebugViewFlags.Joint) == DebugViewFlags.Joint)
            {
                foreach (var j in World.JointList)
                {
                    DrawJoint(j);
                }
            }
            if ((Flags & DebugViewFlags.Pair) == DebugViewFlags.Pair)
            {
                var color = new Color(0.3f, 0.9f, 0.9f);
                for (var i = 0; i < World.ContactManager.ContactList.Count; i++)
                {
                    var c = World.ContactManager.ContactList[i];
                    var fixtureA = c.FixtureA;
                    var fixtureB = c.FixtureB;

                    AABB aabbA;
                    fixtureA.GetAABB(out aabbA, 0);
                    AABB aabbB;
                    fixtureB.GetAABB(out aabbB, 0);

                    var cA = aabbA.Center;
                    var cB = aabbB.Center;

                    DrawSegment(cA, cB, color);
                }
            }
            if ((Flags & DebugViewFlags.AABB) == DebugViewFlags.AABB)
            {
                var color = new Color(0.9f, 0.3f, 0.9f);
                var bp = World.ContactManager.BroadPhase;

                foreach (var b in World.BodyList)
                {
                    if (b.Enabled == false)
                    {
                        continue;
                    }

                    foreach (var f in b.FixtureList)
                    {
                        for (var t = 0; t < f.ProxyCount; ++t)
                        {
                            var proxy = f.Proxies[t];
                            AABB aabb;
                            bp.GetFatAABB(proxy.ProxyId, out aabb);

                            DrawAabb(ref aabb, color);
                        }
                    }
                }
            }
            if ((Flags & DebugViewFlags.CenterOfMass) == DebugViewFlags.CenterOfMass)
            {
                foreach (var b in World.BodyList)
                {
                    Transform xf;
                    b.GetTransform(out xf);
                    xf.Position = b.WorldCenter;
                    DrawTransform(ref xf);
                }
            }
            if ((Flags & DebugViewFlags.Controllers) == DebugViewFlags.Controllers)
            {
                for (var i = 0; i < World.ControllerList.Count; i++)
                {
                    var controller = World.ControllerList[i];

                    var buoyancy = controller as BuoyancyController;
                    if (buoyancy != null)
                    {
                        var container = buoyancy.Container;
                        DrawAabb(ref container, Color.LightBlue);
                    }
                }
            }
            if ((Flags & DebugViewFlags.DebugPanel) == DebugViewFlags.DebugPanel)
            {
                DrawDebugPanel();
            }
        }

        private void DrawPerformanceGraph()
        {
            _graphValues.Add(World.UpdateTime);

            if (_graphValues.Count > ValuesToGraph + 1)
            {
                _graphValues.RemoveAt(0);
            }

            float x = PerformancePanelBounds.X;
            var deltaX = PerformancePanelBounds.Width / (float)ValuesToGraph;
            var yScale = PerformancePanelBounds.Bottom - (float)PerformancePanelBounds.Top;

            // we must have at least 2 values to start rendering
            if (_graphValues.Count > 2)
            {
                _max = (int)_graphValues.Max();
                _avg = (int)_graphValues.Average();
                _min = (int)_graphValues.Min();

                if (AdaptiveLimits)
                {
                    MaximumValue = _max;
                    MinimumValue = 0;
                }

                // start at last value (newest value added)
                // continue until no values are left
                for (var i = _graphValues.Count - 1; i > 0; i--)
                {
                    var y1 = PerformancePanelBounds.Bottom -
                             ((_graphValues[i] / (MaximumValue - MinimumValue)) * yScale);
                    var y2 = PerformancePanelBounds.Bottom -
                             ((_graphValues[i - 1] / (MaximumValue - MinimumValue)) * yScale);

                    var x1 =
                        new Vector2(MathHelper.Clamp(x, PerformancePanelBounds.Left, PerformancePanelBounds.Right),
                                    MathHelper.Clamp(y1, PerformancePanelBounds.Top, PerformancePanelBounds.Bottom));

                    var x2 =
                        new Vector2(
                            MathHelper.Clamp(x + deltaX, PerformancePanelBounds.Left, PerformancePanelBounds.Right),
                            MathHelper.Clamp(y2, PerformancePanelBounds.Top, PerformancePanelBounds.Bottom));

                    DrawSegment(x1, x2, Color.LightGreen);

                    x += deltaX;
                }
            }

            DrawString(PerformancePanelBounds.Right + 10, PerformancePanelBounds.Top, "Max: " + _max);
            DrawString(PerformancePanelBounds.Right + 10, PerformancePanelBounds.Center.Y - 7, "Avg: " + _avg);
            DrawString(PerformancePanelBounds.Right + 10, PerformancePanelBounds.Bottom - 15, "Min: " + _min);

            //Draw background.
            _background[0] = new Vector2(PerformancePanelBounds.X, PerformancePanelBounds.Y);
            _background[1] = new Vector2(PerformancePanelBounds.X,
                                         PerformancePanelBounds.Y + PerformancePanelBounds.Height);
            _background[2] = new Vector2(PerformancePanelBounds.X + PerformancePanelBounds.Width,
                                         PerformancePanelBounds.Y + PerformancePanelBounds.Height);
            _background[3] = new Vector2(PerformancePanelBounds.X + PerformancePanelBounds.Width,
                                         PerformancePanelBounds.Y);

            DrawSolidPolygon(_background, 4, Color.DarkGray, true);
        }

        private void DrawDebugPanel()
        {
            var fixtures = 0;
            for (var i = 0; i < World.BodyList.Count; i++)
            {
                fixtures += World.BodyList[i].FixtureList.Count;
            }

            var x = (int)DebugPanelPosition.X;
            var y = (int)DebugPanelPosition.Y;

            DrawString(x, y, "Objects:" +
                             "\n- Bodies: " + World.BodyList.Count +
                             "\n- Fixtures: " + fixtures +
                             "\n- Contacts: " + World.ContactList.Count +
                             "\n- Joints: " + World.JointList.Count +
                             "\n- Controllers: " + World.ControllerList.Count +
                             "\n- Proxies: " + World.ProxyCount);

            DrawString(x + 110, y, "Update time:" +
                                   "\n- Body: " + World.SolveUpdateTime +
                                   "\n- Contact: " + World.ContactsUpdateTime +
                                   "\n- CCD: " + World.ContinuousPhysicsTime +
                                   "\n- Joint: " + World.Island.JointUpdateTime +
                                   "\n- Controller: " + World.ControllersUpdateTime +
                                   "\n- Total: " + World.UpdateTime);
        }

        public void DrawAabb(ref AABB aabb, Color color)
        {
            var verts = new Vector2[4];
            verts[0] = new Vector2(aabb.LowerBound.X, aabb.LowerBound.Y);
            verts[1] = new Vector2(aabb.UpperBound.X, aabb.LowerBound.Y);
            verts[2] = new Vector2(aabb.UpperBound.X, aabb.UpperBound.Y);
            verts[3] = new Vector2(aabb.LowerBound.X, aabb.UpperBound.Y);

            DrawPolygon(verts, 4, color);
        }

        private void DrawJoint(Joint joint)
        {
            if (!joint.Enabled)
            {
                return;
            }

            var b1 = joint.BodyA;
            var b2 = joint.BodyB;
            Transform xf1, xf2;
            b1.GetTransform(out xf1);

            var x2 = Vector2.Zero;

            // WIP David
            if (!joint.IsFixedType())
            {
                b2.GetTransform(out xf2);
                x2 = xf2.Position;
            }

            var p1 = joint.WorldAnchorA;
            var p2 = joint.WorldAnchorB;
            var x1 = xf1.Position;

            var color = new Color(0.5f, 0.8f, 0.8f);

            switch (joint.JointType)
            {
                case JointType.Distance:
                    DrawSegment(p1, p2, color);
                    break;
                case JointType.Pulley:
                    var pulley = (PulleyJoint)joint;
                    var s1 = pulley.GroundAnchorA;
                    var s2 = pulley.GroundAnchorB;
                    DrawSegment(s1, p1, color);
                    DrawSegment(s2, p2, color);
                    DrawSegment(s1, s2, color);
                    break;
                case JointType.FixedMouse:
                    DrawPoint(p1, 0.5f, new Color(0.0f, 1.0f, 0.0f));
                    DrawSegment(p1, p2, new Color(0.8f, 0.8f, 0.8f));
                    break;
                case JointType.Revolute:
                    //DrawSegment(x2, p1, color);
                    DrawSegment(p2, p1, color);
                    DrawSolidCircle(p2, 0.1f, Vector2.Zero, Color.Red);
                    DrawSolidCircle(p1, 0.1f, Vector2.Zero, Color.Blue);
                    break;
                case JointType.FixedAngle:
                    //Should not draw anything.
                    break;
                case JointType.FixedRevolute:
                    DrawSegment(x1, p1, color);
                    DrawSolidCircle(p1, 0.1f, Vector2.Zero, Color.Pink);
                    break;
                case JointType.FixedLine:
                    DrawSegment(x1, p1, color);
                    DrawSegment(p1, p2, color);
                    break;
                case JointType.FixedDistance:
                    DrawSegment(x1, p1, color);
                    DrawSegment(p1, p2, color);
                    break;
                case JointType.FixedPrismatic:
                    DrawSegment(x1, p1, color);
                    DrawSegment(p1, p2, color);
                    break;
                case JointType.Gear:
                    DrawSegment(x1, x2, color);
                    break;
                //case JointType.Weld:
                //    break;
                default:
                    DrawSegment(x1, p1, color);
                    DrawSegment(p1, p2, color);
                    DrawSegment(x2, p2, color);
                    break;
            }
        }

        public void DrawShape(Fixture fixture, Transform xf, Color color)
        {
            switch (fixture.ShapeType)
            {
                case ShapeType.Circle:
                    {
                        var circle = (CircleShape)fixture.Shape;

                        var center = MathUtils.Multiply(ref xf, circle.Position);
                        var radius = circle.Radius;
                        var axis = xf.R.Col1;

                        DrawSolidCircle(center, radius, axis, color);
                    }
                    break;

                case ShapeType.Polygon:
                    {
                        var poly = (PolygonShape)fixture.Shape;
                        var vertexCount = poly.Vertices.Count;
                        Debug.Assert(vertexCount <= Settings.MaxPolygonVertices);

                        for (var i = 0; i < vertexCount; ++i)
                        {
                            _tempVertices[i] = MathUtils.Multiply(ref xf, poly.Vertices[i]);
                        }

                        DrawSolidPolygon(_tempVertices, vertexCount, color);
                    }
                    break;


                case ShapeType.Edge:
                    {
                        var edge = (EdgeShape)fixture.Shape;
                        var v1 = MathUtils.Multiply(ref xf, edge.Vertex1);
                        var v2 = MathUtils.Multiply(ref xf, edge.Vertex2);
                        DrawSegment(v1, v2, color);
                    }
                    break;

                case ShapeType.Loop:
                    {
                        var loop = (LoopShape)fixture.Shape;
                        var count = loop.Vertices.Count;

                        var v1 = MathUtils.Multiply(ref xf, loop.Vertices[count - 1]);
                        DrawCircle(v1, 0.05f, color);
                        for (var i = 0; i < count; ++i)
                        {
                            var v2 = MathUtils.Multiply(ref xf, loop.Vertices[i]);
                            DrawSegment(v1, v2, color);
                            v1 = v2;
                        }
                    }
                    break;
            }
        }

        public override void DrawPolygon(Vector2[] vertices, int count, float red, float green, float blue)
        {
            DrawPolygon(vertices, count, new Color(red, green, blue));
        }

        public void DrawPolygon(Vector2[] vertices, int count, Color color)
        {
            if (!_primitiveBatch.IsReady())
            {
                throw new InvalidOperationException("BeginCustomDraw must be called before drawing anything.");
            }

            Vector2 m, n;

            for (var i = 0; i < count - 1; i++)
            {
                _primitiveBatch.AddVertex(vertices[i], color, PrimitiveType.LineList);
                _primitiveBatch.AddVertex(vertices[i + 1], color, PrimitiveType.LineList);

                n = Vector2.Subtract(vertices[i], vertices[i + 1]);
                n.X = -n.X;
                n.Normalize();
                m = Vector2.Divide(Vector2.Add(vertices[i], vertices[i + 1]), 2.0f);
                n = Vector2.Add(n * 20.0f, m);
                _primitiveBatch.AddVertex(m, color, PrimitiveType.LineList);
                _primitiveBatch.AddVertex(n, color, PrimitiveType.LineList);
            }

            _primitiveBatch.AddVertex(vertices[count - 1], color, PrimitiveType.LineList);
            _primitiveBatch.AddVertex(vertices[0], color, PrimitiveType.LineList);

            n = Vector2.Subtract(vertices[count - 1], vertices[0]);
            n.X = -n.X;
            n.Normalize();
            m = Vector2.Divide(Vector2.Add(vertices[count - 1], vertices[0]), 2.0f);
            n = Vector2.Add(n * 20.0f, m);
            _primitiveBatch.AddVertex(m, color, PrimitiveType.LineList);
            _primitiveBatch.AddVertex(n, color, PrimitiveType.LineList);
        }

        public override void DrawSolidPolygon(Vector2[] vertices, int count, float red, float green, float blue)
        {
            DrawSolidPolygon(vertices, count, new Color(red, green, blue), true);
        }

        public void DrawSolidPolygon(Vector2[] vertices, int count, Color color)
        {
            DrawSolidPolygon(vertices, count, color, true);
        }

        public void DrawSolidPolygon(Vector2[] vertices, int count, Color color, bool outline)
        {
            if (!_primitiveBatch.IsReady())
            {
                throw new InvalidOperationException("BeginCustomDraw must be called before drawing anything.");
            }
            if (count == 2)
            {
                DrawPolygon(vertices, count, color);
                return;
            }

            var colorFill = color * (outline ? 0.5f : 1.0f);

            for (var i = 1; i < count - 1; i++)
            {
                _primitiveBatch.AddVertex(vertices[0], colorFill, PrimitiveType.TriangleList);
                _primitiveBatch.AddVertex(vertices[i], colorFill, PrimitiveType.TriangleList);
                _primitiveBatch.AddVertex(vertices[i + 1], colorFill, PrimitiveType.TriangleList);
            }

            if (outline)
            {
                DrawPolygon(vertices, count, color);
            }
        }

        public override void DrawCircle(Vector2 center, float radius, float red, float green, float blue)
        {
            DrawCircle(center, radius, new Color(red, green, blue));
        }

        public void DrawCircle(Vector2 center, float radius, Color color)
        {
            if (!_primitiveBatch.IsReady())
            {
                throw new InvalidOperationException("BeginCustomDraw must be called before drawing anything.");
            }
            const double increment = Math.PI * 2.0 / CircleSegments;
            var theta = 0.0;

            for (var i = 0; i < CircleSegments; i++)
            {
                var v1 = center + radius * new Vector2((float)Math.Cos(theta), (float)Math.Sin(theta));
                var v2 = center +
                         radius *
                         new Vector2((float)Math.Cos(theta + increment), (float)Math.Sin(theta + increment));

                _primitiveBatch.AddVertex(v1, color, PrimitiveType.LineList);
                _primitiveBatch.AddVertex(v2, color, PrimitiveType.LineList);

                theta += increment;
            }
        }

        public override void DrawSolidCircle(Vector2 center, float radius, Vector2 axis, float red, float green,
                                             float blue)
        {
            DrawSolidCircle(center, radius, axis, new Color(red, green, blue));
        }

        public void DrawSolidCircle(Vector2 center, float radius, Vector2 axis, Color color)
        {
            if (!_primitiveBatch.IsReady())
            {
                throw new InvalidOperationException("BeginCustomDraw must be called before drawing anything.");
            }
            const double increment = Math.PI * 2.0 / CircleSegments;
            var theta = 0.0;

            var colorFill = color * 0.5f;

            var v0 = center + radius * new Vector2((float)Math.Cos(theta), (float)Math.Sin(theta));
            theta += increment;

            for (var i = 1; i < CircleSegments - 1; i++)
            {
                var v1 = center + radius * new Vector2((float)Math.Cos(theta), (float)Math.Sin(theta));
                var v2 = center +
                         radius *
                         new Vector2((float)Math.Cos(theta + increment), (float)Math.Sin(theta + increment));

                _primitiveBatch.AddVertex(v0, colorFill, PrimitiveType.TriangleList);
                _primitiveBatch.AddVertex(v1, colorFill, PrimitiveType.TriangleList);
                _primitiveBatch.AddVertex(v2, colorFill, PrimitiveType.TriangleList);

                theta += increment;
            }
            DrawCircle(center, radius, color);

            DrawSegment(center, center + axis * radius, color);
        }

        public override void DrawSegment(Vector2 start, Vector2 end, float red, float green, float blue)
        {
            DrawSegment(start, end, new Color(red, green, blue));
        }

        public void DrawSegment(Vector2 start, Vector2 end, Color color)
        {
            if (!_primitiveBatch.IsReady())
            {
                throw new InvalidOperationException("BeginCustomDraw must be called before drawing anything.");
            }
            _primitiveBatch.AddVertex(start, color, PrimitiveType.LineList);
            _primitiveBatch.AddVertex(end, color, PrimitiveType.LineList);
        }

        public override void DrawTransform(ref Transform transform)
        {
            const float axisScale = 0.4f;
            var p1 = transform.Position;

            var p2 = p1 + axisScale * transform.R.Col1;
            DrawSegment(p1, p2, Color.Red);

            p2 = p1 + axisScale * transform.R.Col2;
            DrawSegment(p1, p2, Color.Green);
        }

        public void DrawPoint(Vector2 p, float size, Color color)
        {
            var verts = new Vector2[4];
            var hs = size / 2.0f;
            verts[0] = p + new Vector2(-hs, -hs);
            verts[1] = p + new Vector2(hs, -hs);
            verts[2] = p + new Vector2(hs, hs);
            verts[3] = p + new Vector2(-hs, hs);

            DrawSolidPolygon(verts, 4, color, true);
        }

        public void DrawString(int x, int y, string s, params object[] args)
        {
            _stringData.Add(new StringData(x, y, s, args, TextColor));
        }

        public void DrawArrow(Vector2 start, Vector2 end, float length, float width, bool drawStartIndicator,
                              Color color)
        {
            // Draw connection segment between start- and end-point
            DrawSegment(start, end, color);

            // Precalculate halfwidth
            var halfWidth = width / 2;

            // Create directional reference
            var rotation = (start - end);
            rotation.Normalize();

            // Calculate angle of directional vector
            var angle = (float)Math.Atan2(rotation.X, -rotation.Y);
            // Create matrix for rotation
            var rotMatrix = Matrix.CreateRotationZ(angle);
            // Create translation matrix for end-point
            var endMatrix = Matrix.CreateTranslation(end.X, end.Y, 0);

            // Setup arrow end shape
            var verts = new Vector2[3];
            verts[0] = new Vector2(0, 0);
            verts[1] = new Vector2(-halfWidth, -length);
            verts[2] = new Vector2(halfWidth, -length);

            // Rotate end shape
            Vector2.Transform(verts, ref rotMatrix, verts);
            // Translate end shape
            Vector2.Transform(verts, ref endMatrix, verts);

            // Draw arrow end shape
            DrawSolidPolygon(verts, 3, color, false);

            if (drawStartIndicator)
            {
                // Create translation matrix for start
                var startMatrix = Matrix.CreateTranslation(start.X, start.Y, 0);
                // Setup arrow start shape
                var baseVerts = new Vector2[4];
                baseVerts[0] = new Vector2(-halfWidth, length / 4);
                baseVerts[1] = new Vector2(halfWidth, length / 4);
                baseVerts[2] = new Vector2(halfWidth, 0);
                baseVerts[3] = new Vector2(-halfWidth, 0);

                // Rotate start shape
                Vector2.Transform(baseVerts, ref rotMatrix, baseVerts);
                // Translate start shape
                Vector2.Transform(baseVerts, ref startMatrix, baseVerts);
                // Draw start shape
                DrawSolidPolygon(baseVerts, 4, color, false);
            }
        }

        public void RenderDebugData(ref Matrix projection, ref Matrix view, ref Matrix world)
        {
            if (!Enabled)
            {
                return;
            }

            //Nothing is enabled - don't draw the debug view.
            if (Flags == 0)
            {
                return;
            }

            _device.RasterizerState = RasterizerState.CullNone;
            _device.DepthStencilState = DepthStencilState.Default;

            _primitiveBatch.Begin(ref projection, ref view, ref world);
            DrawDebugData();
            _primitiveBatch.End();

            if ((Flags & DebugViewFlags.PerformanceGraph) == DebugViewFlags.PerformanceGraph)
            {
                _primitiveBatch.Begin(ref _localProjection, ref _localView, ref world);
                DrawPerformanceGraph();
                _primitiveBatch.End();
            }

            // begin the sprite batch effect
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            // draw any strings we have
            for (var i = 0; i < _stringData.Count; i++)
            {
                _batch.DrawString(_font, string.Format(_stringData[i].S, _stringData[i].Args),
                                  new Vector2(_stringData[i].X + 1f, _stringData[i].Y + 1f), Color.Black);
                _batch.DrawString(_font, string.Format(_stringData[i].S, _stringData[i].Args),
                                  new Vector2(_stringData[i].X, _stringData[i].Y), _stringData[i].Color);
            }
            // end the sprite batch effect
            _batch.End();

            _stringData.Clear();
        }

        /*public void RenderDebugData(ref Matrix projection)
        {
            if (!Enabled)
            {
                return;
            }
            Matrix view = Matrix.Identity;
            RenderDebugData(ref projection, ref view);
        }*/

        public void LoadContent(GraphicsDevice device, SpriteBatch batch, SpriteFont font)
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            _device = device;
            _batch = batch;
            _primitiveBatch = new PrimitiveBatch(_device, 1000);
            _font = font;
            _stringData = new List<StringData>();

            _localProjection = Matrix.CreateOrthographicOffCenter(0f, _device.Viewport.Width, _device.Viewport.Height,
                                                                  0f, 0f, 1f);
            _localView = Matrix.Identity;
        }



        private struct ContactPoint
        {
            public Vector2 Normal;
            public Vector2 Position;
            public PointState State;
        }



        private struct StringData
        {
            public readonly object[] Args;
            public readonly Color Color;
            public readonly string S;
            public readonly int X;
            public readonly int Y;

            public StringData(int x, int y, string s, object[] args, Color color)
            {
                X = x;
                Y = y;
                S = s;
                Args = args;
                Color = color;
            }
        }

    }
}
