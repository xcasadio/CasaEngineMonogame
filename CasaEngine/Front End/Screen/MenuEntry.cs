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

        string text;

        float selectionFade;

        readonly Renderer2DComponent m_Renderer2DComponent = null;




        public string Text
        {
            get => text;
            set => text = value;
        }





        public event EventHandler<PlayerIndexEventArgs> Selected;


        protected internal virtual void OnSelectEntry(PlayerIndex playerIndex)
        {
            if (Selected != null)
                Selected(this, new PlayerIndexEventArgs(playerIndex));
        }





        public MenuEntry(string text)
        {
            this.text = text;

            m_Renderer2DComponent = GameHelper.GetGameComponent<Renderer2DComponent>(Engine.Instance.Game);
        }




        public virtual void Update(MenuScreen screen, bool isSelected,
                                                      float elapsedTime_)
        {
            // When the menu selection changes, entries gradually fade between
            // their selected and deselected appearance, rather than instantly
            // popping to the new state.
            float fadeSpeed = elapsedTime_ * 4.0f;//(float)gameTime.ElapsedGameTime.TotalSeconds * 4;

            if (isSelected)
                selectionFade = System.Math.Min(selectionFade + fadeSpeed, 1);
            else
                selectionFade = System.Math.Max(selectionFade - fadeSpeed, 0);
        }


        public virtual void Draw(MenuScreen screen, Vector2 position,
                                 bool isSelected, float elapsedTime_)
        {
            // Draw the selected entry in yellow, otherwise white.
            Color color = isSelected ? Color.Yellow : Color.White;

            // Pulsate the size of the selected menu entry.
            //double time = gameTime.TotalGameTime.TotalSeconds;
            float time = elapsedTime_;

            float pulsate = CasaEngineCommon.Helper.MathHelper.Sin(time * 6) + 1;

            float scale = 1 + pulsate * 0.05f * selectionFade;

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
            m_Renderer2DComponent.AddText2D(font, text, position, 0.0f, new Vector2(scale), color, 0.99f);
        }

        public virtual int GetHeight(MenuScreen screen)
        {
            return Engine.Instance.DefaultSpriteFont.LineSpacing;
        }

    }
}
