using System.ComponentModel;
using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;


namespace CasaEngine.Project
{
    public class ProjectConfig
    {

        private string m_WindowTitle = "Game name undefined";

        private string m_ProjectName = "No Project Opened";

        private string m_FirstScreenName = string.Empty;

        private bool m_AllowUserResizing;
        private bool m_IsFixedTimeStep;
        private bool m_IsMouseVisible;

#if !FINAL
        private bool m_DebugIsFullScreen = false;
        private int m_DebugWidth = 800, m_DebugHeight = 600;
#endif

#if EDITOR

        private string m_DataSrcCtrl_Server = string.Empty;

        private string m_DataSrcCtrl_User = string.Empty;

        private string m_DataSrcCtrl_Password = string.Empty;

        private string m_DataSrcCtrl_Workspace = string.Empty;


        static private readonly uint m_Version = 2;
#endif



#if EDITOR
        [Category("Project")]
#endif
        public string WindowTitle
        {
            get { return m_WindowTitle; }
#if EDITOR
            set { m_WindowTitle = value; }
#endif
        }

#if EDITOR
        [Category("Project")]
#endif
        public string ProjectName
        {
            get { return m_ProjectName; }
#if EDITOR
            set { m_ProjectName = value; }
#endif
        }

#if EDITOR
        [Category("Start")]
#endif
        public string FirstScreenName
        {
            get { return m_FirstScreenName; }
#if EDITOR
            set { m_FirstScreenName = value; }
#endif
        }

#if EDITOR
        [Category("Project")]
#endif
        public bool AllowUserResizing
        {
            get { return m_AllowUserResizing; }
#if EDITOR
            set { m_AllowUserResizing = value; }
#endif
        }

#if EDITOR
        [Category("Project")]
#endif
        public bool IsFixedTimeStep
        {
            get { return m_IsFixedTimeStep; }
#if EDITOR
            set { m_IsFixedTimeStep = value; }
#endif
        }

#if EDITOR
        [Category("Project")]
#endif
        public bool IsMouseVisible
        {
            get { return m_IsMouseVisible; }
#if EDITOR
            set { m_IsMouseVisible = value; }
#endif
        }

#if !FINAL


#if EDITOR
        [Category("Debug")]
#endif
        public bool DebugIsFullScreen
        {
            get { return m_DebugIsFullScreen; }
#if EDITOR
            set { m_DebugIsFullScreen = value; }
#endif
        }

#if EDITOR
        [Category("Debug")]
#endif
        public int DebugWidth
        {
            get { return m_DebugWidth; }
#if EDITOR
            set { m_DebugWidth = value; }
#endif
        }

#if EDITOR
        [Category("Debug")]
#endif
        public int DebugHeight
        {
            get { return m_DebugHeight; }
#if EDITOR
            set { m_DebugHeight = value; }
#endif
        }


#endif



        public ProjectConfig()
        {

        }

        public ProjectConfig(XmlElement el_, SaveOption option_)
        {
            Load(el_, option_);
        }



        public void Load(XmlElement el_, SaveOption option_)
        {
            uint version = uint.Parse(el_.Attributes["version"].Value);

            m_WindowTitle = el_.Attributes["windowTitle"].Value;
            m_ProjectName = el_.Attributes["name"].Value;
            m_FirstScreenName = el_.Attributes["firstScreenName"].Value;

            if (version > 1)
            {
                m_AllowUserResizing = bool.Parse(el_.Attributes["AllowUserResizing"].Value);
                m_IsFixedTimeStep = bool.Parse(el_.Attributes["IsFixedTimeStep"].Value);
                m_IsMouseVisible = bool.Parse(el_.Attributes["IsMouseVisible"].Value);
            }

#if !FINAL
            XmlElement xmlElt = (XmlElement)el_.SelectSingleNode("Debug");

            m_DebugIsFullScreen = bool.Parse(xmlElt.Attributes["debugIsFullScreen"].Value);
            m_DebugHeight = int.Parse(xmlElt.Attributes["debugHeight"].Value);
            m_DebugWidth = int.Parse(xmlElt.Attributes["debugWidth"].Value);
#endif
        }

#if EDITOR

        public void Save(XmlElement el_, SaveOption option_)
        {
            el_.OwnerDocument.AddAttribute(el_, "name", m_ProjectName);
            el_.OwnerDocument.AddAttribute(el_, "version", m_Version.ToString());

            el_.OwnerDocument.AddAttribute(el_, "windowTitle", m_WindowTitle);
            el_.OwnerDocument.AddAttribute(el_, "firstScreenName", m_FirstScreenName);

            el_.OwnerDocument.AddAttribute(el_, "AllowUserResizing", m_AllowUserResizing.ToString());
            el_.OwnerDocument.AddAttribute(el_, "IsFixedTimeStep", m_IsFixedTimeStep.ToString());
            el_.OwnerDocument.AddAttribute(el_, "IsMouseVisible", m_IsMouseVisible.ToString());

            //Debug
            XmlElement xmlElt = el_.OwnerDocument.CreateElement("Debug");
            el_.AppendChild(xmlElt);
            el_.OwnerDocument.AddAttribute(xmlElt, "debugIsFullScreen", m_DebugIsFullScreen.ToString());
            el_.OwnerDocument.AddAttribute(xmlElt, "debugHeight", m_DebugHeight.ToString());
            el_.OwnerDocument.AddAttribute(xmlElt, "debugWidth", m_DebugWidth.ToString());
        }

        public void Save(BinaryWriter bw_, SaveOption option_)
        {
            bw_.Write(m_Version);
            bw_.Write(m_ProjectName);
            bw_.Write(m_WindowTitle);
            bw_.Write(m_FirstScreenName);
            bw_.Write(m_AllowUserResizing);
            bw_.Write(m_IsFixedTimeStep);
            bw_.Write(m_IsMouseVisible);

            //Debug
            bw_.Write(m_DebugIsFullScreen);
            bw_.Write(m_DebugHeight);
            bw_.Write(m_DebugWidth);
        }

#endif

    }
}
