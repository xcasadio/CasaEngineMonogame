using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using TomShane.Neoforce.Controls.Graphics;
using TomShane.Neoforce.Controls.Input;
using TomShane.Neoforce.Controls.Skins;

namespace TomShane.Neoforce.Controls;

public class ContextMenu : MenuBase
{
    private long _timer;

    protected internal Control Sender { get; set; }

    public ContextMenu()
    {
        Visible = false;
        Detached = true;
        StayOnBack = true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Manager.Input.MouseDown -= Input_MouseDown;
        }
        base.Dispose(disposing);
    }

    public override void Initialize(Manager manager)
    {
        base.Initialize(manager);

        Manager.Input.MouseDown += Input_MouseDown;
    }

    protected internal override void InitializeSkin()
    {
        base.InitializeSkin();
        Skin = new SkinControl(Manager.Skin.Controls["ContextMenu"]);
    }

    protected override void DrawControl(IRenderer renderer, Rectangle rect, GameTime gameTime)
    {
        base.DrawControl(renderer, rect, gameTime);

        var l1 = Skin.Layers["Control"];
        var l2 = Skin.Layers["Selection"];

        var vsize = LineHeight();
        var col = Color.White;

        for (var i = 0; i < Items.Count; i++)
        {
            var mod = i > 0 ? 2 : 0;
            var left = rect.Left + l1.ContentMargins.Left + vsize;
            var h = vsize - mod - (i < Items.Count - 1 ? 1 : 0);
            var top = rect.Top + l1.ContentMargins.Top + i * vsize + mod;

            if (Items[i].Separated && i > 0)
            {
                var r = new Rectangle(left, rect.Top + l1.ContentMargins.Top + i * vsize, LineWidth() - vsize + 4, 1);
                renderer.Draw(Manager.Skin.Controls["Control"].Layers[0].Image.Resource, r, l1.Text.Colors.Enabled);
            }
            if (ItemIndex != i)
            {
                if (Items[i].Enabled)
                {
                    var r = new Rectangle(left, top, LineWidth() - vsize, h);
                    renderer.DrawString(this, l1, Items[i].Text, r, false);
                    col = l1.Text.Colors.Enabled;
                }
                else
                {
                    var r = new Rectangle(left + l1.Text.OffsetX,
                        top + l1.Text.OffsetY,
                        LineWidth() - vsize, h);
                    renderer.DrawString(l1.Text.Font.Resource, Items[i].Text, r, l1.Text.Colors.Disabled, l1.Text.Alignment);
                    col = l1.Text.Colors.Disabled;
                }
            }
            else
            {
                if (Items[i].Enabled)
                {
                    var rs = new Rectangle(rect.Left + l1.ContentMargins.Left,
                        top,
                        Width - (l1.ContentMargins.Horizontal - Skin.OriginMargins.Horizontal),
                        h);
                    renderer.DrawLayer(this, l2, rs);

                    var r = new Rectangle(left,
                        top, LineWidth() - vsize, h);

                    renderer.DrawString(this, l2, Items[i].Text, r, false);
                    col = l2.Text.Colors.Enabled;
                }
                else
                {
                    var rs = new Rectangle(rect.Left + l1.ContentMargins.Left,
                        top,
                        Width - (l1.ContentMargins.Horizontal - Skin.OriginMargins.Horizontal),
                        vsize);
                    renderer.DrawLayer(l2, rs, l2.States.Disabled.Color, l2.States.Disabled.Index);

                    var r = new Rectangle(left + l1.Text.OffsetX,
                        top + l1.Text.OffsetY,
                        LineWidth() - vsize, h);
                    renderer.DrawString(l2.Text.Font.Resource, Items[i].Text, r, l2.Text.Colors.Disabled, l2.Text.Alignment);
                    col = l2.Text.Colors.Disabled;
                }

            }

            if (Items[i].Image != null)
            {
                var r = new Rectangle(rect.Left + l1.ContentMargins.Left + 3,
                    top + 3,
                    LineHeight() - 6,
                    LineHeight() - 6);
                renderer.Draw(Items[i].Image, r, Color.White);
            }

            if (Items[i].Items != null && Items[i].Items.Count > 0)
            {
                renderer.Draw(Manager.Skin.Images["Shared.ArrowRight"].Resource, rect.Left + LineWidth() - 4, rect.Top + l1.ContentMargins.Top + i * vsize + 8, col);
            }
        }
    }

    private int LineHeight()
    {
        var h = 0;
        if (Items.Count > 0)
        {
            var l = Skin.Layers["Control"];
            h = l.Text.Font.Resource.LineHeight + 9;
        }
        return h;
    }

    private int LineWidth()
    {
        var w = 0;
        var font = Skin.Layers["Control"].Text.Font;
        if (Items.Count > 0)
        {
            for (var i = 0; i < Items.Count; i++)
            {
                var wx = (int)font.Resource.MeasureString(Items[i].Text).X + 16;
                if (wx > w)
                {
                    w = wx;
                }
            }
        }

        w += 4 + LineHeight();

        return w;
    }

    private void AutoSize()
    {
        var font = Skin.Layers["Control"].Text;
        if (Items != null && Items.Count > 0)
        {
            Height = LineHeight() * Items.Count + (Skin.Layers["Control"].ContentMargins.Vertical - Skin.OriginMargins.Vertical);
            Width = LineWidth() + (Skin.Layers["Control"].ContentMargins.Horizontal - Skin.OriginMargins.Horizontal) + font.OffsetX;
        }
        else
        {
            Height = 16;
            Width = 16;
        }
    }

    private void TrackItem(int x, int y)
    {
        if (Items != null && Items.Count > 0)
        {
            var font = Skin.Layers["Control"].Text;
            var h = LineHeight();
            y -= Skin.Layers["Control"].ContentMargins.Top;
            var i = (int)((float)y / h);
            if (i < Items.Count)
            {
                if (i != ItemIndex && Items[i].Enabled)
                {
                    if (ChildMenu != null)
                    {
                        HideMenu(false);
                    }

                    if (i >= 0 && i != ItemIndex)
                    {
                        Items[i].SelectedInvoke(new EventArgs());
                    }

                    Focused = true;
                    ItemIndex = i;
                    _timer = (long)TimeSpan.FromTicks(DateTime.Now.Ticks).TotalMilliseconds;
                }
                else if (!Items[i].Enabled && ChildMenu == null)
                {
                    ItemIndex = -1;
                }
            }
            Invalidate();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        TrackItem(e.Position.X, e.Position.Y);
    }

    protected internal override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        AutoSize();

        var time = (long)TimeSpan.FromTicks(DateTime.Now.Ticks).TotalMilliseconds;

        if (_timer != 0 && time - _timer >= Manager.MenuDelay && ItemIndex >= 0 && Items[ItemIndex].Items.Count > 0 && ChildMenu == null)
        {
            OnClick(new MouseEventArgs(new MouseState(), MouseButton.Left, Point.Zero));
        }
    }

    protected override void OnMouseOut(MouseEventArgs e)
    {
        base.OnMouseOut(e);

        if (!CheckArea(e.State.X, e.State.Y) && ChildMenu == null)
        {
            ItemIndex = -1;
        }
    }

    protected override void OnClick(EventArgs e)
    {
        if (Sender != null && !(Sender is MenuBase))
        {
            Sender.Focused = true;
        }

        base.OnClick(e);
        _timer = 0;

        var ex = e is MouseEventArgs args ? args : new MouseEventArgs();

        if (ex.Button == MouseButton.Left || ex.Button == MouseButton.None)
        {
            if (ItemIndex >= 0 && Items[ItemIndex].Enabled)
            {
                if (ItemIndex >= 0 && Items[ItemIndex].Items != null && Items[ItemIndex].Items.Count > 0)
                {
                    if (ChildMenu == null)
                    {
                        var contextMenu = new ContextMenu();
                        contextMenu.Initialize(Manager);
                        ChildMenu = contextMenu;
                        contextMenu.RootMenu = RootMenu;
                        contextMenu.ParentMenu = this;
                        contextMenu.Sender = Sender;
                        ChildMenu.Items.AddRange(Items[ItemIndex].Items);
                        contextMenu.AutoSize();
                    }
                    var y = AbsoluteTop + Skin.Layers["Control"].ContentMargins.Top + ItemIndex * LineHeight();
                    (ChildMenu as ContextMenu).Show(Sender, AbsoluteLeft + Width - 1, y);
                    if (ex.Button == MouseButton.None)
                    {
                        (ChildMenu as ContextMenu).ItemIndex = 0;
                    }
                }
                else
                {
                    if (ItemIndex >= 0)
                    {
                        Items[ItemIndex].ClickInvoke(ex);
                    }
                    if (RootMenu is ContextMenu menu)
                    {
                        menu.HideMenu(true);
                    }
                    else if (RootMenu is MainMenu mainMenu)
                    {
                        mainMenu.HideMenu();
                    }
                }
            }
        }
    }

    protected override void OnKeyPress(KeyEventArgs e)
    {
        base.OnKeyPress(e);

        _timer = 0;

        if (e.Key == Keys.Down || (e.Key == Keys.Tab && !e.Shift))
        {
            e.Handled = true;
            ItemIndex += 1;
        }

        if (e.Key == Keys.Up || (e.Key == Keys.Tab && e.Shift))
        {
            e.Handled = true;
            ItemIndex -= 1;
        }

        if (ItemIndex > Items.Count - 1)
        {
            ItemIndex = 0;
        }

        if (ItemIndex < 0)
        {
            ItemIndex = Items.Count - 1;
        }

        if (e.Key == Keys.Right && Items[ItemIndex].Items.Count > 0)
        {
            e.Handled = true;
            OnClick(new MouseEventArgs(new MouseState(), MouseButton.None, Point.Zero));
        }
        if (e.Key == Keys.Left)
        {
            e.Handled = true;
            if (ParentMenu != null && ParentMenu is ContextMenu menu)
            {
                menu.Focused = true;
                menu.HideMenu(false);
            }
        }
        if (e.Key == Keys.Escape)
        {
            e.Handled = true;
            if (ParentMenu != null)
            {
                ParentMenu.Focused = true;
            }

            HideMenu(true);
        }
    }

    protected override void OnGamePadPress(GamePadEventArgs e)
    {
        _timer = 0;

        if (e.Button == GamePadButton.None)
        {
            return;
        }

        if (e.Button == GamePadActions.Down || e.Button == GamePadActions.NextControl)
        {
            e.Handled = true;
            ItemIndex += 1;
        }
        else if (e.Button == GamePadActions.Up || e.Button == GamePadActions.PrevControl)
        {
            e.Handled = true;
            ItemIndex -= 1;
        }

        if (ItemIndex > Items.Count - 1)
        {
            ItemIndex = 0;
        }

        if (ItemIndex < 0)
        {
            ItemIndex = Items.Count - 1;
        }

        if (e.Button == GamePadActions.Right && Items[ItemIndex].Items.Count > 0)
        {
            e.Handled = true;
            OnClick(new MouseEventArgs(new MouseState(), MouseButton.None, Point.Zero));
        }
        if (e.Button == GamePadActions.Left)
        {
            e.Handled = true;
            if (ParentMenu != null && ParentMenu is ContextMenu menu)
            {
                menu.Focused = true;
                menu.HideMenu(false);
            }
        }

        base.OnGamePadPress(e);
    }

    public virtual void HideMenu(bool hideCurrent)
    {
        if (hideCurrent)
        {
            Visible = false;
            ItemIndex = -1;
        }
        if (ChildMenu != null)
        {
            (ChildMenu as ContextMenu).HideMenu(true);
            ChildMenu.Dispose();
            ChildMenu = null;
        }
    }

    public override void Show()
    {
        Show(null, Left, Top);
    }

    public virtual void Show(Control sender, int x, int y)
    {
        AutoSize();
        base.Show();
        if (!Initialized)
        {
            Initialize(Manager); ;
        }

        if (sender != null && sender.Root != null && sender.Root is Container container)
        {
            container.Add(this, false);
        }
        else
        {
            Manager.Add(this);
        }

        Sender = sender;

        if (sender != null && sender.Root != null && sender.Root is Container)
        {
            Left = x - Root.AbsoluteLeft;
            Top = y - Root.AbsoluteTop;
        }
        else
        {
            Left = x;
            Top = y;
        }

        if (AbsoluteLeft + Width > Manager.TargetWidth)
        {
            Left = Left - Width;
            if (ParentMenu != null && ParentMenu is ContextMenu)
            {
                Left = Left - ParentMenu.Width + 2;
            }
            else if (ParentMenu != null)
            {
                Left = Manager.TargetWidth - (Parent?.AbsoluteLeft ?? 0) - Width - 2;
            }
        }
        if (AbsoluteTop + Height > Manager.TargetHeight)
        {
            Top = Top - Height;
            if (ParentMenu != null && ParentMenu is ContextMenu)
            {
                Top = Top + LineHeight();
            }
            else if (ParentMenu != null)
            {
                Top = ParentMenu.Top - Height - 1;
            }
        }

        Focused = true;
    }

    private void Input_MouseDown(object sender, MouseEventArgs e)
    {
        if (RootMenu is ContextMenu menu && !menu.CheckArea(e.Position.X, e.Position.Y) && Visible)
        {
            HideMenu(true);
        }
        else if (RootMenu is MainMenu mainMenu && mainMenu.ChildMenu != null && !(mainMenu.ChildMenu as ContextMenu).CheckArea(e.Position.X, e.Position.Y) && Visible)
        {
            mainMenu.HideMenu();
        }
    }

    private bool CheckArea(int x, int y)
    {
        if (Visible)
        {
            if (x <= AbsoluteLeft ||
                x >= AbsoluteLeft + Width ||
                y <= AbsoluteTop ||
                y >= AbsoluteTop + Height)
            {
                var ret = false;
                if (ChildMenu != null)
                {
                    ret = (ChildMenu as ContextMenu).CheckArea(x, y);
                }
                return ret;
            }

            return true;
        }

        return false;
    }

}