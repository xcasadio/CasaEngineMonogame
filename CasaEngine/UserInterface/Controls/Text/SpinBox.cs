
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

namespace XNAFinalEngine.UserInterface
{


    public enum SpinBoxMode
    {
        Range,
        List
    } // SpinBoxMode


    public class SpinBox : TextBox
    {


        private readonly Button btnUp;
        private readonly Button btnDown;
        private SpinBoxMode mode = SpinBoxMode.List;
        private readonly List<object> items = new List<object>();
        private float value;
        private int rounding = 2;
        private int itemIndex = -1;



        public new virtual SpinBoxMode Mode
        {
            get => mode;
            set => mode = value;
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

        public virtual List<object> Items => items; // Items

        public float Value
        {
            get => value;
            set
            {
                if (this.value != value)
                {
                    this.value = value;
                    Invalidate();
                }
            }
        } // Value

        public float Minimum { get; set; }

        public float Maximum { get; set; }

        public float Step { get; set; }

        public int ItemIndex
        {
            get => itemIndex;
            set
            {
                if (value < 0)
                    value = 0;
                if (value > items.Count - 1)
                    value = items.Count - 1;
                if (mode == SpinBoxMode.List && items.Count != 0)
                {
                    itemIndex = value;
                    Text = items[itemIndex].ToString();
                }
            }
        } // ItemIndex

        public int Rounding
        {
            get => rounding;
            set
            {
                if (rounding != value)
                {
                    rounding = value;
                    Invalidate();
                }
            }
        } // Rounding



        public SpinBox(UserInterfaceManager userInterfaceManager_, SpinBoxMode mode)
            : base(userInterfaceManager_)
        {
            Step = 0.25f;
            Maximum = 100;
            Minimum = 0;
            this.mode = mode;
            ReadOnly = true;

            Height = 20;
            Width = 64;

            btnUp = new Button(UserInterfaceManager) { CanFocus = false };
            btnUp.MousePress += Button_MousePress;
            Add(btnUp, false);

            btnDown = new Button(UserInterfaceManager) { CanFocus = false };
            btnDown.MousePress += Button_MousePress;
            Add(btnDown, false);
        } // SpinBox



        protected internal override void Init()
        {
            base.Init();

            SkinControlInformation sc = new SkinControlInformation(btnUp.SkinInformation);
            sc.Layers["Control"] = new SkinLayer(SkinInformation.Layers["Button"]);
            sc.Layers["Button"].Name = "Control";
            btnUp.SkinInformation = btnDown.SkinInformation = sc;

            btnUp.Glyph = new Glyph(UserInterfaceManager.Skin.Images["Shared.ArrowUp"].Texture)
            {
                SizeMode = SizeMode.Centered,
                Color = UserInterfaceManager.Skin.Controls["Button"].Layers["Control"].Text.Colors.Enabled
            };

            btnDown.Glyph = new Glyph(UserInterfaceManager.Skin.Images["Shared.ArrowDown"].Texture)
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
                SkinLayer lr = SkinInformation.Layers[0];
                Rectangle rc = new Rectangle(rect.Left + lr.ContentMargins.Left,
                                             rect.Top + lr.ContentMargins.Top,
                                             Width - lr.ContentMargins.Horizontal - btnDown.Width - btnUp.Width,
                                             Height - lr.ContentMargins.Vertical);
                UserInterfaceManager.Renderer.Draw(UserInterfaceManager.Skin.Images["ListBox.Selection"].Texture.Resource, rc, Color.FromNonPremultiplied(255, 255, 255, 128));
            }
        } // DrawControl



        private void ShiftIndex(bool direction)
        {
            if (mode == SpinBoxMode.List)
            {
                if (items.Count > 0)
                {
                    if (direction)
                        itemIndex += 1;
                    else
                        itemIndex -= 1;

                    if (itemIndex < 0)
                        itemIndex = 0;
                    if (itemIndex > items.Count - 1)
                        itemIndex = itemIndex = items.Count - 1;

                    Text = items[itemIndex].ToString();
                }
            }
            else
            {
                if (direction)
                    value += Step;
                else
                    value -= Step;

                if (value < Minimum)
                    value = Minimum;
                if (value > Maximum)
                    value = Maximum;

                Text = value.ToString("n" + rounding);
            }
        } // ShiftIndex



        private void Button_MousePress(object sender, MouseEventArgs e)
        {
            Focused = true;
            if (sender == btnUp)
                ShiftIndex(true);
            else if (sender == btnDown)
                ShiftIndex(false);
        } // Button_MousePress



        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);

            if (btnUp != null)
            {
                btnUp.Width = 16;
                btnUp.Height = Height - SkinInformation.Layers["Control"].ContentMargins.Vertical;
                btnUp.Top = SkinInformation.Layers["Control"].ContentMargins.Top;
                btnUp.Left = Width - 16 - 2 - 16 - 1;
            }
            if (btnDown != null)
            {
                btnDown.Width = 16;
                btnDown.Height = Height - SkinInformation.Layers["Control"].ContentMargins.Vertical;
                btnDown.Top = SkinInformation.Layers["Control"].ContentMargins.Top; ;
                btnDown.Left = Width - 16 - 2;
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