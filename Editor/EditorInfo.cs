namespace Editor
{
    /// <summary>
    /// 
    /// </summary>
    /*static class EditorInfo
    {
        public delegate void LaunchEditorDelegate(string objectPath_, BaseObject object_);

        /// <summary>
        /// Register an editor by the type supported by its
        /// </summary>
        static private Dictionary<Type, LaunchEditorDelegate> m_EditorsRegistered = new Dictionary<Type, LaunchEditorDelegate>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type_"></param>
        /// <param name="delegate_"></param>
        static public void RegisterEditor(Type type_, LaunchEditorDelegate delegate_)
        {
            m_EditorsRegistered[type_] = delegate_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type_"></param>
        /// <param name="obj_"></param>
        static public void LaunchEditor(Type type_, string objectPath_, BaseObject obj_)
        {
            if (m_EditorsRegistered.ContainsKey(type_) == true)
            {
                m_EditorsRegistered[type_](objectPath_, obj_);
            }
            else
            {
                LogManager.Instance.WriteLine("No editor found for the type of object '" + type_.FullName + "'");
            }
        }
    }*/
}
