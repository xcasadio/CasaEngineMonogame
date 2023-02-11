
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/


using Microsoft.Xna.Framework;
using Keyboard = XNAFinalEngine.Input.Keyboard;

namespace XNAFinalEngine.UserInterface
{


    public enum TextBoxMode
    {
        Normal,
        Password,
        Multiline
    } // TextBoxMode


    public class TextBox : ClipControl
    {


        private struct Selection
        {
            private int _start;

            private int _end;

            public int Start
            {
                get
                {
                    if (_start > _end && _start != -1 && _end != -1)
                        return _end;
                    return _start;
                }
                set => _start = value;
            } // Start

            public int End
            {
                get
                {
                    if (_end < _start && _start != -1 && _end != -1)
                        return _start;
                    return _end;
                }
                set => _end = value;
            } // End

            public bool IsEmpty => Start == -1 && End == -1; // IsEmpty

            public int Length => IsEmpty ? 0 : (End - Start); // Length

            public Selection(int start, int end)
            {
                this._start = start;
                this._end = end;
            } // Selection

            public void Clear()
            {
                Start = -1;
                End = -1;
            } // Clear
        } // Selection



        private int _positionX;
        private int _positionY;

        private ScrollBarType _scrollBarType = ScrollBarType.Both;

        private readonly ScrollBar _horizontalScrollBar;
        private readonly ScrollBar _verticalScrollBar;

        private char _passwordCharacter = '•';

        private bool _caretVisible = true;

        private bool _autoSelection = true;

        private TextBoxMode _mode = TextBoxMode.Normal;

        private bool _readOnly;

        private bool _drawBorders = true;

        private bool _showCursor;
        private double _flashTime;
        private string _shownText = "";
        private Selection _selection = new Selection(-1, -1);
        private List<string> _lines = new List<string>();
        private int _linesDrawn;
        private int _charsDrawn;
        private CasaEngine.Asset.Fonts.Font _font;
        private bool _wordWrap;
        private const string Separator = "\n";
        private string _text = "";
        private string _buffer = "";

        private string _initialText;




        private int PositionX
        {
            get => _positionX;
            set
            {
                _positionX = value;
                if (_positionX < 0)
                    _positionX = 0;
                if (_positionX > _lines[PositionY].Length)
                    _positionX = _lines[PositionY].Length;
            }
        } // positionX

        private int PositionY
        {
            get => _positionY;
            set
            {
                _positionY = value;

                if (_positionY < 0)
                    _positionY = 0;
                if (_positionY > _lines.Count - 1)
                    _positionY = _lines.Count - 1;
                if (_positionX > _lines[PositionY].Length)
                    _positionX = _lines[PositionY].Length;
            }
        } // PositionY

        private int Position
        {
            get => GetPos(PositionX, PositionY);
            set
            {
                PositionY = GetPosY(value);
                PositionX = GetPosX(value);
            }
        } // Position


        public virtual ScrollBarType ScrollBars
        {
            get => _scrollBarType;
            set
            {
                _scrollBarType = value;
                SetupScrollBars();
            }
        } // ScrollBars

        public virtual char PasswordCharacter
        {
            get => _passwordCharacter;
            set { _passwordCharacter = value; if (ClientArea != null) ClientArea.Invalidate(); }
        } // PasswordCharacter

        public virtual bool CaretVisible
        {
            get => _caretVisible;
            set => _caretVisible = value;
        } // CaretVisible

        public virtual TextBoxMode Mode
        {
            get => _mode;
            set
            {
                if (value != TextBoxMode.Multiline)
                {
                    Text = Text.Replace(Separator, "");
                }
                _mode = value;
                _selection.Clear();

                if (ClientArea != null) ClientArea.Invalidate();
                SetupScrollBars();
            }
        } // TextBoxMode

        public virtual bool ReadOnly
        {
            get => _readOnly;
            set => _readOnly = value;
        } // ReadOnly

        public virtual bool DrawBorders
        {
            get => _drawBorders;
            set { _drawBorders = value; if (ClientArea != null) ClientArea.Invalidate(); }
        } // DrawBorders


