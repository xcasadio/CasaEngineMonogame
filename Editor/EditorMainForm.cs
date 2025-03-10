﻿using System.ComponentModel;
using Editor.WinForm.DocToolkit;
using System.Diagnostics;
using Editor.WinForm;
using System.Reflection;
using Editor.SoundEditor;
using CasaEngine.Editor.Assets;
using Editor.Map;
using Editor.Sprite2DEditor.SpriteSheetPacker;
using Editor.Tools.UIScreenEditor;
using CasaEngine.Core.Logger;
using CasaEngine.Framework;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Project;
using Editor.Debugger;

namespace Editor
{
    /// <summary>
    /// 
    /// </summary>
    public partial class EditorMainForm
        : Form
    {
        private MruManager m_MruManager;
        private const string m_RegistryPath = "Software\\Studio_XC\\CasaEngine2DEditor";

        //private Sprite2DEditor.Sprite2DEditorForm m_Sprite2DEditorForm = null;
        private ProjectConfigForm m_ProjectConfigForm = null;
        private SoundEditorForm m_SoundEditorForm = null;
        private MapEditorForm m_MapEditorForm = null;
        private UIScreenEditorForm m_ScreenEditorForm = null;

        private InputRtfForm m_GameDocForm = null;


        /// <summary>
        /// 
        /// </summary>
        public EditorMainForm()
        {
            InitializeComponent();
            EnableComponent(false);

            //ExternalTool.SaveProject = saveToolStripMenuItem_Click;

            try
            {
                LogManager.Instance.AddLogger(new FileLogger(Environment.CurrentDirectory + "\\log-" + DateTime.Now.ToString("dd-MM-yy-HH-mm-ss") + ".txt"));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Add Logger error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

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

#if !UNITEST
            if (m_MruManager.GetFirstFileName != null)
            {
                LoadProject(m_MruManager.GetFirstFileName);
            }
#endif
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
        private void buttonSprite2DEditor_Click(object sender, EventArgs e)
        {
            /*if (m_Sprite2DEditorForm == null
                || m_Sprite2DEditorForm.IsDisposed == true)
            {
 #if !DEBUG
                try
                {
#endif
                    m_Sprite2DEditorForm = new Editor.Sprite2DEditor.Sprite2DEditorForm();
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
                return;
            }

            if (m_Sprite2DEditorForm != null)
            {
                m_Sprite2DEditorForm.SaveProject = saveToolStripMenuItem_Click;
                m_Sprite2DEditorForm.Show();
            }*/
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonSoundEditor_Click(object sender, EventArgs e)
        {
            if (m_SoundEditorForm == null
                || m_SoundEditorForm.IsDisposed == true)
            {
#if !DEBUG
                try
                {
#endif
                    m_SoundEditorForm = new SoundEditorForm();
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
                m_SoundEditorForm.Focus();
                return;
            }

            if (m_SoundEditorForm != null)
            {
                m_SoundEditorForm.Show();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveDialog = new SaveFileDialog();

            saveDialog.Title = "New project";
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog form = new OpenFileDialog();

            form.Title = "Open Project";
            form.Filter = "Project Files (*.xml)|*.xml|" +
                               "All Files (*.*)|*.*";

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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void saveToolStripMenuItem_Click(object sender, EventArgs e)
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
        private void ClearProject()
        {
            SpriteSheetTaskListForm.Clear();
            CasaEngineGame.Game.GameManager.ExternalToolManager.Clear();
            CasaEngineGame.Game.GameManager.ProjectManager.Clear();

            if (m_ProjectConfigForm != null
                && m_ProjectConfigForm.IsDisposed == false)
            {
                m_ProjectConfigForm.Dispose();
                m_ProjectConfigForm = null;
            }

            /*if (m_Sprite2DEditorForm != null
                && m_Sprite2DEditorForm.IsDisposed == false)
            {
                m_Sprite2DEditorForm.Dispose();
                m_Sprite2DEditorForm = null;
            }*/

            if (m_SoundEditorForm != null
                && m_SoundEditorForm.IsDisposed == false)
            {
                m_SoundEditorForm.Dispose();
                m_SoundEditorForm = null;
            }

            if (m_GameDocForm != null
                && m_GameDocForm.IsDisposed == false)
            {
                m_GameDocForm.Dispose();
                m_GameDocForm = null;
            }

            if (m_MapEditorForm != null
                && m_MapEditorForm.IsDisposed == false)
            {
                m_MapEditorForm.Dispose();
                m_MapEditorForm = null;
            }

            if (m_ScreenEditorForm != null
                && m_ScreenEditorForm.IsDisposed == false)
            {
                m_ScreenEditorForm.Dispose();
                m_ScreenEditorForm = null;
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
                //MessageBox.Show("The file " + fileName_ + " doesn't exists!");
                return;
            }

#if !DEBUG
            try
            {
#endif
                ClearProject();
                CasaEngineGame.Game.GameManager.ProjectManager.Load(fileName_);
                CheckExternalTool();

#if !UNITTEST
                /*SourceControlManager.Instance.SourceControl.Connect();

                if (SourceControlManager.Instance.SourceControl.IsValidConnection() == false)
                {
                    //Ask connection information
                    SourceControlConnectionForm form = new SourceControlConnectionForm();

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
                    }
                }*/
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
        /// <param name="state_"></param>
        private void EnableComponent(bool state_)
        {
            buttonProjectConfig.Enabled = state_;
            buttonSprite2DEditor.Enabled = state_;
            buttonSoundEditor.Enabled = state_;
            buttonScreenEditor.Enabled = state_;
            buttonParticleEditor.Enabled = state_;
            buttonLaunchGame.Enabled = state_;
            buttonGameDocumentation.Enabled = state_;
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
                saveToolStripMenuItem_Click(this, EventArgs.Empty);

                BgWorkerForm form = new BgWorkerForm(BuildAssetFileInContentFolder);
                //BgWorkerForm form = new BgWorkerForm(CopyAssetFileInContentFolder);
                form.Text = "Copying ressource files";
                form.ShowDialog(this);

                bool res = form.Result == null ? false :
                    form.Result.Error != null ? false :
                    form.Result.Result is bool ? (bool)form.Result.Result : false;

                if (res == false)
                {
                    if (form.Result != null) //else : user cancel the operation : no message
                    {
                        if (form.Result.Error != null)
                        {
                            LogManager.Instance.WriteException(form.Result.Error);
                            MessageBox.Show(this, form.Result.Error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        else
                        {
                            LogManager.Instance.WriteLineError("Can't copy ressource files : unknown error!");
                            MessageBox.Show(this, "Can't copy ressource files : unknown error!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }

                    return;
                }

                form.Dispose();

                ProcessStartInfo myInfo = new ProcessStartInfo();
                myInfo.FileName = FindGameExe();

                if (string.IsNullOrWhiteSpace(myInfo.FileName) == false)
                {
                    myInfo.WorkingDirectory = Path.GetDirectoryName(myInfo.FileName);
                    myInfo.Arguments = Path.GetFileName(Path.GetFileName(CasaEngineGame.Game.GameManager.ProjectManager.ProjectFileOpened));
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
        private string FindGameExe()
        {
            string[] exes = Directory.GetFiles(CasaEngineGame.Game.GameManager.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.GameDirPath, "*.exe");

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
        private void CheckExternalTool()
        {
            groupBox2.Controls.Clear();

            /*string[] names = GameInfo.Instance.ExternalToolManager.GetAllToolNames();
            int i = 0;

            foreach (string name in names)
            {
                Button button = new Button();

                button.Location = new System.Drawing.Point(6, 19 + i * 31);
                button.Name = name;
                button.Size = new System.Drawing.Size(192, 23);
                button.TabIndex = 1;
                button.Text = name;
                button.UseVisualStyleBackColor = true;
                button.Click += new System.EventHandler(this.OnExternalToolItem_Click);

                this.groupBox2.Controls.Add(button);

                i++;
            }*/
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnExternalToolItem_Click(object sender, EventArgs e)
        {
#if !DEBUG
            try
            {
#endif
                //Button button = (Button)sender;
                //GameInfo.Instance.ExternalToolManager.RunTool(this, button.Name);
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
        private void documentationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //DisplayDocumentation("Project Editor documentation", "Editor_doc.rtf");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void aboutEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutForm form = new AboutForm();
            form.ShowDialog(this);
            form.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonGameDocumentation_Click(object sender, EventArgs e)
        {
            if (m_GameDocForm == null
                || m_GameDocForm.IsDisposed == true)
            {
                m_GameDocForm = new InputRtfForm(CasaEngineGame.Game.GameManager.ProjectSettings.ProjectName + " Documentation", FindGameDocumentationFile());
            }
            else
            {
                m_GameDocForm.Focus();
                return;
            }

            m_GameDocForm.Show();
        }

        /// <summary>
        /// 
        /// </summary>
        private string FindGameDocumentationFile()
        {
            string[] rtfs = Directory.GetFiles(CasaEngineGame.Game.GameManager.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.GameDirPath, "*.rtf");

            if (rtfs.Length > 0)
            {
                return rtfs[0];
            }

            return string.Empty;
        }

        private void debugToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            debugToolStripMenuItem.Checked = true;
            normalToolStripMenuItem.Checked = false;
            noneToolStripMenuItem.Checked = false;
            LogManager.Instance.Verbosity = LogManager.LogVerbosity.Debug;
        }

        private void normalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            normalToolStripMenuItem.Checked = true;
            debugToolStripMenuItem.Checked = false;
            noneToolStripMenuItem.Checked = false;
            LogManager.Instance.Verbosity = LogManager.LogVerbosity.Normal;
        }

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
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonRefresh_Click(object sender, EventArgs e)
        {
            TreeNode root = new TreeNode(CasaEngineGame.Game.GameManager.ProjectManager.ProjectPath);
            TreeNode node = root;
            node.ImageIndex = 27;
            treeViewFiles.Nodes.Clear();

            string projectPath = CasaEngineGame.Game.GameManager.ProjectManager.ProjectPath;

            foreach (string file in Directory.GetFiles(projectPath, "*.*", SearchOption.AllDirectories))
            {
                node = root;

                string filePath = file.Replace(projectPath, string.Empty);

                filePath = filePath.Replace("\\", "/");

                while (filePath.StartsWith("/") == true)
                {
                    filePath = filePath.Remove(0, 1);
                }

                foreach (string pathBits in filePath.Split('/'))
                {
                    node = AddNode(node, pathBits);
                }
            }

            treeViewFiles.Nodes.Add(root);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="node"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        private TreeNode AddNode(TreeNode node, string key)
        {
            if (node.Nodes.ContainsKey(key))
            {
                return node.Nodes[key];
            }
            else
            {
                return node.Nodes.Add(key, key);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonSubmit_Click(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonMapEditor_Click(object sender, EventArgs e)
        {
            if (m_MapEditorForm == null
                || m_MapEditorForm.IsDisposed == true)
            {
#if !DEBUG
                try
                {
#endif

                    m_MapEditorForm = new MapEditorForm();
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
                m_MapEditorForm.Focus();
                return;
            }

            if (m_MapEditorForm != null)
            {
                m_MapEditorForm.Show();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonScreenEditor_Click(object sender, EventArgs e)
        {
            if (m_ScreenEditorForm == null
                || m_ScreenEditorForm.IsDisposed == true)
            {
#if !DEBUG
                try
                {
#endif
                    m_ScreenEditorForm = new UIScreenEditorForm();
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
                m_ScreenEditorForm.Focus();
                return;
            }

            if (m_ScreenEditorForm != null)
            {
                m_ScreenEditorForm.Show();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EditorMainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            //CloseForm(m_Sprite2DEditorForm);
            CloseForm(m_ProjectConfigForm);
            CloseForm(m_SoundEditorForm);
            CloseForm(m_MapEditorForm);
            CloseForm(m_ScreenEditorForm);
            CloseForm(m_GameDocForm);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="form_"></param>
        private void CloseForm(Form form_)
        {
            if (form_ != null
                && form_.IsDisposed == false)
            {
                form_.Dispose();
            }
        }
    }
}
