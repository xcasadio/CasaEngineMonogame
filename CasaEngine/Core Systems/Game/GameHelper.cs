using CasaEngine.Game;

namespace CasaEngine.CoreSystems.Game
{
    static public class GameHelper
    {
        static public T GetService<T>(Microsoft.Xna.Framework.Game game) where T : class
        {
            return (T)game.Services.GetService(typeof(T));
        }

        static public void RemoveService<T>(Microsoft.Xna.Framework.Game game) where T : class
        {
            if (game.Services.GetService(typeof(T)) != null)
            {
                game.Services.RemoveService(typeof(T));
            }
        }

        static public T GetGameComponent<T>(Microsoft.Xna.Framework.Game game) where T : class
        {
            foreach (Microsoft.Xna.Framework.GameComponent gc in game.Components)
            {
                if (gc is T)
                {
                    return gc as T;
                }
            }

            return null;
        }

        static public T GetDrawableGameComponent<T>(Microsoft.Xna.Framework.Game game) where T : class
        {
            foreach (Microsoft.Xna.Framework.GameComponent gc in game.Components)
            {
                if (gc is T && gc is Microsoft.Xna.Framework.DrawableGameComponent)
                {
                    return gc as T;
                }
            }

            return null;
        }

        static public void RemoveGameComponent<T>(Microsoft.Xna.Framework.Game game)
            where T : Microsoft.Xna.Framework.GameComponent
        {
            T c = GetGameComponent<T>(game);

            if (c != null)
            {
                game.Components.Remove(c);
            }
        }

        static public void EnableAllGameComponent(Microsoft.Xna.Framework.Game game, bool state)
        {
            foreach (Microsoft.Xna.Framework.GameComponent gc in game.Components)
            {
                gc.Enabled = state;
            }
        }

        static public bool EnableGameComponent<T>(Microsoft.Xna.Framework.Game game, bool state) where T : class
        {
            Microsoft.Xna.Framework.GameComponent gc = GetGameComponent<T>(game) as Microsoft.Xna.Framework.GameComponent;

            if (gc != null)
            {
                gc.Enabled = state;
                return true;
            }

            return false;
        }

        static public void VisibleAllDrawableGameComponent(Microsoft.Xna.Framework.Game game, bool state)
        {
            foreach (Microsoft.Xna.Framework.GameComponent gc in game.Components)
            {
                if (gc is Microsoft.Xna.Framework.DrawableGameComponent)
                {
                    ((Microsoft.Xna.Framework.DrawableGameComponent)gc).Visible = state;
                }
            }
        }

        static public bool VisibleDrawableGameComponent<T>(Microsoft.Xna.Framework.Game game, bool state) where T : class
        {
            Microsoft.Xna.Framework.DrawableGameComponent dgc = GetDrawableGameComponent<T>(game) as Microsoft.Xna.Framework.DrawableGameComponent;

            if (dgc != null)
            {
                dgc.Visible = state;
                return true;
            }

            return false;
        }

        static public void ScreenResize(Microsoft.Xna.Framework.Game game)
        {
            foreach (Microsoft.Xna.Framework.GameComponent c in game.Components)
            {
                if (c is IGameComponentResizable)
                {
                    (c as IGameComponentResizable).OnResize();
                }
            }
        }
    }
}
