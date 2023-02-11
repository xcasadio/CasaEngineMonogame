
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

namespace XNAFinalEngine.UserInterface
{

    public class ComboBox : TextBox
    {


        private readonly Button _buttonDown;
        private readonly List<object> _items = new List<object>();
        private readonly ListBox _listCombo;

        private int _maxItems = 5;

        private bool _drawSelection = true;



        public bool ListBoxVisible => _listCombo.Visible;

        public override bool ReadOnly
        {
            get { return base.ReadOnly; }
            set
            {
                base.ReadOnly = value;
                CaretVisible = !value;
#if (WINDOWS)
                Cursor = value ? UserInterfaceManager.Skin.Cursors["Default"].Cursor : UserInterfaceManager.Skin.Cursors["Text"].Cursor;
#endif
            }
        } // ReadOnly

        public bool DrawSelection
        {
            get => _drawSelection;
            set => _drawSelection = value;
        } // DrawSelection

        public override string Text
        {
            get => base.Text;
            set
            {
                base.Text = value;
                if (!_items.Contains(value))
                {
                    ItemIndex = -1;
                }
            }
        } // Text

        public virtual List<object> Items => _items; // Items

        public int MaxItemsShow
        {
            get => _maxItems;
            set
            {
                if (_maxItems != value)
                {
                    _maxItems = value;
                    if (!Suspended)
                        OnMaxItemsChanged(new EventArgs());
                }
            }
        } // MaxItems

        public int ItemIndex
        {
            get => _listCombo.ItemIndex;
            set
            {
                if (value >= 0 && value < _items.Count)
                {
                    _listCombo.ItemIndex = value;
                    Text = _listCombo.Items[value].ToString();
                }
                else
                {
                    _listCombo.ItemIndex = -1;
                }
                if (!Suspended)
                    OnItemIndexChanged(new EventArgs());
            }
        } // ItemIndex



        public event EventHandler MaxItemsChanged;
        public event EventHandler ItemIndexChanged;



        public ComboBox(UserInterfaceManager userInterfaceManager)
            : base(userInterfaceManager)
        {
            Height = 20;
            Width = 64;
            ReadOnly = true;

            _buttonDown = new Button(UserInterfaceManager)
            {
                SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["ComboBox.Button"]),
                CanFocus = false
            };
            _buttonDown.Click += ButtonDownClick;
            Add(_buttonDown, false);

            _listCombo = new ListBox(UserInterfaceManager)
            {
                HotTrack = true,
                Detached = true,
                Visible = false,
                Items = _items
            };
        } // ComboBox



        protected override void DisposeManagedResources()
        {
            // A disposed object could be still generating events, because it is alive for a time, in a disposed state, but alive nevertheless.
            MaxItemsChanged = null;
            ItemIndexChanged = null;
            // We added the listbox to another parent other than this control, so we dispose it manually.
            if (_listCombo != null)
            {
                _listCombo.Dispose();
            }
            UserInterfaceManager.InputSystem.MouseDown -= InputMouseDown;
            base.DisposeManagedResources();
        } // DisposeManagedResources



