namespace Editor
{
    partial class ContentBrowserForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }


        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ContentBrowserForm));
            this.imageListTreeViewIcon = new System.Windows.Forms.ImageList(this.components);
            this.contextMenuStripListView = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.toolStripMenuItem2 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemRename = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemDelete = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripMenuItemFullpathToClipboard = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripMenuItem4 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripMenuItemAddItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addAnimSetToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addAnimTreeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addSoundToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.newSpritesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addScreenToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addSkinUIToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addFontToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.imageListPreviewObject = new System.Windows.Forms.ImageList(this.components);
            this.contextMenuStripTreeView = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.enregistrerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.label1 = new System.Windows.Forms.Label();
            this.treeViewFolder = new System.Windows.Forms.TreeView();
            this.listViewItems = new System.Windows.Forms.ListView();
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.contextMenuStripListView.SuspendLayout();
            this.contextMenuStripTreeView.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // imageListTreeViewIcon
            // 
            this.imageListTreeViewIcon.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageListTreeViewIcon.ImageStream")));
            this.imageListTreeViewIcon.TransparentColor = System.Drawing.Color.Transparent;
            this.imageListTreeViewIcon.Images.SetKeyName(0, "p4v_folderworkspace.png");
            // 
            // contextMenuStripListView
            // 
            this.contextMenuStripListView.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItem2,
            this.toolStripMenuItemRename,
            this.toolStripMenuItemDelete,
            this.toolStripSeparator3,
            this.toolStripMenuItemFullpathToClipboard,
            this.toolStripSeparator2,
            this.toolStripMenuItem4,
            this.toolStripSeparator1,
            this.toolStripMenuItemAddItem,
            this.addAnimSetToolStripMenuItem,
            this.addAnimTreeToolStripMenuItem,
            this.addSoundToolStripMenuItem,
            this.newSpritesToolStripMenuItem,
            this.addScreenToolStripMenuItem,
            this.addSkinUIToolStripMenuItem,
            this.addFontToolStripMenuItem});
            this.contextMenuStripListView.Name = "contextMenuStripListView";
            this.contextMenuStripListView.Size = new System.Drawing.Size(214, 330);
            this.contextMenuStripListView.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenuStripListView_Opening);
            // 
            // toolStripMenuItem2
            // 
            this.toolStripMenuItem2.Name = "toolStripMenuItem2";
            this.toolStripMenuItem2.Size = new System.Drawing.Size(213, 22);
            this.toolStripMenuItem2.Text = "Load";
            // 
            // toolStripMenuItemRename
            // 
            this.toolStripMenuItemRename.Name = "toolStripMenuItemRename";
            this.toolStripMenuItemRename.Size = new System.Drawing.Size(213, 22);
            this.toolStripMenuItemRename.Text = "Rename";
            this.toolStripMenuItemRename.Click += new System.EventHandler(this.toolStripMenuItemRename_Click);
            // 
            // toolStripMenuItemDelete
            // 
            this.toolStripMenuItemDelete.Name = "toolStripMenuItemDelete";
            this.toolStripMenuItemDelete.Size = new System.Drawing.Size(213, 22);
            this.toolStripMenuItemDelete.Text = "Delete";
            this.toolStripMenuItemDelete.Click += new System.EventHandler(this.toolStripMenuItemDelete_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(210, 6);
            // 
            // toolStripMenuItemFullpathToClipboard
            // 
            this.toolStripMenuItemFullpathToClipboard.Name = "toolStripMenuItemFullpathToClipboard";
            this.toolStripMenuItemFullpathToClipboard.Size = new System.Drawing.Size(213, 22);
            this.toolStripMenuItemFullpathToClipboard.Text = "Copy fullpath to clipboard";
            this.toolStripMenuItemFullpathToClipboard.Click += new System.EventHandler(this.toolStripMenuItemFullpathToClipboard_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(210, 6);
            // 
            // toolStripMenuItem4
            // 
            this.toolStripMenuItem4.Name = "toolStripMenuItem4";
            this.toolStripMenuItem4.Size = new System.Drawing.Size(213, 22);
            this.toolStripMenuItem4.Text = "Source Control";
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(210, 6);
            // 
            // toolStripMenuItemAddItem
            // 
            this.toolStripMenuItemAddItem.Name = "toolStripMenuItemAddItem";
            this.toolStripMenuItemAddItem.Size = new System.Drawing.Size(213, 22);
            this.toolStripMenuItemAddItem.Text = "Add";
            // 
            // addAnimSetToolStripMenuItem
            // 
            this.addAnimSetToolStripMenuItem.Name = "addAnimSetToolStripMenuItem";
            this.addAnimSetToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.addAnimSetToolStripMenuItem.Text = "Add AnimSet";
            this.addAnimSetToolStripMenuItem.Click += new System.EventHandler(this.addAnimSetToolStripMenuItem_Click);
            // 
            // addAnimTreeToolStripMenuItem
            // 
            this.addAnimTreeToolStripMenuItem.Name = "addAnimTreeToolStripMenuItem";
            this.addAnimTreeToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.addAnimTreeToolStripMenuItem.Text = "Add AnimTree";
            this.addAnimTreeToolStripMenuItem.Click += new System.EventHandler(this.addAnimTreeToolStripMenuItem_Click);
            // 
            // addSoundToolStripMenuItem
            // 
            this.addSoundToolStripMenuItem.Name = "addSoundToolStripMenuItem";
            this.addSoundToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.addSoundToolStripMenuItem.Text = "Add Sound";
            this.addSoundToolStripMenuItem.Click += new System.EventHandler(this.addSoundToolStripMenuItem_Click);
            // 
            // newSpritesToolStripMenuItem
            // 
            this.newSpritesToolStripMenuItem.Name = "newSpritesToolStripMenuItem";
            this.newSpritesToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.newSpritesToolStripMenuItem.Text = "Add Sprites";
            this.newSpritesToolStripMenuItem.Click += new System.EventHandler(this.newSpritesToolStripMenuItem_Click);
            // 
            // addScreenToolStripMenuItem
            // 
            this.addScreenToolStripMenuItem.Name = "addScreenToolStripMenuItem";
            this.addScreenToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.addScreenToolStripMenuItem.Text = "Add UI";
            this.addScreenToolStripMenuItem.Click += new System.EventHandler(this.addScreenToolStripMenuItem_Click);
            // 
            // addSkinUIToolStripMenuItem
            // 
            this.addSkinUIToolStripMenuItem.Name = "addSkinUIToolStripMenuItem";
            this.addSkinUIToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.addSkinUIToolStripMenuItem.Text = "Add Skin UI";
            this.addSkinUIToolStripMenuItem.Click += new System.EventHandler(this.addSkinUIToolStripMenuItem_Click);
            // 
            // addFontToolStripMenuItem
            // 
            this.addFontToolStripMenuItem.Name = "addFontToolStripMenuItem";
            this.addFontToolStripMenuItem.Size = new System.Drawing.Size(213, 22);
            this.addFontToolStripMenuItem.Text = "Add Font";
            this.addFontToolStripMenuItem.Click += new System.EventHandler(this.addFontToolStripMenuItem_Click);
            // 
            // imageListPreviewObject
            // 
            this.imageListPreviewObject.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageListPreviewObject.ImageStream")));
            this.imageListPreviewObject.TransparentColor = System.Drawing.Color.Transparent;
            this.imageListPreviewObject.Images.SetKeyName(0, "sound");
            this.imageListPreviewObject.Images.SetKeyName(1, "animation");
            this.imageListPreviewObject.Images.SetKeyName(2, "unknown");
            this.imageListPreviewObject.Images.SetKeyName(3, "sprite");
            this.imageListPreviewObject.Images.SetKeyName(4, "menu");
            // 
            // contextMenuStripTreeView
            // 
            this.contextMenuStripTreeView.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.enregistrerToolStripMenuItem});
            this.contextMenuStripTreeView.Name = "contextMenuStripTreeView";
            this.contextMenuStripTreeView.Size = new System.Drawing.Size(118, 26);
            // 
            // enregistrerToolStripMenuItem
            // 
            this.enregistrerToolStripMenuItem.Name = "enregistrerToolStripMenuItem";
            this.enregistrerToolStripMenuItem.Size = new System.Drawing.Size(117, 22);
            this.enregistrerToolStripMenuItem.Text = "Rename";
            // 
            // splitContainer1
            // 
            this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer1.Location = new System.Drawing.Point(0, 41);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.label1);
            this.splitContainer1.Panel1.Controls.Add(this.treeViewFolder);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.listViewItems);
            this.splitContainer1.Size = new System.Drawing.Size(1025, 659);
            this.splitContainer1.SplitterDistance = 245;
            this.splitContainer1.TabIndex = 4;
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.Location = new System.Drawing.Point(3, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(239, 23);
            this.label1.TabIndex = 3;
            this.label1.Text = "Packages";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // treeView1
            // 
            this.treeViewFolder.AllowDrop = true;
            this.treeViewFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.treeViewFolder.FullRowSelect = true;
            this.treeViewFolder.HideSelection = false;
            this.treeViewFolder.HotTracking = true;
            this.treeViewFolder.ImageIndex = 0;
            this.treeViewFolder.ImageList = this.imageListTreeViewIcon;
            this.treeViewFolder.Location = new System.Drawing.Point(3, 26);
            this.treeViewFolder.Name = "treeView1";
            this.treeViewFolder.SelectedImageIndex = 0;
            this.treeViewFolder.Size = new System.Drawing.Size(239, 630);
            this.treeViewFolder.TabIndex = 0;
            this.treeViewFolder.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.treeView1_AfterSelect);
            this.treeViewFolder.NodeMouseClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.treeView1_NodeMouseClick);
            this.treeViewFolder.DragDrop += new System.Windows.Forms.DragEventHandler(this.treeView1_DragDrop);
            this.treeViewFolder.DragEnter += new System.Windows.Forms.DragEventHandler(this.treeView1_DragEnter);
            this.treeViewFolder.DragOver += new System.Windows.Forms.DragEventHandler(this.treeView1_DragOver);
            this.treeViewFolder.DragLeave += new System.EventHandler(this.treeView1_DragLeave);
            // 
            // listViewItems
            // 
            this.listViewItems.AllowDrop = true;
            this.listViewItems.ContextMenuStrip = this.contextMenuStripListView;
            this.listViewItems.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listViewItems.FullRowSelect = true;
            this.listViewItems.GridLines = true;
            this.listViewItems.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.listViewItems.ImeMode = System.Windows.Forms.ImeMode.Disable;
            this.listViewItems.LabelEdit = true;
            this.listViewItems.LabelWrap = false;
            this.listViewItems.LargeImageList = this.imageListPreviewObject;
            this.listViewItems.Location = new System.Drawing.Point(0, 0);
            this.listViewItems.Name = "listViewItems";
            this.listViewItems.ShowItemToolTips = true;
            this.listViewItems.Size = new System.Drawing.Size(776, 659);
            this.listViewItems.TabIndex = 3;
            this.listViewItems.UseCompatibleStateImageBehavior = false;
            this.listViewItems.AfterLabelEdit += new System.Windows.Forms.LabelEditEventHandler(this.listViewItems_AfterLabelEdit);
            this.listViewItems.BeforeLabelEdit += new System.Windows.Forms.LabelEditEventHandler(this.listViewItems_BeforeLabelEdit);
            this.listViewItems.ItemDrag += new System.Windows.Forms.ItemDragEventHandler(this.listViewItems_ItemDrag);
            this.listViewItems.DragDrop += new System.Windows.Forms.DragEventHandler(this.listViewItems_DragDrop);
            this.listViewItems.DragEnter += new System.Windows.Forms.DragEventHandler(this.listViewItems_DragEnter);
            this.listViewItems.DragOver += new System.Windows.Forms.DragEventHandler(this.listViewItems_DragOver);
            this.listViewItems.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.listView1_MouseDoubleClick);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(12, 12);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 5;
            this.button1.Text = "Filtre";
            this.button1.UseVisualStyleBackColor = true;
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(93, 12);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(75, 23);
            this.button2.TabIndex = 6;
            this.button2.Text = "Tag";
            this.button2.UseVisualStyleBackColor = true;
            // 
            // ContentBrowserForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1025, 700);
            this.CloseButton = false;
            this.CloseButtonVisible = false;
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.splitContainer1);
            this.DockAreas = WeifenLuo.WinFormsUI.Docking.DockAreas.Document;
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Name = "ContentBrowserForm";
            this.TabText = "Content Browser";
            this.Text = "Content Browser";
            this.contextMenuStripListView.ResumeLayout(false);
            this.contextMenuStripTreeView.ResumeLayout(false);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);

        }


        private System.Windows.Forms.ContextMenuStrip contextMenuStripListView;
        private System.Windows.Forms.ToolStripMenuItem addAnimSetToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addAnimTreeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addSoundToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem newSpritesToolStripMenuItem;
        private System.Windows.Forms.ContextMenuStrip contextMenuStripTreeView;
        private System.Windows.Forms.ToolStripMenuItem enregistrerToolStripMenuItem;
        private System.Windows.Forms.ImageList imageListTreeViewIcon;
        private System.Windows.Forms.ImageList imageListPreviewObject;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem2;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemRename;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemDelete;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem4;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemAddItem;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TreeView treeViewFolder;
        private System.Windows.Forms.ListView listViewItems;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.ToolStripMenuItem addScreenToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemFullpathToClipboard;
        private System.Windows.Forms.ToolStripMenuItem addFontToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addSkinUIToolStripMenuItem;
    }
}