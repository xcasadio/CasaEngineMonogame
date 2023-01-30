using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


namespace CasaEngine.Graphics2D.Tile
{
    public abstract
#if EDITOR
    partial
#endif
    class TileLayer
    {

        private GraphicsDeviceManager m_Graphics;
        /*private Renderer2DComponent m_Renderer2DComponent = null;*/

        private Vector2 worldOffset;
        protected bool visibilityChanged;

        //drawing parameters
        private Vector2 cameraPositionValue;
        private float zoomValue;
        //private Vector2 scaleValue;
        private float rotationValue;
        private Matrix rotationMatrix;
        private Vector2 displaySize;
        private Color layerColor = Color.White;



        /*public Renderer2DComponent Renderer2DComponent
		{
#if EDITOR
			protected 
#endif
			get { return m_Renderer2DComponent; }
#if EDITOR
			set { m_Renderer2DComponent = value; }
#endif
		}*/

        protected Vector2 DisplaySize => displaySize;

        protected Matrix RotationMatrix => rotationMatrix;

        public Vector2 CameraPosition
        {
            set
            {
                cameraPositionValue = value;
                visibilityChanged = true;
            }
            get => cameraPositionValue;
        }

        public float CameraRotation
        {
            set
            {
                rotationValue = value;
                rotationMatrix = Matrix.CreateRotationZ(rotationValue);
                visibilityChanged = true;
            }
            get => rotationValue;
        }

        public float CameraZoom
        {
            set
            {
                zoomValue = value;
                visibilityChanged = true;
            }
            get => zoomValue;
        }

        public Color Color
        {
            set => layerColor = value;
            get => layerColor;
        }

        public Vector2 WorldOffset
        {
            set
            {
                worldOffset = value;
                visibilityChanged = true;
            }
            get => worldOffset;
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

            m_Graphics = graphicsComponent;
            /*m_Renderer2DComponent = Renderer2DComponent_;*/

            InitializeGraphics();

            worldOffset = offset;

            //scaleValue = Vector2.One;
            zoomValue = 1.0f;
            CameraPosition = Vector2.Zero;
        }



        private void InitializeGraphics()
        {
            m_Graphics.DeviceReset +=
                new EventHandler<EventArgs>(OnGraphicsComponentDeviceReset);

            OnGraphicsComponentDeviceReset(this, new EventArgs());
        }

        void OnGraphicsComponentDeviceReset(object sender, EventArgs e)
        {
            displaySize.X =
                m_Graphics.GraphicsDevice.PresentationParameters.BackBufferWidth;

            displaySize.Y =
                m_Graphics.GraphicsDevice.PresentationParameters.BackBufferHeight;

            visibilityChanged = true;
        }



        protected virtual void DetermineVisibility()
        {
            visibilityChanged = false;
        }

        public void Update()
        {
            if (visibilityChanged) DetermineVisibility();
        }

        protected abstract void DrawTiles(SpriteBatch batch);

        public void Draw(SpriteBatch batch)
        {
            DrawTiles(batch);
        }



    }
}