        public virtual string SelectedText
        {
            get
            {
                if (_selection.IsEmpty)
                {
                    return "";
                }
                return Text.Substring(_selection.Start, _selection.Length);
            }
        } // SelectedText

        public virtual int SelectionStart
        {
            get
            {
                if (_selection.IsEmpty)
                    return Position;
                return _selection.Start;
            }
            set
            {
                Position = value;
                if (Position < 0) Position = 0;
                if (Position > Text.Length) Position = Text.Length;
                _selection.Start = Position;
                if (_selection.End == -1) _selection.End = Position;
                ClientArea.Invalidate();
            }
        } // SelectionStart

        public virtual bool AutoSelection
        {
            get => _autoSelection;
            set => _autoSelection = value;
        } // AutoSelection

        public virtual int SelectionLength
        {
            get => _selection.Length;
            set
            {
                if (value == 0)
                {
                    _selection.End = _selection.Start;
                }
                else if (_selection.IsEmpty)
                {
                    _selection.Start = 0;
                    _selection.End = value;
                }
                else if (!_selection.IsEmpty)
                {
                    _selection.End = _selection.Start + value;
                }

                if (!_selection.IsEmpty)
                {
                    if (_selection.Start < 0) _selection.Start = 0;
                    if (_selection.Start > Text.Length) _selection.Start = Text.Length;
                    if (_selection.End < 0) _selection.End = 0;
                    if (_selection.End > Text.Length) _selection.End = Text.Length;
                }
                ClientArea.Invalidate();
            }
        } // SelectionLength


        public override string Text
        {
            get => _text;
            set
            {
                if (_mode != TextBoxMode.Multiline && value != null)
                {
                    value = value.Replace(Separator, "");
                }

                _text = value;

                if (!Suspended) OnTextChanged(new EventArgs());

                _lines = SplitLines(_text);
                if (ClientArea != null) ClientArea.Invalidate();

                SetupScrollBars();
                ProcessScrolling();
            }
        } // Text



        public TextBox(UserInterfaceManager userInterfaceManager)
            : base(userInterfaceManager)
        {
            CheckLayer(SkinInformation, "Cursor");

            SetDefaultSize(128, 20);
            _lines.Add("");

            _verticalScrollBar = new ScrollBar(UserInterfaceManager, Orientation.Vertical)
            {
                Range = 1,
                PageSize = 1,
                Value = 0,
                Anchor = Anchors.Top | Anchors.Right | Anchors.Bottom,
                Visible = false
            };
            _horizontalScrollBar = new ScrollBar(UserInterfaceManager, Orientation.Horizontal)
            {
                Range = ClientArea.Width,
                PageSize = ClientArea.Width,
                Value = 0,
                Anchor = Anchors.Right | Anchors.Left | Anchors.Bottom,
                Visible = false
            };
        } // TextBox



        protected internal override void Init()
        {
            base.Init();
            ClientArea.Draw += ClientAreaDraw;
            _verticalScrollBar.ValueChanged += ScrollBarValueChanged;
            _horizontalScrollBar.ValueChanged += ScrollBarValueChanged;
            Add(_verticalScrollBar, false);
            Add(_horizontalScrollBar, false);
            FocusGained += delegate { _initialText = Text; };
        } // Init

        protected internal override void InitSkin()
        {
            base.InitSkin();
            SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["TextBox"]);

#if (WINDOWS)
            Cursor = UserInterfaceManager.Skin.Cursors["Text"].Cursor;
#endif

            _font = (SkinInformation.Layers["Control"].Text != null) ? SkinInformation.Layers["Control"].Text.Font.Font : null;
        } // InitSkin



        protected override void DrawControl(Rectangle rect)
        {
            if (_drawBorders)
            {
                base.DrawControl(rect);
            }
        } // DrawControl

        private void DeterminePages()
        {
            if (ClientArea != null)
            {
                int sizey = _font.LineSpacing;
                _linesDrawn = ClientArea.Height / sizey;
                if (_linesDrawn > _lines.Count) _linesDrawn = _lines.Count;

                _charsDrawn = ClientArea.Width - 1;
            }
        } // DeterminePages

