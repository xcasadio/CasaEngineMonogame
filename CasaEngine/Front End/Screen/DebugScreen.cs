using CasaEngine.Graphics2D;
using CasaEngine.Game;
using CasaEngine.CoreSystems.Game;

namespace CasaEngine.FrontEnd.Screen
{
    public class DebugScreen
        : Screen
    {

        int _selectedEntry = 0;
        string _menuTitle;

        Renderer2DComponent _renderer2DComponent = null;



        public DebugScreen(string menuTitle, string menuName)
            : base(menuName)
        {
            _menuTitle = menuTitle;

            TransitionOnTime = TimeSpan.FromSeconds(0.5);
            TransitionOffTime = TimeSpan.FromSeconds(0.5);

            _renderer2DComponent = GameHelper.GetGameComponent<Renderer2DComponent>(Engine.Instance.Game);
        }

    }


}
