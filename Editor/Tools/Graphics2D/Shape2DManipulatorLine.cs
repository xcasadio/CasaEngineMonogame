using Microsoft.Xna.Framework;
using CasaEngine.Editor.Manipulation;
using CasaEngine.Graphics2D;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Core_Systems.Math.Shape2D;
using Color = Microsoft.Xna.Framework.Color;
using Point = Microsoft.Xna.Framework.Point;

namespace Editor.Tools.Graphics2D
{
    class Shape2DManipulatorLine
        : Shape2DManipulator
    {
        /// <summary>
        /// 
        /// </summary>
        public ShapeLine ShapeLine
        {
            get
            {
                return (ShapeLine)Shape2DObject;
            }
            internal set
            {
                Shape2DObject = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj_"></param>
        public Shape2DManipulatorLine(ShapeLine obj_)
            : base(obj_)
        { }

        /// <summary>
        /// 
        /// </summary>
        protected override void CreateAnchor()
        {
            m_AnchorList.Clear();

            m_AnchorList.Add(new Anchor((int)ShapeLine.Start.X, (int)ShapeLine.Start.Y, AnchorWidth, AnchorHeight));
            m_AnchorList[0].LocationChanged += new EventHandler<AnchorLocationChangedEventArgs>(StartLocationChanged);
            m_AnchorList[0].CursorOver = Cursors.Hand;
            m_AnchorList[0].CursorPressed = Cursors.SizeAll;

            m_AnchorList.Add(new Anchor((int)ShapeLine.End.X, (int)ShapeLine.End.Y, AnchorWidth, AnchorHeight));
            m_AnchorList[1].LocationChanged += new EventHandler<AnchorLocationChangedEventArgs>(EndLocationChanged);
            m_AnchorList[1].CursorOver = Cursors.Hand;
            m_AnchorList[1].CursorPressed = Cursors.SizeAll;

            InitializeAnchorsEvent();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void StartLocationChanged(object sender, AnchorLocationChangedEventArgs e)
        {
            ShapeLine.Start = new Point(ShapeLine.Start.X + e.OffsetX, ShapeLine.Start.Y + e.OffsetY);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void EndLocationChanged(object sender, AnchorLocationChangedEventArgs e)
        {
            ShapeLine.End = new Point(ShapeLine.End.X + e.OffsetX, ShapeLine.End.Y + e.OffsetY);
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

            Vector2 v1, v2;
            Vector2 position = Offset;

            v1 = new Vector2(position.X + ShapeLine.Start.X, position.Y + ShapeLine.Start.Y);
            v2 = new Vector2(position.X + ShapeLine.End.X, position.Y + ShapeLine.End.Y);

            line2DRenderer_.DrawLine(spriteBatch_, Color.Blue, v1, v2);
        }
    }
}