        private string GetMaxLine()
        {
            int max = 0;
            int x = 0;

            for (int i = 0; i < _lines.Count; i++)
            {
                if (_lines[i].Length > max)
                {
                    max = _lines[i].Length;
                    x = i;
                }
            }
            return _lines.Count > 0 ? _lines[x] : "";
        } // GetMaxLine

        private void ClientAreaDraw(object sender, DrawEventArgs e)
        {
            Color col = SkinInformation.Layers["Control"].Text.Colors.Enabled;
            SkinLayer cursor = SkinInformation.Layers["Cursor"];
            Alignment al = _mode == TextBoxMode.Multiline ? Alignment.TopLeft : Alignment.MiddleLeft;
            Rectangle r = e.Rectangle;
            bool drawsel = !_selection.IsEmpty;
            string tmpText;

            _font = (SkinInformation.Layers["Control"].Text != null) ? SkinInformation.Layers["Control"].Text.Font.Font : null;

            if (Text != null && _font != null)
            {
                DeterminePages();

                if (_mode == TextBoxMode.Multiline)
                {
                    _shownText = Text;
                    tmpText = _lines[PositionY];
                }
                else if (_mode == TextBoxMode.Password)
                {
                    _shownText = "";
                    foreach (char character in Text)
                    {
                        _shownText = _shownText + _passwordCharacter;
                    }
                    tmpText = _shownText;
                }
                else
                {
                    _shownText = Text;
                    tmpText = _lines[PositionY];
                }

                if (TextColor != UndefinedColor && ControlState != ControlState.Disabled)
                {
                    col = TextColor;
                }

                if (_mode != TextBoxMode.Multiline)
                {
                    _linesDrawn = 0;
                    _verticalScrollBar.Value = 0;
                }

                if (drawsel)
                {
                    DrawSelection(r);
                }

                int sizey = _font.LineSpacing;

                if (_showCursor && _caretVisible)
                {
                    Vector2 size = Vector2.Zero;
                    if (PositionX > 0 && PositionX <= tmpText.Length)
                    {
                        size = _font.MeasureString(tmpText.Substring(0, PositionX));
                    }
                    if (size.Y == 0)
                    {
                        size = _font.MeasureString(" ");
                        size.X = 0;
                    }

                    int m = r.Height - _font.LineSpacing;

                    Rectangle rc = new Rectangle(r.Left - _horizontalScrollBar.Value + (int)size.X, r.Top + m / 2, cursor.Width, _font.LineSpacing);

                    if (_mode == TextBoxMode.Multiline)
                    {
                        rc = new Rectangle(r.Left + (int)size.X - _horizontalScrollBar.Value, r.Top + (int)((PositionY - _verticalScrollBar.Value) * _font.LineSpacing), cursor.Width, _font.LineSpacing);
                    }
                    cursor.Alignment = al;
                    UserInterfaceManager.Renderer.DrawLayer(cursor, rc, col, 0);
                }

                for (int i = 0; i < _linesDrawn + 1; i++)
                {
                    int ii = i + _verticalScrollBar.Value;
                    if (ii >= _lines.Count || ii < 0) break;

                    if (_lines[ii] != "")
                    {
                        if (_mode == TextBoxMode.Multiline)
                        {
                            UserInterfaceManager.Renderer.DrawString(_font, _lines[ii], r.Left - _horizontalScrollBar.Value, r.Top + (i * sizey), col);
                        }
                        else
                        {
                            Rectangle rx = new Rectangle(r.Left - _horizontalScrollBar.Value, r.Top, r.Width, r.Height);
                            UserInterfaceManager.Renderer.DrawString(_font, _shownText, rx, col, al, false);
                        }
                    }
                }
            }
        } // ClientArea_Draw

