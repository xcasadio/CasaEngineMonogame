#if UNITTEST


using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.IO;
using CasaEngineCommon.Logger;

namespace Editor
{
    [TestFixture]
	public partial class EditorMainForm
    {
        string m_ProjectPathToDelete;


        /// <summary>
        /// 
        /// </summary>
        [SetUp]
        public void CopyProjectUnitTest()
        {
            m_ProjectPathToDelete = Path.Combine(Environment.CurrentDirectory, "UnitTest");
            DirectoryInfo dInfo = Directory.CreateDirectory(m_ProjectPathToDelete);

            if (dInfo.Exists == false)
            {
                throw new InvalidOperationException("Unit test initialization failed !!");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        [TearDown]
        public void ClearProjectUnitTestCopy()
        {
            ClearProject();
            LogManager.Instance.Close();
            if (Directory.Exists(m_ProjectPathToDelete) == true)
            {
                Directory.Delete(m_ProjectPathToDelete, true);
            }
        }


        [Test]
        public void CreateNewProjectAndSave()
        {
            string projectFileName = Path.Combine(m_ProjectPathToDelete, "TestProject.xml");
            CreateProject(projectFileName);
            SaveProject(projectFileName);
        }

        [Test]
        public void OpenOldVersion()
        {

        }

        [Test]
        public void OpenAndEditProjectConfig()
        {
            string projectFileName = Path.Combine(m_ProjectPathToDelete, "TestProject.xml");
            //edit
            CreateProject(projectFileName);
            buttonProjectConfig_Click(this, EventArgs.Empty);
            m_ProjectConfigForm.LaunchUnitTest();
            SaveProject(projectFileName);
            //check changes
            LoadProject(projectFileName);
            buttonProjectConfig_Click(this, EventArgs.Empty);
            m_ProjectConfigForm.CheckUnitTest();
        }

        [Test]
        public void OpenAndEditSprite2DEditor()
        {

        }
	}
}

#endif

