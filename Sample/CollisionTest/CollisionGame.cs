using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using CasaEngine.Math;
using CasaEngine.Math.Shape2D;
using System.Xml;
using CasaEngine.Helper;
using CasaEngine.Game;
using Microsoft.Xna.Framework.Input;
using CasaEngine.Math.Collision;

namespace CollisionTest
{
    class CollisionGame
        : Game
    {
        #region Fields

        private GraphicsDeviceManager m_GraphicsDeviceManager;

        private ShapeRendererComponent m_ShapeRendererComponent;

        private ShapePolygone m_ShapePoly;
        private ShapeRectangle m_ShapeRectangle;
        private ShapeCircle m_ShapeCircle, m_ShapeCircle2;

        bool IsCollide = false;

        #endregion

        #region Properties


        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        public CollisionGame()
        {
            Engine.Instance.Game = this;
            m_GraphicsDeviceManager = new GraphicsDeviceManager(this);

            IsMouseVisible = true;
        }

        #endregion

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        protected override void Initialize()
        {
            m_ShapeRendererComponent = new ShapeRendererComponent(this);
            m_ShapeRendererComponent.SetCurrentPhysicsWorld(new FarseerPhysics.Dynamics.World(GameInfo.Instance.WorldInfo.WorldGravity));

            base.Initialize();
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void LoadContent()
        {
            Engine.Instance.SpriteBatch = new Microsoft.Xna.Framework.Graphics.SpriteBatch(GraphicsDevice);

            base.LoadContent();

            m_ShapePoly = new ShapePolygone();
            m_ShapeCircle = new ShapeCircle();

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(@"<?xml version=""1.0""?>
                <Root>
                    <Shape version=""1"" type=""Circle"" rotation=""0"" flag=""1"" radius=""30"">
                        <Location x=""0"" y=""0"" />
                    </Shape>
                    <Shape version=""1"" type=""Polygone"" rotation=""0"" flag=""0"">
                      <Location x=""0"" y=""0"" />
                      <IsABox>False</IsABox>
                      <PointList>              
                        <Point x=""0"" y=""0"" />
                        <Point x=""119"" y=""0"" />
                        <Point x=""119"" y=""181"" />
                        <Point x=""0"" y=""181"" />
                      </PointList>
                    </Shape>
                </Root>");

            m_ShapeCircle.Load((XmlElement)xmlDoc.SelectSingleNode("Root/Shape"), CasaEngineCommon.Design.SaveOption.Game);
            m_ShapeCircle2 = (ShapeCircle)m_ShapeCircle.Clone();
            m_ShapePoly.Load((XmlElement)xmlDoc.SelectNodes("Root/Shape")[1], CasaEngineCommon.Design.SaveOption.Game);
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void UnloadContent()
        {
            base.UnloadContent();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameTime"></param>
        protected override void Update(Microsoft.Xna.Framework.GameTime gameTime)
        {
            KeyboardState ks = Microsoft.Xna.Framework.Input.Keyboard.GetState();

            // Polygone
            if (ks.IsKeyDown(Keys.Up) == true)
            {
                m_ShapePoly.Location = new Point(m_ShapePoly.Location.X, m_ShapePoly.Location.Y - 1);
            }

            if (ks.IsKeyDown(Keys.Down) == true)
            {
                m_ShapePoly.Location = new Point(m_ShapePoly.Location.X, m_ShapePoly.Location.Y + 1);
            }

            if (ks.IsKeyDown(Keys.Right) == true)
            {
                m_ShapePoly.Location = new Point(m_ShapePoly.Location.X + 1, m_ShapePoly.Location.Y);
            }

            if (ks.IsKeyDown(Keys.Left) == true)
            {
                m_ShapePoly.Location = new Point(m_ShapePoly.Location.X - 1, m_ShapePoly.Location.Y);
            }

            // Circle
            if (ks.IsKeyDown(Keys.Z) == true)
            {
                m_ShapeCircle.Location = new Point(m_ShapeCircle.Location.X, m_ShapeCircle.Location.Y - 1);
            }

            if (ks.IsKeyDown(Keys.S) == true)
            {
                m_ShapeCircle.Location = new Point(m_ShapeCircle.Location.X, m_ShapeCircle.Location.Y + 1);
            }

            if (ks.IsKeyDown(Keys.D) == true)
            {
                m_ShapeCircle.Location = new Point(m_ShapeCircle.Location.X + 1, m_ShapeCircle.Location.Y);
            }

            if (ks.IsKeyDown(Keys.Q) == true)
            {
                m_ShapeCircle.Location = new Point(m_ShapeCircle.Location.X - 1, m_ShapeCircle.Location.Y);
            }

            m_ShapeCircle2.Location = new Point(Mouse.GetState().X, Mouse.GetState().Y);

            base.Update(gameTime);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameTime"></param>
        protected override void Draw(Microsoft.Xna.Framework.GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            Vector2 contactPoint = new Vector2();
            Vector2 p1Vec = new Vector2(), c1Vec = new Vector2(), c2Vec = new Vector2();
            p1Vec.X = m_ShapePoly.Location.X;
            p1Vec.Y = m_ShapePoly.Location.Y;

            c1Vec.X = m_ShapeCircle.Location.X;
            c1Vec.Y = m_ShapeCircle.Location.Y;

            c2Vec.X = m_ShapeCircle2.Location.X;
            c2Vec.Y = m_ShapeCircle2.Location.Y;

            IsCollide = Collision2D.CollidePolygonAndCircle(ref contactPoint, m_ShapePoly, ref p1Vec, m_ShapeCircle, ref c1Vec);
            Color c = IsCollide == true ? Color.Red : Color.Green;
            m_ShapeRendererComponent.AddShape2DObject(m_ShapeCircle, c);

            IsCollide = Collision2D.CollideCircles(ref contactPoint, m_ShapeCircle, ref c1Vec, m_ShapeCircle2, ref c2Vec);
            c = IsCollide == true ? Color.Red : Color.Green;
            m_ShapeRendererComponent.AddShape2DObject(m_ShapeCircle2, c);

            IsCollide = Collision2D.CollidePolygonAndCircle(ref contactPoint, m_ShapePoly, ref p1Vec, m_ShapeCircle2, ref c2Vec)
                || Collision2D.CollidePolygonAndCircle(ref contactPoint, m_ShapePoly, ref p1Vec, m_ShapeCircle, ref c1Vec);
            c = IsCollide == true ? Color.Red : Color.Green;
            m_ShapeRendererComponent.AddShape2DObject(m_ShapePoly, c);

            base.Draw(gameTime);
        }

        #endregion
    }
}
