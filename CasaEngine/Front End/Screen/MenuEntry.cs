//-----------------------------------------------------------------------------
// MenuEntry.cs
//
// XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------


using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Game;
using CasaEngine.Graphics2D;
using CasaEngine.CoreSystems.Game;

namespace CasaEngine.FrontEnd.Screen
{
    public class MenuEntry
    {

        string _text;

        float _selectionFade;

        readonly Renderer2DComponent _renderer2DComponent = null;




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

            _renderer2DComponent = GameHelper.GetGameComponent<Renderer2DComponent>(Engine.Instance.Game);
        }




        public virtual void Update(MenuScreen screen, bool isSelected,
                                                      float elapsedTime)
        {
            // When the menu selection changes, entries gradually fade between
            // their selected and deselected appearance, rather than instantly
            // popping to the new state.
            float fadeSpeed = elapsedTime * 4.0f;//(float)gameTime.ElapsedGameTime.TotalSeconds * 4;

            if (isSelected)
            {
                _selectionFade = System.Math.Min(_selectionFade + fadeSpeed, 1);
            }
            else
            {
                _selectionFade = System.Math.Max(_selectionFade - fadeSpeed, 0);
            }
        }


        public virtual void Draw(MenuScreen screen, Vector2 position,
                                 bool isSelected, float elapsedTime)
        {
            // Draw the selected entry in yellow, otherwise white.
            Color color = isSelected ? Color.Yellow : Color.White;

            // Pulsate the size of the selected menu entry.
            //double time = gameTime.TotalGameTime.TotalSeconds;
            float time = elapsedTime;

            float pulsate = CasaEngineCommon.Helper.MathHelper.Sin(time * 6) + 1;

            float scale = 1 + pulsate * 0.05f * _selectionFade;

            // Modify the alpha to fade text out during transitions.
            color = new Color(color.R, color.G, color.B, screen.TransitionAlpha);

            // Draw text, centered on the middle of each line.
            ScreenManagerComponent screenManager = screen.ScreenManagerComponent;
            SpriteBatch spriteBatch = Engine.Instance.SpriteBatch;
            SpriteFont font = Engine.Instance.DefaultSpriteFont;

            Vector2 origin = new Vector2(0, font.LineSpacing / 2);

            position = Vector2.Subtract(position, origin);

            //spriteBatch.DrawString(font, text, position, color, 0,
            //                       origin, scale, SpriteEffects.None, 0);
            _renderer2DComponent.AddText2D(font, _text, position, 0.0f, new Vector2(scale), color, 0.99f);
        }

        public virtual int GetHeight(MenuScreen screen)
        {
            return Engine.Instance.DefaultSpriteFont.LineSpacing;
        }

    }
}
