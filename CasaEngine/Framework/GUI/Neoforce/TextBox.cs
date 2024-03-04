using CasaEngine.Framework.GUI.Neoforce.Graphics;
using CasaEngine.Framework.GUI.Neoforce.Input;
using CasaEngine.Framework.GUI.Neoforce.Skins;
using FontStashSharp;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.GUI.Neoforce;

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
                {
                    return _end;
                }

                return _start;
            }
            set => _start = value;
        }

        public int End
        {
            get
            {
                if (_end < _start && _start != -1 && _end != -1)
                {
                    return _start;
                }

                return _end;
            }
            set => _end = value;
        }

        public bool IsEmpty => Start == -1 && End == -1;

        public int Length => IsEmpty ? 0 : End - Start;

        public Selection(int start, int end)
        {
            _start = start;
            _end = end;
        }

        public void Clear()
        {
            Start = -1;
            End = -1;
        }
    }

    private const string SkTextBox = "TextBox";
    private const string LrTextBox = "Control";
    private const string LrCursor = "Cursor";

    private const string CrDefault = "Default";
    private const string CrText = "Text";

    private bool _showCursor;
    private double _flashTime;
    private int _posx;
    private int _posy;
    private char _passwordChar = '•';
    private TextBoxMode _mode = TextBoxMode.Normal;
    private string _shownText = "";
    private bool _readOnly;
    private bool _drawBorders = true;
    private Selection _selection = new(-1, -1);
    private bool _caretVisible = true;
    private ScrollBar _horz;
    private ScrollBar _vert;
    private int _linesDrawn;
    private int _charsDrawn;
    private SpriteFontBase _font;
    private bool _wordWrap;
    private ScrollBars _scrollBars = ScrollBars.Both;
    private string _separator = "\n";
    private string _text = "";
    private string _buffer = "";
    private bool _autoSelection = true;

    public string Placeholder { get; set; } = "";

    public Color PlaceholderColor { get; set; } = Color.LightGray;

    private int PosX
    {
        get => _posx;
        set
        {
            _posx = value;

            if (_posx < 0)
            {
                _posx = 0;
            }

            if (_posx > Lines[PosY].Length)
            {
                _posx = Lines[PosY].Length;
            }
        }
    }

    private int PosY
    {
        get => _posy;
        set
        {
            _posy = value;

            if (_posy < 0)
            {
                _posy = 0;
            }

            if (_posy > Lines.Count - 1)
            {
                _posy = Lines.Count - 1;
            }

            PosX = PosX;
        }
    }

    private int Pos
    {
        get => GetPos(PosX, PosY);
        set
        {
            PosY = GetPosY(value);
            PosX = GetPosX(value);
        }
    }

    //>>>>

    public virtual bool WordWrap
    {
        get => _wordWrap;
        set
        {
            _wordWrap = value;
            ClientArea?.Invalidate();

            SetupBars();
        }
    }

    public virtual ScrollBars ScrollBars
    {
        get => _scrollBars;
        set
        {
            _scrollBars = value;
            SetupBars();
        }
    }

    public virtual char PasswordChar
    {
        get => _passwordChar;
        set
        {
            _passwordChar = value;
            ClientArea?.Invalidate();
        }
    }

    public virtual bool CaretVisible
    {
        get => _caretVisible;
        set => _caretVisible = value;
    }

    public virtual TextBoxMode Mode
    {
        get => _mode;
        set
        {
            if (value != TextBoxMode.Multiline)
            {
                Text = Text.Replace(_separator, "");
            }
            _mode = value;
            _selection.Clear();

            ClientArea?.Invalidate();

            SetupBars();
        }
    }

    public virtual bool ReadOnly
    {
        get => _readOnly;
        set => _readOnly = value;
    }

    public virtual bool DrawBorders
    {
        get => _drawBorders;
        set
        {
            _drawBorders = value;
            ClientArea?.Invalidate();
        }
    }

    public virtual int CursorPosition
    {
        get => Pos;
        set => Pos = value;
    }

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
    }

    public virtual int SelectionStart
    {
        get
        {
            if (_selection.IsEmpty)
            {
                return Pos;
            }

            return _selection.Start;
        }
        set
        {
            Pos = value;
            if (Pos < 0)
            {
                Pos = 0;
            }

            if (Pos > Text.Length)
            {
                Pos = Text.Length;
            }

            _selection.Start = Pos;
            if (_selection.End == -1)
            {
                _selection.End = Pos;
            }

            ClientArea.Invalidate();
        }
    }

    public virtual bool AutoSelection
    {
        get => _autoSelection;
        set => _autoSelection = value;
    }

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
                if (_selection.Start < 0)
                {
                    _selection.Start = 0;
                }

                if (_selection.Start > Text.Length)
                {
                    _selection.Start = Text.Length;
                }

                if (_selection.End < 0)
                {
                    _selection.End = 0;
                }

                if (_selection.End > Text.Length)
                {
                    _selection.End = Text.Length;
                }
            }
            ClientArea.Invalidate();
        }
    }

    private List<string> Lines { get; set; } = new();

    public override string Text
    {
        get => _text;
        set
        {
            if (_wordWrap)
            {
                value = WrapWords(value, ClientWidth);
            }

            if (_mode != TextBoxMode.Multiline && value != null)
            {
                value = value.Replace(_separator, "");
            }

            _text = value;

            if (!Suspended)
            {
                OnTextChanged(new EventArgs());
            }

            Lines = SplitLines(_text);
            ClientArea?.Invalidate();

            SetupBars();
            ProcessScrolling();
        }
    }

    public override void Initialize(Manager manager)
    {
        base.Initialize(manager);

        Lines.Add("");
        CheckLayer(Skin, LrCursor);
        SetDefaultSize(128, 20);
        ClientArea.Draw += ClientArea_Draw;

        _vert = new ScrollBar(Orientation.Vertical);
        _horz = new ScrollBar(Orientation.Horizontal);

        _vert.Initialize(manager);
        _vert.Range = 1;
        _vert.PageSize = 1;
        _vert.Value = 0;
        _vert.Anchor = Anchors.Top | Anchors.Right | Anchors.Bottom;
        _vert.ValueChanged += sb_ValueChanged;

        _horz.Initialize(manager);
        _horz.Range = ClientArea.Width;
        _horz.PageSize = ClientArea.Width;
        _horz.Value = 0;
        _horz.Anchor = Anchors.Right | Anchors.Left | Anchors.Bottom;
        _horz.ValueChanged += sb_ValueChanged;

        _horz.Visible = false;
        _vert.Visible = false;

        Add(_vert, false);
        Add(_horz, false);
    }

    protected internal override void InitializeSkin()
    {
        base.InitializeSkin();
        Skin = new SkinControl(Manager.Skin.Controls[SkTextBox]);
        Cursor = Manager.Skin.Cursors[CrText]?.Resource;
        _font = Skin.Layers[LrTextBox]?.Text?.Font.Resource;
    }

    protected override void DrawControl(IRenderer renderer, Rectangle rect, GameTime gameTime)
    {
        if (_drawBorders)
        {
            base.DrawControl(renderer, rect, gameTime);
        }
    }

    private int GetFitChars(string text, int width)
    {
        var ret = text.Length;
        var size = 0;

        for (var i = 0; i < text.Length; i++)
        {
            size = (int)_font.MeasureString(text.Substring(0, i)).X;
            if (size > width)
            {
                ret = i;
                break;
            }
        }

        return ret;
    }

    private void DeterminePages()
    {
        if (ClientArea != null && _font != null)
        {
            var sizey = _font.LineHeight;
            _linesDrawn = ClientArea.Height / sizey;
            if (_linesDrawn > Lines.Count)
            {
                _linesDrawn = Lines.Count;
            }

            _charsDrawn = ClientArea.Width - 1;
        }
    }

    private string GetMaxLine()
    {
        var max = 0;
        var x = 0;

        for (var i = 0; i < Lines.Count; i++)
        {
            if (Lines[i].Length > max)
            {
                max = Lines[i].Length;
                x = i;
            }
        }
        return Lines.Count > 0 ? Lines[x] : "";
    }

    void ClientArea_Draw(object sender, DrawEventArgs e)
    {
        var layer = Skin.Layers[LrTextBox];

        if (layer == null || _font == null)
        {
            return;
        }

        var col = layer.Text.Colors.Enabled;
        var cursor = Skin.Layers[LrCursor];
        var al = _mode == TextBoxMode.Multiline ? Alignment.TopLeft : Alignment.MiddleLeft;
        var renderer = e.Renderer;
        var r = e.Rectangle;
        var drawsel = !_selection.IsEmpty;
        var tmpText = "";

        _font = layer.Text?.Font.Resource;

        if (Text != null && _font != null)
        {
            DeterminePages();

            if (_mode == TextBoxMode.Multiline)
            {
                _shownText = Text;
                tmpText = Lines[PosY];
            }
            else if (_mode == TextBoxMode.Password)
            {
                _shownText = "";
                for (var i = 0; i < Text.Length; i++)
                {
                    _shownText = _shownText + _passwordChar;
                }
                tmpText = _shownText;
            }
            else
            {
                _shownText = Text;
                tmpText = Lines[PosY];
            }

            if (TextColor != UndefinedColor && ControlState != ControlState.Disabled)
            {
                col = TextColor;
            }

            if (_mode != TextBoxMode.Multiline)
            {
                _linesDrawn = 0;
                _vert.Value = 0;
            }

            if (string.IsNullOrEmpty(_text))
            {
                var rx = new Rectangle(r.Left - _horz.Value, r.Top, r.Width, r.Height);
                renderer.DrawString(_font, Placeholder, rx, PlaceholderColor, al, false);
            }

            if (drawsel)
            {
                DrawSelection(e.Renderer, r);
                /*
                          renderer.End();          
                          renderer.SpriteBatch.Begin(SpriteBlendMode.AlphaBlend, SpriteSortMode.Immediate, SaveStateMode.None);
                          renderer.SpriteBatch.GraphicsDevice.RenderState.SeparateAlphaBlendEnabled = true;
                          renderer.SpriteBatch.GraphicsDevice.RenderState.SourceBlend = Blend.DestinationColor;
                          renderer.SpriteBatch.GraphicsDevice.RenderState.DestinationBlend = Blend.SourceColor;
                          renderer.SpriteBatch.GraphicsDevice.RenderState.BlendFunction = BlendFunction.Subtract;          
                          //renderer.SpriteBatch.GraphicsDevice.RenderState.AlphaFunction = CompareFunction.Equal;
                          //renderer.SpriteBatch.GraphicsDevice.RenderState.AlphaSourceBlend = Blend.One;
                          //renderer.SpriteBatch.GraphicsDevice.RenderState.AlphaDestinationBlend = Blend.DestinationAlpha;
                 */
            }

            var sizey = _font.LineHeight;

            if (_showCursor && _caretVisible)
            {
                var size = Vector2.Zero;
                if (PosX > 0 && PosX <= tmpText.Length)
                {
                    size = _font.MeasureString(tmpText.Substring(0, PosX));
                }
                if (size.Y == 0)
                {
                    size = _font.MeasureString(" ");
                    size.X = 0;
                }

                var m = r.Height - _font.LineHeight;

                var rc = new Rectangle(r.Left - _horz.Value + (int)size.X, r.Top + m / 2, cursor.Width, _font.LineHeight);

                if (_mode == TextBoxMode.Multiline)
                {
                    rc = new Rectangle(r.Left + (int)size.X - _horz.Value, r.Top + (PosY - _vert.Value) * _font.LineHeight, cursor.Width, _font.LineHeight);
                }
                cursor.Alignment = al;
                renderer.DrawLayer(cursor, rc, col, 0);
            }

            for (var i = 0; i < _linesDrawn + 1; i++)
            {
                var ii = i + _vert.Value;
                if (ii >= Lines.Count || ii < 0)
                {
                    break;
                }

                if (Lines[ii] != "")
                {
                    if (_mode == TextBoxMode.Multiline)
                    {
                        renderer.DrawString(_font, Lines[ii], r.Left - _horz.Value, r.Top + i * sizey, col);
                    }
                    else
                    {
                        var rx = new Rectangle(r.Left - _horz.Value, r.Top, r.Width, r.Height);
                        renderer.DrawString(_font, _shownText, rx, col, al, false);
                    }
                }
            }
            /*  if (drawsel)
              {
                renderer.End();
                renderer.Begin(BlendingMode.Premultiplied);
              }*/
        }
    }

    private int GetStringWidth(string text, int count)
    {
        if (count > text.Length)
        {
            count = text.Length;
        }

        if (_font == null)
        {
            return 0;
        }

        return (int)_font.MeasureString(text.Substring(0, count)).X;
    }

    private void ProcessScrolling()
    {
        if (_vert != null && _horz != null)
        {
            _vert.PageSize = _linesDrawn;
            _horz.PageSize = _charsDrawn;

            if (_horz.PageSize > _horz.Range)
            {
                _horz.PageSize = _horz.Range;
            }

            if (PosY >= _vert.Value + _vert.PageSize)
            {
                _vert.Value = PosY + 1 - _vert.PageSize;
            }
            else if (PosY < _vert.Value)
            {
                _vert.Value = PosY;
            }

            if (GetStringWidth(Lines[PosY], PosX) >= _horz.Value + _horz.PageSize)
            {
                _horz.Value = GetStringWidth(Lines[PosY], PosX) + 1 - _horz.PageSize;
            }
            else if (GetStringWidth(Lines[PosY], PosX) < _horz.Value)
            {
                _horz.Value = GetStringWidth(Lines[PosY], PosX) - _horz.PageSize;
            }
        }
    }

    private void DrawSelection(IRenderer renderer, Rectangle rect)
    {
        if (!_selection.IsEmpty)
        {
            var s = _selection.Start;
            var e = _selection.End;

            var sl = GetPosY(s);
            var el = GetPosY(e);
            var sc = GetPosX(s);
            var ec = GetPosX(e);

            var hgt = _font.LineHeight;

            var start = sl;
            var end = el;

            if (start < _vert.Value)
            {
                start = _vert.Value;
            }

            if (end > _vert.Value + _linesDrawn)
            {
                end = _vert.Value + _linesDrawn;
            }

            for (var i = start; i <= end; i++)
            {
                var r = Rectangle.Empty;

                if (_mode == TextBoxMode.Normal)
                {
                    var m = ClientArea.Height - _font.LineHeight;
                    r = new Rectangle(rect.Left - _horz.Value + (int)_font.MeasureString(Lines[i].Substring(0, sc)).X, rect.Top + m / 2,
                        (int)_font.MeasureString(Lines[i].Substring(0, ec + 0)).X - (int)_font.MeasureString(Lines[i].Substring(0, sc)).X, hgt);
                }
                else if (sl == el)
                {
                    r = new Rectangle(rect.Left - _horz.Value + (int)_font.MeasureString(Lines[i].Substring(0, sc)).X, rect.Top + (i - _vert.Value) * hgt,
                        (int)_font.MeasureString(Lines[i].Substring(0, ec + 0)).X - (int)_font.MeasureString(Lines[i].Substring(0, sc)).X, hgt);
                }
                else
                {
                    if (i == sl)
                    {
                        r = new Rectangle(rect.Left - _horz.Value + (int)_font.MeasureString(Lines[i].Substring(0, sc)).X, rect.Top + (i - _vert.Value) * hgt, (int)_font.MeasureString(Lines[i]).X - (int)_font.MeasureString(Lines[i].Substring(0, sc)).X, hgt);
                    }
                    else if (i == el)
                    {
                        r = new Rectangle(rect.Left - _horz.Value, rect.Top + (i - _vert.Value) * hgt, (int)_font.MeasureString(Lines[i].Substring(0, ec + 0)).X, hgt);
                    }
                    else
                    {
                        r = new Rectangle(rect.Left - _horz.Value, rect.Top + (i - _vert.Value) * hgt, (int)_font.MeasureString(Lines[i]).X, hgt);
                    }
                }

                renderer.Draw(Manager.Skin.Images["Control"].Resource, r, Color.FromNonPremultiplied(160, 160, 160, 128));
            }
        }
    }

    protected internal override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        var sc = _showCursor;

        _showCursor = Focused;

        if (Focused)
        {
            _flashTime += gameTime.ElapsedGameTime.TotalSeconds;
            _showCursor = _flashTime < 0.5;
            if (_flashTime > 1)
            {
                _flashTime = 0;
            }
        }
        if (sc != _showCursor)
        {
            ClientArea.Invalidate();
        }
    }

    private int FindPrevWord(string text)
    {
        var letter = false;

        var p = Pos - 1;
        if (p < 0)
        {
            p = 0;
        }

        if (p >= text.Length)
        {
            p = text.Length - 1;
        }

        for (var i = p; i >= 0; i--)
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
    }

    private int FindNextWord(string text)
    {
        var space = false;

        for (var i = Pos; i < text.Length - 1; i++)
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
    }

    private int GetPosY(int pos)
    {
        if (pos >= Text.Length)
        {
            return Lines.Count - 1;
        }

        var p = pos;
        for (var i = 0; i < Lines.Count; i++)
        {
            p -= Lines[i].Length + _separator.Length;
            if (p < 0)
            {
                p = p + Lines[i].Length + _separator.Length;
                return i;
            }
        }
        return 0;
    }

    private int GetPosX(int pos)
    {
        if (pos >= Text.Length)
        {
            return Lines[Lines.Count - 1].Length;
        }

        var p = pos;
        for (var i = 0; i < Lines.Count; i++)
        {
            p -= Lines[i].Length + _separator.Length;
            if (p < 0)
            {
                p = p + Lines[i].Length + _separator.Length;
                return p;
            }
        }
        return 0;
    }

    private int GetPos(int x, int y)
    {
        var p = 0;

        for (var i = 0; i < y; i++)
        {
            p += Lines[i].Length + _separator.Length;
        }
        p += x;

        return p;
    }

    private int CharAtPos(Point pos)
    {
        var x = pos.X;
        var y = pos.Y;
        var px = 0;
        var py = 0;

        if (_mode == TextBoxMode.Multiline)
        {
            py = _vert.Value + (y - ClientTop) / _font.LineHeight;
            if (py < 0)
            {
                py = 0;
            }

            if (py >= Lines.Count)
            {
                py = Lines.Count - 1;
            }
        }
        else
        {
            py = 0;
        }

        var str = _mode == TextBoxMode.Multiline ? Lines[py] : _shownText;

        if (str != null && str != "")
        {
            for (var i = 1; i <= Lines[py].Length; i++)
            {
                var v = _font.MeasureString(str.Substring(0, i)) - _font.MeasureString(str[i - 1].ToString()) / 3;
                if (x <= ClientLeft + (int)v.X - _horz.Value)
                {
                    px = i - 1;
                    break;
                }
            }
            if (x > ClientLeft + (int)_font.MeasureString(str).X - _horz.Value - _font.MeasureString(str[str.Length - 1].ToString()).X / 3)
            {
                px = str.Length;
            }
        }

        return GetPos(px, py);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        _flashTime = 0;

        Pos = CharAtPos(e.Position);
        _selection.Clear();

        if (e.Button == MouseButton.Left && _caretVisible && _mode != TextBoxMode.Password)
        {
            _selection.Start = Pos;
            _selection.End = Pos;
        }
        ClientArea.Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (e.Button == MouseButton.Left && !_selection.IsEmpty && _mode != TextBoxMode.Password && _selection.Length < Text.Length)
        {
            var pos = CharAtPos(e.Position);
            _selection.End = CharAtPos(e.Position);
            Pos = pos;

            ClientArea.Invalidate();

            ProcessScrolling();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button == MouseButton.Left && !_selection.IsEmpty && _mode != TextBoxMode.Password)
        {
            if (_selection.Length == 0)
            {
                _selection.Clear();
            }
        }
    }

    protected override void OnMouseScroll(MouseEventArgs e)
    {
        if (Mode != TextBoxMode.Multiline)
        {
            base.OnMouseScroll(e);
            return;
        }

        if (e.ScrollDirection == MouseScrollDirection.Down)
        {
            _vert.ScrollDown();
        }
        else
        {
            _vert.ScrollUp();
        }

        base.OnMouseScroll(e);
    }

    protected override void OnKeyPress(KeyEventArgs e)
    {
        _flashTime = 0;

        if (Manager.UseGuide)
        {
            return;
        }

        if (!e.Handled)
        {
            if (e.Key == Keys.A && e.Control && _mode != TextBoxMode.Password)
            {
                SelectAll();
            }
            if (e.Key == Keys.Up)
            {
                e.Handled = true;

                if (e.Shift && _selection.IsEmpty && _mode != TextBoxMode.Password)
                {
                    _selection.Start = Pos;
                }
                if (!e.Control)
                {
                    PosY -= 1;
                }
            }
            else if (e.Key == Keys.Down)
            {
                e.Handled = true;
                if (e.Shift && _selection.IsEmpty && _mode != TextBoxMode.Password)
                {
                    _selection.Start = Pos;
                }
                if (!e.Control)
                {
                    PosY += 1;
                }
            }
            else if (e.Key == Keys.Back && !_readOnly)
            {
                e.Handled = true;
                if (!_selection.IsEmpty)
                {
                    Text = Text.Remove(_selection.Start, _selection.Length);
                    Pos = _selection.Start;
                }
                else if (Text.Length > 0 && Pos > 0)
                {
                    Pos -= 1;
                    Text = Text.Remove(Pos, 1);
                }
                _selection.Clear();
            }
            else if (e.Key == Keys.Delete && !_readOnly)
            {
                e.Handled = true;
                if (!_selection.IsEmpty)
                {
                    Text = Text.Remove(_selection.Start, _selection.Length);
                    Pos = _selection.Start;
                }
                else if (Pos < Text.Length)
                {
                    Text = Text.Remove(Pos, 1);
                }
                _selection.Clear();
            }
            else if (e.Key == Keys.Left)
            {
                e.Handled = true;
                if (e.Shift && _selection.IsEmpty && _mode != TextBoxMode.Password)
                {
                    _selection.Start = Pos;
                }
                if (!e.Control)
                {
                    Pos -= 1;
                }
                if (e.Control)
                {
                    Pos = FindPrevWord(_shownText);
                }
            }
            else if (e.Key == Keys.Right)
            {
                e.Handled = true;
                if (e.Shift && _selection.IsEmpty && _mode != TextBoxMode.Password)
                {
                    _selection.Start = Pos;
                }
                if (!e.Control)
                {
                    Pos += 1;
                }
                if (e.Control)
                {
                    Pos = FindNextWord(_shownText);
                }
            }
            else if (e.Key == Keys.Home)
            {
                e.Handled = true;
                if (e.Shift && _selection.IsEmpty && _mode != TextBoxMode.Password)
                {
                    _selection.Start = Pos;
                }
                if (!e.Control)
                {
                    PosX = 0;
                }
                if (e.Control)
                {
                    Pos = 0;
                }
            }
            else if (e.Key == Keys.End)
            {
                e.Handled = true;
                if (e.Shift && _selection.IsEmpty && _mode != TextBoxMode.Password)
                {
                    _selection.Start = Pos;
                }
                if (!e.Control)
                {
                    PosX = Lines[PosY].Length;
                }
                if (e.Control)
                {
                    Pos = Text.Length;
                }
            }
            else if (e.Key == Keys.PageUp)
            {
                e.Handled = true;
                if (e.Shift && _selection.IsEmpty && _mode != TextBoxMode.Password)
                {
                    _selection.Start = Pos;
                }
                if (!e.Control)
                {
                    PosY -= _linesDrawn;
                }
            }
            else if (e.Key == Keys.PageDown)
            {
                e.Handled = true;
                if (e.Shift && _selection.IsEmpty && _mode != TextBoxMode.Password)
                {
                    _selection.Start = Pos;
                }
                if (!e.Control)
                {
                    PosY += _linesDrawn;
                }
            }
            else if (e.Key == Keys.Enter && _mode == TextBoxMode.Multiline && !_readOnly)
            {
                e.Handled = true;
                Text = Text.Insert(Pos, _separator);
                PosX = 0;
                PosY += 1;
            }
            else if (e.Key == Keys.Tab)
            {
            }
            else if (!_readOnly && !e.Control)
            {
                var c = Manager.KeyboardLayout.GetKey(e);
                if (_selection.IsEmpty)
                {
                    Text = Text.Insert(Pos, c);
                    if (c != "")
                    {
                        PosX += 1;
                    }
                }
                else
                {
                    if (Text.Length > 0)
                    {
                        Text = Text.Remove(_selection.Start, _selection.Length);
                        Text = Text.Insert(_selection.Start, c);
                        Pos = _selection.Start + 1;
                    }
                    _selection.Clear();
                }
            }

            if (e.Shift && !_selection.IsEmpty)
            {
                _selection.End = Pos;
            }

            /*
             * TODO: Fix
             * MONOTODO: Fix
          if (e.Control && e.Key == Keys.C && mode != TextBoxMode.Password)
          {
  
            System.Windows.Forms.Clipboard.Clear();
            if (mode != TextBoxMode.Password && !selection.IsEmpty)
            {
              System.Windows.Forms.Clipboard.SetText((Text.Substring(selection.Start, selection.Length)).Replace("\n", Environment.NewLine));
            }
  #endif
          }
          else if (e.Control && e.Key == Keys.V && !readOnly && mode != TextBoxMode.Password)
          {
  
            string t = System.Windows.Forms.Clipboard.GetText().Replace(Environment.NewLine, "\n");
            if (selection.IsEmpty)
            {
              Text = Text.Insert(Pos, t);
              Pos = Pos + t.Length;
            }
            else
            {
              Text = Text.Remove(selection.Start, selection.Length);
              Text = Text.Insert(selection.Start, t);
              PosX = selection.Start + t.Length;
              selection.Clear();
            }
  #endif
          }
            */
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
            ClientArea?.Invalidate();

            DeterminePages();
            ProcessScrolling();
        }
        base.OnKeyPress(e);
    }

    protected override void OnGamePadDown(GamePadEventArgs e)
    {
        if (Manager.UseGuide)
        {
            return;
        }

        if (!e.Handled)
        {
            if (e.Button == GamePadActions.Click)
            {
                e.Handled = true;
                HandleGuide(e.PlayerIndex);
            }
        }
        base.OnGamePadDown(e);
    }

    private void HandleGuide(PlayerIndex pi)
    {
        if (Manager.UseGuide)
        {
            //Guide.BeginShowKeyboardInput(pi, "Enter Text", "", Text, GetText, pi.ToString());
        }
    }

    private void GetText(IAsyncResult result)
    {
        var res = string.Empty; //Guide.EndShowKeyboardInput(result);
        Text = res != null ? res : "";
        Pos = _text.Length;
    }

    private void SetupBars()
    {
        DeterminePages();

        if (_vert != null)
        {
            _vert.Range = Lines.Count;
        }

        if (_horz != null && _font != null)
        {
            _horz.Range = (int)_font.MeasureString(GetMaxLine()).X;
            if (_horz.Range == 0)
            {
                _horz.Range = ClientArea.Width;
            }
        }

        if (_vert != null)
        {
            _vert.Left = Width - 16 - 2;
            _vert.Top = 2;
            _vert.Height = Height - 4 - 16;

            if (Height < 50 || (_scrollBars != ScrollBars.Both && _scrollBars != ScrollBars.Vertical))
            {
                _vert.Visible = false;
            }
            else if ((_scrollBars == ScrollBars.Vertical || _scrollBars == ScrollBars.Both) && _mode == TextBoxMode.Multiline)
            {
                _vert.Visible = true;
            }
        }
        if (_horz != null)
        {
            _horz.Left = 2;
            _horz.Top = Height - 16 - 2;
            _horz.Width = Width - 4 - 16;

            if (Width < 50 || _wordWrap || (_scrollBars != ScrollBars.Both && _scrollBars != ScrollBars.Horizontal))
            {
                _horz.Visible = false;
            }
            else if ((_scrollBars == ScrollBars.Horizontal || _scrollBars == ScrollBars.Both) && _mode == TextBoxMode.Multiline && !_wordWrap)
            {
                _horz.Visible = true;
            }
        }

        AdjustMargins();

        if (_vert != null)
        {
            _vert.PageSize = _linesDrawn;
        }

        if (_horz != null)
        {
            _horz.PageSize = _charsDrawn;
        }
    }

    protected override void AdjustMargins()
    {
        if (_horz != null && !_horz.Visible)
        {
            _vert.Height = Height - 4;
            ClientMargins = new Margins(ClientMargins.Left, ClientMargins.Top, ClientMargins.Right, (Skin == null ? 0 : Skin.ClientMargins.Bottom));
        }
        else
        {
            ClientMargins = new Margins(ClientMargins.Left, ClientMargins.Top, ClientMargins.Right, 18 + (Skin == null ? 0 : Skin.ClientMargins.Bottom));
        }

        if (_vert != null && !_vert.Visible)
        {
            _horz.Width = Width - 4;
            ClientMargins = new Margins(ClientMargins.Left, ClientMargins.Top, (Skin == null ? 0 : Skin.ClientMargins.Right), ClientMargins.Bottom);
        }
        else
        {
            ClientMargins = new Margins(ClientMargins.Left, ClientMargins.Top, 18 + (Skin == null ? 0 : Skin.ClientMargins.Right), ClientMargins.Bottom);
        }
        base.AdjustMargins();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        _selection.Clear();
        SetupBars();
    }

    private string WrapWords(string text, int size)
    {
        var ret = "";
        var line = "";

        var words = text.Replace("\v", "").Split(" ".ToCharArray());

        for (var i = 0; i < words.Length; i++)
        {
            if (_font.MeasureString(line + words[i]).X > size)
            {
                ret += line + "\n";
                line = words[i] + " ";
            }
            else
            {
                line += words[i] + " ";
            }
        }

        ret += line;

        return ret.Remove(ret.Length - 1, 1);
    }

    public virtual void SelectAll()
    {
        if (_text.Length > 0)
        {
            _selection.Start = 0;
            _selection.End = Text.Length;
        }
    }

    private List<string> SplitLines(string text)
    {
        if (_buffer != text)
        {
            _buffer = text;
            var list = new List<string>();
            var s = text.Split(new char[] { _separator[0] });
            list.Clear();

            //Before adding the lines back in, we will want to first, measure the lines, and split words if needed...

            list.AddRange(s);

            if (_posy < 0)
            {
                _posy = 0;
            }

            if (_posy > list.Count - 1)
            {
                _posy = list.Count - 1;
            }

            if (_posx < 0)
            {
                _posx = 0;
            }

            if (_posx > list[PosY].Length)
            {
                _posx = list[PosY].Length;
            }

            return list;
        }

        return Lines;
    }

    void sb_ValueChanged(object sender, EventArgs e)
    {
        ClientArea.Invalidate();
    }

    protected override void OnFocusLost(EventArgs e)
    {
        _selection.Clear();
        ClientArea.Invalidate();
        base.OnFocusLost(e);
    }

    protected override void OnFocusGained(EventArgs e)
    {
        if (!_readOnly && _autoSelection)
        {
            SelectAll();
            ClientArea.Invalidate();
        }

        base.OnFocusGained(e);
    }

}