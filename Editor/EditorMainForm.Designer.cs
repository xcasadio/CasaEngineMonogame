namespace Editor
{
    partial class EditorMainForm
    {
        /// <summary>
        /// Variable nécessaire au concepteur.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Nettoyage des ressources utilisées.
        /// </summary>
        /// <param name="disposing">true si les ressources managées doivent être supprimées ; sinon, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }


        /// <summary>
        /// Méthode requise pour la prise en charge du concepteur - ne modifiez pas
        /// le contenu de cette méthode avec l'éditeur de code.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditorMainForm));
            System.Windows.Forms.TreeNode treeNode1 = new System.Windows.Forms.TreeNode("default");
            this.imageListWorkspace = new System.Windows.Forms.ImageList(this.components);
            this.splitContainerMain = new System.Windows.Forms.SplitContainer();
            this.buttonGameDocumentation = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.buttonMapEditor = new System.Windows.Forms.Button();
            this.buttonSoundEditor = new System.Windows.Forms.Button();
            this.buttonScreenEditor = new System.Windows.Forms.Button();
            this.buttonParticleEditor = new System.Windows.Forms.Button();
            this.buttonProjectConfig = new System.Windows.Forms.Button();
            this.buttonSprite2DEditor = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.buttonLaunchGame = new System.Windows.Forms.Button();
            this.tabControlMain = new System.Windows.Forms.TabControl();
            this.tabPageSourceControl = new System.Windows.Forms.TabPage();
            this.tabControlSourceControl = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.label2 = new System.Windows.Forms.Label();
            this.treeViewFiles = new System.Windows.Forms.TreeView();
            this.buttonRefresh = new System.Windows.Forms.Button();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.buttonRefreshChangeList = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.treeViewChangeList = new System.Windows.Forms.TreeView();
            this.buttonSubmit = new System.Windows.Forms.Button();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.newToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveAsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripMenuItemRecentProject = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.windowToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator6 = new System.Windows.Forms.ToolStripSeparator();
            this.buildAssetToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator5 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripMenuItemSourceControlOption = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            this.debugVerbosityToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.debugToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.normalToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.noneToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.documentationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.aboutEditorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).BeginInit();
            this.splitContainerMain.Panel1.SuspendLayout();
            this.splitContainerMain.Panel2.SuspendLayout();
            this.splitContainerMain.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.tabControlMain.SuspendLayout();
            this.tabPageSourceControl.SuspendLayout();
            this.tabControlSourceControl.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // imageListWorkspace
            // 
            this.imageListWorkspace.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageListWorkspace.ImageStream")));
            this.imageListWorkspace.TransparentColor = System.Drawing.Color.Transparent;
            this.imageListWorkspace.Images.SetKeyName(0, "p4v_folderlocal.png");
            this.imageListWorkspace.Images.SetKeyName(1, "p4v_file_add.png");
            this.imageListWorkspace.Images.SetKeyName(2, "p4v_file_add_other_ws.png");
            this.imageListWorkspace.Images.SetKeyName(3, "p4v_file_branch.png");
            this.imageListWorkspace.Images.SetKeyName(4, "p4v_file_delete.png");
            this.imageListWorkspace.Images.SetKeyName(5, "p4v_file_delete_other.png");
            this.imageListWorkspace.Images.SetKeyName(6, "p4v_file_deleted.png");
            this.imageListWorkspace.Images.SetKeyName(7, "p4v_file_differs.png");
            this.imageListWorkspace.Images.SetKeyName(8, "p4v_file_edit_head.png");
            this.imageListWorkspace.Images.SetKeyName(9, "p4v_file_edit_other.png");
            this.imageListWorkspace.Images.SetKeyName(10, "p4v_file_integ.png");
            this.imageListWorkspace.Images.SetKeyName(11, "p4v_file_lock.png");
            this.imageListWorkspace.Images.SetKeyName(12, "p4v_file_lock_other.png");
            this.imageListWorkspace.Images.SetKeyName(13, "p4v_file_needs_resolve.png");
            this.imageListWorkspace.Images.SetKeyName(14, "p4v_file_notmapped.png");
            this.imageListWorkspace.Images.SetKeyName(15, "p4v_file_notsync.png");
            this.imageListWorkspace.Images.SetKeyName(16, "p4v_file_sync.png");
            this.imageListWorkspace.Images.SetKeyName(17, "p4v_file_txt.png");
            this.imageListWorkspace.Images.SetKeyName(18, "p4v_file_ws.png");
            this.imageListWorkspace.Images.SetKeyName(19, "p4v_move.png");
            this.imageListWorkspace.Images.SetKeyName(20, "p4v_move_target.png");
            this.imageListWorkspace.Images.SetKeyName(21, "p4v_symlink.png");
            this.imageListWorkspace.Images.SetKeyName(22, "p4v_pending_resolve_icon.png");
            this.imageListWorkspace.Images.SetKeyName(23, "p4v_remotedepot.png");
            this.imageListWorkspace.Images.SetKeyName(24, "shelved-file.png");
            this.imageListWorkspace.Images.SetKeyName(25, "specdepot.png");
            this.imageListWorkspace.Images.SetKeyName(26, "stream_depot_icon.png");
            this.imageListWorkspace.Images.SetKeyName(27, "p4v_folderworkspace.png");
            this.imageListWorkspace.Images.SetKeyName(28, "p4v_pending_shelved_icon.png");
            this.imageListWorkspace.Images.SetKeyName(29, "p4v_file_import.png");
            this.imageListWorkspace.Images.SetKeyName(30, "p4v_folderdepot.png");
            // 
            // splitContainerMain
            // 
            this.splitContainerMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerMain.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainerMain.IsSplitterFixed = true;
            this.splitContainerMain.Location = new System.Drawing.Point(0, 24);
            this.splitContainerMain.Name = "splitContainerMain";
            // 
            // splitContainerMain.Panel1
            // 
            this.splitContainerMain.Panel1.Controls.Add(this.buttonGameDocumentation);
            this.splitContainerMain.Panel1.Controls.Add(this.groupBox1);
            this.splitContainerMain.Panel1.Controls.Add(this.groupBox2);
            this.splitContainerMain.Panel1.Controls.Add(this.buttonLaunchGame);
            // 
            // splitContainerMain.Panel2
            // 
            this.splitContainerMain.Panel2.Controls.Add(this.tabControlMain);
            this.splitContainerMain.Size = new System.Drawing.Size(593, 435);
            this.splitContainerMain.SplitterDistance = 216;
            this.splitContainerMain.TabIndex = 7;
            // 
            // buttonGameDocumentation
            // 
            this.buttonGameDocumentation.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonGameDocumentation.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            //this.buttonGameDocumentation.Image = global::Editor.Properties.Resources.chm_icon;
            this.buttonGameDocumentation.Location = new System.Drawing.Point(12, 405);
            this.buttonGameDocumentation.Name = "buttonGameDocumentation";
            this.buttonGameDocumentation.Size = new System.Drawing.Size(23, 23);
            this.buttonGameDocumentation.TabIndex = 6;
            this.buttonGameDocumentation.TextImageRelation = System.Windows.Forms.TextImageRelation.TextBeforeImage;
            this.buttonGameDocumentation.UseVisualStyleBackColor = true;
            this.buttonGameDocumentation.Click += new System.EventHandler(this.buttonGameDocumentation_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.buttonMapEditor);
            this.groupBox1.Controls.Add(this.buttonSoundEditor);
            this.groupBox1.Controls.Add(this.buttonScreenEditor);
            this.groupBox1.Controls.Add(this.buttonParticleEditor);
            this.groupBox1.Controls.Add(this.buttonProjectConfig);
            this.groupBox1.Controls.Add(this.buttonSprite2DEditor);
            this.groupBox1.Location = new System.Drawing.Point(3, 3);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(204, 205);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Internals Tools";
            // 
            // buttonMapEditor
            // 
            this.buttonMapEditor.Location = new System.Drawing.Point(6, 173);
            this.buttonMapEditor.Name = "buttonMapEditor";
            this.buttonMapEditor.Size = new System.Drawing.Size(192, 23);
            this.buttonMapEditor.TabIndex = 5;
            this.buttonMapEditor.Text = "Map Editor";
            this.buttonMapEditor.UseVisualStyleBackColor = true;
            this.buttonMapEditor.Click += new System.EventHandler(this.buttonMapEditor_Click);
            // 
            // buttonSoundEditor
            // 
            this.buttonSoundEditor.Location = new System.Drawing.Point(6, 86);
            this.buttonSoundEditor.Name = "buttonSoundEditor";
            this.buttonSoundEditor.Size = new System.Drawing.Size(192, 23);
            this.buttonSoundEditor.TabIndex = 4;
            this.buttonSoundEditor.Text = "Sound Editor";
            this.buttonSoundEditor.UseVisualStyleBackColor = true;
            this.buttonSoundEditor.Click += new System.EventHandler(this.buttonSoundEditor_Click);
            // 
            // buttonScreenEditor
            // 
            this.buttonScreenEditor.Location = new System.Drawing.Point(6, 144);
            this.buttonScreenEditor.Name = "buttonScreenEditor";
            this.buttonScreenEditor.Size = new System.Drawing.Size(192, 23);
            this.buttonScreenEditor.TabIndex = 3;
            this.buttonScreenEditor.Text = "Screen Editor";
            this.buttonScreenEditor.UseVisualStyleBackColor = true;
            this.buttonScreenEditor.Click += new System.EventHandler(this.buttonScreenEditor_Click);
            // 
            // buttonParticleEditor
            // 
            this.buttonParticleEditor.Location = new System.Drawing.Point(6, 115);
            this.buttonParticleEditor.Name = "buttonParticleEditor";
            this.buttonParticleEditor.Size = new System.Drawing.Size(192, 23);
            this.buttonParticleEditor.TabIndex = 2;
            this.buttonParticleEditor.Text = "Particle Editor";
            this.buttonParticleEditor.UseVisualStyleBackColor = true;
            // 
            // buttonProjectConfig
            // 
            this.buttonProjectConfig.Location = new System.Drawing.Point(6, 19);
            this.buttonProjectConfig.Name = "buttonProjectConfig";
            this.buttonProjectConfig.Size = new System.Drawing.Size(192, 23);
            this.buttonProjectConfig.TabIndex = 1;
            this.buttonProjectConfig.Text = "Project Config";
            this.buttonProjectConfig.UseVisualStyleBackColor = true;
            this.buttonProjectConfig.Click += new System.EventHandler(this.buttonProjectConfig_Click);
            // 
            // buttonSprite2DEditor
            // 
            this.buttonSprite2DEditor.Location = new System.Drawing.Point(6, 57);
            this.buttonSprite2DEditor.Name = "buttonSprite2DEditor";
            this.buttonSprite2DEditor.Size = new System.Drawing.Size(192, 23);
            this.buttonSprite2DEditor.TabIndex = 0;
            this.buttonSprite2DEditor.Text = "Animation / Sprite 2D Editor";
            this.buttonSprite2DEditor.UseVisualStyleBackColor = true;
            this.buttonSprite2DEditor.Click += new System.EventHandler(this.buttonSprite2DEditor_Click);
            // 
            // groupBox2
            // 
            this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox2.Location = new System.Drawing.Point(3, 214);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(204, 174);
            this.groupBox2.TabIndex = 1;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Externals Tools";
            // 
            // buttonLaunchGame
            // 
            this.buttonLaunchGame.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonLaunchGame.Location = new System.Drawing.Point(41, 405);
            this.buttonLaunchGame.Name = "buttonLaunchGame";
            this.buttonLaunchGame.Size = new System.Drawing.Size(166, 23);
            this.buttonLaunchGame.TabIndex = 2;
            this.buttonLaunchGame.Text = "Launch Game";
            this.buttonLaunchGame.UseVisualStyleBackColor = true;
            this.buttonLaunchGame.Click += new System.EventHandler(this.buttonLaunchGame_Click);
            // 
            // tabControlMain
            // 
            this.tabControlMain.Controls.Add(this.tabPageSourceControl);
            this.tabControlMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControlMain.Location = new System.Drawing.Point(0, 0);
            this.tabControlMain.Name = "tabControlMain";
            this.tabControlMain.SelectedIndex = 0;
            this.tabControlMain.Size = new System.Drawing.Size(373, 435);
            this.tabControlMain.TabIndex = 5;
            // 
            // tabPageSourceControl
            // 
            this.tabPageSourceControl.Controls.Add(this.tabControlSourceControl);
            this.tabPageSourceControl.Location = new System.Drawing.Point(4, 22);
            this.tabPageSourceControl.Name = "tabPageSourceControl";
            this.tabPageSourceControl.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageSourceControl.Size = new System.Drawing.Size(365, 409);
            this.tabPageSourceControl.TabIndex = 0;
            this.tabPageSourceControl.Text = "Source Control";
            this.tabPageSourceControl.UseVisualStyleBackColor = true;
            // 
            // tabControlSourceControl
            // 
            this.tabControlSourceControl.Controls.Add(this.tabPage1);
            this.tabControlSourceControl.Controls.Add(this.tabPage2);
            this.tabControlSourceControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControlSourceControl.Location = new System.Drawing.Point(3, 3);
            this.tabControlSourceControl.Name = "tabControlSourceControl";
            this.tabControlSourceControl.SelectedIndex = 0;
            this.tabControlSourceControl.Size = new System.Drawing.Size(359, 403);
            this.tabControlSourceControl.TabIndex = 4;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.label2);
            this.tabPage1.Controls.Add(this.treeViewFiles);
            this.tabPage1.Controls.Add(this.buttonRefresh);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(351, 377);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Workspace";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.Location = new System.Drawing.Point(3, 3);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(345, 15);
            this.label2.TabIndex = 2;
            this.label2.Text = "Files";
            this.label2.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // treeViewFiles
            // 
            this.treeViewFiles.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.treeViewFiles.ImageIndex = 0;
            this.treeViewFiles.ImageList = this.imageListWorkspace;
            this.treeViewFiles.Location = new System.Drawing.Point(3, 21);
            this.treeViewFiles.Name = "treeViewFiles";
            this.treeViewFiles.SelectedImageIndex = 0;
            this.treeViewFiles.Size = new System.Drawing.Size(345, 324);
            this.treeViewFiles.TabIndex = 0;
            // 
            // buttonRefresh
            // 
            this.buttonRefresh.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonRefresh.Location = new System.Drawing.Point(3, 351);
            this.buttonRefresh.Name = "buttonRefresh";
            this.buttonRefresh.Size = new System.Drawing.Size(75, 23);
            this.buttonRefresh.TabIndex = 3;
            this.buttonRefresh.Text = "Refresh";
            this.buttonRefresh.UseVisualStyleBackColor = true;
            this.buttonRefresh.Click += new System.EventHandler(this.buttonRefresh_Click);
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.buttonRefreshChangeList);
            this.tabPage2.Controls.Add(this.label3);
            this.tabPage2.Controls.Add(this.treeViewChangeList);
            this.tabPage2.Controls.Add(this.buttonSubmit);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(351, 377);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Change List";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // buttonRefreshChangeList
            // 
            this.buttonRefreshChangeList.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonRefreshChangeList.Location = new System.Drawing.Point(6, 346);
            this.buttonRefreshChangeList.Name = "buttonRefreshChangeList";
            this.buttonRefreshChangeList.Size = new System.Drawing.Size(75, 23);
            this.buttonRefreshChangeList.TabIndex = 4;
            this.buttonRefreshChangeList.Text = "Refresh";
            this.buttonRefreshChangeList.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            this.label3.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label3.Location = new System.Drawing.Point(3, 3);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(0, 15);
            this.label3.TabIndex = 3;
            this.label3.Text = "Change list";
            this.label3.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // treeViewChangeList
            // 
            this.treeViewChangeList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.treeViewChangeList.ImageIndex = 28;
            this.treeViewChangeList.ImageList = this.imageListWorkspace;
            this.treeViewChangeList.Location = new System.Drawing.Point(3, 21);
            this.treeViewChangeList.Name = "treeViewChangeList";
            treeNode1.Name = "Nœud0";
            treeNode1.Text = "default";
            this.treeViewChangeList.Nodes.AddRange(new System.Windows.Forms.TreeNode[] {
            treeNode1});
            this.treeViewChangeList.SelectedImageIndex = 0;
            this.treeViewChangeList.Size = new System.Drawing.Size(0, 319);
            this.treeViewChangeList.TabIndex = 2;
            // 
            // buttonSubmit
            // 
            this.buttonSubmit.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSubmit.Location = new System.Drawing.Point(-127, 348);
            this.buttonSubmit.Name = "buttonSubmit";
            this.buttonSubmit.Size = new System.Drawing.Size(75, 23);
            this.buttonSubmit.TabIndex = 1;
            this.buttonSubmit.Text = "Submit";
            this.buttonSubmit.UseVisualStyleBackColor = true;
            this.buttonSubmit.Click += new System.EventHandler(this.buttonSubmit_Click);
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.toolsToolStripMenuItem,
            this.aboutToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(593, 24);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.newToolStripMenuItem,
            this.openToolStripMenuItem,
            this.saveToolStripMenuItem,
            this.saveAsToolStripMenuItem,
            this.toolStripSeparator2,
            this.toolStripMenuItemRecentProject,
            this.toolStripSeparator1,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // newToolStripMenuItem
            // 
            this.newToolStripMenuItem.Name = "newToolStripMenuItem";
            this.newToolStripMenuItem.Size = new System.Drawing.Size(150, 22);
            this.newToolStripMenuItem.Text = "New";
            this.newToolStripMenuItem.Click += new System.EventHandler(this.newToolStripMenuItem_Click);
            // 
            // openToolStripMenuItem
            // 
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.Size = new System.Drawing.Size(150, 22);
            this.openToolStripMenuItem.Text = "Open";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);
            // 
            // saveToolStripMenuItem
            // 
            this.saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            this.saveToolStripMenuItem.Size = new System.Drawing.Size(150, 22);
            this.saveToolStripMenuItem.Text = "Save";
            this.saveToolStripMenuItem.Click += new System.EventHandler(this.saveToolStripMenuItem_Click);
            // 
            // saveAsToolStripMenuItem
            // 
            this.saveAsToolStripMenuItem.Name = "saveAsToolStripMenuItem";
            this.saveAsToolStripMenuItem.Size = new System.Drawing.Size(150, 22);
            this.saveAsToolStripMenuItem.Text = "Save as";
            this.saveAsToolStripMenuItem.Click += new System.EventHandler(this.saveAsToolStripMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(147, 6);
            // 
            // toolStripMenuItemRecentProject
            // 
            this.toolStripMenuItemRecentProject.Name = "toolStripMenuItemRecentProject";
            this.toolStripMenuItemRecentProject.Size = new System.Drawing.Size(150, 22);
            this.toolStripMenuItemRecentProject.Text = "Recent Project";
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(147, 6);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(150, 22);
            this.exitToolStripMenuItem.Text = "Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // toolsToolStripMenuItem
            // 
            this.toolsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.windowToolStripMenuItem,
            this.toolStripSeparator6,
            this.buildAssetToolStripMenuItem,
            this.toolStripSeparator5,
            this.toolStripMenuItemSourceControlOption,
            this.toolStripSeparator4,
            this.debugVerbosityToolStripMenuItem});
            this.toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            this.toolsToolStripMenuItem.Size = new System.Drawing.Size(48, 20);
            this.toolsToolStripMenuItem.Text = "Tools";
            // 
            // windowToolStripMenuItem
            // 
            this.windowToolStripMenuItem.Name = "windowToolStripMenuItem";
            this.windowToolStripMenuItem.Size = new System.Drawing.Size(193, 22);
            this.windowToolStripMenuItem.Text = "Window";
            // 
            // toolStripSeparator6
            // 
            this.toolStripSeparator6.Name = "toolStripSeparator6";
            this.toolStripSeparator6.Size = new System.Drawing.Size(190, 6);
            // 
            // buildAssetToolStripMenuItem
            // 
            this.buildAssetToolStripMenuItem.Name = "buildAssetToolStripMenuItem";
            this.buildAssetToolStripMenuItem.Size = new System.Drawing.Size(193, 22);
            this.buildAssetToolStripMenuItem.Text = "Build Asset";
            this.buildAssetToolStripMenuItem.Click += new System.EventHandler(this.buildAssetToolStripMenuItem_Click);
            // 
            // toolStripSeparator5
            // 
            this.toolStripSeparator5.Name = "toolStripSeparator5";
            this.toolStripSeparator5.Size = new System.Drawing.Size(190, 6);
            // 
            // toolStripMenuItemSourceControlOption
            // 
            this.toolStripMenuItemSourceControlOption.Name = "toolStripMenuItemSourceControlOption";
            this.toolStripMenuItemSourceControlOption.Size = new System.Drawing.Size(193, 22);
            this.toolStripMenuItemSourceControlOption.Text = "Source Control Option";
            this.toolStripMenuItemSourceControlOption.Click += new System.EventHandler(this.toolStripMenuItemSourceControlOption_Click);
            // 
            // toolStripSeparator4
            // 
            this.toolStripSeparator4.Name = "toolStripSeparator4";
            this.toolStripSeparator4.Size = new System.Drawing.Size(190, 6);
            // 
            // debugVerbosityToolStripMenuItem
            // 
            this.debugVerbosityToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.debugToolStripMenuItem,
            this.normalToolStripMenuItem,
            this.noneToolStripMenuItem});
            this.debugVerbosityToolStripMenuItem.Name = "debugVerbosityToolStripMenuItem";
            this.debugVerbosityToolStripMenuItem.Size = new System.Drawing.Size(193, 22);
            this.debugVerbosityToolStripMenuItem.Text = "Debug Verbosity";
            // 
            // debugToolStripMenuItem
            // 
            this.debugToolStripMenuItem.Name = "debugToolStripMenuItem";
            this.debugToolStripMenuItem.Size = new System.Drawing.Size(114, 22);
            this.debugToolStripMenuItem.Text = "Debug";
            this.debugToolStripMenuItem.Click += new System.EventHandler(this.debugToolStripMenuItem1_Click);
            // 
            // normalToolStripMenuItem
            // 
            this.normalToolStripMenuItem.Checked = true;
            this.normalToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.normalToolStripMenuItem.Name = "normalToolStripMenuItem";
            this.normalToolStripMenuItem.Size = new System.Drawing.Size(114, 22);
            this.normalToolStripMenuItem.Text = "Normal";
            this.normalToolStripMenuItem.Click += new System.EventHandler(this.normalToolStripMenuItem_Click);
            // 
            // noneToolStripMenuItem
            // 
            this.noneToolStripMenuItem.Name = "noneToolStripMenuItem";
            this.noneToolStripMenuItem.Size = new System.Drawing.Size(114, 22);
            this.noneToolStripMenuItem.Text = "None";
            this.noneToolStripMenuItem.Click += new System.EventHandler(this.noneToolStripMenuItem_Click);
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.documentationToolStripMenuItem,
            this.toolStripSeparator3,
            this.aboutEditorToolStripMenuItem});
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(52, 20);
            this.aboutToolStripMenuItem.Text = "About";
            // 
            // documentationToolStripMenuItem
            // 
            this.documentationToolStripMenuItem.Name = "documentationToolStripMenuItem";
            this.documentationToolStripMenuItem.Size = new System.Drawing.Size(181, 22);
            this.documentationToolStripMenuItem.Text = "Documentation";
            this.documentationToolStripMenuItem.Click += new System.EventHandler(this.documentationToolStripMenuItem_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(178, 6);
            // 
            // aboutEditorToolStripMenuItem
            // 
            this.aboutEditorToolStripMenuItem.Name = "aboutEditorToolStripMenuItem";
            this.aboutEditorToolStripMenuItem.Size = new System.Drawing.Size(181, 22);
            this.aboutEditorToolStripMenuItem.Text = "About Project Editor";
            this.aboutEditorToolStripMenuItem.Click += new System.EventHandler(this.aboutEditorToolStripMenuItem_Click);
            // 
            // EditorMainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(593, 459);
            this.Controls.Add(this.splitContainerMain);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.MaximizeBox = false;
            this.MinimumSize = new System.Drawing.Size(509, 471);
            this.Name = "EditorMainForm";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Project Editor";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.EditorMainForm_FormClosing);
            this.splitContainerMain.Panel1.ResumeLayout(false);
            this.splitContainerMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).EndInit();
            this.splitContainerMain.ResumeLayout(false);
            this.groupBox1.ResumeLayout(false);
            this.tabControlMain.ResumeLayout(false);
            this.tabPageSourceControl.ResumeLayout(false);
            this.tabControlSourceControl.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }


        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem newToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveAsToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button buttonProjectConfig;
        private System.Windows.Forms.Button buttonSprite2DEditor;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Button buttonScreenEditor;
        private System.Windows.Forms.Button buttonParticleEditor;
        private System.Windows.Forms.Button buttonLaunchGame;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemRecentProject;
        private System.Windows.Forms.ToolStripMenuItem toolsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem buildAssetToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem documentationToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutEditorToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.Button buttonGameDocumentation;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
        private System.Windows.Forms.ToolStripMenuItem debugVerbosityToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem debugToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem normalToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem noneToolStripMenuItem;
        private System.Windows.Forms.Button buttonSoundEditor;
        private System.Windows.Forms.SplitContainer splitContainerMain;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button buttonSubmit;
        private System.Windows.Forms.TreeView treeViewFiles;
        private System.Windows.Forms.ImageList imageListWorkspace;
        private System.Windows.Forms.Button buttonRefresh;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator5;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemSourceControlOption;
        private System.Windows.Forms.TabControl tabControlSourceControl;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TreeView treeViewChangeList;
        private System.Windows.Forms.Button buttonRefreshChangeList;
        private System.Windows.Forms.ToolStripMenuItem windowToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator6;
        private System.Windows.Forms.Button buttonMapEditor;
        private System.Windows.Forms.TabControl tabControlMain;
        private System.Windows.Forms.TabPage tabPageSourceControl;
    }
}

