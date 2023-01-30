using CasaEngine.Game;

namespace CasaEngine.CoreSystems.Game
{
    static public class GameHelper
    {
        static public T GetService<T>(Microsoft.Xna.Framework.Game game_) where T : class
        {
            return (T)game_.Services.GetService(typeof(T));
        }

        static public void RemoveService<T>(Microsoft.Xna.Framework.Game game_) where T : class
        {
            if (game_.Services.GetService(typeof(T)) != null)
            {
                game_.Services.RemoveService(typeof(T));
            }
        }

        static public T GetGameComponent<T>(Microsoft.Xna.Framework.Game game_) where T : class
        {
            foreach (Microsoft.Xna.Framework.GameComponent gc in game_.Components)
            {
                if (gc is T)
                {
                    return gc as T;
                }
            }

            return null;
        }

        static public T GetDrawableGameComponent<T>(Microsoft.Xna.Framework.Game game_) where T : class
        {
            foreach (Microsoft.Xna.Framework.GameComponent gc in game_.Components)
            {
                if (gc is T && gc is Microsoft.Xna.Framework.DrawableGameComponent)
                {
                    return gc as T;
                }
            }

            return null;
        }

        static public void RemoveGameComponent<T>(Microsoft.Xna.Framework.Game game_)
            where T : Microsoft.Xna.Framework.GameComponent
        {
            T c = GetGameComponent<T>(game_);

            if (c != null)
            {
                game_.Components.Remove(c);
            }
        }

        static public void EnableAllGameComponent(Microsoft.Xna.Framework.Game game_, bool state_)
        {
            foreach (Microsoft.Xna.Framework.GameComponent gc in game_.Components)
            {
                gc.Enabled = state_;
            }
        }

        static public bool EnableGameComponent<T>(Microsoft.Xna.Framework.Game game_, bool state_) where T : class
        {
            Microsoft.Xna.Framework.GameComponent gc = GetGameComponent<T>(game_) as Microsoft.Xna.Framework.GameComponent;

            if (gc != null)
            {
                gc.Enabled = state_;
                return true;
            }

            return false;
        }

        static public void VisibleAllDrawableGameComponent(Microsoft.Xna.Framework.Game game_, bool state_)
        {
            foreach (Microsoft.Xna.Framework.GameComponent gc in game_.Components)
            {
                if (gc is Microsoft.Xna.Framework.DrawableGameComponent)
                {
                    ((Microsoft.Xna.Framework.DrawableGameComponent)gc).Visible = state_;
                }
            }
        }

        static public bool VisibleDrawableGameComponent<T>(Microsoft.Xna.Framework.Game game_, bool state_) where T : class
        {
            Microsoft.Xna.Framework.DrawableGameComponent dgc = GetDrawableGameComponent<T>(game_) as Microsoft.Xna.Framework.DrawableGameComponent;

            if (dgc != null)
            {
                dgc.Visible = state_;
                return true;
            }

            return false;
        }

        static public void ScreenResize(Microsoft.Xna.Framework.Game game_)
        {
            foreach (Microsoft.Xna.Framework.GameComponent c in game_.Components)
            {
                if (c is IGameComponentResizable)
                {
                    (c as IGameComponentResizable).OnResize();
                }
            }
        }
    }
}
