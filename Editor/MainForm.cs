using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using CasaEngine.Editor.Assets;
using Editor.Log;
using Editor.Sprite2DEditor.SpriteSheetPacker;
using Editor.WinForm.DocToolkit;
using WeifenLuo.WinFormsUI.Docking;
using Editor.World;
using Editor.Tools.Graphics2D;
using Editor.Tools.UIScreenEditor;
using Editor.Tools.Font;
using Editor.Tools.SkinUIEditor;
using Font = CasaEngine.Framework.Assets.Fonts.Font;
using CasaEngine.Core.Logger;
using CasaEngine.Framework;
using CasaEngine.Framework.Assets.Graphics2D;
using CasaEngine.Framework.FrontEnd.Screen;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.UserInterface.UI;
using CasaEngine.Framework.Project;

#if !DEBUG
using Editor.Debugger;
#endif

namespace Editor
{
    public partial class MainForm : Form
    {
        private MruManager m_MruManager;
        private const string m_RegistryPath = "Software\\Studio_XC\\CasaEngineEditor";

        private ProjectConfigForm m_ProjectConfigForm = null;
        private LogForm m_LogForm = null;
        private WorldEditorForm m_WorldEditorForm = null;
        private WorldObjectForm m_WorldObjectForm = null;

        private Sprite2DEditorForm m_Sprite2DEditorForm = null;
        private Animation2DEditorForm m_Animation2DEditorForm = null;

        public MainForm()
        {
            InitializeComponent();

            try
            {
                LogManager.Instance.AddLogger(new FileLogger(Environment.CurrentDirectory + "\\log-" + DateTime.Now.ToString("dd-MM-yy-HH-mm-ss") + ".txt"));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Add Logger error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            m_LogForm = new LogForm();
            m_LogForm.Show(dockPanel1, DockState.DockBottom);

            //ExternalTool.SaveProject = saveToolStripMenuItem_Click;

            //AnimationListEventForm.AddEventWindowFactory("PlaySound", new SoundEventWindowFactory());

            //SourceControlManager.Instance.Initialize(new P4SourceControl());

            m_MruManager = new MruManager();
            m_MruManager.Initialize(
                this,									// owner form
                toolStripMenuItemRecentProject,         // Recent Files menu item
                fileToolStripMenuItem,					// parent
                m_RegistryPath);						// Registry path to keep MRU list

            m_MruManager.MruOpenEvent += delegate (object sender_, MruFileOpenEventArgs e_)
            {
                LoadProject(e_.FileName);
            };

            SetTitle();

            Load += new EventHandler(MainForm_Load);
        }

        void MainForm_Load(object sender, EventArgs e)
        {
            m_WorldEditorForm = new WorldEditorForm();
            m_WorldEditorForm.Show(dockPanel1, DockState.Document);

            m_WorldObjectForm = new WorldObjectForm();
            m_WorldObjectForm.Show(dockPanel1, DockState.DockLeft);

            EnableComponent(false);

#if !UNITEST
            if (m_MruManager.GetFirstFileName != null)
            {
#if !DEBUG
                LoadProject(m_MruManager.GetFirstFileName);
#endif
            }
#endif
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveDialog = new SaveFileDialog();

            saveDialog.Title = "New Project";
            saveDialog.Filter = "Resources Files (*.xml)|*.xml|" +
                               "All Files (*.*)|*.*";

            if (saveDialog.ShowDialog(this) == DialogResult.OK)
            {
#if !DEBUG
                try
                {
#endif
                    CreateProject(saveDialog.FileName);
                    LogManager.Instance.WriteLine("New project successfully created");
#if !DEBUG
                }
                catch (Exception ex)
                {
                    LogManager.Instance.WriteException(ex);
                }
#endif
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog form = new OpenFileDialog();

            form.Title = "Open Project";
            form.Filter = "Project Files (*.xml)|*.xml|All Files (*.*)|*.*";

            if (form.ShowDialog(this) == DialogResult.OK)
            {
#if !DEBUG
                try
#endif
                {
                    LoadProject(form.FileName);
                }
#if !DEBUG
                catch (Exception ex)
                {
                    LogManager.Instance.WriteException(ex);
                }
#endif
            }

            form.Dispose();
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(CasaEngineGame.Game.GameManager.ProjectManager.ProjectFileOpened) == true)
            {
                saveAsToolStripMenuItem_Click(this, EventArgs.Empty);
            }
            else
            {
#if !DEBUG
                try
#endif
                {
                    SaveProject(CasaEngineGame.Game.GameManager.ProjectManager.ProjectFileOpened);
                }
#if !DEBUG
                catch (Exception ex)
                {
                    LogManager.Instance.WriteException(ex);
                }
#endif
            }
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveDialog = new SaveFileDialog();

            saveDialog.Title = "Save project";
            saveDialog.Filter = "Project Files (*.xml)|*.xml|" +
                               "All Files (*.*)|*.*";

            if (saveDialog.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    SaveProject(saveDialog.FileName);
                }
                catch (Exception ex)
                {
                    LogManager.Instance.WriteException(ex);
                }
            }
        }

