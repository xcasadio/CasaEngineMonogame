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

        private ShapeRenderer _shapeRenderer;
        Matrix _projectionMatrix, _worldMatrix, _view;
        private FarseerPhysics.Dynamics.World _world;
        private readonly List<DisplayCollisionData> _displayCollisionData = new List<DisplayCollisionData>();

        //to avoid GC
        private readonly Stack<DisplayCollisionData> _freeDisplayCollisionData = new Stack<DisplayCollisionData>();
        private Vector2 _vector2D1 = new Vector2();



        public ShapeRenderer ShapeRenderer => _shapeRenderer;


        public ShapeRendererComponent(Microsoft.Xna.Framework.Game game)
            : base(game)
        {
            if (game == null)
            {
                throw new ArgumentException("ScreenManagerComponent : Game is null");
            }

            game.Components.Add(this);

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

        public void SetCurrentPhysicsWorld(FarseerPhysics.Dynamics.World world)
        {
            _world = world;

            //if (_World != null)
            {
                _shapeRenderer = new ShapeRenderer(world);
            }
        }

        /*public void AddShape2DObject(IEnumerable<Shape2DObject> a_)
        {
            _DisplayCollisionData.AddRange(a_);
        }*/

        public void AddShape2DObject(Shape2DObject g, Color color)
        {
            DisplayCollisionData data = GetDisplayCollisionData();
            data.Shape2DObject = g;
            data.Color = color;
            _displayCollisionData.Add(data);
        }

        public override void Update(GameTime gameTime)
        {
            foreach (DisplayCollisionData d in _displayCollisionData)
            {
                _freeDisplayCollisionData.Push(d);
            }

            _displayCollisionData.Clear();

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            if (_shapeRenderer == null)
            {
                return;
            }

            if (DisplayCollisions == true)
            {
                _shapeRenderer.BeginCustomDraw(ref _projectionMatrix, ref _view, ref _worldMatrix);

                foreach (DisplayCollisionData d in _displayCollisionData)
                {
                    DisplayShape2D(d);
                }

                _shapeRenderer.EndCustomDraw();
            }

            if (DisplayPhysics == true)
            {
                _shapeRenderer.RenderDebugData(ref _projectionMatrix, ref _view, ref _worldMatrix);
            }
        }

        private void DisplayShape2D(DisplayCollisionData data)
        {
            _vector2D1.X = data.Shape2DObject.Location.X;
            _vector2D1.Y = data.Shape2DObject.Location.Y;

            switch (data.Shape2DObject.Shape2DType)
            {
                case Shape2DType.Circle:
                    ShapeCircle c = (ShapeCircle)data.Shape2DObject;
                    _shapeRenderer.DrawCircle(
                        _vector2D1,
                        c.Radius,
                        data.Color);
                    break;

                case Shape2DType.Polygone:
                    ShapePolygone p = (ShapePolygone)data.Shape2DObject;

#if EDITOR
                    Vector2[] vec = p.Points.ToArray();
#else
                    Vector2[] vec = new Vector2[p.Points.Length];
                    p.Points.CopyTo(vec, 0);
#endif

                    for (int i = 0; i < vec.Length; i++)
                    {
                        vec[i] = Vector2.Add(vec[i], _vector2D1);
                    }

                    _shapeRenderer.DrawPolygon(vec, vec.Length, data.Color);

                    break;

                case Shape2DType.Rectangle:
                    ShapeRectangle r = (ShapeRectangle)data.Shape2DObject;

                    Vector2[] vecs = new Vector2[4];
                    float w = r.Width / 2.0f;
                    float h = r.Height / 2.0f;
                    vecs[0] = new Vector2(_vector2D1.X - w, _vector2D1.Y - h);
                    vecs[1] = new Vector2(_vector2D1.X + w, _vector2D1.Y - h);
                    vecs[2] = new Vector2(_vector2D1.X + w, _vector2D1.Y + h);
                    vecs[3] = new Vector2(_vector2D1.X - w, _vector2D1.Y + h);

                    _shapeRenderer.DrawPolygon(vecs, 4, data.Color);
                    break;

                default:
                    throw new InvalidOperationException("ShapeRendererComponent.Draw() : Shape2DType '" + Enum.GetName(typeof(Shape2DType), data.Shape2DObject.Shape2DType) + "' not supported.");
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
            //_BasicEffect.Projection = Matrix.CreateOrthographicOffCenter(lower.X, upper.X, lower.Y, upper.Y, -1, 1);
            float ww = (float)tw / 2.0f;
            float hh = (float)th / 2.0f;

            //pour jeux utilisant le spritebatch (screen coordinate)
            _projectionMatrix = Matrix.CreateOrthographicOffCenter(
                0, ww * 2,
                -hh * 2, 0,
                0f, 1f);

            _worldMatrix = Matrix.CreateScale(new Vector3(1, -1, 1));
            _view = Matrix.Identity;

            //jeux utilisant le world coordinate (3D)
            //_BasicEffect.Projection = Matrix.CreateOrthographicOffCenter(0, ww * 2, 0, hh * 2, 0f, 1f);
        }

        private DisplayCollisionData GetDisplayCollisionData()
        {
            if (_freeDisplayCollisionData.Count > 0)
            {
                return _freeDisplayCollisionData.Pop();
            }

            return new DisplayCollisionData();
        }
    }
}