        private void DrawSelection(Rectangle rect)
        {
            if (!_selection.IsEmpty)
            {
                int s = _selection.Start;
                int e = _selection.End;

                int sl = GetPosY(s);
                int el = GetPosY(e);
                int sc = GetPosX(s);
                int ec = GetPosX(e);

                int hgt = _font.LineSpacing;

                int start = sl;
                int end = el;

                if (start < _verticalScrollBar.Value) start = _verticalScrollBar.Value;
                if (end > _verticalScrollBar.Value + _linesDrawn) end = _verticalScrollBar.Value + _linesDrawn;

                for (int i = start; i <= end; i++)
                {
                    Rectangle r = Rectangle.Empty;

                    if (_mode == TextBoxMode.Normal)
                    {
                        int m = ClientArea.Height - _font.LineSpacing;
                        r = new Rectangle(rect.Left - _horizontalScrollBar.Value + (int)_font.MeasureString(_lines[i].Substring(0, sc)).X, rect.Top + m / 2,
                                         (int)_font.MeasureString(_lines[i].Substring(0, ec + 0)).X - (int)_font.MeasureString(_lines[i].Substring(0, sc)).X, hgt);
                    }
                    else if (sl == el)
                    {
                        r = new Rectangle(rect.Left - _horizontalScrollBar.Value + (int)_font.MeasureString(_lines[i].Substring(0, sc)).X, rect.Top + (i - _verticalScrollBar.Value) * hgt,
                                          (int)_font.MeasureString(_lines[i].Substring(0, ec + 0)).X - (int)_font.MeasureString(_lines[i].Substring(0, sc)).X, hgt);
                    }
                    else
                    {
                        if (i == sl) r = new Rectangle(rect.Left - _horizontalScrollBar.Value + (int)_font.MeasureString(_lines[i].Substring(0, sc)).X, rect.Top + (i - _verticalScrollBar.Value) * hgt, (int)_font.MeasureString(_lines[i]).X - (int)_font.MeasureString(_lines[i].Substring(0, sc)).X, hgt);
                        else if (i == el) r = new Rectangle(rect.Left - _horizontalScrollBar.Value, rect.Top + (i - _verticalScrollBar.Value) * hgt, (int)_font.MeasureString(_lines[i].Substring(0, ec + 0)).X, hgt);
                        else r = new Rectangle(rect.Left - _horizontalScrollBar.Value, rect.Top + (i - _verticalScrollBar.Value) * hgt, (int)_font.MeasureString(_lines[i]).X, hgt);
                    }

                    UserInterfaceManager.Renderer.Draw(UserInterfaceManager.Skin.Images["Control"].Texture.Resource, r, Color.FromNonPremultiplied(160, 160, 160, 128));
                }
            }
        } // DrawSelection



        private int GetStringWidth(string text, int count)
        {
            if (count > text.Length) count = text.Length;
            return (int)_font.MeasureString(text.Substring(0, count)).X;
        } // GetStringWidth

        private void ProcessScrolling()
        {
            if (_verticalScrollBar != null && _horizontalScrollBar != null)
            {
                _verticalScrollBar.PageSize = _linesDrawn;
                _horizontalScrollBar.PageSize = _charsDrawn;

                if (_horizontalScrollBar.PageSize > _horizontalScrollBar.Range) _horizontalScrollBar.PageSize = _horizontalScrollBar.Range;

                if (PositionY >= _verticalScrollBar.Value + _verticalScrollBar.PageSize)
                {
                    _verticalScrollBar.Value = (PositionY + 1) - _verticalScrollBar.PageSize;
                }
                else if (PositionY < _verticalScrollBar.Value)
                {
                    _verticalScrollBar.Value = PositionY;
                }

                if (GetStringWidth(_lines[PositionY], PositionX) >= _horizontalScrollBar.Value + _horizontalScrollBar.PageSize)
                {
                    _horizontalScrollBar.Value = (GetStringWidth(_lines[PositionY], PositionX) + 1) - _horizontalScrollBar.PageSize;
                }
                else if (GetStringWidth(_lines[PositionY], PositionX) < _horizontalScrollBar.Value)
                {
                    _horizontalScrollBar.Value = GetStringWidth(_lines[PositionY], PositionX) - _horizontalScrollBar.PageSize;
                }
            }
        } // ProcessScrolling