        protected internal override void Init()
        {
            base.Init();

            _listCombo.Click += ListComboClick;
            UserInterfaceManager.InputSystem.MouseDown += InputMouseDown;

            _listCombo.SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["ComboBox.ListBox"]);
            _buttonDown.Glyph = new Glyph(UserInterfaceManager.Skin.Images["Shared.ArrowDown"].Texture)
            {
                Color = UserInterfaceManager.Skin.Controls["ComboBox.Button"].Layers["Control"].Text.Colors.Enabled,
                SizeMode = SizeMode.Centered
            };
        } // Init

        protected internal override void InitSkin()
        {
            base.InitSkin();
            SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["ComboBox"]);
            AdjustMargins();
            ReadOnly = ReadOnly; // To init the right cursor
        } // InitSkin



        protected override void DrawControl(Rectangle rect)
        {
            base.DrawControl(rect);

            if (ReadOnly && (Focused || _listCombo.Focused) && _drawSelection)
            {
                SkinLayer lr = SkinInformation.Layers[0];
                Rectangle rc = new Rectangle(rect.Left + lr.ContentMargins.Left,
                                             rect.Top + lr.ContentMargins.Top,
                                             Width - lr.ContentMargins.Horizontal - _buttonDown.Width,
                                             Height - lr.ContentMargins.Vertical);
                UserInterfaceManager.Renderer.Draw(UserInterfaceManager.Skin.Images["ListBox.Selection"].Texture.Resource, rc, Color.FromNonPremultiplied(255, 255, 255, 128));
            }
        } // DrawControl



        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);

            if (_buttonDown != null)
            {
                _buttonDown.Width = 16;
                _buttonDown.Height = Height - SkinInformation.Layers[0].ContentMargins.Vertical;
                _buttonDown.Top = SkinInformation.Layers[0].ContentMargins.Top;
                _buttonDown.Left = Width - _buttonDown.Width - 2;
            }
        } // OnResize



        private void ListComboClick(object sender, EventArgs e)
        {
            MouseEventArgs ex = (e is MouseEventArgs) ? (MouseEventArgs)e : new MouseEventArgs();

            if (ex.Button == MouseButton.Left || ex.Button == MouseButton.None)
            {
                _listCombo.Visible = false;
                if (_listCombo.ItemIndex >= 0)
                {
                    Text = _listCombo.Items[_listCombo.ItemIndex].ToString();
                    Focused = true;
                    ItemIndex = _listCombo.ItemIndex;
                }
            }
            // The focus was removed to assure that the list box is focused.
            CanFocus = true;
            Focused = true;
        } // ListComboClick



        private void ButtonDownClick(object sender, EventArgs e)
        {
            if (_items != null && _items.Count > 0)
            {
                if (Root != null && Root is Container)
                {
                    (Root as Container).Add(_listCombo, false);
                    _listCombo.Alpha = Root.Alpha;
                    _listCombo.Left = ControlLeftAbsoluteCoordinate - Root.Left;
                    _listCombo.Top = ControlTopAbsoluteCoordinate - Root.Top + Height + 1;
                }
                else
                {
                    UserInterfaceManager.Add(_listCombo);
                    _listCombo.Alpha = Alpha;
                    _listCombo.Left = ControlLeftAbsoluteCoordinate;
                    _listCombo.Top = ControlTopAbsoluteCoordinate + Height + 1;
                }

                _listCombo.AutoHeight(_maxItems);
                // If there is no place to put the list box under the control then is moved up.
                if (_listCombo.ControlTopAbsoluteCoordinate + _listCombo.Height > UserInterfaceManager.Screen.Height)
                    _listCombo.Top = _listCombo.Top - Height - _listCombo.Height - 2;

                _listCombo.Visible = !_listCombo.Visible;
                if (_listCombo.Visible)
                {
                    // The focus is removed to assure that the list box is focused.
                    CanFocus = false;
                    _listCombo.Focused = true;
                }
                else
                {
                    // The focus was removed to assure that the list box is focused.
                    CanFocus = true;
                    Focused = true;
                }
                _listCombo.Width = Width;
            }
        } // ButtonDownClick



        private void InputMouseDown(object sender, MouseEventArgs e)
        {
            if (ReadOnly &&
                (e.Position.X >= ControlLeftAbsoluteCoordinate &&
                 e.Position.X <= ControlLeftAbsoluteCoordinate + Width &&
                 e.Position.Y >= ControlTopAbsoluteCoordinate &&
                 e.Position.Y <= ControlTopAbsoluteCoordinate + Height))
                return;

            // If the user click outside the list box then it is hide.
            if (_listCombo.Visible &&
               (e.Position.X < _listCombo.ControlLeftAbsoluteCoordinate ||
                e.Position.X > _listCombo.ControlLeftAbsoluteCoordinate + _listCombo.Width ||
                e.Position.Y < _listCombo.ControlTopAbsoluteCoordinate ||
                e.Position.Y > _listCombo.ControlTopAbsoluteCoordinate + _listCombo.Height) &&
               (e.Position.X < _buttonDown.ControlLeftAbsoluteCoordinate ||
                e.Position.X > _buttonDown.ControlLeftAbsoluteCoordinate + _buttonDown.Width ||
                e.Position.Y < _buttonDown.ControlTopAbsoluteCoordinate ||
                e.Position.Y > _buttonDown.ControlTopAbsoluteCoordinate + _buttonDown.Height))
            {
                ButtonDownClick(sender, e);
            }
        } // InputMouseDown



        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Keys.Down)
            {
                e.Handled = true;
                ButtonDownClick(this, new MouseEventArgs());
            }
            base.OnKeyDown(e);
        } // OnKeyDown

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (ReadOnly && e.Button == MouseButton.Left)
                ButtonDownClick(this, e);
        } // OnMouseDown

        protected virtual void OnMaxItemsChanged(EventArgs e)
        {
            if (MaxItemsChanged != null)
                MaxItemsChanged.Invoke(this, e);
        } // OnMaxItemsChanged

        protected virtual void OnItemIndexChanged(EventArgs e)
        {
            if (ItemIndexChanged != null)
                ItemIndexChanged.Invoke(this, e);
        } // OnItemIndexChanged



        protected override void AdjustMargins()
        {
            base.AdjustMargins();
            ClientMargins = new Margins(ClientMargins.Left, ClientMargins.Top, ClientMargins.Right + 16, ClientMargins.Bottom);
        } // AdjustMargins


    } // ComboBox
} // XNAFinalEngine.UserInterface