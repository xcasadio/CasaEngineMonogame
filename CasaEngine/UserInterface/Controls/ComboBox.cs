
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using CasaEngine.CoreSystems;




namespace XNAFinalEngine.UserInterface
{

    /// <summary>
    /// Combo Box
    /// </summary>
    public class ComboBox : TextBox
    {


        private readonly Button buttonDown;
        private readonly List<object> items = new List<object>();
        private readonly ListBox listCombo;

        /// <summary>
        /// Maximum Number of items showed by time.
        /// </summary>
        private int maxItems = 5;

        /// <summary>
        /// Draw Selection?
        /// </summary>
        private bool drawSelection = true;



        /// <summary>
        /// Indicates if the internal list box is visible or not.
        /// </summary>
        public bool ListBoxVisible { get { return listCombo.Visible; } }

        /// <summary>
        /// Read Only?
        /// </summary>
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

        /// <summary>
        /// Draw Selection?
        /// </summary>
        public bool DrawSelection
        {
            get { return drawSelection; }
            set { drawSelection = value; }
        } // DrawSelection

        /// <summary>
        /// Text
        /// </summary>
        public override string Text
        {
            get { return base.Text; }
            set
            {
                base.Text = value;
                if (!items.Contains(value))
                {
                    ItemIndex = -1;
                }
            }
        } // Text

        /// <summary>
        /// List of Items.
        /// </summary>
        public virtual List<object> Items
        {
            get { return items; }
        } // Items

        /// <summary>
        /// Maximum Number of items showed by time.
        /// </summary>
        public int MaxItemsShow
        {
            get { return maxItems; }
            set
            {
                if (maxItems != value)
                {
                    maxItems = value;
                    if (!Suspended)
                        OnMaxItemsChanged(new EventArgs());
                }
            }
        } // MaxItems

        /// <summary>
        /// Item index.
        /// </summary>
        public int ItemIndex
        {
            get { return listCombo.ItemIndex; }
            set
            {
                if (value >= 0 && value < items.Count)
                {
                    listCombo.ItemIndex = value;
                    Text = listCombo.Items[value].ToString();
                }
                else
                {
                    listCombo.ItemIndex = -1;
                }
                if (!Suspended)
                    OnItemIndexChanged(new EventArgs());
            }
        } // ItemIndex



        public event EventHandler MaxItemsChanged;
        public event EventHandler ItemIndexChanged;



        /// <summary>
        /// Combo Box
        /// </summary>
        public ComboBox(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            Height = 20;
            Width = 64;
            ReadOnly = true;

            buttonDown = new Button(UserInterfaceManager)
            {
                SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["ComboBox.Button"]),
                CanFocus = false
            };
            buttonDown.Click += ButtonDownClick;
            Add(buttonDown, false);

            listCombo = new ListBox(UserInterfaceManager)
            {
                HotTrack = true,
                Detached = true,
                Visible = false,
                Items = items
            };
        } // ComboBox



        /// <summary>
        /// Dispose managed resources.
        /// </summary>
        protected override void DisposeManagedResources()
        {
            // A disposed object could be still generating events, because it is alive for a time, in a disposed state, but alive nevertheless.
            MaxItemsChanged = null;
            ItemIndexChanged = null;
            // We added the listbox to another parent other than this control, so we dispose it manually.
            if (listCombo != null)
            {
                listCombo.Dispose();
            }
            UserInterfaceManager.InputSystem.MouseDown -= InputMouseDown;
            base.DisposeManagedResources();
        } // DisposeManagedResources



        protected internal override void Init()
        {
            base.Init();

            listCombo.Click += ListComboClick;
            UserInterfaceManager.InputSystem.MouseDown += InputMouseDown;

            listCombo.SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["ComboBox.ListBox"]);
            buttonDown.Glyph = new Glyph(UserInterfaceManager.Skin.Images["Shared.ArrowDown"].Texture)
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



