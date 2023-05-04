//-----------------------------------------------------------------------------
// MenuEntry.cs
//
// XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

using CasaEngine.Framework.Graphics2D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MathHelper = CasaEngine.Core.Helper.MathHelper;

namespace CasaEngine.Framework.FrontEnd.Screen;

public class MenuEntry
{
    private string _text;

    private float _selectionFade;

    private readonly Renderer2dComponent _renderer2dComponent;
    private readonly SpriteFont _font;

    public string Text
    {
        get => _text;
        set => _text = value;
    }

    public event EventHandler<PlayerIndexEventArgs> Selected;

    protected internal virtual void OnSelectEntry(PlayerIndex playerIndex)
    {
        if (Selected != null)
        {
            Selected(this, new PlayerIndexEventArgs(playerIndex));
        }
    }

    public MenuEntry(string text, Renderer2dComponent renderer2dComponent, SpriteFont font)
    {
        _text = text;
        _renderer2dComponent = renderer2dComponent;
        _font = font;
    }

    public virtual void Update(MenuScreen screen, bool isSelected, float elapsedTime)
    {
        // When the menu selection changes, entries gradually fade between
        // their selected and deselected appearance, rather than instantly
        // popping to the new state.
        var fadeSpeed = elapsedTime * 4.0f;//(float)gameTime.ElapsedGameTime.TotalSeconds * 4;

        if (isSelected)
        {
            _selectionFade = Math.Min(_selectionFade + fadeSpeed, 1);
        }
        else
        {
            _selectionFade = Math.Max(_selectionFade - fadeSpeed, 0);
        }
    }

    public virtual void Draw(MenuScreen screen, Vector2 position, bool isSelected, float elapsedTime)
    {
        // Draw the selected entry in yellow, otherwise white.
        var color = isSelected ? Color.Yellow : Color.White;

        // Pulsate the size of the selected menu entry.
        //double time = gameTime.TotalGameTime.TotalSeconds;
        var time = elapsedTime;

        var pulsate = MathHelper.Sin(time * 6) + 1;

        var scale = 1 + pulsate * 0.05f * _selectionFade;

        // Modify the alpha to fade text out during transitions.
        color = new Color(color.R, color.G, color.B, screen.TransitionAlpha);

        // Draw text, centered on the middle of each line.
        var screenManager = screen.ScreenManagerComponent;

        var origin = new Vector2(0, _font.LineSpacing / 2);

        position = Vector2.Subtract(position, origin);

        _renderer2dComponent.AddText2d(_font, _text, position, 0.0f, new Vector2(scale), color, 0.99f);
    }

    public virtual int GetHeight(MenuScreen screen)
    {
        return _font.LineSpacing;
    }

}