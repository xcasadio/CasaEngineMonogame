using System.ComponentModel;
using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;


namespace CasaEngine.Project
{
    public class ProjectConfig
    {

        private string _windowTitle = "Game name undefined";

        private string _projectName = "No Project Opened";

        private string _firstScreenName = string.Empty;

        private bool _allowUserResizing;
        private bool _isFixedTimeStep;
        private bool _isMouseVisible;

#if !FINAL
        private bool _debugIsFullScreen = false;
        private int _debugWidth = 800, _debugHeight = 600;
#endif

#if EDITOR

        private string _dataSrcCtrlServer = string.Empty;

        private string _dataSrcCtrlUser = string.Empty;

        private string _dataSrcCtrlPassword = string.Empty;

        private string _dataSrcCtrlWorkspace = string.Empty;


        static private readonly uint Version = 2;
#endif



#if EDITOR
        [Category("Project")]
#endif
        public string WindowTitle
        {
            get { return _windowTitle; }
#if EDITOR
            set { _windowTitle = value; }
#endif
        }

#if EDITOR
        [Category("Project")]
#endif
        public string ProjectName
        {
            get { return _projectName; }
#if EDITOR
            set { _projectName = value; }
#endif
        }

#if EDITOR
        [Category("Start")]
#endif
        public string FirstScreenName
        {
            get { return _firstScreenName; }
#if EDITOR
            set { _firstScreenName = value; }
#endif
        }

#if EDITOR
        [Category("Project")]
#endif
        public bool AllowUserResizing
        {
            get { return _allowUserResizing; }
#if EDITOR
            set { _allowUserResizing = value; }
#endif
        }

#if EDITOR
        [Category("Project")]
#endif
        public bool IsFixedTimeStep
        {
            get { return _isFixedTimeStep; }
#if EDITOR
            set { _isFixedTimeStep = value; }
#endif
        }

#if EDITOR
        [Category("Project")]
#endif
        public bool IsMouseVisible
        {
            get { return _isMouseVisible; }
#if EDITOR
            set { _isMouseVisible = value; }
#endif
        }

#if !FINAL


#if EDITOR
        [Category("Debug")]
#endif
        public bool DebugIsFullScreen
        {
            get { return _debugIsFullScreen; }
#if EDITOR
            set { _debugIsFullScreen = value; }
#endif
        }

#if EDITOR
        [Category("Debug")]
#endif
        public int DebugWidth
        {
            get { return _debugWidth; }
#if EDITOR
            set { _debugWidth = value; }
#endif
        }

#if EDITOR
        [Category("Debug")]
#endif
        public int DebugHeight
        {
            get { return _debugHeight; }
#if EDITOR
            set { _debugHeight = value; }
#endif
        }


#endif



        public ProjectConfig()
        {

        }

        public ProjectConfig(XmlElement el, SaveOption option)
        {
            Load(el, option);
        }



        public void Load(XmlElement el, SaveOption option)
        {
            uint version = uint.Parse(el.Attributes["version"].Value);

            _windowTitle = el.Attributes["windowTitle"].Value;
            _projectName = el.Attributes["name"].Value;
            _firstScreenName = el.Attributes["firstScreenName"].Value;

            if (version > 1)
            {
                _allowUserResizing = bool.Parse(el.Attributes["AllowUserResizing"].Value);
                _isFixedTimeStep = bool.Parse(el.Attributes["IsFixedTimeStep"].Value);
                _isMouseVisible = bool.Parse(el.Attributes["IsMouseVisible"].Value);
            }

#if !FINAL
            XmlElement xmlElt = (XmlElement)el.SelectSingleNode("Debug");

            _debugIsFullScreen = bool.Parse(xmlElt.Attributes["debugIsFullScreen"].Value);
            _debugHeight = int.Parse(xmlElt.Attributes["debugHeight"].Value);
            _debugWidth = int.Parse(xmlElt.Attributes["debugWidth"].Value);
#endif
        }

#if EDITOR

        public void Save(XmlElement el, SaveOption option)
        {
            el.OwnerDocument.AddAttribute(el, "name", _projectName);
            el.OwnerDocument.AddAttribute(el, "version", Version.ToString());

            el.OwnerDocument.AddAttribute(el, "windowTitle", _windowTitle);
            el.OwnerDocument.AddAttribute(el, "firstScreenName", _firstScreenName);

            el.OwnerDocument.AddAttribute(el, "AllowUserResizing", _allowUserResizing.ToString());
            el.OwnerDocument.AddAttribute(el, "IsFixedTimeStep", _isFixedTimeStep.ToString());
            el.OwnerDocument.AddAttribute(el, "IsMouseVisible", _isMouseVisible.ToString());

            //Debug
            XmlElement xmlElt = el.OwnerDocument.CreateElement("Debug");
            el.AppendChild(xmlElt);
            el.OwnerDocument.AddAttribute(xmlElt, "debugIsFullScreen", _debugIsFullScreen.ToString());
            el.OwnerDocument.AddAttribute(xmlElt, "debugHeight", _debugHeight.ToString());
            el.OwnerDocument.AddAttribute(xmlElt, "debugWidth", _debugWidth.ToString());
        }

        public void Save(BinaryWriter bw, SaveOption option)
        {
            bw.Write(Version);
            bw.Write(_projectName);
            bw.Write(_windowTitle);
            bw.Write(_firstScreenName);
            bw.Write(_allowUserResizing);
            bw.Write(_isFixedTimeStep);
            bw.Write(_isMouseVisible);

            //Debug
            bw.Write(_debugIsFullScreen);
            bw.Write(_debugHeight);
            bw.Write(_debugWidth);
        }

#endif

    }
}
