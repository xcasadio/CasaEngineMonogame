using CasaEngine.Core.Math.Shape2D;
using CasaEngine.Editor.Manipulation;
using CasaEngine.Graphics2D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Editor.UndoRedo;
using Microsoft.Xna.Framework.Input;
using Color = Microsoft.Xna.Framework.Color;
using Point = Microsoft.Xna.Framework.Point;

namespace Editor.Tools.Graphics2D
{
    class Shape2DManipulatorPolygone
        : Shape2DManipulator
    {
        private bool m_MouseLeftButtonPressed = false;
        private volatile object m_MySyncRoot = new();

        /// <summary>
        /// 
        /// </summary>
        public ShapePolygone ShapePolygone
        {
            get
            {
                return (ShapePolygone)Shape2DObject;
            }
            internal set
            {
                Shape2DObject = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="circle_"></param>
        public Shape2DManipulatorPolygone(ShapePolygone obj_)
            : base(obj_)
        {
            ShapePolygone.OnPointAdded += new EventHandler(OnPointAdded);
            ShapePolygone.OnPointDeleted += new EventHandler(OnPointDeleted);
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void CreateAnchor()
        {
            Vector2[] points;
            Point location;

            lock (m_MySyncRoot)
            {
                points = ShapePolygone.Points;
                location = ShapePolygone.Location;
            }

            lock (SyncRoot)
            {
                m_AnchorList.Clear();

                foreach (Vector2 p in points)
                {
                    Anchor a = new Anchor(
                        location.X + (int)p.X,
                        location.Y + (int)p.Y,
                        AnchorWidth, AnchorHeight);
                    a.LocationChanged += new EventHandler<AnchorLocationChangedEventArgs>(AnchorLocationChanged);
                    a.CursorOver = Cursors.Hand;
                    a.CursorPressed = Cursors.SizeAll;
                    a.CursorOverShiftPressed = new Cursor(new MemoryStream(Properties.Resources.CursorAdd));
                    a.CursorOverControlPressed = new Cursor(new MemoryStream(Properties.Resources.CursorRemove));

                    m_AnchorList.Add(a);
                }
            }

            InitializeAnchorsEvent();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void AnchorLocationChanged(object sender, AnchorLocationChangedEventArgs e)
        {
            Anchor a = sender as Anchor;
            int index = m_AnchorList.IndexOf(a);

            lock (m_MySyncRoot)
            {
                ShapePolygone.ModifyPoint(index,
                                new Vector2(ShapePolygone.PointList[index].X + e.OffsetX,
                                    ShapePolygone.PointList[index].Y + e.OffsetY));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnPointDeleted(object sender, EventArgs e)
        {
            CreateAnchor();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnPointAdded(object sender, EventArgs e)
        {
            CreateAnchor();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void ManipulatorLocationChanged(object sender, AnchorLocationChangedEventArgs e)
        {
            ShapePolygone.Location = new Point(
                ShapePolygone.Location.X + e.OffsetX,
                ShapePolygone.Location.Y + e.OffsetY);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mouseState_"></param>
        public override void Update(MouseState mouseState_, bool shiftPressed_, bool controlPressed_)
        {
            base.Update(mouseState_, shiftPressed_, controlPressed_);

            if (mouseState_.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
            {
                // just pressed ?
                if (m_MouseLeftButtonPressed == false)
                {
                    //add
                    if (shiftPressed_ == true)
                    {
                        bool onOther = false;

                        lock (SyncRoot)
                        {
                            foreach (Anchor a in m_AnchorList)
                            {
                                onOther = a.IsInside(new Vector2(mouseState_.X, mouseState_.Y));

                                if (onOther == true)
                                {
                                    break;
                                }
                            }
                        }

                        if (onOther == false)
                        {
                            Vector2 v = new Vector2(mouseState_.X - Offset.X, mouseState_.Y - Offset.Y);
                            AddPoint(v, ShapePolygone.PointList.Count);
                            UndoRedoPolygoneCommand command = new UndoRedoPolygoneCommand(v, ShapePolygone.PointList.Count - 1, true);
                            UndoRedoManager.Add(command, Sprite2DEditorComponent);
                        }
                    }

                    //delete
                    if (controlPressed_ == true
                        && ShapePolygone.PointList.Count > 3)
                    {
                        int index = -1;

                        lock (SyncRoot)
                        {
                            foreach (Anchor a in m_AnchorList)
                            {
                                if (a.IsInside(new Vector2(mouseState_.X, mouseState_.Y)) == true)
                                {
                                    index = m_AnchorList.IndexOf(a);
                                    break;
                                }
                            }
                        }

                        if (index != -1)
                        {
                            UndoRedoPolygoneCommand command = new UndoRedoPolygoneCommand(ShapePolygone.Points[index], index, false);
                            UndoRedoManager.Add(command, Sprite2DEditorComponent);
                            RemovePoint(index);
                        }
                    }
                }

                m_MouseLeftButtonPressed = true;
            }
            else
            {
                m_MouseLeftButtonPressed = false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="line2DRenderer_"></param>
        /// <param name="spriteBatch_"></param>
        public override void Draw(Line2DRenderer line2DRenderer_, SpriteBatch spriteBatch_)
        {
            Vector2 position = Offset;
            Vector2[] points;

            lock (m_MySyncRoot)
            {
                points = ShapePolygone.Points;
            }

            for (int i = 0; i < points.Length - 1; i++)
            {
                line2DRenderer_.DrawLine(spriteBatch_, Color.Blue,
                    new Vector2(position.X + points[i].X, position.Y + points[i].Y),
                    new Vector2(position.X + points[i + 1].X, position.Y + points[i + 1].Y));
            }

            //last edge
            if (points.Length > 1)
            {
                line2DRenderer_.DrawLine(spriteBatch_, Color.Blue,
                    new Vector2(position.X + points[0].X,
                        position.Y + points[0].Y),
                    new Vector2(position.X + points[points.Length - 1].X,
                        position.Y + points[points.Length - 1].Y));
            }

            base.Draw(line2DRenderer_, spriteBatch_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="p_"></param>
        /// <param name="index_"></param>
        public void AddPoint(Vector2 p_, int index_)
        {
            lock (m_MySyncRoot)
            {
                ShapePolygone.AddPoint(index_, p_);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="m_Index"></param>
        /// <returns></returns>
        public void RemovePoint(int index_)
        {
            lock (m_MySyncRoot)
            {
                ShapePolygone.RemovePointAt(index_);
            }
        }

    }
}
