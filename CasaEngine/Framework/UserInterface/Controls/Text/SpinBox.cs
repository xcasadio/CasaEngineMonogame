
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using CasaEngine.Framework.UserInterface.Controls.Auxiliary;
using Button = CasaEngine.Framework.UserInterface.Controls.Buttons.Button;

namespace CasaEngine.Framework.UserInterface.Controls.Text
{


    public enum SpinBoxMode
    {
        Range,
        List
    } // SpinBoxMode


    public class SpinBox : TextBox
    {


        private readonly Button _btnUp;
        private readonly Button _btnDown;
        private SpinBoxMode _mode = SpinBoxMode.List;
        private readonly List<object> _items = new();
        private float _value;
        private int _rounding = 2;
        private int _itemIndex = -1;



        public new virtual SpinBoxMode Mode
        {
            get => _mode;
            set => _mode = value;
        } // Mode

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

        public virtual List<object> Items => _items; // Items

        public float Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    Invalidate();
                }
            }
        } // Value

        public float Minimum { get; set; }

        public float Maximum { get; set; }

        public float Step { get; set; }

        public int ItemIndex
        {
            get => _itemIndex;
            set
            {
                if (value < 0)
                {
                    value = 0;
                }

                if (value > _items.Count - 1)
                {
                    value = _items.Count - 1;
                }

                if (_mode == SpinBoxMode.List && _items.Count != 0)
                {
                    _itemIndex = value;
                    Text = _items[_itemIndex].ToString();
                }
            }
        } // ItemIndex

        public int Rounding
        {
            get => _rounding;
            set
            {
                if (_rounding != value)
                {
                    _rounding = value;
                    Invalidate();
                }
            }
        } // Rounding



        public SpinBox(UserInterfaceManager userInterfaceManager, SpinBoxMode mode)
            : base(userInterfaceManager)
        {
            Step = 0.25f;
            Maximum = 100;
            Minimum = 0;
            _mode = mode;
            ReadOnly = true;

            Height = 20;
            Width = 64;

            _btnUp = new Button(UserInterfaceManager) { CanFocus = false };
            _btnUp.MousePress += Button_MousePress;
            Add(_btnUp, false);

            _btnDown = new Button(UserInterfaceManager) { CanFocus = false };
            _btnDown.MousePress += Button_MousePress;
            Add(_btnDown, false);
        } // SpinBox



        protected internal override void Init()
        {
            base.Init();

            var sc = new SkinControlInformation(_btnUp.SkinInformation);
            sc.Layers["Control"] = new SkinLayer(SkinInformation.Layers["Button"]);
            sc.Layers["Button"].Name = "Control";
            _btnUp.SkinInformation = _btnDown.SkinInformation = sc;

            _btnUp.Glyph = new Glyph(UserInterfaceManager.Skin.Images["Shared.ArrowUp"].Texture)
            {
                SizeMode = SizeMode.Centered,
                Color = UserInterfaceManager.Skin.Controls["Button"].Layers["Control"].Text.Colors.Enabled
            };

            _btnDown.Glyph = new Glyph(UserInterfaceManager.Skin.Images["Shared.ArrowDown"].Texture)
            {
                SizeMode = SizeMode.Centered,
                Color = UserInterfaceManager.Skin.Controls["Button"].Layers["Control"].Text.Colors.Enabled
            };
        } // Init

        protected internal override void InitSkin()
        {
            base.InitSkin();
            SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["SpinBox"]);
        } // InitSkin



        protected override void DrawControl(Rectangle rect)
        {
            base.DrawControl(rect);

            if (ReadOnly && Focused)
            {
                var lr = SkinInformation.Layers[0];
                var rc = new Rectangle(rect.Left + lr.ContentMargins.Left,
                                             rect.Top + lr.ContentMargins.Top,
                                             Width - lr.ContentMargins.Horizontal - _btnDown.Width - _btnUp.Width,
                                             Height - lr.ContentMargins.Vertical);
                UserInterfaceManager.Renderer.Draw(UserInterfaceManager.Skin.Images["ListBox.Selection"].Texture.Resource, rc, Color.FromNonPremultiplied(255, 255, 255, 128));
            }
        } // DrawControl



        private void ShiftIndex(bool direction)
        {
            if (_mode == SpinBoxMode.List)
            {
                if (_items.Count > 0)
                {
                    if (direction)
                    {
                        _itemIndex += 1;
                    }
                    else
                    {
                        _itemIndex -= 1;
                    }

                    if (_itemIndex < 0)
                    {
                        _itemIndex = 0;
                    }

                    if (_itemIndex > _items.Count - 1)
                    {
                        _itemIndex = _itemIndex = _items.Count - 1;
                    }

                    Text = _items[_itemIndex].ToString();
                }
            }
            else
            {
                if (direction)
                {
                    _value += Step;
                }
                else
                {
                    _value -= Step;
                }

                if (_value < Minimum)
                {
                    _value = Minimum;
                }

                if (_value > Maximum)
                {
                    _value = Maximum;
                }

                Text = _value.ToString("n" + _rounding);
            }
        } // ShiftIndex



        private void Button_MousePress(object sender, MouseEventArgs e)
        {
            Focused = true;
            if (sender == _btnUp)
            {
                ShiftIndex(true);
            }
            else if (sender == _btnDown)
            {
                ShiftIndex(false);
            }
        } // Button_MousePress



        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);

            if (_btnUp != null)
            {
                _btnUp.Width = 16;
                _btnUp.Height = Height - SkinInformation.Layers["Control"].ContentMargins.Vertical;
                _btnUp.Top = SkinInformation.Layers["Control"].ContentMargins.Top;
                _btnUp.Left = Width - 16 - 2 - 16 - 1;
            }
            if (_btnDown != null)
            {
                _btnDown.Width = 16;
                _btnDown.Height = Height - SkinInformation.Layers["Control"].ContentMargins.Vertical;
                _btnDown.Top = SkinInformation.Layers["Control"].ContentMargins.Top; ;
                _btnDown.Left = Width - 16 - 2;
            }
        } // OnResize

        protected override void OnKeyPress(KeyEventArgs e)
        {
            if (e.Key == Keys.Up)
            {
                e.Handled = true;
                ShiftIndex(true);
            }
            else if (e.Key == Keys.Down)
            {
                e.Handled = true;
                ShiftIndex(false);
            }
            base.OnKeyPress(e);
        } // OnKeyPress


    } // SpinBox
} // XNAFinalEngine.UserInterface