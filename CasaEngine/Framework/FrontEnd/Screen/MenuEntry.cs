//-----------------------------------------------------------------------------
// MenuEntry.cs
//
// XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------


using CasaEngine.Framework.Game;
using CasaEngine.Framework.Graphics2D;
using Microsoft.Xna.Framework;
using MathHelper = CasaEngine.Core.Helper.MathHelper;

namespace CasaEngine.Framework.FrontEnd.Screen;

public class MenuEntry
{
    private string _text;

    private float _selectionFade;

    private readonly Renderer2DComponent _renderer2DComponent;




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





    public MenuEntry(string text)
    {
        _text = text;

        _renderer2DComponent = EngineComponents.Game.GetGameComponent<Renderer2DComponent>();
    }




    public virtual void Update(MenuScreen screen, bool isSelected,
        float elapsedTime)
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


    public virtual void Draw(MenuScreen screen, Vector2 position,
        bool isSelected, float elapsedTime)
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
        var spriteBatch = EngineComponents.SpriteBatch;
        var font = EngineComponents.DefaultSpriteFont;

        var origin = new Vector2(0, font.LineSpacing / 2);

        position = Vector2.Subtract(position, origin);

        //spriteBatch.DrawString(font, text, position, color, 0,
        //                       origin, scale, SpriteEffects.None, 0);
        _renderer2DComponent.AddText2D(font, _text, position, 0.0f, new Vector2(scale), color, 0.99f);
    }

    public virtual int GetHeight(MenuScreen screen)
    {
        return EngineComponents.DefaultSpriteFont.LineSpacing;
    }

}