using CasaEngine.Framework.Game;
using CasaEngine.Framework.Graphics2D;

namespace CasaEngine.Framework.FrontEnd.Screen
{
    public class DebugScreen : Screen
    {
        private int _selectedEntry = 0;
        private string _menuTitle;

        private Renderer2DComponent _renderer2DComponent;

        public DebugScreen(string menuTitle, string menuName)
            : base(menuName)
        {
            _menuTitle = menuTitle;

            TransitionOnTime = TimeSpan.FromSeconds(0.5);
            TransitionOffTime = TimeSpan.FromSeconds(0.5);

            _renderer2DComponent = EngineComponents.Game.GetGameComponent<Renderer2DComponent>();
        }
    }
}
