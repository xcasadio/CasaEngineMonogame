using Microsoft.Xna.Framework;
using CasaEngine.Game;
using CasaEngine.Math.Shape2D;

namespace CasaEngine.Helper
{
    public class ShapeRendererComponent
        : Microsoft.Xna.Framework.DrawableGameComponent
    {
        private struct DisplayCollisionData
        {
            public Shape2DObject Shape2DObject;
            public Color Color;
        }


        static public bool DisplayCollisions = true;
        static public bool DisplayPhysics = false;

        private ShapeRenderer m_ShapeRenderer;
        Matrix m_ProjectionMatrix, m_WorldMatrix, m_View;
        private FarseerPhysics.Dynamics.World m_World;
        private readonly List<DisplayCollisionData> m_DisplayCollisionData = new List<DisplayCollisionData>();

        //to avoid GC
        private readonly Stack<DisplayCollisionData> m_FreeDisplayCollisionData = new Stack<DisplayCollisionData>();
        private Vector2 vector2D1 = new Vector2();



        public ShapeRenderer ShapeRenderer => m_ShapeRenderer;


        public ShapeRendererComponent(Microsoft.Xna.Framework.Game game_)
            : base(game_)
        {
            if (game_ == null)
            {
                throw new ArgumentException("ScreenManagerComponent : Game is null");
            }

            game_.Components.Add(this);

            //Enabled = false;
            //Visible = false;

            UpdateOrder = (int)ComponentUpdateOrder.DebugPhysics;
            DrawOrder = (int)ComponentDrawOrder.DebugPhysics;
        }



        protected override void LoadContent()
        {
            Resize(0, 0);
            base.LoadContent();
        }

        public void SetCurrentPhysicsWorld(FarseerPhysics.Dynamics.World world_)
        {
            m_World = world_;

            //if (m_World != null)
            {
                m_ShapeRenderer = new ShapeRenderer(world_);
            }
        }

        /*public void AddShape2DObject(IEnumerable<Shape2DObject> a_)
        {
            m_DisplayCollisionData.AddRange(a_);
        }*/

        public void AddShape2DObject(Shape2DObject g_, Color color_)
        {
            DisplayCollisionData data = GetDisplayCollisionData();
            data.Shape2DObject = g_;
            data.Color = color_;
            m_DisplayCollisionData.Add(data);
        }

        public override void Update(GameTime gameTime)
        {
            foreach (DisplayCollisionData d in m_DisplayCollisionData)
            {
                m_FreeDisplayCollisionData.Push(d);
            }

            m_DisplayCollisionData.Clear();

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            if (m_ShapeRenderer == null)
            {
                return;
            }

            if (DisplayCollisions == true)
            {
                m_ShapeRenderer.BeginCustomDraw(ref m_ProjectionMatrix, ref m_View, ref m_WorldMatrix);

                foreach (DisplayCollisionData d in m_DisplayCollisionData)
                {
                    DisplayShape2D(d);
                }

                m_ShapeRenderer.EndCustomDraw();
            }

            if (DisplayPhysics == true)
            {
                m_ShapeRenderer.RenderDebugData(ref m_ProjectionMatrix, ref m_View, ref m_WorldMatrix);
            }
        }

        private void DisplayShape2D(DisplayCollisionData data_)
        {
            vector2D1.X = data_.Shape2DObject.Location.X;
            vector2D1.Y = data_.Shape2DObject.Location.Y;

            switch (data_.Shape2DObject.Shape2DType)
            {
                case Shape2DType.Circle:
                    ShapeCircle c = (ShapeCircle)data_.Shape2DObject;
                    m_ShapeRenderer.DrawCircle(
                        vector2D1,
                        c.Radius,
                        data_.Color);
                    break;

                case Shape2DType.Polygone:
                    ShapePolygone p = (ShapePolygone)data_.Shape2DObject;

#if EDITOR
                    Vector2[] vec = p.Points.ToArray();
#else
                    Vector2[] vec = new Vector2[p.Points.Length];
                    p.Points.CopyTo(vec, 0);
#endif

                    for (int i = 0; i < vec.Length; i++)
                    {
                        vec[i] = Vector2.Add(vec[i], vector2D1);
                    }

                    m_ShapeRenderer.DrawPolygon(vec, vec.Length, data_.Color);

                    break;

                case Shape2DType.Rectangle:
                    ShapeRectangle r = (ShapeRectangle)data_.Shape2DObject;

                    Vector2[] vecs = new Vector2[4];
                    float w = r.Width / 2.0f;
                    float h = r.Height / 2.0f;
                    vecs[0] = new Vector2(vector2D1.X - w, vector2D1.Y - h);
                    vecs[1] = new Vector2(vector2D1.X + w, vector2D1.Y - h);
                    vecs[2] = new Vector2(vector2D1.X + w, vector2D1.Y + h);
                    vecs[3] = new Vector2(vector2D1.X - w, vector2D1.Y + h);

                    m_ShapeRenderer.DrawPolygon(vecs, 4, data_.Color);
                    break;

                default:
                    throw new InvalidOperationException("ShapeRendererComponent.Draw() : Shape2DType '" + Enum.GetName(typeof(Shape2DType), data_.Shape2DObject.Shape2DType) + "' not supported.");
            }
        }


        public void Resize(int w, int h)
        {
            /*width = w;
            height = h;*/

            int tw = GraphicsDevice.Viewport.Width;
            int th = GraphicsDevice.Viewport.Height;
            int x = GraphicsDevice.Viewport.X;
            int y = GraphicsDevice.Viewport.Y;

            float ratio = (float)tw / (float)th;

            Vector2 extents = new Vector2(ratio * 25.0f, 25.0f);
            //extents *= viewZoom;
            Vector2 viewCenter = new Vector2(0.0f, 20.0f);
            Vector2 lower = viewCenter - extents;
            Vector2 upper = viewCenter + extents;

            // L/R/B/T
            //m_BasicEffect.Projection = Matrix.CreateOrthographicOffCenter(lower.X, upper.X, lower.Y, upper.Y, -1, 1);
            float ww = (float)tw / 2.0f;
            float hh = (float)th / 2.0f;

            //pour jeux utilisant le spritebatch (screen coordinate)
            m_ProjectionMatrix = Matrix.CreateOrthographicOffCenter(
                0, ww * 2,
                -hh * 2, 0,
                0f, 1f);

            m_WorldMatrix = Matrix.CreateScale(new Vector3(1, -1, 1));
            m_View = Matrix.Identity;

            //jeux utilisant le world coordinate (3D)
            //m_BasicEffect.Projection = Matrix.CreateOrthographicOffCenter(0, ww * 2, 0, hh * 2, 0f, 1f);
        }

        private DisplayCollisionData GetDisplayCollisionData()
        {
            if (m_FreeDisplayCollisionData.Count > 0)
            {
                return m_FreeDisplayCollisionData.Pop();
            }

            return new DisplayCollisionData();
        }
    }
}
