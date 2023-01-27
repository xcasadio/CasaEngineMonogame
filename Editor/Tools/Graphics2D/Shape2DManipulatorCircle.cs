using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Math.Shape2D;
using CasaEngine.Editor.Manipulation;
using CasaEngine.Graphics2D;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using System.Windows.Forms;
using Editor.UndoRedo;

namespace Editor.Tools.Graphics2D
{
    public class Shape2DManipulatorCircle
        : Shape2DManipulator
    {
        /// <summary>
        /// 
        /// </summary>
        public CasaEngine.Math.Shape2D.ShapeCircle ShapeCircle
        {
            get 
            { 
                return (ShapeCircle)base.Shape2DObject; 
            }
            internal set
            {
                base.Shape2DObject = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="circle_"></param>
        public Shape2DManipulatorCircle(ShapeCircle circle_)
            : base(circle_)
        { }

        /// <summary>
        /// 
        /// </summary>
        protected override void CreateAnchor()
        {
            m_AnchorList.Clear();

            m_AnchorList.Add(new Anchor(ShapeCircle.Location.X, ShapeCircle.Location.Y, AnchorWidth, AnchorHeight));
            m_AnchorList[0].LocationChanged += new EventHandler<AnchorLocationChangedEventArgs>(ManipulatorLocationChanged);
            m_AnchorList[0].CursorOver = Cursors.Hand;
            m_AnchorList[0].CursorPressed = Cursors.SizeAll;

            m_AnchorList.Add(new Anchor(ShapeCircle.Location.X + ShapeCircle.Radius, ShapeCircle.Location.Y, AnchorWidth, AnchorHeight));
            m_AnchorList[1].LocationChanged += new EventHandler<AnchorLocationChangedEventArgs>(RadiusLocationChanged);
            m_AnchorList[1].MoveOnY = false;
            m_AnchorList[1].CursorOver = Cursors.Hand;
            m_AnchorList[1].CursorPressed = Cursors.SizeWE;

            InitializeAnchorsEvent();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void RadiusLocationChanged(object sender, AnchorLocationChangedEventArgs e)
        {
            ShapeCircle.Radius += e.OffsetX;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void ManipulatorLocationChanged(object sender, AnchorLocationChangedEventArgs e)
        {
            ShapeCircle.Location = new Microsoft.Xna.Framework.Point(
                ShapeCircle.Location.X + e.OffsetX,
                ShapeCircle.Location.Y + e.OffsetY);

            m_AnchorList[1].SetLocationOffSet(e.OffsetX, e.OffsetY, false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="line2DRenderer_"></param>
        /// <param name="spriteBatch_"></param>
        public override void Draw(Line2DRenderer line2DRenderer_, SpriteBatch spriteBatch_)
        {
            base.Draw(line2DRenderer_, spriteBatch_);

            Vector2 v1, v2 = Vector2.Zero, v3 = Vector2.Zero;
            double step = 2.0 * Math.PI / 32.0;

            Vector2 position = Offset;
            position.X += ShapeCircle.Location.X;
            position.Y += ShapeCircle.Location.Y;

            for (int i = 0; i < 31; i++)
            {
                v1 = new Vector2(position.X +ShapeCircle.Radius * (float)Math.Cos(step * (double)i),
                    position.Y + ShapeCircle.Radius * (float)Math.Sin(step * (double)i));
                v2 = new Vector2(position.X + ShapeCircle.Radius * (float)Math.Cos(step * ((double)i + 1.0)),
                    position.Y + ShapeCircle.Radius * (float)Math.Sin(step * ((double)i + 1.0)));

                line2DRenderer_.DrawLine(spriteBatch_, Color.Blue, v1, v2);

                if (i == 0)
                {
                    v3 = v1;
                }
            }

            line2DRenderer_.DrawLine(spriteBatch_, Color.Blue, v2, v3);
        }
    }
}
