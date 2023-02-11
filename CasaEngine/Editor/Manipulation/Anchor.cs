using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using CasaEngine.Graphics2D;
using Microsoft.Xna.Framework.Graphics;
using System.ComponentModel;

namespace CasaEngine.Editor.Manipulation
{
    public class Anchor
    {

        private Vector2 _position;
        private Vector2 _mouseStart;
        private bool _mousePressed = false;
        private readonly List<Anchor> _anchors = new();

        public event EventHandler<AnchorLocationChangedEventArgs> LocationChanged;
        public event EventHandler StartManipulating, FinishManipulating;

        public event EventHandler CursorChanged;
        private bool _isOver = false;
        private bool _shiftPressed = false, _controlPressed = false;



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

        public Rectangle Bounds => new((int)(_position.X + Offset.X), (int)(_position.Y + Offset.Y), Width, Height);

        public bool IsManipulating
        {
            get;
            private set;
        }

        public bool CanManipulate
        {
            get
            {
                foreach (Anchor a in _anchors)
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



        public Anchor(int x, int y, int width, int height)
        {
            _position = new Vector2(x - width / 2, y - height / 2);
            Width = width;
            Height = height;
            IsManipulating = true;

            MoveOnX = true;
            MoveOnY = true;

            Offset = Vector2.Zero;
        }



        public void LinkWithAnchor(Anchor a)
        {
            _anchors.Add(a);
        }

        public void Update(MouseState mouseState, bool shiftKeyPressed, bool controlKeyPressed, bool altKeyPressed)
        {
            int mouseX = mouseState.X, mouseY = mouseState.Y;

            if (Selected && IsManipulating
                && shiftKeyPressed == false
                && controlKeyPressed == false
                && altKeyPressed == false)
            {
                int offsetX = MoveOnX ? mouseState.X - (int)_mouseStart.X : 0;
                int offsetY = MoveOnY ? mouseState.Y - (int)_mouseStart.Y : 0;

                _position = new Vector2(_position.X + offsetX, _position.Y + offsetY);

                if (LocationChanged != null)
                {
                    LocationChanged.Invoke(this, new AnchorLocationChangedEventArgs(offsetX, offsetY));
                }
            }

            if (mouseState.LeftButton == ButtonState.Released)
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
                if (mouseState.LeftButton == ButtonState.Pressed
                    && _mousePressed == false
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

                _isOver = true;

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
                    && _isOver == true)
                {
                    InvokeCursorChanged(Cursors.Default);
                    _isOver = false;
                }

                if (shiftKeyPressed
                    && _shiftPressed == false)
                {
                    InvokeCursorChanged(CursorOverShiftPressed);
                    _shiftPressed = true;
                }
                else if (shiftKeyPressed == false
                    && _shiftPressed == true)
                {
                    InvokeCursorChanged(Cursors.Default);
                    _shiftPressed = false;
                }

                if (controlKeyPressed
                    && _controlPressed == false)
                {
                    InvokeCursorChanged(CursorOverControlPressed);
                    _controlPressed = true;
                }
                else if (controlKeyPressed == false
                    && _controlPressed == true)
                {
                    InvokeCursorChanged(Cursors.Default);
                    _controlPressed = false;
                }
            }

            _mouseStart = new Vector2(mouseState.X, mouseState.Y);

            _mousePressed = Mouse.GetState().LeftButton == ButtonState.Pressed;
        }

        public void Draw(Renderer2DComponent r)
        {
            r.AddBox(_position.X + Offset.X, _position.Y + Offset.Y, Width, Height, Color.Black);
        }

        public void Draw(Line2DRenderer lineRenderer, SpriteBatch spriteBatch, Color color)
        {
            lineRenderer.DrawLine(spriteBatch, color,
                new Vector2(_position.X + Offset.X, _position.Y + Offset.Y),
                new Vector2(_position.X + Offset.X + Width, _position.Y + Offset.Y));

            lineRenderer.DrawLine(spriteBatch, color,
                new Vector2(_position.X + Offset.X + Width, _position.Y + Offset.Y),
                new Vector2(_position.X + Offset.X + Width, _position.Y + Offset.Y + Height));

            lineRenderer.DrawLine(spriteBatch, color,
                new Vector2(_position.X + Offset.X + Width, _position.Y + Offset.Y + Height),
                new Vector2(_position.X + Offset.X, _position.Y + Offset.Y + Height));

            lineRenderer.DrawLine(spriteBatch, color,
                new Vector2(_position.X + Offset.X, _position.Y + Offset.Y + Height),
                new Vector2(_position.X + Offset.X, _position.Y + Offset.Y));
        }

        public void SetLocation(int x, int y, bool callHandler)
        {
            _position.X = x;
            _position.Y = y;

            if (callHandler == true
                && LocationChanged != null)
            {
                LocationChanged.Invoke(this, new AnchorLocationChangedEventArgs(x, y));
            }
        }

        public void SetLocationOffSet(int offsetX, int offsetY, bool callHandler)
        {
            _position.X += offsetX;
            _position.Y += offsetY;

            if (callHandler == true
                && LocationChanged != null)
            {
                LocationChanged.Invoke(this, new AnchorLocationChangedEventArgs(offsetX, offsetY));
            }
        }

        private void InvokeCursorChanged(Cursor cursor)
        {
            if (CursorChanged != null)
            {
                CursorChanged.Invoke(cursor, EventArgs.Empty);
            }
        }

        public bool IsInside(int x, int y)
        {
            return Bounds.Contains(x, y);
        }

        public bool IsInside(Point p)
        {
            return IsInside(p.X, p.Y);
        }

        public bool IsInside(Vector2 v)
        {
            return IsInside((int)v.X, (int)v.Y);
        }

    }
}
