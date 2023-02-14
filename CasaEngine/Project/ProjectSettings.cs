using System.ComponentModel;
using System.Xml;
using CasaEngineCommon.Extension;
using CasaEngineCommon.Design;

namespace CasaEngine.Project
{
    public class ProjectSettings
    {
#if EDITOR
        private string _dataSrcCtrlServer = string.Empty;
        private string _dataSrcCtrlUser = string.Empty;
        private string _dataSrcCtrlPassword = string.Empty;
        private string _dataSrcCtrlWorkspace = string.Empty;
        private static readonly uint Version = 2;
#endif

        [Category("Project")]
        public string WindowTitle { get; set; } = "Game name undefined";

        [Category("Project")]
        public string ProjectName { get; set; } = "No Project Opened";

        [Category("Start")]
        public string FirstScreenName { get; set; } = string.Empty;

        [Category("Project")]
        public bool AllowUserResizing { get; set; }

        [Category("Project")]
        public bool IsFixedTimeStep { get; set; }

        [Category("Project")]
        public bool IsMouseVisible { get; set; }

#if !FINAL

        [Category("Debug")]
        public bool DebugIsFullScreen { get; set; } = false;

        [Category("Debug")]
        public int DebugWidth { get; set; } = 800;

        [Category("Debug")]
        public int DebugHeight { get; set; } = 600;

#endif

        public void Load(XmlElement el, SaveOption option)
        {
            var version = uint.Parse(el.Attributes["version"].Value);

            WindowTitle = el.Attributes["windowTitle"].Value;
            ProjectName = el.Attributes["name"].Value;
            FirstScreenName = el.Attributes["firstScreenName"].Value;

            if (version > 1)
            {
                AllowUserResizing = bool.Parse(el.Attributes["AllowUserResizing"].Value);
                IsFixedTimeStep = bool.Parse(el.Attributes["IsFixedTimeStep"].Value);
                IsMouseVisible = bool.Parse(el.Attributes["IsMouseVisible"].Value);
            }

#if !FINAL
            var xmlElt = (XmlElement)el.SelectSingleNode("Debug");

            DebugIsFullScreen = bool.Parse(xmlElt.Attributes["debugIsFullScreen"].Value);
            DebugHeight = int.Parse(xmlElt.Attributes["debugHeight"].Value);
            DebugWidth = int.Parse(xmlElt.Attributes["debugWidth"].Value);
#endif
        }

#if EDITOR

        public void Save(XmlElement el, SaveOption option)
        {
            el.OwnerDocument.AddAttribute(el, "name", ProjectName);
            el.OwnerDocument.AddAttribute(el, "version", Version.ToString());

            el.OwnerDocument.AddAttribute(el, "windowTitle", WindowTitle);
            el.OwnerDocument.AddAttribute(el, "firstScreenName", FirstScreenName);

            el.OwnerDocument.AddAttribute(el, "AllowUserResizing", AllowUserResizing.ToString());
            el.OwnerDocument.AddAttribute(el, "IsFixedTimeStep", IsFixedTimeStep.ToString());
            el.OwnerDocument.AddAttribute(el, "IsMouseVisible", IsMouseVisible.ToString());

            //Debug
            var xmlElt = el.OwnerDocument.CreateElement("Debug");
            el.AppendChild(xmlElt);
            el.OwnerDocument.AddAttribute(xmlElt, "debugIsFullScreen", DebugIsFullScreen.ToString());
            el.OwnerDocument.AddAttribute(xmlElt, "debugHeight", DebugHeight.ToString());
            el.OwnerDocument.AddAttribute(xmlElt, "debugWidth", DebugWidth.ToString());
        }

        public void Save(BinaryWriter bw, SaveOption option)
        {
            bw.Write(Version);
            bw.Write(ProjectName);
            bw.Write(WindowTitle);
            bw.Write(FirstScreenName);
            bw.Write(AllowUserResizing);
            bw.Write(IsFixedTimeStep);
            bw.Write(IsMouseVisible);

            //Debug
            bw.Write(DebugIsFullScreen);
            bw.Write(DebugHeight);
            bw.Write(DebugWidth);
        }

#endif
    }
}
