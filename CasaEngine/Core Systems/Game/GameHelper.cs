using CasaEngine.Game;

namespace CasaEngine.Core_Systems.Game
{
    public static class GameHelper
    {
        public static T GetService<T>(Microsoft.Xna.Framework.Game game) where T : class
        {
            return (T)game.Services.GetService(typeof(T));
        }

        public static void RemoveService<T>(Microsoft.Xna.Framework.Game game) where T : class
        {
            if (game.Services.GetService(typeof(T)) != null)
            {
                game.Services.RemoveService(typeof(T));
            }
        }

        public static T GetGameComponent<T>(Microsoft.Xna.Framework.Game game) where T : class
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

        public static T GetDrawableGameComponent<T>(Microsoft.Xna.Framework.Game game) where T : class
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

        public static void RemoveGameComponent<T>(Microsoft.Xna.Framework.Game game)
            where T : Microsoft.Xna.Framework.GameComponent
        {
            var c = GetGameComponent<T>(game);

            if (c != null)
            {
                game.Components.Remove(c);
            }
        }

        public static void EnableAllGameComponent(Microsoft.Xna.Framework.Game game, bool state)
        {
            foreach (Microsoft.Xna.Framework.GameComponent gc in game.Components)
            {
                gc.Enabled = state;
            }
        }

        public static bool EnableGameComponent<T>(Microsoft.Xna.Framework.Game game, bool state) where T : class
        {
            var gc = GetGameComponent<T>(game) as Microsoft.Xna.Framework.GameComponent;

            if (gc != null)
            {
                gc.Enabled = state;
                return true;
            }

            return false;
        }

        public static void VisibleAllDrawableGameComponent(Microsoft.Xna.Framework.Game game, bool state)
        {
            foreach (Microsoft.Xna.Framework.GameComponent gc in game.Components)
            {
                if (gc is Microsoft.Xna.Framework.DrawableGameComponent)
                {
                    ((Microsoft.Xna.Framework.DrawableGameComponent)gc).Visible = state;
                }
            }
        }

        public static bool VisibleDrawableGameComponent<T>(Microsoft.Xna.Framework.Game game, bool state) where T : class
        {
            var dgc = GetDrawableGameComponent<T>(game) as Microsoft.Xna.Framework.DrawableGameComponent;

            if (dgc != null)
            {
                dgc.Visible = state;
                return true;
            }

            return false;
        }

        public static void ScreenResize(Microsoft.Xna.Framework.Game game)
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
