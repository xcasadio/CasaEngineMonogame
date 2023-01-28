

using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Graphics2D;
using CasaEngine.Gameplay.Actor;
using CasaEngine;
using CasaEngine.World;
using CasaEngine.CoreSystems.Game;
using CasaEngine.FrontEnd.Screen;
using CasaEngine.Gameplay;
using CasaEngine.Project;
using CasaEngine.Asset;

#if EDITOR

using CasaEngine.Editor;
using CasaEngine.Editor.Tools;
using CasaEngine.Editor.Assets;

#endif


namespace CasaEngine.Game
{
    /// <summary>
    /// 
    /// </summary>
    public class GameInfo
    {

        static readonly private GameInfo m_GameInfo = new GameInfo();

        private WorldInfo m_WorldInfo;



        /// <summary>
        /// Gets
        /// </summary>
        static public GameInfo Instance
        {
            get { return m_GameInfo; }
        }


        /// <summary>
        /// Gets
        /// </summary>
        public WorldInfo WorldInfo
        {
            get { return m_WorldInfo; }
        }



        /// <summary>
        /// 
        /// </summary>
        public int MaximumPlayers
        {
            get;
            set;
        }

        /// <summary>
        /// 
        /// </summary>
        public int MaximumLocalPlayers
        {
            get;
            set;
        }




        /// <summary>
        /// 
        /// </summary>
        public GameInfo()
        {
            m_WorldInfo = new WorldInfo();
        }



    }
}