        protected internal override void Update(float elapsedTime)
        {
            base.Update(elapsedTime);

            bool showCursorTemp = _showCursor;

            _showCursor = Focused;

            if (Focused)
            {
                _flashTime += elapsedTime;
                _showCursor = _flashTime < 0.5;
                if (_flashTime > 1) _flashTime = 0;
            }
            if (showCursorTemp != _showCursor) ClientArea.Invalidate();
        } // Update



        private int FindPreviousWord(string text)
        {
            bool letter = false;

            int p = Position - 1;
            if (p < 0) p = 0;
            if (p >= text.Length) p = text.Length - 1;

            for (int i = p; i >= 0; i--)
            {
                if (char.IsLetterOrDigit(text[i]))
                {
                    letter = true;
                    continue;
                }
                if (letter && !char.IsLetterOrDigit(text[i]))
                {
                    return i + 1;
                }
            }

            return 0;
        } // FindPreviousWord

        private int FindNextWord(string text)
        {
            bool space = false;

            for (int i = Position; i < text.Length - 1; i++)
            {
                if (!char.IsLetterOrDigit(text[i]))
                {
                    space = true;
                    continue;
                }
                if (space && char.IsLetterOrDigit(text[i]))
                {
                    return i;
                }
            }

            return text.Length;
        } // FindNextWord

        private int GetPosY(int pos)
        {
            if (pos >= Text.Length) return _lines.Count - 1;

            int p = pos;
            for (int i = 0; i < _lines.Count; i++)
            {
                p -= _lines[i].Length + Separator.Length;
                if (p < 0)
                {
                    return i;
                }
            }
            return 0;
        }

        private int GetPosX(int pos)
        {
            if (pos >= Text.Length) return _lines[_lines.Count - 1].Length;

            int p = pos;
            for (int i = 0; i < _lines.Count; i++)
            {
                p -= _lines[i].Length + Separator.Length;
                if (p < 0)
                {
                    p = p + _lines[i].Length + Separator.Length;
                    return p;
                }
            }
            return 0;
        }

        private int GetPos(int x, int y)
        {
            int p = 0;

            for (int i = 0; i < y; i++)
            {
                p += _lines[i].Length + Separator.Length;
            }
            p += x;

            return p;
        }

        private int CharAtPos(Point pos)
        {
            int x = pos.X;
            int y = pos.Y;
            int px = 0;
            int py = 0;

            if (_mode == TextBoxMode.Multiline)
            {
                py = _verticalScrollBar.Value + (int)((y - ClientTop) / _font.LineSpacing);
                if (py < 0) py = 0;
                if (py >= _lines.Count) py = _lines.Count - 1;
            }
            else
            {
                py = 0;
            }

            string str = _mode == TextBoxMode.Multiline ? _lines[py] : _shownText;

            if (!string.IsNullOrEmpty(str))
            {
                for (int i = 1; i <= _lines[py].Length; i++)
                {
                    Vector2 v = _font.MeasureString(str.Substring(0, i)) - (_font.MeasureString(str[i - 1].ToString()) / 3);
                    if (x <= (ClientLeft + (int)v.X) - _horizontalScrollBar.Value)
                    {
                        px = i - 1;
                        break;
                    }
                }
                if (x > ClientLeft + ((int)_font.MeasureString(str).X) - _horizontalScrollBar.Value - (_font.MeasureString(str[str.Length - 1].ToString()).X / 3)) px = str.Length;
            }

            return GetPos(px, py);
        }



        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            _flashTime = 0;

            Position = CharAtPos(e.Position);
            _selection.Clear();

