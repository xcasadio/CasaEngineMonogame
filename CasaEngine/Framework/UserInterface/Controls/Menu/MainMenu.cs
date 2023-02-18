
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Framework.UserInterface.Controls.Menu
{

    public class MainMenu : MenuBase
    {


        private Rectangle[] _rectangle;
        private int _lastIndex = -1;



        public MainMenu(UserInterfaceManager userInterfaceManager)
            : base(userInterfaceManager)
        {
            Left = 0;
            Top = 0;
            Height = 24;
            Detached = false;
            DoubleClicks = false;
            StayOnTop = true;
        } // MainMenu



        protected internal override void InitSkin()
        {
            base.InitSkin();
            SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["MainMenu"]);
        } // InitSkin



        protected override void DrawControl(Rectangle rect)
        {
            var layerControl = SkinInformation.Layers["Control"];
            var layerSelection = SkinInformation.Layers["Selection"];
            _rectangle = new Rectangle[Items.Count];

            UserInterfaceManager.Renderer.DrawLayer(this, layerControl, rect, ControlState.Enabled);

            var prev = layerControl.ContentMargins.Left;

            // Draw root menu items (the others are rendered using context menu controls)
            for (var i = 0; i < Items.Count; i++)
            {
                var menuItem = Items[i];

                var textWidth = (int)layerControl.Text.Font.Font.MeasureString(menuItem.Text).X + layerControl.ContentMargins.Horizontal;
                _rectangle[i] = new Rectangle(rect.Left + prev, rect.Top + layerControl.ContentMargins.Top, textWidth, Height - layerControl.ContentMargins.Vertical);
                prev += textWidth;

                if (ItemIndex != i)
                {
                    if (menuItem.Enabled && Enabled)
                    {
                        UserInterfaceManager.Renderer.DrawString(this, layerControl, menuItem.Text, _rectangle[i], ControlState.Enabled, false);
                    }
                    else
                    {
                        UserInterfaceManager.Renderer.DrawString(this, layerControl, menuItem.Text, _rectangle[i], ControlState.Disabled, false);
                    }
                }
                else
                {
                    if (Items[i].Enabled && Enabled)
                    {
                        UserInterfaceManager.Renderer.DrawLayer(this, layerSelection, _rectangle[i], ControlState.Enabled);
                        UserInterfaceManager.Renderer.DrawString(this, layerSelection, menuItem.Text, _rectangle[i], ControlState.Enabled, false);
                    }
                    else
                    {
                        UserInterfaceManager.Renderer.DrawLayer(this, layerSelection, _rectangle[i], ControlState.Disabled);
                        UserInterfaceManager.Renderer.DrawString(this, layerSelection, menuItem.Text, _rectangle[i], ControlState.Disabled, false);
                    }
                }
            }
        } // DrawControl



        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var i = _lastIndex;

            TrackItem(e.State.X - Root.ControlLeftAbsoluteCoordinate, e.State.Y - Root.ControlTopAbsoluteCoordinate);

            if (ItemIndex >= 0 && (i == -1 || i != ItemIndex) && Items[ItemIndex].Items != null && Items[ItemIndex].Items.Count > 0 && ChildMenu != null)
            {
                HideSubMenu();
                _lastIndex = ItemIndex;
                OnClick(e);
            }
            else if (ChildMenu != null && i != ItemIndex)
            {
                HideSubMenu();
                Focused = true;
            }
        } // OnMouseMove

        private void TrackItem(int x, int y)
        {
            if (Items != null && Items.Count > 0 && _rectangle != null)
            {
                Invalidate();
                for (var i = 0; i < _rectangle.Length; i++)
                {
                    if (_rectangle[i].Contains(x, y))
                    {
                        if (i >= 0 && i != ItemIndex)
                        {
                            Items[i].OnSelected(new EventArgs());
                        }
                        ItemIndex = i;
                        return;
                    }
                }
                if (ChildMenu == null)
                {
                    ItemIndex = -1;
                }
            }
        } // TrackItem



        protected override void OnMouseOut(MouseEventArgs e)
        {
            base.OnMouseOut(e);
            OnMouseMove(e);
        } // OnMouseOut



        private void HideSubMenu()
        {
            if (ChildMenu != null)
            {
                ((ContextMenu)ChildMenu).HideMenu(true);
                ChildMenu.Dispose();
                ChildMenu = null;
            }
        } // HideSubMenu

        public virtual void HideMenu()
        {
            if (ChildMenu != null)
            {
                ((ContextMenu)ChildMenu).HideMenu(true);
                ChildMenu.Dispose();
                ChildMenu = null;
            }
            if (UserInterfaceManager.FocusedControl is MenuBase)
            {
                Focused = true;
            }

            Invalidate();
            ItemIndex = -1;
        } // HideMenu



        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);

            var ex = e is MouseEventArgs ? (MouseEventArgs)e : new MouseEventArgs();

            if (ex.Button == MouseButton.Left || ex.Button == MouseButton.None)
            {
                if (ItemIndex >= 0 && Items[ItemIndex].Enabled)
                {
                    if (ItemIndex >= 0 && Items[ItemIndex].Items != null && Items[ItemIndex].Items.Count > 0)
                    {
                        if (ChildMenu != null)
                        {
                            ChildMenu.Dispose();
                            ChildMenu = null;
                        }
                        ChildMenu = new ContextMenu(UserInterfaceManager);
                        (ChildMenu as ContextMenu).RootMenu = this;
                        (ChildMenu as ContextMenu).ParentMenu = this;
                        (ChildMenu as ContextMenu).Sender = Root;
                        ChildMenu.Items.AddRange(Items[ItemIndex].Items);

                        var y = Root.ControlTopAbsoluteCoordinate + _rectangle[ItemIndex].Bottom + 1;
                        (ChildMenu as ContextMenu).Show(Root, Root.ControlLeftAbsoluteCoordinate + _rectangle[ItemIndex].Left, y);
                        if (ex.Button == MouseButton.None)
                        {
                            (ChildMenu as ContextMenu).ItemIndex = 0;
                        }
                    }
                    else
                    {
                        if (ItemIndex >= 0)
                        {
                            Items[ItemIndex].OnClick(ex);
                        }
                    }
                }
            }
        } // OnClick



        protected override void OnKeyPress(KeyEventArgs e)
        {
            base.OnKeyPress(e);

            if (e.Key == Keys.Right)
            {
                ItemIndex += 1;
                e.Handled = true;
            }
            if (e.Key == Keys.Left)
            {
                ItemIndex -= 1;
                e.Handled = true;
            }

            if (ItemIndex > Items.Count - 1)
            {
                ItemIndex = 0;
            }

            if (ItemIndex < 0)
            {
                ItemIndex = Items.Count - 1;
            }

            if (e.Key == Keys.Down && Items.Count > 0 && Items[ItemIndex].Items.Count > 0)
            {
                e.Handled = true;
                OnClick(new MouseEventArgs(new MouseState(), MouseButton.None, Point.Zero));
            }
            if (e.Key == Keys.Escape)
            {
                e.Handled = true;
                ItemIndex = -1;
            }
        } // OnKeyPress



        protected override void OnFocusGained()
        {
            base.OnFocusGained();
            if (ItemIndex < 0 && Items.Count > 0)
            {
                ItemIndex = 0;
            }
        } // OnFocusGained

        protected override void OnFocusLost()
        {
            base.OnFocusLost();
            if (ChildMenu == null || !ChildMenu.Visible)
            {
                ItemIndex = -1;
            }
        } // OnFocusLost


    } // MainMenu
} // XNAFinalEngine.UserInterface