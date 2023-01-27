using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


namespace CasaEngine.Graphics2D.Tile
{
	/// <summary>
	/// 
	/// </summary>
	public abstract
#if EDITOR
	partial
#endif
	class TileLayer
	{
		#region Fields

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

        #endregion

		#region Properties

		/// <summary>
		/// Gets/Sets(Editor only)
		/// </summary>
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

		/// <summary>
		/// Gets
		/// </summary>
		protected Vector2 DisplaySize
		{
			get { return displaySize; }
		}

		/// <summary>
		/// Gets
		/// </summary>
		protected Matrix RotationMatrix
		{
			get { return rotationMatrix; }
		}

		/// <summary>
		/// 
		/// </summary>
		public Vector2 CameraPosition
		{
			set
			{
				cameraPositionValue = value;
				visibilityChanged = true;
			}
			get
			{
				return cameraPositionValue;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public float CameraRotation
		{
			set
			{
				rotationValue = value;
				rotationMatrix = Matrix.CreateRotationZ(rotationValue);
				visibilityChanged = true;
			}
			get
			{
				return rotationValue;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public float CameraZoom
		{
			set
			{
				zoomValue = value;
				visibilityChanged = true;
			}
			get
			{
				return zoomValue;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public Color Color
		{
			set
			{
				layerColor = value;
			}
			get
			{
				return layerColor;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public Vector2 WorldOffset
		{
			set
			{
				worldOffset = value;
				visibilityChanged = true;
			}
			get
			{
				return worldOffset;
			}
		}

		#endregion

        #region Constructors

		/// <summary>
		/// 
		/// </summary>
		/// <param name="offset"></param>
		/// <param name="graphicsComponent"></param>
		/// <param name="Renderer2DComponent_"></param>
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

		#endregion

		#region Initialization

		/// <summary>
		/// 
		/// </summary>
		private void InitializeGraphics()
		{
			m_Graphics.DeviceReset +=
				new EventHandler<EventArgs>(OnGraphicsComponentDeviceReset);

			OnGraphicsComponentDeviceReset(this, new EventArgs());
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
        void OnGraphicsComponentDeviceReset(object sender, EventArgs e)
        {
            displaySize.X = 
                m_Graphics.GraphicsDevice.PresentationParameters.BackBufferWidth;
            
            displaySize.Y = 
                m_Graphics.GraphicsDevice.PresentationParameters.BackBufferHeight;
            
            visibilityChanged = true;
        }

        #endregion

        #region Methods

        /// <summary>
        /// This function determines which tiles are visible on the screen,
        /// given the current camera position, rotation, zoom, and tile scale
        /// </summary>
        protected virtual void DetermineVisibility()
        {
            visibilityChanged = false;
        }

		/// <summary>
		/// 
		/// </summary>
		public void Update()
		{
			if (visibilityChanged) DetermineVisibility();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="batch"></param>
		protected abstract void DrawTiles(SpriteBatch batch);

		/// <summary>
		/// 
		/// </summary>
        public void Draw(SpriteBatch batch)
        {
            DrawTiles(batch);
        }



        #endregion
	}
}
