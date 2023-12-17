using Microsoft.Xna.Framework;
using System;
using TomShane.Neoforce.Controls.Graphics;

namespace TomShane.Neoforce.Controls;

public class StackPanel : Container
{

    private Orientation _orientation;
    public Orientation Orientation
    {
        get => _orientation;
        set
        {
            _orientation = value;
            CalcLayout();
        }
    }

    /// <summary>
    /// Should the stack panel refresh itself, when a control is added
    /// </summary>
    public bool AutoRefresh { get; set; }


    private TimeSpan _refreshTimer;
    private const int RefreshTime = 300; //ms


    public StackPanel(Manager manager, Orientation orientation) : base(manager)
    {
        _orientation = orientation;
        Color = Color.Transparent;
        AutoRefresh = true;
        _refreshTimer = new TimeSpan(0, 0, 0, 0, RefreshTime);
    }



    private void CalcLayout()
    {
        var top = Top;
        var left = Left;

        foreach (var c in ClientArea.Controls)
        {
            var m = c.Margins;

            if (_orientation == Orientation.Vertical)
            {
                top += m.Top;
                c.Top = top;
                top += c.Height;
                top += m.Bottom;
                c.Left = left;
            }

            if (_orientation == Orientation.Horizontal)
            {
                left += m.Left;
                c.Left = left;
                left += c.Width;
                left += m.Right;
                c.Top = top;
            }
        }
    }



    protected override void DrawControl(IRenderer renderer, Rectangle rect, GameTime gameTime)
    {
        base.DrawControl(renderer, rect, gameTime);
    }



    protected override void OnResize(ResizeEventArgs e)
    {
        CalcLayout();
        base.OnResize(e);
    }


    protected internal override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (AutoRefresh)
        {
            _refreshTimer = _refreshTimer.Subtract(TimeSpan.FromMilliseconds(gameTime.ElapsedGameTime.TotalMilliseconds));
            if (_refreshTimer.TotalMilliseconds <= 0.00)
            {
                Refresh();
                _refreshTimer = new TimeSpan(0, 0, 0, 0, RefreshTime);
            }
        }
    }

    public override void Add(Control control)
    {
        base.Add(control);
        if (AutoRefresh)
        {
            Refresh();
        }
    }

    public override void Add(Control control, bool client)
    {
        base.Add(control, client);
        if (AutoRefresh)
        {
            Refresh();
        }
    }
}