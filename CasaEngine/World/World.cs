using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using FarseerPhysics.Common;
using CasaEngine.Game;
using CasaEngine.Gameplay;
using CasaEngine.Gameplay.Actor;
using CasaEngine.Design;
using CasaEngine.Physics2D;
using CasaEngineCommon.Design;

namespace CasaEngine.World
{
    /// <summary>
    /// 
    /// </summary>
    public class World
    {

        private List<Actor2D> m_Actors = new List<Actor2D>(30);
        private List<Actor2D> m_ActorsToAdd = new List<Actor2D>();

        private FarseerPhysics.Dynamics.World m_PhysicWorld;
        private HUDBase m_HUD = null;

        public event EventHandler Initializing;
        public event EventHandler LoadingContent;
        public event EventHandler Starting;



        /// <summary>
        /// Gets
        /// </summary>
        public Actor2D[] Actors
        {
            get { return m_Actors.ToArray(); }
        }

        /// <summary>
        /// Gets
        /// </summary>
        public FarseerPhysics.Dynamics.World PhysicWorld
        {
            get { return m_PhysicWorld; }
        }

        /// <summary>
        /// Gets/Sets
        /// </summary>
        public HUDBase HUD
        {
            get { return m_HUD; }
            set { m_HUD = value; }
        }



        /// <summary>
        /// 
        /// </summary>
        public World(bool usePhysics = true)
        {
            if (usePhysics == true)
            {
                m_PhysicWorld = new FarseerPhysics.Dynamics.World(GameInfo.Instance.WorldInfo.WorldGravity);
            }
        }




        /// <summary>
        /// Use only when the world is not running else use PushObject
        /// </summary>
        /// <param name="actor2D_"></param>
        public void AddObject(Actor2D actor2D_)
        {
            m_Actors.Add(actor2D_);
        }

        /// <summary>
        /// Use only when the world is not running else use PushObject
        /// </summary>
        /// <param name="actor2D_"></param>
        public void PushObject(Actor2D actor2D_)
        {
            m_ActorsToAdd.Add(actor2D_);
        }


        /// <summary>
        /// 
        /// </summary>
        public void Initialize()
        {


            if (Initializing != null)
            {
                Initializing.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void LoadContent()
        {
            if (m_HUD != null)
            {
                //m_HUD.LoadContent(GraphicsDevice);
            }

            if (LoadingContent != null)
            {
                LoadingContent.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void Start()
        {
            if (m_HUD != null)
            {
                m_HUD.Start();
            }

            if (Starting != null)
            {
                Starting.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elapsedTime_"></param>
        public virtual void Update(float elapsedTime_)
        {
            List<Actor2D> toRemove = new List<Actor2D>();

            foreach (Actor2D a in m_ActorsToAdd)
            {
                m_Actors.Add(a);
            }

            m_ActorsToAdd.Clear();

            GameInfo.Instance.WorldInfo.Update(elapsedTime_);

            if (m_PhysicWorld != null)
            {
                m_PhysicWorld.Step(elapsedTime_);
            }

            foreach (Actor2D a in m_Actors)
            {
                a.Update(elapsedTime_);

                if (a.Remove == true)
                {
                    toRemove.Add(a);
                }
            }

            foreach (Actor2D a in toRemove)
            {
                m_Actors.Remove(a);
            }

            if (m_HUD != null)
            {
                m_HUD.Update(elapsedTime_);
            }

            Collision2DManager.Instance.Update();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elapsedTime_"></param>
        public virtual void Draw(float elapsedTime_)
        {
            foreach (Actor2D a in m_Actors)
            {
                if (a is IRenderable)
                {
                    ((IRenderable)a).Draw(elapsedTime_);
                }
            }

            if (m_HUD != null)
            {
                m_HUD.Draw(elapsedTime_);
            }
        }

    }
}
