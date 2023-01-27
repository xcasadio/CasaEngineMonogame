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

namespace Editor.Tools.Graphics2D
{
    public class Shape2DManipulatorRectangle
        : Shape2DManipulator
    {
        /// <summary>
        /// 
        /// </summary>
        public CasaEngine.Math.Shape2D.ShapeRectangle ShapeRectangle
        {
            get
            {
                return (ShapeRectangle)base.Shape2DObject;
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
        public Shape2DManipulatorRectangle(ShapeRectangle obj_)
            : base(obj_)
        { }

        /// <summary>
        /// 
        /// </summary>
        protected override void CreateAnchor()
        {
            lock (SyncRoot)
            {
                m_AnchorList.Clear();

                Vector2 v1, v2, v3, v4;

                v1 = new Vector2(ShapeRectangle.Location.X - ShapeRectangle.Width / 2,
                    ShapeRectangle.Location.Y - ShapeRectangle.Height / 2);
                v2 = new Vector2(ShapeRectangle.Location.X + ShapeRectangle.Width / 2,
                    ShapeRectangle.Location.Y - ShapeRectangle.Height / 2);
                v3 = new Vector2(ShapeRectangle.Location.X + ShapeRectangle.Width / 2,
                    ShapeRectangle.Location.Y + ShapeRectangle.Height / 2);
                v4 = new Vector2(ShapeRectangle.Location.X - ShapeRectangle.Width / 2,
                    ShapeRectangle.Location.Y + ShapeRectangle.Height / 2);

                m_AnchorList.Add(new Anchor((int)v1.X, (int)v1.Y, AnchorWidth, AnchorHeight));
                m_AnchorList[0].LocationChanged += new EventHandler<AnchorLocationChangedEventArgs>(LeftTopLocationChanged);
                m_AnchorList[0].CursorOver = Cursors.Hand;
                m_AnchorList[0].CursorPressed = Cursors.SizeAll;

                m_AnchorList.Add(new Anchor((int)v1.X, (int)v1.Y, AnchorWidth, AnchorHeight));
                m_AnchorList[1].LocationChanged += new EventHandler<AnchorLocationChangedEventArgs>(RightTopLocationChanged);
                m_AnchorList[1].CursorOver = Cursors.Hand;
                m_AnchorList[1].CursorPressed = Cursors.SizeAll;

                m_AnchorList.Add(new Anchor((int)v1.X, (int)v1.Y, AnchorWidth, AnchorHeight));
                m_AnchorList[2].LocationChanged += new EventHandler<AnchorLocationChangedEventArgs>(RightBottomLocationChanged);
                m_AnchorList[2].CursorOver = Cursors.Hand;
                m_AnchorList[2].CursorPressed = Cursors.SizeAll;

                m_AnchorList.Add(new Anchor((int)v1.X, (int)v1.Y, AnchorWidth, AnchorHeight));
                m_AnchorList[3].LocationChanged += new EventHandler<AnchorLocationChangedEventArgs>(LeftBottomLocationChanged);
                m_AnchorList[3].CursorOver = Cursors.Hand;
                m_AnchorList[3].CursorPressed = Cursors.SizeAll;
            }

            InitializeAnchorsEvent();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void LeftTopLocationChanged(object sender, AnchorLocationChangedEventArgs e)
        {
            lock (SyncRoot)
            {
                m_AnchorList[1].SetLocationOffSet(0, e.OffsetY, false); // right top
                m_AnchorList[3].SetLocationOffSet(e.OffsetX, 0, false); // left top
            }

            ShapeRectangle.Height += -e.OffsetY;
            ShapeRectangle.Width += -e.OffsetX;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void RightTopLocationChanged(object sender, AnchorLocationChangedEventArgs e)
        {
            lock (SyncRoot)
            {
                m_AnchorList[0].SetLocationOffSet(0, e.OffsetY, false);
                m_AnchorList[2].SetLocationOffSet(e.OffsetX, 0, false);
            }

            ShapeRectangle.Height += -e.OffsetY;
            ShapeRectangle.Width += e.OffsetX;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void RightBottomLocationChanged(object sender, AnchorLocationChangedEventArgs e)
        {
            lock (SyncRoot)
            {
                m_AnchorList[1].SetLocationOffSet(0, e.OffsetY, false); // right top
                m_AnchorList[3].SetLocationOffSet(e.OffsetX, 0, false); // left top
            }

            ShapeRectangle.Height += -e.OffsetY;
            ShapeRectangle.Width += -e.OffsetX;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void LeftBottomLocationChanged(object sender, AnchorLocationChangedEventArgs e)
        {
            lock (SyncRoot)
            {
                m_AnchorList[0].SetLocationOffSet(0, e.OffsetY, false);
                m_AnchorList[2].SetLocationOffSet(e.OffsetX, 0, false);
            }

            ShapeRectangle.Height += -e.OffsetY;
            ShapeRectangle.Width += -e.OffsetX;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="line2DRenderer_"></param>
        /// <param name="spriteBatch_"></param>
        /// <param name="position_"></param>
        public override void Draw(Line2DRenderer line2DRenderer_, SpriteBatch spriteBatch_)
        {
            base.Draw(line2DRenderer_, spriteBatch_);

            Vector2 v1, v2, v3, v4;
            Vector2 position = Offset;

            v1 = new Vector2(position.X + ShapeRectangle.Location.X - ShapeRectangle.Width / 2,
                position.Y + ShapeRectangle.Location.Y - ShapeRectangle.Height / 2);
            v2 = new Vector2(position.X + ShapeRectangle.Location.X + ShapeRectangle.Width / 2,
                position.Y + ShapeRectangle.Location.Y - ShapeRectangle.Height / 2);
            v3 = new Vector2(position.X + ShapeRectangle.Location.X + ShapeRectangle.Width / 2,
                position.Y + ShapeRectangle.Location.Y + ShapeRectangle.Height / 2);
            v4 = new Vector2(position.X + ShapeRectangle.Location.X - ShapeRectangle.Width / 2,
                position.Y + ShapeRectangle.Location.Y + ShapeRectangle.Height / 2);

            line2DRenderer_.DrawLine(spriteBatch_, Color.Blue, v1, v2);
            line2DRenderer_.DrawLine(spriteBatch_, Color.Blue, v2, v3);
            line2DRenderer_.DrawLine(spriteBatch_, Color.Blue, v3, v4);
            line2DRenderer_.DrawLine(spriteBatch_, Color.Blue, v1, v4);
        }
    }
}
