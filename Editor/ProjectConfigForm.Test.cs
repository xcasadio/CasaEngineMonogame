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
            Engine.Instance.ProjectSettings.AllowUserResizing = true;
            Engine.Instance.ProjectSettings.DebugHeight = 100;
            Engine.Instance.ProjectSettings.DebugWidth = 100;
            Engine.Instance.ProjectSettings.DebugIsFullScreen = true;
            Engine.Instance.ProjectSettings.FirstScreenName = "FirstScreenNameTest";
            Engine.Instance.ProjectSettings.IsFixedTimeStep = true;
            Engine.Instance.ProjectSettings.IsMouseVisible = true;
            Engine.Instance.ProjectSettings.ProjectName = "ProjectNameTest";
            Engine.Instance.ProjectSettings.WindowTitle = "WindowTitleTest";
        }

        /// <summary>
        /// 
        /// </summary>
        public void CheckUnitTest()
        {
            Assert.AreEqual(Engine.Instance.ProjectSettings.AllowUserResizing, true, "AllowUserResizing");
            Assert.AreEqual(Engine.Instance.ProjectSettings.DebugHeight, 100, "DebugHeight");
            Assert.AreEqual(Engine.Instance.ProjectSettings.DebugWidth, 100, "DebugWidth");
            Assert.AreEqual(Engine.Instance.ProjectSettings.DebugIsFullScreen, true, "DebugIsFullScreen");
            Assert.AreEqual(Engine.Instance.ProjectSettings.FirstScreenName, "FirstScreenNameTest", "FirstScreenName");
            Assert.AreEqual(Engine.Instance.ProjectSettings.IsFixedTimeStep, true, "IsFixedTimeStep");
            Assert.AreEqual(Engine.Instance.ProjectSettings.IsMouseVisible, true, "IsMouseVisible");
            Assert.AreEqual(Engine.Instance.ProjectSettings.ProjectName, "ProjectNameTest", "ProjectName");
            Assert.AreEqual(Engine.Instance.ProjectSettings.WindowTitle, "WindowTitleTest", "WindowTitle");
        }
	}
}

#endif
