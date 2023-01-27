using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using CasaEngine.FrontEnd.Screen;
using CasaEngine.FrontEnd.Screen.Gadget;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Game;
using CasaEngine.Graphics2D;
using CasaEngineCommon.Helper;
using Editor.Game;
using XNAFinalEngine.UserInterface;
using XNAFinalEngine;

namespace Editor.Tools.UIScreenEditor
{
    /// <summary>
    /// 
    /// </summary>
    internal class UIScreenEditorComponent
        : CasaEngine.Game.DrawableGameComponent
    {
        #region Fields

        UIScreenDesigner m_Screen;
        System.Windows.Forms.Control m_Control;
        //Renderer2DComponent m_Renderer2DComponent;
        UserInterfaceManager m_UIManager;
        List<XNAFinalEngine.UserInterface.Control> m_ControlToAdd = new List<XNAFinalEngine.UserInterface.Control>();
        List<XNAFinalEngine.UserInterface.Control> m_ControlToRemove = new List<XNAFinalEngine.UserInterface.Control>();

        #endregion

        #region Properties

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        /// <param name="game_"></param>
        public UIScreenEditorComponent(CustomGameEditor game_)
            : base(game_)
        {
            game_.Components.Add(this);
            m_Control = game_.Control;
            m_Screen = new UIScreenDesigner(game_.UIManager);
            m_UIManager = game_.UIManager;
        }

        #endregion

        #region Methods

        #region GameComponent

        /// <summary>
        /// 
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void LoadContent()
        {
            base.LoadContent();

            m_Screen = new UIScreenDesigner(((CustomGameEditor)Game).UIManager);
            //m_Screen.ControllingPlayer = new PlayerIndex?(PlayerIndex.One);
            //m_Screen.ScreenState = ScreenState.Active;
            m_Screen.TransitionOffTime = TimeSpan.Zero;
            m_Screen.TransitionOnTime = TimeSpan.Zero;            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameTime"></param>
        public override void Update(GameTime gameTime)
        {
            float elpasedTime = GameTimeHelper.GameTimeToMilliseconds(gameTime);

            lock (this)
            {
                foreach (var control in m_ControlToAdd)
                {
                    control.CanFocus = true;
                    m_UIManager.RootControls.Add(control);                    
                    control.Focused = true;
                }
                m_ControlToAdd.Clear();

                foreach (var control in m_ControlToRemove)
                {
                    m_UIManager.RootControls.Remove(control);
                }
                m_ControlToRemove.Clear();
            }
            
            //m_Screen.Update(elpasedTime, false, false);

            base.Update(gameTime);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameTime"></param>
        public override void Draw(GameTime gameTime)
        {
            float elpasedTime = GameTimeHelper.GameTimeToMilliseconds(gameTime);
            m_Screen.Draw(elpasedTime);
            base.Draw(gameTime);
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="x_"></param>
        /// <param name="y_"></param>
        /// <returns></returns>
        public Control GetControlAt(int x_, int y_)
        {


            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="screen_"></param>
        public void SetCurrentScreen(UIScreen screen_)
        {
            //m_Screen.LoadFromScreen(screen_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="control_"></param>
        public void AddControl(XNAFinalEngine.UserInterface.Control control_)
        {
            control_.Movable = true;
            control_.Visible = true;
            control_.Enabled = true;
            control_.DesignMode = true;

            lock (this)
            {
                m_ControlToAdd.Add(control_);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="control_"></param>
        public void RemoveControl(XNAFinalEngine.UserInterface.Control control_)
        {
            lock (this)
            {
                m_ControlToRemove.Add(control_);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="screen_"></param>
        public void ApplyChanges(UIScreen screen_)
        {
            //screen_.CopyFrom(m_Screen);
        }

        #endregion
    }
}
