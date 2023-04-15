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
            CasaEngine.Game.CasaEngineGame.Game.GameManager.ProjectSettings.AllowUserResizing = true;
            CasaEngine.Game.CasaEngineGame.Game.GameManager.ProjectSettings.DebugHeight = 100;
            CasaEngine.Game.CasaEngineGame.Game.GameManager.ProjectSettings.DebugWidth = 100;
            CasaEngine.Game.CasaEngineGame.Game.GameManager.ProjectSettings.DebugIsFullScreen = true;
            CasaEngine.Game.CasaEngineGame.Game.GameManager.ProjectSettings.FirstScreenName = "FirstScreenNameTest";
            CasaEngine.Game.CasaEngineGame.Game.GameManager.ProjectSettings.IsFixedTimeStep = true;
            CasaEngine.Game.CasaEngineGame.Game.GameManager.ProjectSettings.IsMouseVisible = true;
            CasaEngine.Game.CasaEngineGame.Game.GameManager.ProjectSettings.ProjectName = "ProjectNameTest";
            CasaEngine.Game.CasaEngineGame.Game.GameManager.ProjectSettings.WindowTitle = "WindowTitleTest";
        }

        /// <summary>
        /// 
        /// </summary>
        public void CheckUnitTest()
        {
            Assert.AreEqual(CasaEngine.Game.CasaEngineGame.Game.GameManager.ProjectSettings.AllowUserResizing, true, "AllowUserResizing");
            Assert.AreEqual(CasaEngine.Game.CasaEngineGame.Game.GameManager.ProjectSettings.DebugHeight, 100, "DebugHeight");
            Assert.AreEqual(CasaEngine.Game.CasaEngineGame.Game.GameManager.ProjectSettings.DebugWidth, 100, "DebugWidth");
            Assert.AreEqual(CasaEngine.Game.CasaEngineGame.Game.GameManager.ProjectSettings.DebugIsFullScreen, true, "DebugIsFullScreen");
            Assert.AreEqual(CasaEngine.Game.CasaEngineGame.Game.GameManager.ProjectSettings.FirstScreenName, "FirstScreenNameTest", "FirstScreenName");
            Assert.AreEqual(CasaEngine.Game.CasaEngineGame.Game.GameManager.ProjectSettings.IsFixedTimeStep, true, "IsFixedTimeStep");
            Assert.AreEqual(CasaEngine.Game.CasaEngineGame.Game.GameManager.ProjectSettings.IsMouseVisible, true, "IsMouseVisible");
            Assert.AreEqual(CasaEngine.Game.CasaEngineGame.Game.GameManager.ProjectSettings.ProjectName, "ProjectNameTest", "ProjectName");
            Assert.AreEqual(CasaEngine.Game.CasaEngineGame.Game.GameManager.ProjectSettings.WindowTitle, "WindowTitleTest", "WindowTitle");
        }
	}
}

#endif
