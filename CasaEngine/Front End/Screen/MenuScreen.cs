using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Game;
using CasaEngine.Graphics2D;
using CasaEngine.CoreSystems.Game;

namespace CasaEngine.FrontEnd.Screen
{
    public class MenuScreen
        : Screen
    {
        readonly List<MenuEntry> menuEntries = new List<MenuEntry>();
        int selectedEntry = 0;
        readonly string menuTitle;

        readonly Renderer2DComponent m_Renderer2DComponent = null;



        protected IList<MenuEntry> MenuEntries => menuEntries;


        public MenuScreen(string menuTitle, string menuName_)
            : base(menuName_)
        {
            this.menuTitle = menuTitle;

            TransitionOnTime = TimeSpan.FromSeconds(0.5);
            TransitionOffTime = TimeSpan.FromSeconds(0.5);

            m_Renderer2DComponent = GameHelper.GetGameComponent<Renderer2DComponent>(Engine.Instance.Game);
        }



        public override void HandleInput(InputState input)
        {
            // Move to the previous menu entry?
            if (input.IsMenuUp(ControllingPlayer))
            {
                selectedEntry--;

                if (selectedEntry < 0)
                    selectedEntry = menuEntries.Count - 1;
            }

            // Move to the next menu entry?
            if (input.IsMenuDown(ControllingPlayer))
            {
                selectedEntry++;

                if (selectedEntry >= menuEntries.Count)
                    selectedEntry = 0;
            }

            // Accept or cancel the menu? We pass in our ControllingPlayer, which may
            // either be null (to accept input from any player) or a specific index.
            // If we pass a null controlling player, the InputState helper returns to
            // us which player actually provided the input. We pass that through to
            // OnSelectEntry and OnCancel, so they can tell which player triggered them.
            PlayerIndex playerIndex;

            if (input.IsMenuSelect(ControllingPlayer, out playerIndex))
            {
                OnSelectEntry(selectedEntry, playerIndex);
            }
            else if (input.IsMenuCancel(ControllingPlayer, out playerIndex))
            {
                OnCancel(playerIndex);
            }
        }

        protected virtual void OnSelectEntry(int entryIndex, PlayerIndex playerIndex)
        {
            if (selectedEntry < 0 || selectedEntry >= menuEntries.Count)
            {
                return;
            }

            menuEntries[selectedEntry].OnSelectEntry(playerIndex);
        }

        protected virtual void OnCancel(PlayerIndex playerIndex)
        {
            ExitScreen();
        }

        protected void OnCancel(object sender, PlayerIndexEventArgs e)
        {
            OnCancel(e.PlayerIndex);
        }



        public override void Update(float elapsedTime, bool otherScreenHasFocus,
                                                       bool coveredByOtherScreen)
        {
            base.Update(elapsedTime, otherScreenHasFocus, coveredByOtherScreen);

            // Update each nested MenuEntry object.
            for (int i = 0; i < menuEntries.Count; i++)
            {
                bool isSelected = IsActive && (i == selectedEntry);

                menuEntries[i].Update(this, isSelected, elapsedTime);
            }
        }

        public override void Draw(float elapsedTime_)
        {
            SpriteBatch spriteBatch = Engine.Instance.SpriteBatch;
            SpriteFont font = Engine.Instance.DefaultSpriteFont;

            Vector2 position = new Vector2(100, 150);

            // Make the menu slide into place during transitions, using a
            // power curve to make things look more interesting (this makes
            // the movement slow down as it nears the end).
            float transitionOffset = (float)System.Math.Pow(TransitionPosition, 2);

            if (ScreenState == ScreenState.TransitionOn)
                position.X -= transitionOffset * 256;
            else
                position.X += transitionOffset * 512;

            //spriteBatch.Begin();

            // Draw each menu entry in turn.
            for (int i = 0; i < menuEntries.Count; i++)
            {
                MenuEntry menuEntry = menuEntries[i];

                bool isSelected = IsActive && (i == selectedEntry);

                menuEntry.Draw(this, position, isSelected, elapsedTime_);

                position.Y += menuEntry.GetHeight(this);
            }

            // Draw the menu title.
            Vector2 titlePosition = new Vector2(426, 80);
            Vector2 titleOrigin = font.MeasureString(menuTitle) / 2.0f;
            titlePosition = Vector2.Subtract(titlePosition, titleOrigin);
            Color titleColor = new Color((byte)192, (byte)192, (byte)192, TransitionAlpha);
            float titleScale = 1.25f;

            titlePosition.Y -= transitionOffset * 100;

            m_Renderer2DComponent.AddText2D(font, menuTitle, titlePosition, 0.0f, new Vector2(titleScale), titleColor, 0.99f);
            //spriteBatch.DrawString(font, menuTitle, titlePosition, titleColor, 0,
            //					   titleOrigin, titleScale, SpriteEffects.None, 0);

            //spriteBatch.End();
        }

    }
}
