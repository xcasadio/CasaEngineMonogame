using GameComponent = Microsoft.Xna.Framework.GameComponent;
using DrawableGameComponent = Microsoft.Xna.Framework.DrawableGameComponent;


namespace CasaEngine.Framework.Game;

public static class GameExtension
{
    public static T GetService<T>(this Microsoft.Xna.Framework.Game game) where T : class
    {
        return (T)game.Services.GetService(typeof(T));
    }

    public static void RemoveService<T>(this Microsoft.Xna.Framework.Game game) where T : GameComponent
    {
        if (game.Services.GetService(typeof(T)) != null)
        {
            game.Services.RemoveService(typeof(T));
        }
    }

    public static T? GetGameComponent<T>(this Microsoft.Xna.Framework.Game game) where T : GameComponent
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

    public static T? GetDrawableGameComponent<T>(this Microsoft.Xna.Framework.Game game) where T : DrawableGameComponent
    {
        foreach (var gameComponent in game.Components)
        {
            if (gameComponent is T component)
            {
                return component;
            }
        }

        return null;
    }

    public static void RemoveGameComponent<T>(this Microsoft.Xna.Framework.Game game)
        where T : GameComponent
    {
        var component = game.GetGameComponent<T>();

        if (component != null)
        {
            game.Components.Remove(component);
        }
    }

    public static void EnableAllGameComponent(this Microsoft.Xna.Framework.Game game, bool state)
    {
        foreach (GameComponent component in game.Components)
        {
            component.Enabled = state;
        }
    }

    public static bool SetEnableGameComponent<T>(this Microsoft.Xna.Framework.Game game, bool state) where T : GameComponent
    {
        if (game.GetGameComponent<T>() is GameComponent gc)
        {
            gc.Enabled = state;
            return true;
        }

        return false;
    }

    public static void SetVisibleAllDrawableGameComponent(this Microsoft.Xna.Framework.Game game, bool state)
    {
        foreach (var component in game.Components)
        {
            if (component is DrawableGameComponent drawableGameComponent)
            {
                drawableGameComponent.Visible = state;
            }
        }
    }

    public static bool SetVisibleDrawableGameComponent<T>(this Microsoft.Xna.Framework.Game game, bool state) where T : DrawableGameComponent
    {
        var drawableGameComponent = game.GetDrawableGameComponent<T>();
        if (drawableGameComponent != null)
        {
            drawableGameComponent.Visible = state;
            return true;
        }

        return false;
    }

    public static void ScreenResize(this Microsoft.Xna.Framework.Game game, int width, int height)
    {
        foreach (var component in game.Components)
        {
            if (component is IGameComponentResizable resizable)
            {
                resizable?.OnResize(width, height);
            }
        }
    }
}