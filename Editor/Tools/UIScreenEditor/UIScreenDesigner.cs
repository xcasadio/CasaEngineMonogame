using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.FrontEnd.Screen.Gadget;
using XNAFinalEngine.UserInterface;

namespace Editor.Tools.UIScreenEditor
{
    /// <summary>
    /// 
    /// </summary>
    class UIScreenDesigner
    {
        UserInterfaceManager m_UIManager;

        /// <summary>
        /// Gets
        /// </summary>
        /*public UserInterfaceManager UIManager
        {
            get { return m_UIManager; }
        }*/

        /// <summary>
        /// 
        /// </summary>
        public TimeSpan TransitionOffTime
        {
            get;
            set;
        }

        /// <summary>
        /// 
        /// </summary>
        public TimeSpan TransitionOnTime
        {
            get;
            set;
        }

        /// <summary>
        /// 
        /// </summary>
        public UIScreenDesigner(UserInterfaceManager UIManager_)
        {
            m_UIManager = UIManager_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameTime"></param>
        public void Update(float elpasedTime)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameTime"></param>
        public void Draw(float elpasedTime)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="control_"></param>
        public void AddControl(XNAFinalEngine.UserInterface.Control control_)
        {
            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="control_"></param>
        public void RemoveControl(XNAFinalEngine.UserInterface.Control control_)
        {
            m_UIManager.RootControls.Remove(control_);
        }
    }
}
