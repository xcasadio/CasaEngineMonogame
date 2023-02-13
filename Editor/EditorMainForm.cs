using System.ComponentModel;
using CasaEngine.Game;
using CasaEngineCommon.Logger;
using Editor.WinForm.DocToolkit;
using System.Diagnostics;
using Editor.WinForm;
using System.Reflection;
using Editor.SoundEditor;
using Editor.SourceControl;
using CasaEngine.Editor.Assets;
using CasaEngine.Editor.SourceControl;
using Editor.Map;
using Editor.Sprite2DEditor.SpriteSheetPacker;
using CasaEngine.Project;
using Editor.Tools.UIScreenEditor;

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
            if (string.IsNullOrEmpty(Engine.Instance.ProjectManager.ProjectFileOpened) == true)
            {
                saveAsToolStripMenuItem_Click(this, EventArgs.Empty);
            }
            else
            {
#if !DEBUG
                try
#endif
                {
                    SaveProject(Engine.Instance.ProjectManager.ProjectFileOpened);
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
            Engine.Instance.ExternalToolManager.Clear();
            Engine.Instance.ProjectManager.Clear();

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
            Engine.Instance.ProjectManager.CreateProject(fileName_);
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
            SourceControlManager.Instance.Initialize(new P4SourceControl());
            Engine.Instance.ProjectManager.Load(fileName_);
            CheckExternalTool();
            SourceControlManager.Instance.LoadConfig(Path.GetDirectoryName(fileName_) + Path.DirectorySeparatorChar + ProjectManager.ConfigDirPath);

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
            if (Engine.Instance.ProjectManager.Save(fileName_) == true)
            {
                LogManager.Instance.WriteLine("Project ",
                    "\"" + Engine.Instance.ProjectConfig.ProjectName + "\"", Color.Blue,
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
                "\"" + Engine.Instance.ProjectConfig.ProjectName + "\" ", Color.Blue,
                "(", Engine.Instance.ProjectManager.ProjectFileOpened, ")", " successfully loaded.");
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
                myInfo.Arguments = Path.GetFileName(Path.GetFileName(Engine.Instance.ProjectManager.ProjectFileOpened));
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

            string xnbPath = Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.GameDirPath + "\\Content\\";

            /*
             * AssetBuildParam doit implementer IObservable
             * pour modifier BuildParamChanged
             */
            foreach (AssetInfo info in Engine.Instance.AssetManager.Assets)
            {
                bg.ReportProgress(
                    (int)(((float)percent / (float)(Engine.Instance.AssetManager.Assets.Length + 1)) * 100.0f),
                    "building " + info.Name + " ... (" + percent + "/" + (Engine.Instance.AssetManager.Assets.Length + 1) + ")");

                fi = new FileInfo(info.FileName);
                if (File.Exists(xnbPath + info.Name + ".xnb") == false
                    || Engine.Instance.AssetManager.AssetNeedToBeRebuild(info) == true)
                {
                    Engine.Instance.AssetManager.RebuildAsset(info);
                }

                percent++;
            }

            Engine.Instance.AssetManager.SaveAssetBuildInfo();

            //project File
            destFile = Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.GameDirPath + "\\Content\\" + Path.GetFileName(Engine.Instance.ProjectManager.ProjectFileOpened);
            fi = new FileInfo(Engine.Instance.ProjectManager.ProjectFileOpened);
            ro = fi.IsReadOnly;
            fi.IsReadOnly = false;

            fi2 = new FileInfo(destFile);
            if (fi2.Exists == true)
            {
                fi2.IsReadOnly = false;
                fi2.Delete();
            }

            File.Copy(Engine.Instance.ProjectManager.ProjectFileOpened,
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
            string[] exes = Directory.GetFiles(Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.GameDirPath, "*.exe");

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

            Text = "Project Editor - " + Engine.Instance.ProjectConfig.ProjectName + " - " + fvi.ProductVersion;

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
                m_GameDocForm = new InputRtfForm(Engine.Instance.ProjectConfig.ProjectName + " Documentation", FindGameDocumentationFile());
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
            string[] rtfs = Directory.GetFiles(Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.GameDirPath, "*.rtf");

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
            SourceControlManager.Instance.CheckProjectFiles();
            Dictionary<string, Dictionary<SourceControlKeyWord, string>> dic = SourceControlManager.Instance.FilesStatus;

            TreeNode root = new TreeNode(Engine.Instance.ProjectManager.ProjectPath);
            TreeNode node = root;
            node.ImageIndex = 27;
            treeViewFiles.Nodes.Clear();

            string projectPath = Engine.Instance.ProjectManager.ProjectPath;

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
                    node.ImageIndex = (int)SourceControlIcon.FolderWorkspace;
                    node.SelectedImageIndex = (int)SourceControlIcon.FolderWorkspace;
                    node = AddNode(node, pathBits);
                }

                node.ImageIndex = (int)SourceControlIcon.FileInWSButNotInDepot;

                foreach (KeyValuePair<string, Dictionary<SourceControlKeyWord, string>> pair in dic)
                {
                    if (pair.Key.Contains(file) == true)
                    {
                        try
                        {
                            if (pair.Value.ContainsKey(SourceControlKeyWord.HaveRev) == false)
                            {
                                if (pair.Value.ContainsKey(SourceControlKeyWord.HeadAction) == true)
                                {
                                    if (pair.Value[SourceControlKeyWord.HeadAction].Equals("delete") == true)
                                    {
                                        node.ImageIndex = (int)SourceControlIcon.FileDeleted;
                                        node.SelectedImageIndex = (int)SourceControlIcon.FileDeleted;
                                    }
                                    else if (pair.Value[SourceControlKeyWord.HeadAction].Equals("move/delete") == true)
                                    {
                                        node.ImageIndex = (int)SourceControlIcon.FileMoved;
                                        node.SelectedImageIndex = (int)SourceControlIcon.FileMoved;
                                    }
                                }
                                else if (pair.Value.ContainsKey(SourceControlKeyWord.Action) == true)
                                {
                                    if (pair.Value[SourceControlKeyWord.Action].Equals("add") == true)
                                    {
                                        node.ImageIndex = (int)SourceControlIcon.FileAdd;
                                        node.SelectedImageIndex = (int)SourceControlIcon.FileAdd;
                                    }
                                }
                            }
                            else if (pair.Value[SourceControlKeyWord.HaveRev].Equals(pair.Value[SourceControlKeyWord.HeadRev]) == true)
                            {
                                if (pair.Value.ContainsKey(SourceControlKeyWord.Action) == true)
                                {
                                    if ("edit".Equals(pair.Value[SourceControlKeyWord.Action]) == true)
                                    {
                                        if (SourceControlManager.Instance.User.Equals(pair.Value[SourceControlKeyWord.ActionOwner]) == true)
                                        {
                                            node.ImageIndex = (int)SourceControlIcon.FileEditHead;
                                            node.SelectedImageIndex = (int)SourceControlIcon.FileEditHead;
                                        }
                                        else
                                        {
                                            node.ImageIndex = (int)SourceControlIcon.FileEditOther;
                                            node.SelectedImageIndex = (int)SourceControlIcon.FileEditOther;
                                        }
                                    }
                                }
                                else
                                {
                                    node.ImageIndex = (int)SourceControlIcon.FileSync;
                                    node.SelectedImageIndex = (int)SourceControlIcon.FileSync;
                                }

                                node.Text += " #" + pair.Value[SourceControlKeyWord.HaveRev] + "/" + pair.Value[SourceControlKeyWord.HeadRev];
                                node.ToolTipText = "test";
                            }
                            else
                            {
                                node.ImageIndex = (int)SourceControlIcon.FileNotSync;
                                node.SelectedImageIndex = (int)SourceControlIcon.FileNotSync;

                                node.Text += " #" + pair.Value[SourceControlKeyWord.HaveRev] + "/" + pair.Value[SourceControlKeyWord.HeadRev];
                                node.ToolTipText = "test";
                            }
                        }
                        catch (Exception)
                        {
                            node.ImageIndex = (int)SourceControlIcon.FileNotSync;
                            node.SelectedImageIndex = (int)SourceControlIcon.FileNotSync;
                        }

                        break;
                    }
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
        private void toolStripMenuItemSourceControlOption_Click(object sender, EventArgs e)
        {
            SourceControlConnectionForm form = new SourceControlConnectionForm();

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                SourceControlManager.Instance.Server = form.Server;
                SourceControlManager.Instance.User = form.User;
                SourceControlManager.Instance.Workspace = form.Workspace;
                SourceControlManager.Instance.Password = form.Password;
                SourceControlManager.Instance.SaveConfig(Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.ConfigDirPath);
                SourceControlManager.Instance.SourceControl.Connect();
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
