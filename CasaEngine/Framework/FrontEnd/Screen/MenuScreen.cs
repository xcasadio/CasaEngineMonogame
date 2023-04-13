using CasaEngine.Framework.Game;
using CasaEngine.Framework.Graphics2D;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.FrontEnd.Screen;

public class MenuScreen : Screen
{
    private readonly List<MenuEntry> _menuEntries = new();
    private int _selectedEntry;
    private readonly string _menuTitle;

    private readonly Renderer2dComponent _renderer2dComponent;



    protected IList<MenuEntry> MenuEntries => _menuEntries;


    public MenuScreen(string menuTitle, string menuName)
        : base(menuName)
    {
        _menuTitle = menuTitle;

        TransitionOnTime = TimeSpan.FromSeconds(0.5);
        TransitionOffTime = TimeSpan.FromSeconds(0.5);

        _renderer2dComponent = EngineComponents.Game.GetGameComponent<Renderer2dComponent>();
    }



    public override void HandleInput(InputState input)
    {
        // Move to the previous menu entry?
        if (input.IsMenuUp(ControllingPlayer))
        {
            _selectedEntry--;

            if (_selectedEntry < 0)
            {
                _selectedEntry = _menuEntries.Count - 1;
            }
        }

        // Move to the next menu entry?
        if (input.IsMenuDown(ControllingPlayer))
        {
            _selectedEntry++;

            if (_selectedEntry >= _menuEntries.Count)
            {
                _selectedEntry = 0;
            }
        }

        // Accept or cancel the menu? We pass in our ControllingPlayer, which may
        // either be null (to accept input from any player) or a specific index.
        // If we pass a null controlling player, the InputState helper returns to
        // us which player actually provided the input. We pass that through to
        // OnSelectEntry and OnCancel, so they can tell which player triggered them.
        PlayerIndex playerIndex;

        if (input.IsMenuSelect(ControllingPlayer, out playerIndex))
        {
            OnSelectEntry(_selectedEntry, playerIndex);
        }
        else if (input.IsMenuCancel(ControllingPlayer, out playerIndex))
        {
            OnCancel(playerIndex);
        }
    }

    protected virtual void OnSelectEntry(int entryIndex, PlayerIndex playerIndex)
    {
        if (_selectedEntry < 0 || _selectedEntry >= _menuEntries.Count)
        {
            return;
        }

        _menuEntries[_selectedEntry].OnSelectEntry(playerIndex);
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
        for (var i = 0; i < _menuEntries.Count; i++)
        {
            var isSelected = IsActive && i == _selectedEntry;

            _menuEntries[i].Update(this, isSelected, elapsedTime);
        }
    }

    public override void Draw(float elapsedTime)
    {
        var spriteBatch = EngineComponents.SpriteBatch;
        var font = EngineComponents.DefaultSpriteFont;

        var position = new Vector2(100, 150);

        // Make the menu slide into place during transitions, using a
        // power curve to make things look more interesting (this makes
        // the movement slow down as it nears the end).
        var transitionOffset = (float)Math.Pow(TransitionPosition, 2);

        if (ScreenState == ScreenState.TransitionOn)
        {
            position.X -= transitionOffset * 256;
        }
        else
        {
            position.X += transitionOffset * 512;
        }

        //spriteBatch.Begin();

        // Draw each menu entry in turn.
        for (var i = 0; i < _menuEntries.Count; i++)
        {
            var menuEntry = _menuEntries[i];

            var isSelected = IsActive && i == _selectedEntry;

            menuEntry.Draw(this, position, isSelected, elapsedTime);

            position.Y += menuEntry.GetHeight(this);
        }

        // Draw the menu title.
        var titlePosition = new Vector2(426, 80);
        var titleOrigin = font.MeasureString(_menuTitle) / 2.0f;
        titlePosition = Vector2.Subtract(titlePosition, titleOrigin);
        var titleColor = new Color((byte)192, (byte)192, (byte)192, TransitionAlpha);
        var titleScale = 1.25f;

        titlePosition.Y -= transitionOffset * 100;

        _renderer2dComponent.AddText2d(font, _menuTitle, titlePosition, 0.0f, new Vector2(titleScale), titleColor, 0.99f);
        //spriteBatch.DrawString(font, menuTitle, titlePosition, titleColor, 0,
        //					   titleOrigin, titleScale, SpriteEffects.None, 0);

        //spriteBatch.End();
    }

}