            if (e.Button == MouseButton.Left && _caretVisible && _mode != TextBoxMode.Password)
            {
                _selection.Start = Position;
                _selection.End = Position;
            }
            ClientArea.Invalidate();
        } // OnMouseDown



        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (e.Button == MouseButton.Left && !_selection.IsEmpty && _mode != TextBoxMode.Password && _selection.Length < Text.Length)
            {
                int pos = CharAtPos(e.Position);
                _selection.End = CharAtPos(e.Position);
                Position = pos;

                ClientArea.Invalidate();

                ProcessScrolling();
            }
        } // OnMouseMove



        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button == MouseButton.Left && !_selection.IsEmpty && _mode != TextBoxMode.Password)
            {
                if (_selection.Length == 0) _selection.Clear();
            }
        } // OnMouseUp



        protected override void OnKeyPress(KeyEventArgs e)
        {
            _flashTime = 0;

            if (!e.Handled)
            {
                if (e.Key == Keys.Enter && _mode != TextBoxMode.Multiline && !_readOnly)
                {
                    _initialText = Text;
                    _selection = new Selection(-1, -1);
                    Focused = false;
                }
                if (e.Key == Keys.Escape)
                {
                    if (_initialText != null)
                        Text = _initialText;
                    _selection = new Selection(-1, -1);
                    Focused = false;
                }
                if (e.Key == Keys.A && e.Control && _mode != TextBoxMode.Password)
                {
                    SelectAll();
                }
                if (e.Key == Keys.Up)
                {
                    e.Handled = true;

                    if (e.Shift && _selection.IsEmpty && _mode != TextBoxMode.Password)
                    {
                        _selection.Start = Position;
                    }
                    if (!e.Control)
                    {
                        PositionY -= 1;
                    }
                }
                else if (e.Key == Keys.Down)
                {
                    e.Handled = true;
                    if (e.Shift && _selection.IsEmpty && _mode != TextBoxMode.Password)
                    {
                        _selection.Start = Position;
                    }
                    if (!e.Control)
                    {
                        PositionY += 1;
                    }
                }
                else if (e.Key == Keys.Back && !_readOnly)
                {
                    e.Handled = true;
                    if (!_selection.IsEmpty)
                    {
                        Text = Text.Remove(_selection.Start, _selection.Length);
                        Position = _selection.Start;
                    }
                    else if (Text.Length > 0 && Position > 0)
                    {
                        Position -= 1;
                        Text = Text.Remove(Position, 1);
                    }
                    _selection.Clear();
                }
                else if (e.Key == Keys.Delete && !_readOnly)
                {
                    e.Handled = true;
                    if (!_selection.IsEmpty)
                    {
                        Text = Text.Remove(_selection.Start, _selection.Length);
                        Position = _selection.Start;
                    }
                    else if (Position < Text.Length)
                    {
                        Text = Text.Remove(Position, 1);
                    }
                    _selection.Clear();
                }
                else if (e.Key == Keys.Left)
                {
                    e.Handled = true;
                    if (e.Shift && _selection.IsEmpty && _mode != TextBoxMode.Password)
                    {
                        _selection.Start = Position;
                    }
                    if (!e.Control)
                    {
                        Position -= 1;
                    }
                    if (e.Control)
                    {
                        Position = FindPreviousWord(_shownText);
                    }
                }
                else if (e.Key == Keys.Right)
                {
                    e.Handled = true;
                    if (e.Shift && _selection.IsEmpty && _mode != TextBoxMode.Password)
                    {
                        _selection.Start = Position;
                    }
                    if (!e.Control)
                    {
                        Position += 1;
                    }
                    if (e.Control)
                    {
                        Position = FindNextWord(_shownText);
                    }
                }
                else if (e.Key == Keys.Home)
                {
                    e.Handled = true;
                    if (e.Shift && _selection.IsEmpty && _mode != TextBoxMode.Password)
                    {
                        _selection.Start = Position;
                    }
                    if (!e.Control)
                    {
                        PositionX = 0;
                    }
                    if (e.Control)
                    {
                        Position = 0;
                    }
                }
                else if (e.Key == Keys.End)
                {
                    e.Handled = true;
                    if (e.Shift && _selection.IsEmpty && _mode != TextBoxMode.Password)
                    {
                        _selection.Start = Position;
                    }
                    if (!e.Control)
                    {
                        PositionX = _lines[PositionY].Length;
                    }
                    if (e.Control)
                    {
                        Position = Text.Length;
                    }
                }
                else if (e.Key == Keys.PageUp)
                {
                    e.Handled = true;
                    if (e.Shift && _selection.IsEmpty && _mode != TextBoxMode.Password)
                    {
                        _selection.Start = Position;
                    }
                    if (!e.Control)
                    {
                        PositionY -= _linesDrawn;
                    }
                }
                else if (e.Key == Keys.PageDown)
                {
                    e.Handled = true;
                    if (e.Shift && _selection.IsEmpty && _mode != TextBoxMode.Password)
                    {
                        _selection.Start = Position;
                    }
                    if (!e.Control)
                    {
                        PositionY += _linesDrawn;
                    }
                }
                else if (e.Key == Keys.Enter && _mode == TextBoxMode.Multiline && !_readOnly)
                {
                    e.Handled = true;
                    Text = Text.Insert(Position, Separator);
                    PositionX = 0;
                    PositionY += 1;
                }
                else if (e.Key == Keys.Tab)
                {
                }
                else if (!_readOnly && !e.Control)
                {
                    string c = Keyboard.KeyToString(e.Key, e.Shift, e.Caps);
                    if (_selection.IsEmpty)
                    {
                        Text = Text.Insert(Position, c);
                        if (c != "") PositionX += 1;
                    }
                    else
                    {
                        if (Text.Length > 0)
                        {
                            // Avoid out of range.
                            if (_selection.Start + _selection.Length > Text.Length)
                                Text = Text.Remove(_selection.Start, Text.Length - _selection.Start);
                            else
                                Text = Text.Remove(_selection.Start, _selection.Length);
                            Text = Text.Insert(_selection.Start, c);
                            Position = _selection.Start + 1;
                        }
                        _selection.Clear();
                    }
                }

                if (e.Shift && !_selection.IsEmpty)
                {
                    _selection.End = Position;
                }


                // Windows only because it uses the Clipboard class. Of course this could be implemented manually in the XBOX 360 if you want it.
#if (WINDOWS)
                if (e.Control && e.Key == Keys.C && _mode != TextBoxMode.Password)
                {
                    System.Windows.Forms.Clipboard.Clear();
                    if (_mode != TextBoxMode.Password && !_selection.IsEmpty)
                    {
                        System.Windows.Forms.Clipboard.SetText((Text.Substring(_selection.Start, _selection.Length)).Replace("\n", Environment.NewLine));
                    }
                }
                else if (e.Control && e.Key == Keys.V && !_readOnly && _mode != TextBoxMode.Password)
                {
                    string t = System.Windows.Forms.Clipboard.GetText().Replace(Environment.NewLine, "\n");
                    if (_selection.IsEmpty)
                    {
                        Text = Text.Insert(Position, t);
                        Position = Position + t.Length;
                    }
                    else
                    {
                        Text = Text.Remove(_selection.Start, _selection.Length);
                        Text = Text.Insert(_selection.Start, t);
                        PositionX = _selection.Start + t.Length;
                        _selection.Clear();
                    }
                }
#endif


                if ((!e.Shift && !e.Control) || Text.Length <= 0)
                {
                    _selection.Clear();
                }

                if (e.Control && e.Key == Keys.Down)
                {
                    e.Handled = true;
                    HandleGuide(PlayerIndex.One);
                }
                _flashTime = 0;
                if (ClientArea != null) ClientArea.Invalidate();

                DeterminePages();
                ProcessScrolling();
            }
            base.OnKeyPress(e);
        } // OnKeyPress



        private void HandleGuide(PlayerIndex pi)
        {
            //if (!Guide.IsVisible)
            //{
            //    Guide.BeginShowKeyboardInput(pi, "Enter Text", "", Text, GetText, pi.ToString());
            //}
        } // HandleGuide

        private void GetText(IAsyncResult result)
        {
            //string res = Guide.EndShowKeyboardInput(result);
            //Text = res != null ? res : "";
            //Position = text.Length;
        } // GetText



        private void SetupScrollBars()
        {
            DeterminePages();

            if (_verticalScrollBar != null) _verticalScrollBar.Range = _lines.Count;
            if (_horizontalScrollBar != null)
            {
                _horizontalScrollBar.Range = (int)_font.MeasureString(GetMaxLine()).X;
                if (_horizontalScrollBar.Range == 0) _horizontalScrollBar.Range = ClientArea.Width;
            }

            if (_verticalScrollBar != null)
            {
                _verticalScrollBar.Left = Width - 16 - 2;
                _verticalScrollBar.Top = 2;
                _verticalScrollBar.Height = Height - 4 - 16;

                if (Height < 50 || (_scrollBarType != ScrollBarType.Both && _scrollBarType != ScrollBarType.Vertical)) _verticalScrollBar.Visible = false;
                else if ((_scrollBarType == ScrollBarType.Vertical || _scrollBarType == ScrollBarType.Both) && _mode == TextBoxMode.Multiline) _verticalScrollBar.Visible = true;
            }
            if (_horizontalScrollBar != null)
            {
                _horizontalScrollBar.Left = 2;
                _horizontalScrollBar.Top = Height - 16 - 2;
                _horizontalScrollBar.Width = Width - 4 - 16;

                if (Width < 50 || _wordWrap || (_scrollBarType != ScrollBarType.Both && _scrollBarType != ScrollBarType.Horizontal)) _horizontalScrollBar.Visible = false;
                else if ((_scrollBarType == ScrollBarType.Horizontal || _scrollBarType == ScrollBarType.Both) && _mode == TextBoxMode.Multiline && !_wordWrap) _horizontalScrollBar.Visible = true;
            }

            AdjustMargins();

            if (_verticalScrollBar != null) _verticalScrollBar.PageSize = _linesDrawn;
            if (_horizontalScrollBar != null) _horizontalScrollBar.PageSize = _charsDrawn;
        } // SetupScrollBars



        protected override void AdjustMargins()
        {
            if (_horizontalScrollBar != null && !_horizontalScrollBar.Visible)
            {
                _verticalScrollBar.Height = Height - 4;
                ClientMargins = new Margins(ClientMargins.Left, ClientMargins.Top, ClientMargins.Right, SkinInformation.ClientMargins.Bottom);
            }
            else
            {
                ClientMargins = new Margins(ClientMargins.Left, ClientMargins.Top, ClientMargins.Right, 18 + SkinInformation.ClientMargins.Bottom);
            }

            if (_verticalScrollBar != null && !_verticalScrollBar.Visible)
            {
                _horizontalScrollBar.Width = Width - 4;
                ClientMargins = new Margins(ClientMargins.Left, ClientMargins.Top, SkinInformation.ClientMargins.Right, ClientMargins.Bottom);
            }
            else
            {
                ClientMargins = new Margins(ClientMargins.Left, ClientMargins.Top, 18 + SkinInformation.ClientMargins.Right, ClientMargins.Bottom);
            }
            base.AdjustMargins();
        } // AdjustMargins



        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            _selection.Clear();
            SetupScrollBars();
        } // OnResize



        public virtual void SelectAll()
        {
            if (_text.Length > 0)
            {
                _selection.Start = 0;
                _selection.End = Text.Length;
            }
        } // SelectAll



        private List<string> SplitLines(string text)
        {
            if (_buffer != text)
            {
                _buffer = text;
                List<string> list = new List<string>();
                string[] s = text.Split(new char[] { Separator[0] });
                list.Clear();

                list.AddRange(s);

                if (_positionY < 0) _positionY = 0;
                if (_positionY > list.Count - 1) _positionY = list.Count - 1;

                if (_positionX < 0) _positionX = 0;
                if (_positionX > list[PositionY].Length) _positionX = list[PositionY].Length;

                return list;
            }
            return _lines;
        } // SplitLines



        void ScrollBarValueChanged(object sender, EventArgs e)
        {
            ClientArea.Invalidate();
        } // scrollBarValueChanged



        protected override void OnFocusLost()
        {
            _selection.Clear();
            ClientArea.Invalidate();
            base.OnFocusLost();
        } // OnFocusLost

        protected override void OnFocusGained()
        {
            if (!_readOnly && _autoSelection
                && ClientArea != null)
            {
                SelectAll();
                ClientArea.Invalidate();
            }
            base.OnFocusGained();
        } // OnFocusGained


    } // TextBox
} // XNAFinalEngine.UserInterface