        /// <summary>
        /// Prerender the control into the control's render target.
        /// </summary>
        protected override void DrawControl(Rectangle rect)
        {
            base.DrawControl(rect);

            if (ReadOnly && (Focused || listCombo.Focused) && drawSelection)
            {
                SkinLayer lr = SkinInformation.Layers[0];
                Rectangle rc = new Rectangle(rect.Left + lr.ContentMargins.Left,
                                             rect.Top + lr.ContentMargins.Top,
                                             Width - lr.ContentMargins.Horizontal - buttonDown.Width,
                                             Height - lr.ContentMargins.Vertical);
                UserInterfaceManager.Renderer.Draw(UserInterfaceManager.Skin.Images["ListBox.Selection"].Texture.Resource, rc, Color.FromNonPremultiplied(255, 255, 255, 128));
            }
        } // DrawControl



        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);

            if (buttonDown != null)
            {
                buttonDown.Width = 16;
                buttonDown.Height = Height - SkinInformation.Layers[0].ContentMargins.Vertical;
                buttonDown.Top = SkinInformation.Layers[0].ContentMargins.Top;
                buttonDown.Left = Width - buttonDown.Width - 2;
            }
        } // OnResize



        private void ListComboClick(object sender, EventArgs e)
        {
            MouseEventArgs ex = (e is MouseEventArgs) ? (MouseEventArgs)e : new MouseEventArgs();

            if (ex.Button == MouseButton.Left || ex.Button == MouseButton.None)
            {
                listCombo.Visible = false;
                if (listCombo.ItemIndex >= 0)
                {
                    Text = listCombo.Items[listCombo.ItemIndex].ToString();
                    Focused = true;
                    ItemIndex = listCombo.ItemIndex;
                }
            }
            // The focus was removed to assure that the list box is focused.
            CanFocus = true;
            Focused = true;
        } // ListComboClick



        private void ButtonDownClick(object sender, EventArgs e)
        {
            if (items != null && items.Count > 0)
            {
                if (Root != null && Root is Container)
                {
                    (Root as Container).Add(listCombo, false);
                    listCombo.Alpha = Root.Alpha;
                    listCombo.Left = ControlLeftAbsoluteCoordinate - Root.Left;
                    listCombo.Top = ControlTopAbsoluteCoordinate - Root.Top + Height + 1;
                }
                else
                {
                    UserInterfaceManager.Add(listCombo);
                    listCombo.Alpha = Alpha;
                    listCombo.Left = ControlLeftAbsoluteCoordinate;
                    listCombo.Top = ControlTopAbsoluteCoordinate + Height + 1;
                }

                listCombo.AutoHeight(maxItems);
                // If there is no place to put the list box under the control then is moved up.
                if (listCombo.ControlTopAbsoluteCoordinate + listCombo.Height > UserInterfaceManager.Screen.Height)
                    listCombo.Top = listCombo.Top - Height - listCombo.Height - 2;

                listCombo.Visible = !listCombo.Visible;
                if (listCombo.Visible)
                {
                    // The focus is removed to assure that the list box is focused.
                    CanFocus = false;
                    listCombo.Focused = true;
                }
                else
                {
                    // The focus was removed to assure that the list box is focused.
                    CanFocus = true;
                    Focused = true;
                }
                listCombo.Width = Width;
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
            if (listCombo.Visible &&
               (e.Position.X < listCombo.ControlLeftAbsoluteCoordinate ||
                e.Position.X > listCombo.ControlLeftAbsoluteCoordinate + listCombo.Width ||
                e.Position.Y < listCombo.ControlTopAbsoluteCoordinate ||
                e.Position.Y > listCombo.ControlTopAbsoluteCoordinate + listCombo.Height) &&
               (e.Position.X < buttonDown.ControlLeftAbsoluteCoordinate ||
                e.Position.X > buttonDown.ControlLeftAbsoluteCoordinate + buttonDown.Width ||
                e.Position.Y < buttonDown.ControlTopAbsoluteCoordinate ||
                e.Position.Y > buttonDown.ControlTopAbsoluteCoordinate + buttonDown.Height))
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