
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
            private int start;

            private int end;

            public int Start
            {
                get
                {
                    if (start > end && start != -1 && end != -1)
                        return end;
                    return start;
                }
                set => start = value;
            } // Start

            public int End
            {
                get
                {
                    if (end < start && start != -1 && end != -1)
                        return start;
                    return end;
                }
                set => end = value;
            } // End

            public bool IsEmpty => Start == -1 && End == -1; // IsEmpty

            public int Length => IsEmpty ? 0 : (End - Start); // Length

            public Selection(int start, int end)
            {
                this.start = start;
                this.end = end;
            } // Selection

            public void Clear()
            {
                Start = -1;
                End = -1;
            } // Clear
        } // Selection



        private int positionX;
        private int positionY;

        private ScrollBarType scrollBarType = ScrollBarType.Both;

        private readonly ScrollBar horizontalScrollBar;
        private readonly ScrollBar verticalScrollBar;

        private char passwordCharacter = '•';

        private bool caretVisible = true;

        private bool autoSelection = true;

        private TextBoxMode mode = TextBoxMode.Normal;

        private bool readOnly;

        private bool drawBorders = true;

        private bool showCursor;
        private double flashTime;
        private string shownText = "";
        private Selection selection = new Selection(-1, -1);
        private List<string> lines = new List<string>();
        private int linesDrawn;
        private int charsDrawn;
        private CasaEngine.Asset.Fonts.Font font;
        private bool wordWrap;
        private const string Separator = "\n";
        private string text = "";
        private string buffer = "";

        private string initialText;




        private int PositionX
        {
            get => positionX;
            set
            {
                positionX = value;
                if (positionX < 0)
                    positionX = 0;
                if (positionX > lines[PositionY].Length)
                    positionX = lines[PositionY].Length;
            }
        } // positionX

        private int PositionY
        {
            get => positionY;
            set
            {
                positionY = value;

                if (positionY < 0)
                    positionY = 0;
                if (positionY > lines.Count - 1)
                    positionY = lines.Count - 1;
                if (positionX > lines[PositionY].Length)
                    positionX = lines[PositionY].Length;
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
            get => scrollBarType;
            set
            {
                scrollBarType = value;
                SetupScrollBars();
            }
        } // ScrollBars

        public virtual char PasswordCharacter
        {
            get => passwordCharacter;
            set { passwordCharacter = value; if (ClientArea != null) ClientArea.Invalidate(); }
        } // PasswordCharacter

        public virtual bool CaretVisible
        {
            get => caretVisible;
            set => caretVisible = value;
        } // CaretVisible

        public virtual TextBoxMode Mode
        {
            get => mode;
            set
            {
                if (value != TextBoxMode.Multiline)
                {
                    Text = Text.Replace(Separator, "");
                }
                mode = value;
                selection.Clear();

                if (ClientArea != null) ClientArea.Invalidate();
                SetupScrollBars();
            }
        } // TextBoxMode

        public virtual bool ReadOnly
        {
            get => readOnly;
            set => readOnly = value;
        } // ReadOnly

        public virtual bool DrawBorders
        {
            get => drawBorders;
            set { drawBorders = value; if (ClientArea != null) ClientArea.Invalidate(); }
        } // DrawBorders


        public virtual string SelectedText
        {
            get
            {
                if (selection.IsEmpty)
                {
                    return "";
                }
                return Text.Substring(selection.Start, selection.Length);
            }
        } // SelectedText

        public virtual int SelectionStart
        {
            get
            {
                if (selection.IsEmpty)
                    return Position;
                return selection.Start;
            }
            set
            {
                Position = value;
                if (Position < 0) Position = 0;
                if (Position > Text.Length) Position = Text.Length;
                selection.Start = Position;
                if (selection.End == -1) selection.End = Position;
                ClientArea.Invalidate();
            }
        } // SelectionStart

        public virtual bool AutoSelection
        {
            get => autoSelection;
            set => autoSelection = value;
        } // AutoSelection

        public virtual int SelectionLength
        {
            get => selection.Length;
            set
            {
                if (value == 0)
                {
                    selection.End = selection.Start;
                }
                else if (selection.IsEmpty)
                {
                    selection.Start = 0;
                    selection.End = value;
                }
                else if (!selection.IsEmpty)
                {
                    selection.End = selection.Start + value;
                }

                if (!selection.IsEmpty)
                {
                    if (selection.Start < 0) selection.Start = 0;
                    if (selection.Start > Text.Length) selection.Start = Text.Length;
                    if (selection.End < 0) selection.End = 0;
                    if (selection.End > Text.Length) selection.End = Text.Length;
                }
                ClientArea.Invalidate();
            }
        } // SelectionLength


        public override string Text
        {
            get => text;
            set
            {
                if (mode != TextBoxMode.Multiline && value != null)
                {
                    value = value.Replace(Separator, "");
                }

                text = value;

                if (!Suspended) OnTextChanged(new EventArgs());

                lines = SplitLines(text);
                if (ClientArea != null) ClientArea.Invalidate();

                SetupScrollBars();
                ProcessScrolling();
            }
        } // Text



        public TextBox(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            CheckLayer(SkinInformation, "Cursor");

            SetDefaultSize(128, 20);
            lines.Add("");

            verticalScrollBar = new ScrollBar(UserInterfaceManager, Orientation.Vertical)
            {
                Range = 1,
                PageSize = 1,
                Value = 0,
                Anchor = Anchors.Top | Anchors.Right | Anchors.Bottom,
                Visible = false
            };
            horizontalScrollBar = new ScrollBar(UserInterfaceManager, Orientation.Horizontal)
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
            verticalScrollBar.ValueChanged += ScrollBarValueChanged;
            horizontalScrollBar.ValueChanged += ScrollBarValueChanged;
            Add(verticalScrollBar, false);
            Add(horizontalScrollBar, false);
            FocusGained += delegate { initialText = Text; };
        } // Init

        protected internal override void InitSkin()
        {
            base.InitSkin();
            SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["TextBox"]);

#if (WINDOWS)
            Cursor = UserInterfaceManager.Skin.Cursors["Text"].Cursor;
#endif

            font = (SkinInformation.Layers["Control"].Text != null) ? SkinInformation.Layers["Control"].Text.Font.Font : null;
        } // InitSkin



        protected override void DrawControl(Rectangle rect)
        {
            if (drawBorders)
            {
                base.DrawControl(rect);
            }
        } // DrawControl

        private void DeterminePages()
        {
            if (ClientArea != null)
            {
                int sizey = font.LineSpacing;
                linesDrawn = ClientArea.Height / sizey;
                if (linesDrawn > lines.Count) linesDrawn = lines.Count;

                charsDrawn = ClientArea.Width - 1;
            }
        } // DeterminePages

        private string GetMaxLine()
        {
            int max = 0;
            int x = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Length > max)
                {
                    max = lines[i].Length;
                    x = i;
                }
            }
            return lines.Count > 0 ? lines[x] : "";
        } // GetMaxLine

        private void ClientAreaDraw(object sender, DrawEventArgs e)
        {
            Color col = SkinInformation.Layers["Control"].Text.Colors.Enabled;
            SkinLayer cursor = SkinInformation.Layers["Cursor"];
            Alignment al = mode == TextBoxMode.Multiline ? Alignment.TopLeft : Alignment.MiddleLeft;
            Rectangle r = e.Rectangle;
            bool drawsel = !selection.IsEmpty;
            string tmpText;

            font = (SkinInformation.Layers["Control"].Text != null) ? SkinInformation.Layers["Control"].Text.Font.Font : null;

            if (Text != null && font != null)
            {
                DeterminePages();

                if (mode == TextBoxMode.Multiline)
                {
                    shownText = Text;
                    tmpText = lines[PositionY];
                }
                else if (mode == TextBoxMode.Password)
                {
                    shownText = "";
                    foreach (char character in Text)
                    {
                        shownText = shownText + passwordCharacter;
                    }
                    tmpText = shownText;
                }
                else
                {
                    shownText = Text;
                    tmpText = lines[PositionY];
                }

                if (TextColor != UndefinedColor && ControlState != ControlState.Disabled)
                {
                    col = TextColor;
                }

                if (mode != TextBoxMode.Multiline)
                {
                    linesDrawn = 0;
                    verticalScrollBar.Value = 0;
                }

                if (drawsel)
                {
                    DrawSelection(r);
                }

                int sizey = font.LineSpacing;

                if (showCursor && caretVisible)
                {
                    Vector2 size = Vector2.Zero;
                    if (PositionX > 0 && PositionX <= tmpText.Length)
                    {
                        size = font.MeasureString(tmpText.Substring(0, PositionX));
                    }
                    if (size.Y == 0)
                    {
                        size = font.MeasureString(" ");
                        size.X = 0;
                    }

                    int m = r.Height - font.LineSpacing;

                    Rectangle rc = new Rectangle(r.Left - horizontalScrollBar.Value + (int)size.X, r.Top + m / 2, cursor.Width, font.LineSpacing);

                    if (mode == TextBoxMode.Multiline)
                    {
                        rc = new Rectangle(r.Left + (int)size.X - horizontalScrollBar.Value, r.Top + (int)((PositionY - verticalScrollBar.Value) * font.LineSpacing), cursor.Width, font.LineSpacing);
                    }
                    cursor.Alignment = al;
                    UserInterfaceManager.Renderer.DrawLayer(cursor, rc, col, 0);
                }

                for (int i = 0; i < linesDrawn + 1; i++)
                {
                    int ii = i + verticalScrollBar.Value;
                    if (ii >= lines.Count || ii < 0) break;

                    if (lines[ii] != "")
                    {
                        if (mode == TextBoxMode.Multiline)
                        {
                            UserInterfaceManager.Renderer.DrawString(font, lines[ii], r.Left - horizontalScrollBar.Value, r.Top + (i * sizey), col);
                        }
                        else
                        {
                            Rectangle rx = new Rectangle(r.Left - horizontalScrollBar.Value, r.Top, r.Width, r.Height);
                            UserInterfaceManager.Renderer.DrawString(font, shownText, rx, col, al, false);
                        }
                    }
                }
            }
        } // ClientArea_Draw

        private void DrawSelection(Rectangle rect)
        {
            if (!selection.IsEmpty)
            {
                int s = selection.Start;
                int e = selection.End;

                int sl = GetPosY(s);
                int el = GetPosY(e);
                int sc = GetPosX(s);
                int ec = GetPosX(e);

                int hgt = font.LineSpacing;

                int start = sl;
                int end = el;

                if (start < verticalScrollBar.Value) start = verticalScrollBar.Value;
                if (end > verticalScrollBar.Value + linesDrawn) end = verticalScrollBar.Value + linesDrawn;

                for (int i = start; i <= end; i++)
                {
                    Rectangle r = Rectangle.Empty;

                    if (mode == TextBoxMode.Normal)
                    {
                        int m = ClientArea.Height - font.LineSpacing;
                        r = new Rectangle(rect.Left - horizontalScrollBar.Value + (int)font.MeasureString(lines[i].Substring(0, sc)).X, rect.Top + m / 2,
                                         (int)font.MeasureString(lines[i].Substring(0, ec + 0)).X - (int)font.MeasureString(lines[i].Substring(0, sc)).X, hgt);
                    }
                    else if (sl == el)
                    {
                        r = new Rectangle(rect.Left - horizontalScrollBar.Value + (int)font.MeasureString(lines[i].Substring(0, sc)).X, rect.Top + (i - verticalScrollBar.Value) * hgt,
                                          (int)font.MeasureString(lines[i].Substring(0, ec + 0)).X - (int)font.MeasureString(lines[i].Substring(0, sc)).X, hgt);
                    }
                    else
                    {
                        if (i == sl) r = new Rectangle(rect.Left - horizontalScrollBar.Value + (int)font.MeasureString(lines[i].Substring(0, sc)).X, rect.Top + (i - verticalScrollBar.Value) * hgt, (int)font.MeasureString(lines[i]).X - (int)font.MeasureString(lines[i].Substring(0, sc)).X, hgt);
                        else if (i == el) r = new Rectangle(rect.Left - horizontalScrollBar.Value, rect.Top + (i - verticalScrollBar.Value) * hgt, (int)font.MeasureString(lines[i].Substring(0, ec + 0)).X, hgt);
                        else r = new Rectangle(rect.Left - horizontalScrollBar.Value, rect.Top + (i - verticalScrollBar.Value) * hgt, (int)font.MeasureString(lines[i]).X, hgt);
                    }

                    UserInterfaceManager.Renderer.Draw(UserInterfaceManager.Skin.Images["Control"].Texture.Resource, r, Color.FromNonPremultiplied(160, 160, 160, 128));
                }
            }
        } // DrawSelection



        private int GetStringWidth(string text, int count)
        {
            if (count > text.Length) count = text.Length;
            return (int)font.MeasureString(text.Substring(0, count)).X;
        } // GetStringWidth

        private void ProcessScrolling()
        {
            if (verticalScrollBar != null && horizontalScrollBar != null)
            {
                verticalScrollBar.PageSize = linesDrawn;
                horizontalScrollBar.PageSize = charsDrawn;

                if (horizontalScrollBar.PageSize > horizontalScrollBar.Range) horizontalScrollBar.PageSize = horizontalScrollBar.Range;

                if (PositionY >= verticalScrollBar.Value + verticalScrollBar.PageSize)
                {
                    verticalScrollBar.Value = (PositionY + 1) - verticalScrollBar.PageSize;
                }
                else if (PositionY < verticalScrollBar.Value)
                {
                    verticalScrollBar.Value = PositionY;
                }

                if (GetStringWidth(lines[PositionY], PositionX) >= horizontalScrollBar.Value + horizontalScrollBar.PageSize)
                {
                    horizontalScrollBar.Value = (GetStringWidth(lines[PositionY], PositionX) + 1) - horizontalScrollBar.PageSize;
                }
                else if (GetStringWidth(lines[PositionY], PositionX) < horizontalScrollBar.Value)
                {
                    horizontalScrollBar.Value = GetStringWidth(lines[PositionY], PositionX) - horizontalScrollBar.PageSize;
                }
            }
        } // ProcessScrolling



        protected internal override void Update(float elapsedTime_)
        {
            base.Update(elapsedTime_);

            bool showCursorTemp = showCursor;

            showCursor = Focused;

            if (Focused)
            {
                flashTime += elapsedTime_;
                showCursor = flashTime < 0.5;
                if (flashTime > 1) flashTime = 0;
            }
            if (showCursorTemp != showCursor) ClientArea.Invalidate();
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
            if (pos >= Text.Length) return lines.Count - 1;

            int p = pos;
            for (int i = 0; i < lines.Count; i++)
            {
                p -= lines[i].Length + Separator.Length;
                if (p < 0)
                {
                    return i;
                }
            }
            return 0;
        }

        private int GetPosX(int pos)
        {
            if (pos >= Text.Length) return lines[lines.Count - 1].Length;

            int p = pos;
            for (int i = 0; i < lines.Count; i++)
            {
                p -= lines[i].Length + Separator.Length;
                if (p < 0)
                {
                    p = p + lines[i].Length + Separator.Length;
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
                p += lines[i].Length + Separator.Length;
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

            if (mode == TextBoxMode.Multiline)
            {
                py = verticalScrollBar.Value + (int)((y - ClientTop) / font.LineSpacing);
                if (py < 0) py = 0;
                if (py >= lines.Count) py = lines.Count - 1;
            }
            else
            {
                py = 0;
            }

            string str = mode == TextBoxMode.Multiline ? lines[py] : shownText;

            if (!string.IsNullOrEmpty(str))
            {
                for (int i = 1; i <= lines[py].Length; i++)
                {
                    Vector2 v = font.MeasureString(str.Substring(0, i)) - (font.MeasureString(str[i - 1].ToString()) / 3);
                    if (x <= (ClientLeft + (int)v.X) - horizontalScrollBar.Value)
                    {
                        px = i - 1;
                        break;
                    }
                }
                if (x > ClientLeft + ((int)font.MeasureString(str).X) - horizontalScrollBar.Value - (font.MeasureString(str[str.Length - 1].ToString()).X / 3)) px = str.Length;
            }

            return GetPos(px, py);
        }



        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            flashTime = 0;

            Position = CharAtPos(e.Position);
            selection.Clear();

            if (e.Button == MouseButton.Left && caretVisible && mode != TextBoxMode.Password)
            {
                selection.Start = Position;
                selection.End = Position;
            }
            ClientArea.Invalidate();
        } // OnMouseDown



        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (e.Button == MouseButton.Left && !selection.IsEmpty && mode != TextBoxMode.Password && selection.Length < Text.Length)
            {
                int pos = CharAtPos(e.Position);
                selection.End = CharAtPos(e.Position);
                Position = pos;

                ClientArea.Invalidate();

                ProcessScrolling();
            }
        } // OnMouseMove



        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button == MouseButton.Left && !selection.IsEmpty && mode != TextBoxMode.Password)
            {
                if (selection.Length == 0) selection.Clear();
            }
        } // OnMouseUp



        protected override void OnKeyPress(KeyEventArgs e)
        {
            flashTime = 0;

            if (!e.Handled)
            {
                if (e.Key == Keys.Enter && mode != TextBoxMode.Multiline && !readOnly)
                {
                    initialText = Text;
                    selection = new Selection(-1, -1);
                    Focused = false;
                }
                if (e.Key == Keys.Escape)
                {
                    if (initialText != null)
                        Text = initialText;
                    selection = new Selection(-1, -1);
                    Focused = false;
                }
                if (e.Key == Keys.A && e.Control && mode != TextBoxMode.Password)
                {
                    SelectAll();
                }
                if (e.Key == Keys.Up)
                {
                    e.Handled = true;

                    if (e.Shift && selection.IsEmpty && mode != TextBoxMode.Password)
                    {
                        selection.Start = Position;
                    }
                    if (!e.Control)
                    {
                        PositionY -= 1;
                    }
                }
                else if (e.Key == Keys.Down)
                {
                    e.Handled = true;
                    if (e.Shift && selection.IsEmpty && mode != TextBoxMode.Password)
                    {
                        selection.Start = Position;
                    }
                    if (!e.Control)
                    {
                        PositionY += 1;
                    }
                }
                else if (e.Key == Keys.Back && !readOnly)
                {
                    e.Handled = true;
                    if (!selection.IsEmpty)
                    {
                        Text = Text.Remove(selection.Start, selection.Length);
                        Position = selection.Start;
                    }
                    else if (Text.Length > 0 && Position > 0)
                    {
                        Position -= 1;
                        Text = Text.Remove(Position, 1);
                    }
                    selection.Clear();
                }
                else if (e.Key == Keys.Delete && !readOnly)
                {
                    e.Handled = true;
                    if (!selection.IsEmpty)
                    {
                        Text = Text.Remove(selection.Start, selection.Length);
                        Position = selection.Start;
                    }
                    else if (Position < Text.Length)
                    {
                        Text = Text.Remove(Position, 1);
                    }
                    selection.Clear();
                }
                else if (e.Key == Keys.Left)
                {
                    e.Handled = true;
                    if (e.Shift && selection.IsEmpty && mode != TextBoxMode.Password)
                    {
                        selection.Start = Position;
                    }
                    if (!e.Control)
                    {
                        Position -= 1;
                    }
                    if (e.Control)
                    {
                        Position = FindPreviousWord(shownText);
                    }
                }
                else if (e.Key == Keys.Right)
                {
                    e.Handled = true;
                    if (e.Shift && selection.IsEmpty && mode != TextBoxMode.Password)
                    {
                        selection.Start = Position;
                    }
                    if (!e.Control)
                    {
                        Position += 1;
                    }
                    if (e.Control)
                    {
                        Position = FindNextWord(shownText);
                    }
                }
                else if (e.Key == Keys.Home)
                {
                    e.Handled = true;
                    if (e.Shift && selection.IsEmpty && mode != TextBoxMode.Password)
                    {
                        selection.Start = Position;
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
                    if (e.Shift && selection.IsEmpty && mode != TextBoxMode.Password)
                    {
                        selection.Start = Position;
                    }
                    if (!e.Control)
                    {
                        PositionX = lines[PositionY].Length;
                    }
                    if (e.Control)
                    {
                        Position = Text.Length;
                    }
                }
                else if (e.Key == Keys.PageUp)
                {
                    e.Handled = true;
                    if (e.Shift && selection.IsEmpty && mode != TextBoxMode.Password)
                    {
                        selection.Start = Position;
                    }
                    if (!e.Control)
                    {
                        PositionY -= linesDrawn;
                    }
                }
                else if (e.Key == Keys.PageDown)
                {
                    e.Handled = true;
                    if (e.Shift && selection.IsEmpty && mode != TextBoxMode.Password)
                    {
                        selection.Start = Position;
                    }
                    if (!e.Control)
                    {
                        PositionY += linesDrawn;
                    }
                }
                else if (e.Key == Keys.Enter && mode == TextBoxMode.Multiline && !readOnly)
                {
                    e.Handled = true;
                    Text = Text.Insert(Position, Separator);
                    PositionX = 0;
                    PositionY += 1;
                }
                else if (e.Key == Keys.Tab)
                {
                }
                else if (!readOnly && !e.Control)
                {
                    string c = Keyboard.KeyToString(e.Key, e.Shift, e.Caps);
                    if (selection.IsEmpty)
                    {
                        Text = Text.Insert(Position, c);
                        if (c != "") PositionX += 1;
                    }
                    else
                    {
                        if (Text.Length > 0)
                        {
                            // Avoid out of range.
                            if (selection.Start + selection.Length > Text.Length)
                                Text = Text.Remove(selection.Start, Text.Length - selection.Start);
                            else
                                Text = Text.Remove(selection.Start, selection.Length);
                            Text = Text.Insert(selection.Start, c);
                            Position = selection.Start + 1;
                        }
                        selection.Clear();
                    }
                }

                if (e.Shift && !selection.IsEmpty)
                {
                    selection.End = Position;
                }


                // Windows only because it uses the Clipboard class. Of course this could be implemented manually in the XBOX 360 if you want it.
#if (WINDOWS)
                if (e.Control && e.Key == Keys.C && mode != TextBoxMode.Password)
                {
                    System.Windows.Forms.Clipboard.Clear();
                    if (mode != TextBoxMode.Password && !selection.IsEmpty)
                    {
                        System.Windows.Forms.Clipboard.SetText((Text.Substring(selection.Start, selection.Length)).Replace("\n", Environment.NewLine));
                    }
                }
                else if (e.Control && e.Key == Keys.V && !readOnly && mode != TextBoxMode.Password)
                {
                    string t = System.Windows.Forms.Clipboard.GetText().Replace(Environment.NewLine, "\n");
                    if (selection.IsEmpty)
                    {
                        Text = Text.Insert(Position, t);
                        Position = Position + t.Length;
                    }
                    else
                    {
                        Text = Text.Remove(selection.Start, selection.Length);
                        Text = Text.Insert(selection.Start, t);
                        PositionX = selection.Start + t.Length;
                        selection.Clear();
                    }
                }
#endif


                if ((!e.Shift && !e.Control) || Text.Length <= 0)
                {
                    selection.Clear();
                }

                if (e.Control && e.Key == Keys.Down)
                {
                    e.Handled = true;
                    HandleGuide(PlayerIndex.One);
                }
                flashTime = 0;
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

            if (verticalScrollBar != null) verticalScrollBar.Range = lines.Count;
            if (horizontalScrollBar != null)
            {
                horizontalScrollBar.Range = (int)font.MeasureString(GetMaxLine()).X;
                if (horizontalScrollBar.Range == 0) horizontalScrollBar.Range = ClientArea.Width;
            }

            if (verticalScrollBar != null)
            {
                verticalScrollBar.Left = Width - 16 - 2;
                verticalScrollBar.Top = 2;
                verticalScrollBar.Height = Height - 4 - 16;

                if (Height < 50 || (scrollBarType != ScrollBarType.Both && scrollBarType != ScrollBarType.Vertical)) verticalScrollBar.Visible = false;
                else if ((scrollBarType == ScrollBarType.Vertical || scrollBarType == ScrollBarType.Both) && mode == TextBoxMode.Multiline) verticalScrollBar.Visible = true;
            }
            if (horizontalScrollBar != null)
            {
                horizontalScrollBar.Left = 2;
                horizontalScrollBar.Top = Height - 16 - 2;
                horizontalScrollBar.Width = Width - 4 - 16;

                if (Width < 50 || wordWrap || (scrollBarType != ScrollBarType.Both && scrollBarType != ScrollBarType.Horizontal)) horizontalScrollBar.Visible = false;
                else if ((scrollBarType == ScrollBarType.Horizontal || scrollBarType == ScrollBarType.Both) && mode == TextBoxMode.Multiline && !wordWrap) horizontalScrollBar.Visible = true;
            }

            AdjustMargins();

            if (verticalScrollBar != null) verticalScrollBar.PageSize = linesDrawn;
            if (horizontalScrollBar != null) horizontalScrollBar.PageSize = charsDrawn;
        } // SetupScrollBars



        protected override void AdjustMargins()
        {
            if (horizontalScrollBar != null && !horizontalScrollBar.Visible)
            {
                verticalScrollBar.Height = Height - 4;
                ClientMargins = new Margins(ClientMargins.Left, ClientMargins.Top, ClientMargins.Right, SkinInformation.ClientMargins.Bottom);
            }
            else
            {
                ClientMargins = new Margins(ClientMargins.Left, ClientMargins.Top, ClientMargins.Right, 18 + SkinInformation.ClientMargins.Bottom);
            }

            if (verticalScrollBar != null && !verticalScrollBar.Visible)
            {
                horizontalScrollBar.Width = Width - 4;
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
            selection.Clear();
            SetupScrollBars();
        } // OnResize



        public virtual void SelectAll()
        {
            if (text.Length > 0)
            {
                selection.Start = 0;
                selection.End = Text.Length;
            }
        } // SelectAll



        private List<string> SplitLines(string text)
        {
            if (buffer != text)
            {
                buffer = text;
                List<string> list = new List<string>();
                string[] s = text.Split(new char[] { Separator[0] });
                list.Clear();

                list.AddRange(s);

                if (positionY < 0) positionY = 0;
                if (positionY > list.Count - 1) positionY = list.Count - 1;

                if (positionX < 0) positionX = 0;
                if (positionX > list[PositionY].Length) positionX = list[PositionY].Length;

                return list;
            }
            return lines;
        } // SplitLines



        void ScrollBarValueChanged(object sender, EventArgs e)
        {
            ClientArea.Invalidate();
        } // scrollBarValueChanged



        protected override void OnFocusLost()
        {
            selection.Clear();
            ClientArea.Invalidate();
            base.OnFocusLost();
        } // OnFocusLost

        protected override void OnFocusGained()
        {
            if (!readOnly && autoSelection
                && ClientArea != null)
            {
                SelectAll();
                ClientArea.Invalidate();
            }
            base.OnFocusGained();
        } // OnFocusGained


    } // TextBox
} // XNAFinalEngine.UserInterface