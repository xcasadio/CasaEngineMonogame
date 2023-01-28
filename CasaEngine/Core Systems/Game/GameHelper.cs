using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using CasaEngine.Game;

namespace CasaEngine.CoreSystems.Game
{
    /// <summary>
    /// 
    /// </summary>
    static public class GameHelper
    {
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="game_"></param>
        /// <returns></returns>
        static public T GetService<T>(Microsoft.Xna.Framework.Game game_) where T : class
        {
            return (T)game_.Services.GetService(typeof(T));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="game_"></param>
        static public void RemoveService<T>(Microsoft.Xna.Framework.Game game_) where T : class
        {
            if (game_.Services.GetService(typeof(T)) != null)
            {
                game_.Services.RemoveService(typeof(T));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="game_"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="game_"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="game_"></param>
        /// <returns></returns>
        static public void RemoveGameComponent<T>(Microsoft.Xna.Framework.Game game_)
            where T : Microsoft.Xna.Framework.GameComponent
        {
            T c = GetGameComponent<T>(game_);

            if (c != null)
            {
                game_.Components.Remove(c);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="game_"></param>
        /// <param name="state_"></param>
        /// <returns></returns>
        static public void EnableAllGameComponent(Microsoft.Xna.Framework.Game game_, bool state_)
        {
            foreach (Microsoft.Xna.Framework.GameComponent gc in game_.Components)
            {
                gc.Enabled = state_;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="game_"></param>
        /// <param name="state_"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="game_"></param>
        /// <param name="state_"></param>
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

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="game_"></param>
        /// <param name="state_"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="game_"></param>
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
