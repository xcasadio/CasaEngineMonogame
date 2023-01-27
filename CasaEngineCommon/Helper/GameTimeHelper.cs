using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace CasaEngineCommon.Helper
{
    public static class GameTimeHelper
    {
        /// <summary>
        /// Convert GameTime into milliseconds
        /// </summary>
        /// <param name="gameTime_"></param>
        /// <returns></returns>
        public static float GameTimeToMilliseconds(GameTime gameTime_)
        {
            return (float)gameTime_.ElapsedGameTime.Ticks / (float)TimeSpan.TicksPerSecond;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameTime_"></param>
        /// <returns></returns>
        public static float TotalGameTimeToMilliseconds(GameTime gameTime_)
        {
            return (float)gameTime_.TotalGameTime.Ticks / (float)TimeSpan.TicksPerSecond;
        }
    }
}
