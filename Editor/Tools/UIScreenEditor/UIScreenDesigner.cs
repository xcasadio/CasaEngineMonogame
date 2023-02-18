using CasaEngine.Framework.UserInterface;
using Control = CasaEngine.Framework.UserInterface.Control;

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
        public void AddControl(Control control_)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="control_"></param>
        public void RemoveControl(Control control_)
        {
            m_UIManager.RootControls.Remove(control_);
        }
    }
}
