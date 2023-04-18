namespace CasaEngine.Framework.FrontEnd.Screen;

public class DebugScreen : Screen
{
    private int _selectedEntry = 0;
    private string _menuTitle;

    public DebugScreen(string menuTitle, string menuName) : base(menuName)
    {
        _menuTitle = menuTitle;

        TransitionOnTime = TimeSpan.FromSeconds(0.5);
        TransitionOffTime = TimeSpan.FromSeconds(0.5);
    }
}