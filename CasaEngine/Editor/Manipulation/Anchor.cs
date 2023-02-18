using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using System.ComponentModel;
using CasaEngine.Framework.Graphics2D;

namespace CasaEngine.Editor.Manipulation
{
    public class Anchor
    {

        private Vector2 _position;
        private Vector2 _mouseStart;
        private bool _mousePressed;
        private readonly List<Anchor> _anchors = new();

        public event EventHandler<AnchorLocationChangedEventArgs> LocationChanged;
        public event EventHandler StartManipulating, FinishManipulating;

        public event EventHandler CursorChanged;
        private bool _isOver;
        private bool _shiftPressed, _controlPressed;



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
                foreach (var a in _anchors)
                {
                    if (a.IsManipulating)
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
                var offsetX = MoveOnX ? mouseState.X - (int)_mouseStart.X : 0;
                var offsetY = MoveOnY ? mouseState.Y - (int)_mouseStart.Y : 0;

                _position = new Vector2(_position.X + offsetX, _position.Y + offsetY);

                if (LocationChanged != null)
                {
                    LocationChanged.Invoke(this, new AnchorLocationChangedEventArgs(offsetX, offsetY));
                }
            }

            if (mouseState.LeftButton == ButtonState.Released)
            {
                if (IsManipulating)
                {
                    IsManipulating = false;
                    if (FinishManipulating != null)
                    {
                        FinishManipulating.Invoke(this, EventArgs.Empty);
                    }
                }
            }

            if (Bounds.Contains(mouseX, mouseY))
            {
                if (mouseState.LeftButton == ButtonState.Pressed
                    && _mousePressed == false
                    && CanManipulate)
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

                if (IsManipulating)
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
                    && _isOver)
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
                    && _shiftPressed)
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
                    && _controlPressed)
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

            if (callHandler
                && LocationChanged != null)
            {
                LocationChanged.Invoke(this, new AnchorLocationChangedEventArgs(x, y));
            }
        }

        public void SetLocationOffSet(int offsetX, int offsetY, bool callHandler)
        {
            _position.X += offsetX;
            _position.Y += offsetY;

            if (callHandler
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
