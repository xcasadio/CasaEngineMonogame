namespace Editor.WinForm
{
    /// <summary>
    /// 
    /// </summary>
    /*public class EditorForm
        : Form
    {

        public ExternalTool.SaveProjectDelegate SaveProject;
        private string m_IniFileName = string.Empty;
        private InputRtfForm m_DocForm = null;



        /// <summary>
        /// Use for form designer
        /// </summary>
        protected EditorForm()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        protected EditorForm(string preferenceFileName)
        {
            m_IniFileName = preferenceFileName;
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.FormClosedEvent);
            this.Load += new EventHandler(EditorForm_Load);
        }       



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void EditorForm_Load(object sender, EventArgs e)
        {
            LoadPreferences();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FormClosedEvent(object sender, FormClosedEventArgs e)
        {
            SavePreferences();
        }

        /// <summary>
        /// 
        /// </summary>
        protected void LoadPreferences()
        {
#if !DEBUG
            try
            {
#endif
                if (string.IsNullOrEmpty(m_IniFileName) == false
                    && File.Exists(m_IniFileName) == true)
                {
                    IniFile iniFile = new IniFile(m_IniFileName);
                    LoadPreferences(iniFile);
                }
#if !DEBUG
            }
            catch (Exception ex)
            {
                LogManager.Instance.WriteException(ex);
            }
#endif
        }

        /// <summary>
        /// 
        /// </summary>
        private void SavePreferences()
        {
#if !DEBUG
            try
            {
#endif
                if (string.IsNullOrEmpty(m_IniFileName) == false)
                {
                    IniFile iniFile = new IniFile();
                    SavePreferences(iniFile);
                    iniFile.Save(m_IniFileName);
                }
#if !DEBUG
            }
            catch (Exception ex)
            {
                LogManager.Instance.WriteException(ex);
            }
#endif
        }

        protected virtual void LoadPreferences(IniFile iniFile_) { }
        protected virtual void SavePreferences(IniFile iniFile_) { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName_"></param>
        protected void DisplayDocumentation(string title_, string fileName_)
        {
            if (m_DocForm == null
                || m_DocForm.IsDisposed == true)
            {
                m_DocForm = new InputRtfForm(title_, fileName_);
            }
            else
            {
                m_DocForm.Focus();
                return;
            }

            m_DocForm.Show();
        }

    }*/
}
