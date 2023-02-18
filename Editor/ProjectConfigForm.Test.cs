#if UNITTEST

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using CasaEngine.Game;

namespace Editor
{
    [TestFixture]
	public partial class ProjectConfigForm
	{
        /// <summary>
        /// 
        /// </summary>
        public void LaunchUnitTest()
        {
            CasaEngine.Game.Engine.Instance.ProjectSettings.AllowUserResizing = true;
            CasaEngine.Game.Engine.Instance.ProjectSettings.DebugHeight = 100;
            CasaEngine.Game.Engine.Instance.ProjectSettings.DebugWidth = 100;
            CasaEngine.Game.Engine.Instance.ProjectSettings.DebugIsFullScreen = true;
            CasaEngine.Game.Engine.Instance.ProjectSettings.FirstScreenName = "FirstScreenNameTest";
            CasaEngine.Game.Engine.Instance.ProjectSettings.IsFixedTimeStep = true;
            CasaEngine.Game.Engine.Instance.ProjectSettings.IsMouseVisible = true;
            CasaEngine.Game.Engine.Instance.ProjectSettings.ProjectName = "ProjectNameTest";
            CasaEngine.Game.Engine.Instance.ProjectSettings.WindowTitle = "WindowTitleTest";
        }

        /// <summary>
        /// 
        /// </summary>
        public void CheckUnitTest()
        {
            Assert.AreEqual(CasaEngine.Game.Engine.Instance.ProjectSettings.AllowUserResizing, true, "AllowUserResizing");
            Assert.AreEqual(CasaEngine.Game.Engine.Instance.ProjectSettings.DebugHeight, 100, "DebugHeight");
            Assert.AreEqual(CasaEngine.Game.Engine.Instance.ProjectSettings.DebugWidth, 100, "DebugWidth");
            Assert.AreEqual(CasaEngine.Game.Engine.Instance.ProjectSettings.DebugIsFullScreen, true, "DebugIsFullScreen");
            Assert.AreEqual(CasaEngine.Game.Engine.Instance.ProjectSettings.FirstScreenName, "FirstScreenNameTest", "FirstScreenName");
            Assert.AreEqual(CasaEngine.Game.Engine.Instance.ProjectSettings.IsFixedTimeStep, true, "IsFixedTimeStep");
            Assert.AreEqual(CasaEngine.Game.Engine.Instance.ProjectSettings.IsMouseVisible, true, "IsMouseVisible");
            Assert.AreEqual(CasaEngine.Game.Engine.Instance.ProjectSettings.ProjectName, "ProjectNameTest", "ProjectName");
            Assert.AreEqual(CasaEngine.Game.Engine.Instance.ProjectSettings.WindowTitle, "WindowTitleTest", "WindowTitle");
        }
	}
}

#endif
