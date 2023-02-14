using CasaEngine.Game;
using GameComponent = Microsoft.Xna.Framework.GameComponent;

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

        public static T? GetGameComponent<T>(Microsoft.Xna.Framework.Game game) where T : class
        {
            foreach (var gameComponent in game.Components)
            {
                if (gameComponent is T gc)
                {
                    return gc;
                }
            }

            return null;
        }

        public static T? GetDrawableGameComponent<T>(Microsoft.Xna.Framework.Game game) where T : class
        {
            foreach (var gameComponent in game.Components)
            {
                if (gameComponent is T component and Microsoft.Xna.Framework.DrawableGameComponent)
                {
                    return component;
                }
            }

            return null;
        }

        public static void RemoveGameComponent<T>(Microsoft.Xna.Framework.Game game)
            where T : GameComponent
        {
            var c = GetGameComponent<T>(game);

            if (c != null)
            {
                game.Components.Remove(c);
            }
        }

        public static void EnableAllGameComponent(Microsoft.Xna.Framework.Game game, bool state)
        {
            foreach (GameComponent gc in game.Components)
            {
                gc.Enabled = state;
            }
        }

        public static bool EnableGameComponent<T>(Microsoft.Xna.Framework.Game game, bool state) where T : class
        {
            var gc = GetGameComponent<T>(game) as GameComponent;

            if (gc != null)
            {
                gc.Enabled = state;
                return true;
            }

            return false;
        }

        public static void VisibleAllDrawableGameComponent(Microsoft.Xna.Framework.Game game, bool state)
        {
            foreach (GameComponent gc in game.Components)
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
            foreach (GameComponent c in game.Components)
            {
                if (c is IGameComponentResizable)
                {
                    (c as IGameComponentResizable).OnResize();
                }
            }
        }
    }
}
