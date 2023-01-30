using CasaEngine.Graphics2D;
using CasaEngine.Game;
using CasaEngine.CoreSystems.Game;

namespace CasaEngine.FrontEnd.Screen
{
    public class DebugScreen
        : Screen
    {

        int selectedEntry = 0;
        string menuTitle;

        Renderer2DComponent m_Renderer2DComponent = null;



        public DebugScreen(string menuTitle, string menuName_)
            : base(menuName_)
        {
            this.menuTitle = menuTitle;

            TransitionOnTime = TimeSpan.FromSeconds(0.5);
            TransitionOffTime = TimeSpan.FromSeconds(0.5);

            m_Renderer2DComponent = GameHelper.GetGameComponent<Renderer2DComponent>(Engine.Instance.Game);
        }

    }


}
