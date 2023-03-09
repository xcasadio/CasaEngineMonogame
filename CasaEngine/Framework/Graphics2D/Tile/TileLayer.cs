using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Graphics2D.Tile
{
    public abstract class TileLayer
    {
        private GraphicsDeviceManager _graphics;
        /*private Renderer2DComponent _Renderer2DComponent = null;*/

        private Vector2 _worldOffset;
        protected bool VisibilityChanged;

        //drawing parameters
        private Vector2 _cameraPositionValue;
        private float _zoomValue;
        //private Vector2 scaleValue;
        private float _rotationValue;
        private Matrix _rotationMatrix;
        private Vector2 _displaySize;
        private Color _layerColor = Color.White;

        /*public Renderer2DComponent Renderer2DComponent
		{
#if EDITOR
			protected 
#endif
			get { return _Renderer2DComponent; }
#if EDITOR
			set { _Renderer2DComponent = value; }
#endif
		}*/

        protected Vector2 DisplaySize => _displaySize;

        protected Matrix RotationMatrix => _rotationMatrix;

        public Vector2 CameraPosition
        {
            set
            {
                _cameraPositionValue = value;
                VisibilityChanged = true;
            }
            get => _cameraPositionValue;
        }

        public float CameraRotation
        {
            set
            {
                _rotationValue = value;
                _rotationMatrix = Matrix.CreateRotationZ(_rotationValue);
                VisibilityChanged = true;
            }
            get => _rotationValue;
        }

        public float CameraZoom
        {
            set
            {
                _zoomValue = value;
                VisibilityChanged = true;
            }
            get => _zoomValue;
        }

        public Color Color
        {
            set => _layerColor = value;
            get => _layerColor;
        }

        public Vector2 WorldOffset
        {
            set
            {
                _worldOffset = value;
                VisibilityChanged = true;
            }
            get => _worldOffset;
        }

        public TileLayer(Vector2 offset, GraphicsDeviceManager graphicsComponent/*, Renderer2DComponent Renderer2DComponent_*/)
        {
#if !EDITOR
            if (graphicsComponent == null)
            {
                throw new ArgumentNullException("TileLayer() : GraphicsDeviceManager is null");
            }
#endif

            /*if (Renderer2DComponent_ == null)
			{
				throw new ArgumentException("TileLayer() : Renderer2DComponent is null");
			}*/

            _graphics = graphicsComponent;
            /*_Renderer2DComponent = Renderer2DComponent_;*/

            InitializeGraphics();

            _worldOffset = offset;

            //scaleValue = Vector2.One;
            _zoomValue = 1.0f;
            CameraPosition = Vector2.Zero;
        }

        private void InitializeGraphics()
        {
            _graphics.DeviceReset += OnGraphicsComponentDeviceReset;
            OnGraphicsComponentDeviceReset(this, EventArgs.Empty);
        }

        private void OnGraphicsComponentDeviceReset(object? sender, EventArgs e)
        {
            _displaySize.X = Game.Engine.Instance.Game.GraphicsDevice.PresentationParameters.BackBufferWidth;
            _displaySize.Y = Game.Engine.Instance.Game.GraphicsDevice.PresentationParameters.BackBufferHeight;
            VisibilityChanged = true;
        }

        protected virtual void DetermineVisibility()
        {
            VisibilityChanged = false;
        }

        public void Update()
        {
            if (VisibilityChanged)
            {
                DetermineVisibility();
            }
        }

        protected abstract void DrawTiles(SpriteBatch batch);

        public void Draw(SpriteBatch batch)
        {
            DrawTiles(batch);
        }

#if EDITOR
        public GraphicsDeviceManager Graphics
        {
            get => _graphics;
            set => _graphics = value;
        }
#endif
    }
}
