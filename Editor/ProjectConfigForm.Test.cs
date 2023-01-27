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
            Engine.Instance.ProjectConfig.AllowUserResizing = true;
            Engine.Instance.ProjectConfig.DebugHeight = 100;
            Engine.Instance.ProjectConfig.DebugWidth = 100;
            Engine.Instance.ProjectConfig.DebugIsFullScreen = true;
            Engine.Instance.ProjectConfig.FirstScreenName = "FirstScreenNameTest";
            Engine.Instance.ProjectConfig.IsFixedTimeStep = true;
            Engine.Instance.ProjectConfig.IsMouseVisible = true;
            Engine.Instance.ProjectConfig.ProjectName = "ProjectNameTest";
            Engine.Instance.ProjectConfig.WindowTitle = "WindowTitleTest";
        }

        /// <summary>
        /// 
        /// </summary>
        public void CheckUnitTest()
        {
            Assert.AreEqual(Engine.Instance.ProjectConfig.AllowUserResizing, true, "AllowUserResizing");
            Assert.AreEqual(Engine.Instance.ProjectConfig.DebugHeight, 100, "DebugHeight");
            Assert.AreEqual(Engine.Instance.ProjectConfig.DebugWidth, 100, "DebugWidth");
            Assert.AreEqual(Engine.Instance.ProjectConfig.DebugIsFullScreen, true, "DebugIsFullScreen");
            Assert.AreEqual(Engine.Instance.ProjectConfig.FirstScreenName, "FirstScreenNameTest", "FirstScreenName");
            Assert.AreEqual(Engine.Instance.ProjectConfig.IsFixedTimeStep, true, "IsFixedTimeStep");
            Assert.AreEqual(Engine.Instance.ProjectConfig.IsMouseVisible, true, "IsMouseVisible");
            Assert.AreEqual(Engine.Instance.ProjectConfig.ProjectName, "ProjectNameTest", "ProjectName");
            Assert.AreEqual(Engine.Instance.ProjectConfig.WindowTitle, "WindowTitleTest", "WindowTitle");
        }
	}
}

#endif