        private void toolStripMenuItemCloseProject_Click(object sender, EventArgs e)
        {
            ClearProject();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckExternalTool()
        {
            /*this.pluginsToolStripMenuItem.DropDownItems.Clear();
            string[] names = GameInfo.Instance.ExternalToolManager.GetAllToolNames();
            int i = 0;

            foreach (string name in names)
            {
                ToolStripMenuItem toolStripItem = new ToolStripMenuItem();
                toolStripItem.Name = name;
                this.newToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
                toolStripItem.Text = name;
                toolStripItem.Click += new System.EventHandler(this.OnExternalToolItem_Click);
                this.pluginsToolStripMenuItem.DropDownItems.Add(toolStripItem);
                i++;
            }*/

            CasaEngineGame.Game.GameManager.ExternalToolManager.RegisterEditor(typeof(Sprite2D).FullName, typeof(Sprite2DEditorForm));
            CasaEngineGame.Game.GameManager.ExternalToolManager.RegisterEditor(typeof(Animation2D).FullName, typeof(Animation2DEditorForm));
            CasaEngineGame.Game.GameManager.ExternalToolManager.RegisterEditor(typeof(UiScreen).FullName, typeof(UIScreenEditorForm));
            CasaEngineGame.Game.GameManager.ExternalToolManager.RegisterEditor(typeof(Font).FullName, typeof(FontPreviewForm));
            CasaEngineGame.Game.GameManager.ExternalToolManager.RegisterEditor(typeof(SkinUi).FullName, typeof(SkinUIEditorForm));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnExternalToolItem_Click(object sender, EventArgs e)
        {
            /*
#if !DEBUG
            try
            {
#endif
            ToolStripMenuItem item = (ToolStripMenuItem)sender;
            GameInfo.Instance.ExternalToolManager.RunTool(this, item.Name);
#if !DEBUG
            }
            catch (Exception ex)
            {
                DebugHelper.ShowException(ex);
            }
#endif
            */
        }

        /// <summary>
        /// 
        /// </summary>
        private void SetTitle()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(asm.Location);

            Text = "Project Editor - " + CasaEngineGame.Game.GameManager.ProjectSettings.ProjectName + " - " + fvi.ProductVersion;

#if DEBUG
            Text += " - DEBUG";
#endif
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buildAssetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BuildAssetForm form = new BuildAssetForm();
            form.ShowDialog(this);
            form.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void debugToolStripMenuItem_Click(object sender, EventArgs e)
        {
            debugToolStripMenuItem.Checked = true;
            normalToolStripMenuItem.Checked = false;
            noneToolStripMenuItem.Checked = false;
            LogManager.Instance.Verbosity = LogManager.LogVerbosity.Debug;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void normalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            normalToolStripMenuItem.Checked = true;
            debugToolStripMenuItem.Checked = false;
            noneToolStripMenuItem.Checked = false;
            LogManager.Instance.Verbosity = LogManager.LogVerbosity.Normal;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void noneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            noneToolStripMenuItem.Checked = true;
            debugToolStripMenuItem.Checked = false;
            normalToolStripMenuItem.Checked = false;
            LogManager.Instance.Verbosity = LogManager.LogVerbosity.None;
        }

        /// <summary>
        /// 
        /// </summary>
        private void ClearProject()
        {
            SpriteSheetTaskListForm.Clear();
            CasaEngineGame.Game.GameManager.ProjectManager.Clear();
            EnableComponent(false);

            DisposeControl(m_ProjectConfigForm);
            DisposeControl(m_Sprite2DEditorForm);
            DisposeControl(m_Animation2DEditorForm);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="control_"></param>
        private void DisposeControl(Control control_)
        {
            if (control_ != null
                && control_.IsDisposed == false)
            {
                control_.Dispose();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName_"></param>
        private void CreateProject(string fileName_)
        {
#if !DEBUG
            try
            {
#endif
                ClearProject();
                CasaEngineGame.Game.GameManager.ProjectManager.CreateProject(fileName_, Path.GetDirectoryName(fileName_));
                CasaEngineGame.Game.GameManager.ProjectSettings.ProjectName = "New Project";
                OnProjectLoaded(fileName_);
#if !DEBUG
            }
            catch (System.Exception e)
            {

            }
#endif
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName_"></param>
        private void LoadProject(string fileName_)
        {
            if (File.Exists(fileName_) == false)
            {
                m_MruManager.Remove(fileName_);
                MessageBox.Show("The project file " + fileName_ + " doesn't exists!\n It will be remove from the recent files list.");
                return;
            }

#if !DEBUG
            try
            {
#endif
                ClearProject();
                CasaEngineGame.Game.GameManager.ProjectManager.Load(fileName_);
                CheckExternalTool();

                CasaEngineGame.Game.GameManager.AssetContentManager.RootDirectory = CasaEngineGame.Game.GameManager.ProjectManager.ProjectPath +
                                                                    Path.DirectorySeparatorChar + ProjectManager.AssetDirPath;

#if !UNITTEST
                //SourceControlManager.Instance.SourceControl.Connect();

                //if (SourceControlManager.Instance.SourceControl.IsValidConnection() == false)
                {
                    //Ask connection information
                    /*SourceControlConnectionForm form = new SourceControlConnectionForm();

                    if (form.ShowDialog(this) == DialogResult.OK)
                    {
                        SourceControlManager.Instance.Server = form.Server;
                        SourceControlManager.Instance.User = form.User;
                        SourceControlManager.Instance.Workspace = form.Workspace;
                        SourceControlManager.Instance.Password = form.Password;
                        SourceControlManager.Instance.SourceControl.Connect();

                        if (SourceControlManager.Instance.SourceControl.IsValidConnection() == true)
                        {
                            SourceControlManager.Instance.CheckProjectFiles();
                        }
                    }*/
                }
#endif

                OnProjectLoaded(fileName_);
#if !DEBUG
            }
            catch (System.Exception ex)
            {
                LogManager.Instance.WriteException(ex);
            }
#endif
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName_"></param>
        private void SaveProject(string fileName_)
        {
#if !DEBUG
            try
            {
#endif
                if (CasaEngineGame.Game.GameManager.ProjectManager.Save(fileName_) == true)
                {
                    LogManager.Instance.WriteLine("Project ",
                        "\"" + CasaEngineGame.Game.GameManager.ProjectSettings.ProjectName + "\"", Color.Blue,
                        " successfully saved.");
                }
                else
                {
                    LogManager.Instance.WriteLineError("Can't save the project.");
                }

#if !DEBUG
            }
            catch (System.Exception ex)
            {
                LogManager.Instance.WriteException(ex);
            }
#endif
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnProjectLoaded(string fileName_)
        {
            EnableComponent(true);
            m_MruManager.Add(fileName_);
            SetTitle();
            LogManager.Instance.WriteLine("Project ",
                "\"" + CasaEngineGame.Game.GameManager.ProjectSettings.ProjectName + "\" ", Color.Blue,
                "(", CasaEngineGame.Game.GameManager.ProjectManager.ProjectFileOpened, ")", " successfully loaded.");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="state_"></param>
        private void EnableComponent(bool state_)
        {
            buildAssetToolStripMenuItem.Enabled = state_;

            foreach (ToolStripItem item in toolStrip1.Items)
            {
                item.Enabled = state_;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonProjectConfig_Click(object sender, EventArgs e)
        {
            if (m_ProjectConfigForm == null
                || m_ProjectConfigForm.IsDisposed == true)
            {
#if !DEBUG
                try
                {
#endif
                    m_ProjectConfigForm = new ProjectConfigForm();
#if !DEBUG
                }
                catch (Exception ex)
                {
                    DebugHelper.ShowException(ex);
                }
#endif
            }
            else
            {
                m_ProjectConfigForm.Focus();
                return;
            }

            if (m_ProjectConfigForm != null)
            {
                m_ProjectConfigForm.Show();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButtonWorldProperties_Click(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButtonActionGraph_Click(object sender, EventArgs e)
        {
            //ActionGraphForm f = new ActionGraphForm();
            //f.Show(dockPanel1, DockState.Document);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonLaunchGame_Click(object sender, EventArgs e)
        {
            LaunchGame();
        }

        /// <summary>
        /// 
        /// </summary>
        private void LaunchGame()
        {
#if !DEBUG
            try
            {
#endif
                string projectFileOpened = CasaEngineGame.Game.GameManager.ProjectManager.ProjectFileOpened;

                string file = CasaEngineGame.Game.GameManager.ProjectManager.ProjectPath + Path.DirectorySeparatorChar;
                file += ProjectManager.GameDirPath + Path.DirectorySeparatorChar + Path.GetFileName(CasaEngineGame.Game.GameManager.ProjectManager.ProjectFileOpened);

                SaveProject(file); // temporary project file
                //CasaEngineGame.Game.GameManager.ProjectManager.ProjectFileOpened = projectFileOpened; // ProjectFileOpened will be change in SaveProject function

                ProcessStartInfo myInfo = new ProcessStartInfo();
                myInfo.FileName = FindGameExe(Path.GetDirectoryName(file));

                if (string.IsNullOrWhiteSpace(myInfo.FileName) == false)
                {
                    myInfo.WorkingDirectory = Path.GetDirectoryName(myInfo.FileName);
                    myInfo.Arguments = file + " " + CasaEngineGame.Game.GameManager.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.AssetDirPath;
                    Process.Start(myInfo);
                }
                else
                {
                    LogManager.Instance.WriteLineError("Game exe not found!");
                    MessageBox.Show(this, "Game exe not found!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
#if !DEBUG
            }
            catch (Exception ex)
            {
                DebugHelper.ShowException(ex);
            }
#endif
        }

        /// <summary>
        /// 
        /// </summary>
        /*private void CopyAssetFileInContentFolder(object sender, DoWorkEventArgs e)
        {
            FileInfo fi, fi2;
            bool ro;
            string destFile;

            string[] xnbs = Directory.GetFiles(GameInfo.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.XNBDirPath, "*.xnb");

            int percent = 0;
            BackgroundWorker bg = sender as BackgroundWorker;

            string destPath = GameInfo.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.GameDirPath + "\\Content\\";

            List<string> readOnlyFiles = new List<string>();

            foreach (string file in xnbs)
            {
                percent++;
                bg.ReportProgress(
                    (int)(((float)percent / (float)(xnbs.Length + 1)) * 100.0f),
                    "copying " + Path.GetFileName(file) + " ... (" + percent + "/" + (xnbs.Length + 1) + ")");

                fi = new FileInfo(file);
                ro = fi.IsReadOnly;
                destFile = destPath + Path.GetFileName(file);

                fi.IsReadOnly = false; //source control can put the file in readonly mode

                //avoid erase readonly file
                if (File.Exists(destFile) == true)
                {
                    fi2 = new FileInfo(destFile);
                    fi2.IsReadOnly = false;
                    fi2.Delete();
                }

                File.Copy(file, destFile, true);
                fi.IsReadOnly = ro;
            }

            //project File
            destFile = GameInfo.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.GameDirPath + "\\Content\\" + Path.GetFileName(GameInfo.Instance.ProjectManager.ProjectFileOpened);
            fi = new FileInfo(GameInfo.Instance.ProjectManager.ProjectFileOpened);
            ro = fi.IsReadOnly;
            fi.IsReadOnly = false;

            fi2 = new FileInfo(destFile);
            if (fi2.Exists == true)
            {
                fi2.IsReadOnly = false;
                fi2.Delete();
            }

            File.Copy(GameInfo.Instance.ProjectManager.ProjectFileOpened,
                destFile,
                true);

            fi.IsReadOnly = ro;

            //percent++;
            //bg.ReportProgress(
            //    (int)(((float)percent / (float)(xnbs.Length + 1)) * 100.0f),
            //    "copying files ... (" + percent + "/" + (xnbs.Length + 1) + ")");

            e.Result = true;
        }*/

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BuildAssetFileInContentFolder(object sender, DoWorkEventArgs e)
        {
            FileInfo fi, fi2;
            bool ro;
            string destFile;

            int percent = 0;
            BackgroundWorker bg = sender as BackgroundWorker;

            string xnbPath = CasaEngineGame.Game.GameManager.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.GameDirPath + "\\Content\\";

            /*
             * AssetBuildParam doit implementer IObservable
             * pour modifier BuildParamChanged
             */
            foreach (AssetInfo info in CasaEngineGame.Game.GameManager.AssetManager.Assets)
            {
                bg.ReportProgress(
                    (int)(((float)percent / (float)(CasaEngineGame.Game.GameManager.AssetManager.Assets.Length + 1)) * 100.0f),
                    "building " + info.Name + " ... (" + percent + "/" + (CasaEngineGame.Game.GameManager.AssetManager.Assets.Length + 1) + ")");

                fi = new FileInfo(info.FileName);
                if (File.Exists(xnbPath + info.Name + ".xnb") == false)
                {
                    CasaEngineGame.Game.GameManager.AssetManager.RebuildAsset(info);
                }

                percent++;
            }

            //project File
            destFile = CasaEngineGame.Game.GameManager.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.GameDirPath + "\\Content\\" + Path.GetFileName(CasaEngineGame.Game.GameManager.ProjectManager.ProjectFileOpened);
            fi = new FileInfo(CasaEngineGame.Game.GameManager.ProjectManager.ProjectFileOpened);
            ro = fi.IsReadOnly;
            fi.IsReadOnly = false;

            fi2 = new FileInfo(destFile);
            if (fi2.Exists == true)
            {
                fi2.IsReadOnly = false;
                fi2.Delete();
            }

            File.Copy(CasaEngineGame.Game.GameManager.ProjectManager.ProjectFileOpened,
                destFile,
                true);

            fi.IsReadOnly = ro;

            /*percent++;
            bg.ReportProgress(
                (int)(((float)percent / (float)(xnbs.Length + 1)) * 100.0f),
                "copying files ... (" + percent + "/" + (xnbs.Length + 1) + ")");*/

            e.Result = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path_"></param>
        /// <returns></returns>
        private string FindGameExe(string path_)
        {
            string[] exes = Directory.GetFiles(path_, "*.exe");

            if (exes.Length > 0)
            {
                return exes[0];
            }

            return string.Empty;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            ClearProject();
            Dispose(true);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj_"></param>
        /*private void LaunchSprite2DEditor(string objectPath_, Entity obj_)
        {
            if (obj_ is Sprite2D == false)
            {
                return;
            }

            Sprite2D sprite2D = obj_ as Sprite2D;

            if (m_Sprite2DEditorForm == null
                || m_Sprite2DEditorForm.IsDisposed == true)
            {
#if !DEBUG
                try
                {
#endif
                m_Sprite2DEditorForm = new Tools.Graphics2D.Sprite2DEditorForm();
#if !DEBUG
                }
                catch (Exception ex)
                {
                    DebugHelper.ShowException(ex);
                }
#endif
            }
            else
            {
                m_Sprite2DEditorForm.Focus();
                m_Sprite2DEditorForm.SetCurrentSprite2D(objectPath_, sprite2D);
                return;
            }

            if (m_Sprite2DEditorForm != null)
            {
                //m_Sprite2DEditorForm.SaveProject = saveToolStripMenuItem_Click;
                m_Sprite2DEditorForm.Show();
                m_Sprite2DEditorForm.SetCurrentSprite2D(objectPath_, sprite2D);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="package_"></param>
        private void LaunchAnimation2DEditor(string objectPath_, Entity obj_)
        {
            if (obj_ is Animation2D == false)
            {
                return;
            }

            Animation2D anim2D = obj_ as Animation2D;

            if (m_Animation2DEditorForm == null
                || m_Animation2DEditorForm.IsDisposed == true)
            {
#if !DEBUG
                try
                {
#endif
                m_Animation2DEditorForm = new Tools.Graphics2D.Animation2DEditorForm();
#if !DEBUG
                }
                catch (Exception ex)
                {
                    DebugHelper.ShowException(ex);
                }
#endif
            }
            else
            {
                m_Animation2DEditorForm.Focus();
                m_Animation2DEditorForm.SetCurrentAnimation2D(objectPath_, anim2D);
                return;
            }

            if (m_Animation2DEditorForm != null)
            {
                //m_Sprite2DEditorForm.SaveProject = saveToolStripMenuItem_Click;
                m_Animation2DEditorForm.Show();
                m_Animation2DEditorForm.SetCurrentAnimation2D(objectPath_, anim2D);
            }
        }*/

    }
}
