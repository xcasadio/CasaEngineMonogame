using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using CasaEngine.Graphics2D;
using Microsoft.Xna.Framework.Graphics;
using System.ComponentModel;

namespace CasaEngine.Editor.Manipulation
{
    public class Anchor
    {

        private Vector2 m_Position;
        private Vector2 mouseStart;
        private bool m_MousePressed = false;
        private readonly List<Anchor> m_Anchors = new List<Anchor>();

        public event EventHandler<AnchorLocationChangedEventArgs> LocationChanged;
        public event EventHandler StartManipulating, FinishManipulating;

        public event EventHandler CursorChanged;
        private bool m_IsOver = false;
        private bool m_shiftPressed = false, controlPressed = false;



        public int Height
        {
            get;
            set;
        }

        public int Width
        {
            get;
            set;
        }

        public bool Selected
        {
            get;
            set;
        }

        public bool MoveOnX
        {
            get;
            set;
        }

        public bool MoveOnY
        {
            get;
            set;
        }

        public Rectangle Bounds => new Rectangle((int)(m_Position.X + Offset.X), (int)(m_Position.Y + Offset.Y), Width, Height);

        public bool IsManipulating
        {
            get;
            private set;
        }

        public bool CanManipulate
        {
            get
            {
                foreach (Anchor a in m_Anchors)
                {
                    if (a.IsManipulating == true)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public Vector2 Offset
        {
            get;
            set;
        }

        [DefaultValue(null)]
        public Cursor CursorOverShiftPressed
        {
            get;
            set;
        }

        [DefaultValue(null)]
        public Cursor CursorOverControlPressed
        {
            get;
            set;
        }

        [DefaultValue(null)]
        public Cursor CursorOverAltPressed
        {
            get;
            set;
        }

        [DefaultValue(null)]
        public Cursor CursorOver
        {
            get;
            set;
        }

        [DefaultValue(null)]
        public Cursor CursorPressed
        {
            get;
            set;
        }



        public Anchor(int x_, int y_, int width_, int height_)
        {
            m_Position = new Vector2(x_ - width_ / 2, y_ - height_ / 2);
            Width = width_;
            Height = height_;
            IsManipulating = true;

            MoveOnX = true;
            MoveOnY = true;

            Offset = Vector2.Zero;
        }



        public void LinkWithAnchor(Anchor a_)
        {
            m_Anchors.Add(a_);
        }

        public void Update(MouseState mouseState_, bool shiftKeyPressed_, bool controlKeyPressed_, bool altKeyPressed_)
        {
            int mouseX = mouseState_.X, mouseY = mouseState_.Y;

            if (Selected && IsManipulating
                && shiftKeyPressed_ == false
                && controlKeyPressed_ == false
                && altKeyPressed_ == false)
            {
                int offsetX = MoveOnX ? mouseState_.X - (int)mouseStart.X : 0;
                int offsetY = MoveOnY ? mouseState_.Y - (int)mouseStart.Y : 0;

                m_Position = new Vector2(m_Position.X + offsetX, m_Position.Y + offsetY);

                if (LocationChanged != null)
                {
                    LocationChanged.Invoke(this, new AnchorLocationChangedEventArgs(offsetX, offsetY));
                }
            }

            if (mouseState_.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Released)
            {
                if (IsManipulating == true)
                {
                    IsManipulating = false;
                    if (FinishManipulating != null)
                    {
                        FinishManipulating.Invoke(this, EventArgs.Empty);
                    }
                }
            }

            if (Bounds.Contains(mouseX, mouseY) == true)
            {
                if (mouseState_.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed
                    && m_MousePressed == false
                    && CanManipulate == true)
                {
                    if (IsManipulating == false
                        && StartManipulating != null)
                    {
                        StartManipulating.Invoke(this, EventArgs.Empty);
                    }

                    IsManipulating = true;
                    Selected = true;
                }

                m_IsOver = true;

                if (IsManipulating == true)
                {
                    InvokeCursorChanged(CursorPressed);
                }
                else
                {
                    InvokeCursorChanged(CursorOver);
                }
            }
            else
            {
                if (IsManipulating == false
                    && m_IsOver == true)
                {
                    InvokeCursorChanged(Cursors.Default);
                    m_IsOver = false;
                }

                if (shiftKeyPressed_
                    && m_shiftPressed == false)
                {
                    InvokeCursorChanged(CursorOverShiftPressed);
                    m_shiftPressed = true;
                }
                else if (shiftKeyPressed_ == false
                    && m_shiftPressed == true)
                {
                    InvokeCursorChanged(Cursors.Default);
                    m_shiftPressed = false;
                }

                if (controlKeyPressed_
                    && controlPressed == false)
                {
                    InvokeCursorChanged(CursorOverControlPressed);
                    controlPressed = true;
                }
                else if (controlKeyPressed_ == false
                    && controlPressed == true)
                {
                    InvokeCursorChanged(Cursors.Default);
                    controlPressed = false;
                }
            }

            mouseStart = new Vector2(mouseState_.X, mouseState_.Y);

            m_MousePressed = Mouse.GetState().LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
        }

        public void Draw(Renderer2DComponent r_)
        {
            r_.AddBox(m_Position.X + Offset.X, m_Position.Y + Offset.Y, Width, Height, Color.Black);
        }

        public void Draw(Line2DRenderer lineRenderer_, SpriteBatch spriteBatch_, Color color_)
        {
            lineRenderer_.DrawLine(spriteBatch_, color_,
                new Vector2(m_Position.X + Offset.X, m_Position.Y + Offset.Y),
                new Vector2(m_Position.X + Offset.X + Width, m_Position.Y + Offset.Y));

            lineRenderer_.DrawLine(spriteBatch_, color_,
                new Vector2(m_Position.X + Offset.X + Width, m_Position.Y + Offset.Y),
                new Vector2(m_Position.X + Offset.X + Width, m_Position.Y + Offset.Y + Height));

            lineRenderer_.DrawLine(spriteBatch_, color_,
                new Vector2(m_Position.X + Offset.X + Width, m_Position.Y + Offset.Y + Height),
                new Vector2(m_Position.X + Offset.X, m_Position.Y + Offset.Y + Height));

            lineRenderer_.DrawLine(spriteBatch_, color_,
                new Vector2(m_Position.X + Offset.X, m_Position.Y + Offset.Y + Height),
                new Vector2(m_Position.X + Offset.X, m_Position.Y + Offset.Y));
        }

        public void SetLocation(int x_, int y_, bool callHandler_)
        {
            m_Position.X = x_;
            m_Position.Y = y_;

            if (callHandler_ == true
                && LocationChanged != null)
            {
                LocationChanged.Invoke(this, new AnchorLocationChangedEventArgs(x_, y_));
            }
        }

        public void SetLocationOffSet(int offsetX_, int offsetY_, bool callHandler_)
        {
            m_Position.X += offsetX_;
            m_Position.Y += offsetY_;

            if (callHandler_ == true
                && LocationChanged != null)
            {
                LocationChanged.Invoke(this, new AnchorLocationChangedEventArgs(offsetX_, offsetY_));
            }
        }

        private void InvokeCursorChanged(Cursor cursor_)
        {
            if (CursorChanged != null)
            {
                CursorChanged.Invoke(cursor_, EventArgs.Empty);
            }
        }

        public bool IsInside(int x_, int y_)
        {
            return Bounds.Contains(x_, y_);
        }

        public bool IsInside(Point p_)
        {
            return IsInside(p_.X, p_.Y);
        }

        public bool IsInside(Vector2 v_)
        {
            return IsInside((int)v_.X, (int)v_.Y);
        }

    }
}
