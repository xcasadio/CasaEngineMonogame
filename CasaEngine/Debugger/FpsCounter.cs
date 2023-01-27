#region File Description
//-----------------------------------------------------------------------------
// FpsCounter.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

#region Using Statements

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngineCommon.Extension;

using CasaEngine.Graphics2D;
using CasaEngine.Game;
using CasaEngine.CoreSystems.Game;
#if EDITOR
//using CasaEngine.Editor.GameComponent;
#endif

#endregion

namespace CasaEngine.Debugger
{
    /// <summary>
    /// Component for FPS measure and draw.
    /// </summary>
    public class FpsCounter
        : Microsoft.Xna.Framework.DrawableGameComponent
#if EDITOR
		, CasaEngineCommon.Design.IObservable<FpsCounter>
#endif
    {
        #region Properties

        /// <summary>
        /// Gets current FPS
        /// </summary>
        public float Fps { get; private set; }

		/// <summary>
		/// Gets Minimum FPS
		/// </summary>
		public float FpsMin { get; private set; }

		/// <summary>
		/// Gets Maximum FPS
		/// </summary>
		public float FpsMax { get; private set; }

		/// <summary>
		/// Gets Average FPS
		/// </summary>
		public float FpsAvg 
		{ 
			get
			{
				return m_FpsAverage / (float)m_NumberOfFPSCount;
			} 
		}

		/// <summary>
		/// Gets total number of frames
		/// </summary>
		public int TotalNumberOfFrames { get; private set; }

        /// <summary>
        /// Gets/Sets FPS sample duration.
        /// </summary>
        public TimeSpan SampleSpan { get; set; }

        #endregion

        #region Fields

        // Reference for debug manager.
        private DebugManager debugManager;

        // Stopwatch for fps measuring.
        private Stopwatch stopwatch;

        private int sampleFrames;

        // stringBuilder for FPS counter draw.
        private StringBuilder stringBuilder = new StringBuilder(16);

		private Renderer2DComponent m_Renderer2DComponent = null;

		private Color m_ColorBackground = new Color(0, 0, 0, 128);

        private List<CasaEngineCommon.Design.IObserver<FpsCounter>> m_ListObserver = new List<CasaEngineCommon.Design.IObserver<FpsCounter>>();

		private float m_FpsAverage = 0.0f;

		private int m_NumberOfFPSCount = 0;

		private bool m_FirstCompute = true;

        #endregion

        #region Initialize

		/// <summary>
		/// 
		/// </summary>
		/// <param name="game"></param>
        public FpsCounter(Microsoft.Xna.Framework.Game game)
            : base(game)
        {
            SampleSpan = TimeSpan.FromSeconds(1);

			UpdateOrder = (int)ComponentUpdateOrder.DebugManager;
			DrawOrder = (int)ComponentDrawOrder.DebugManager;
        }

		/// <summary>
		/// 
		/// </summary>
        public override void Initialize()
        {
            // Get debug manager from game service.
            debugManager =
                Game.Services.GetService(typeof(DebugManager)) as DebugManager;

            if (debugManager == null)
                throw new InvalidOperationException("DebugManaer is not registered.");

            // Register 'fps' command if debug command is registered as a service.
            IDebugCommandHost host =
                                Game.Services.GetService(typeof(IDebugCommandHost))
                                                                as IDebugCommandHost;

            if (host != null)
            {
                host.RegisterCommand("fps", "FPS Counter", this.CommandExecute);
                Visible = false;
            }

			Reset();

            base.Initialize();
        }

		/// <summary>
		/// 
		/// </summary>
		protected override void LoadContent()
		{
			m_Renderer2DComponent = GameHelper.GetGameComponent<Renderer2DComponent>(Game);

			if (m_Renderer2DComponent == null)
			{
				throw new InvalidOperationException("FpsCounter.LoadContent() : Renderer2DComponent is null");
			}

			base.LoadContent();
		}

        #endregion

		/// <summary>
		/// 
		/// </summary>
		public void Reset()
		{
			Fps = 0;
			FpsMin = Single.MaxValue;
			FpsMax = Single.MinValue;
			TotalNumberOfFrames = 0;
			m_FirstCompute = true;
			m_FpsAverage = 0.0f;
			sampleFrames = 0;
			stopwatch = Stopwatch.StartNew();
			stringBuilder.Length = 0;
		}

        /// <summary>
        /// FPS command implementation.
        /// </summary>
        private void CommandExecute(IDebugCommandHost host,
                                    string command, IList<string> arguments)
        {
            if (arguments.Count == 0)
                Visible = !Visible;

            foreach (string arg in arguments)
            {
                switch (arg.ToLower())
                {
                    case "on":
                        Visible = true;
                        break;
                    case "off":
                        Visible = false;
                        break;
                }
            }
        }

        #region Update and Draw

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gameTime"></param>
        public override void Update(GameTime gameTime)
        {
            if (stopwatch.Elapsed > SampleSpan)
            {
				if (m_FirstCompute == false)
				{
					Fps = (float)sampleFrames / (float)stopwatch.Elapsed.TotalSeconds;

					if (FpsMin > Fps)
					{
						FpsMin = Fps;
					}

					if (FpsMax < Fps)
					{
						FpsMax = Fps;
					}

					m_FpsAverage += Fps;
					m_NumberOfFPSCount++;
				}

				m_FirstCompute = false;
                sampleFrames = 0;

                // Update draw string.
                stringBuilder.Length = 0;
                stringBuilder.Append("FPS: ");
                stringBuilder.AppendNumber(Fps);

                foreach (CasaEngineCommon.Design.IObserver<FpsCounter> ob in m_ListObserver)
				{
					ob.OnNotify(this);
				}

				stopwatch.Reset();
				stopwatch.Start();
            }

			sampleFrames++;
			TotalNumberOfFrames++;
        }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="gameTime"></param>
        public override void Draw(GameTime gameTime)
        {
            SpriteFont font = Engine.Instance.DefaultSpriteFont;

            // Compute size of border area.
            Vector2 size = font.MeasureString("X");
            Rectangle rc =
                new Rectangle(0, 0, (int)(size.X * 14f), (int)(size.Y * 1.3f));

            Layout layout = new Layout(Engine.Instance.SpriteBatch.GraphicsDevice.Viewport);
            rc = layout.Place(rc, 0.01f, 0.01f, Alignment.TopLeft);

            // Place FPS string in border area.
            size = font.MeasureString(stringBuilder);
            layout.ClientArea = rc;
            Vector2 pos = layout.Place(size, 0, 0.1f, Alignment.Center);

            // Draw
            m_Renderer2DComponent.AddSprite2D(debugManager.WhiteTexture, rc, Point.Zero, pos, 0.0f, Vector2.One, m_ColorBackground, 0.001f, SpriteEffects.None);
			m_Renderer2DComponent.AddText2D(Engine.Instance.DefaultSpriteFont, stringBuilder.ToString(), 
				pos, 0.0f, Vector2.One, Color.White, 0f);

            base.Draw(gameTime);
        }

        #endregion

		#region IObservable<FpsCounter> Members

		/// <summary>
		/// 
		/// </summary>
		/// <param name="arg_"></param>
        public void RegisterObserver(CasaEngineCommon.Design.IObserver<FpsCounter> arg_)
		{
			m_ListObserver.Add(arg_);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="arg_"></param>
        public void UnRegisterObserver(CasaEngineCommon.Design.IObserver<FpsCounter> arg_)
		{
			m_ListObserver.Remove(arg_);
			arg_.OnUnregister(this);
		}

		/// <summary>
		/// 
		/// </summary>
		public void NotifyObservers()
		{
            foreach (CasaEngineCommon.Design.IObserver<FpsCounter> ob in m_ListObserver)
			{
				ob.OnNotify(this);
			}
		}

		#endregion
	}
}
