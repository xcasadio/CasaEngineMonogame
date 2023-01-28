namespace Editor.Tools.Graphics2D
{
    partial class Sprite2DEditorForm
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
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.comboBoxSelectedSprite2D = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.propertyGridSprite2D = new System.Windows.Forms.PropertyGrid();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.splitContainerCollision = new System.Windows.Forms.SplitContainer();
            this.buttonAddLine = new System.Windows.Forms.Button();
            this.buttonAddRectangle = new System.Windows.Forms.Button();
            this.buttonAddPoly = new System.Windows.Forms.Button();
            this.buttonAddCircle = new System.Windows.Forms.Button();
            this.buttonAddColl = new System.Windows.Forms.Button();
            this.listBoxCollision = new System.Windows.Forms.ListBox();
            this.contextMenuStripTreeView = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.copyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pasteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.label6 = new System.Windows.Forms.Label();
            this.buttonDelColl = new System.Windows.Forms.Button();
            this.propertyGridCollision = new System.Windows.Forms.PropertyGrid();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.buttonAddSocket = new System.Windows.Forms.Button();
            this.listBoxSockets = new System.Windows.Forms.ListBox();
            this.label2 = new System.Windows.Forms.Label();
            this.buttonDelSocket = new System.Windows.Forms.Button();
            this.propertyGridSocket = new System.Windows.Forms.PropertyGrid();
            this.panelXna = new System.Windows.Forms.Panel();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.editionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.undoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.redoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.dToolStripMenuItem = new System.Windows.Forms.ToolStripSeparator();
            this.couperToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.pasteToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.optionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.backgroundToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.tabPage1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerCollision)).BeginInit();
            this.splitContainerCollision.Panel1.SuspendLayout();
            this.splitContainerCollision.Panel2.SuspendLayout();
            this.splitContainerCollision.SuspendLayout();
            this.contextMenuStripTreeView.SuspendLayout();
            this.tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.comboBoxSelectedSprite2D);
            this.splitContainer1.Panel1.Controls.Add(this.label1);
            this.splitContainer1.Panel1.Controls.Add(this.tabControl1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.panelXna);
            this.splitContainer1.Size = new System.Drawing.Size(854, 537);
            this.splitContainer1.SplitterDistance = 343;
            this.splitContainer1.TabIndex = 0;
            // 
            // comboBoxSelectedSprite2D
            // 
            this.comboBoxSelectedSprite2D.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBoxSelectedSprite2D.Enabled = false;
            this.comboBoxSelectedSprite2D.FormattingEnabled = true;
            this.comboBoxSelectedSprite2D.Location = new System.Drawing.Point(86, 27);
            this.comboBoxSelectedSprite2D.Name = "comboBoxSelectedSprite2D";
            this.comboBoxSelectedSprite2D.Size = new System.Drawing.Size(251, 21);
            this.comboBoxSelectedSprite2D.TabIndex = 4;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Enabled = false;
            this.label1.Location = new System.Drawing.Point(3, 30);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(77, 13);
            this.label1.TabIndex = 3;
            this.label1.Text = "Selected sprite";
            // 
            // tabControl1
            // 
            this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Location = new System.Drawing.Point(0, 54);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(341, 483);
            this.tabControl1.TabIndex = 0;
            this.tabControl1.SelectedIndexChanged += new System.EventHandler(this.tabControl1_TabIndexChanged);
            this.tabControl1.TabIndexChanged += new System.EventHandler(this.tabControl1_TabIndexChanged);
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.propertyGridSprite2D);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Size = new System.Drawing.Size(333, 457);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Sprite";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // propertyGridSprite2D
            // 
            this.propertyGridSprite2D.Dock = System.Windows.Forms.DockStyle.Fill;
            this.propertyGridSprite2D.Location = new System.Drawing.Point(0, 0);
            this.propertyGridSprite2D.Name = "propertyGridSprite2D";
            this.propertyGridSprite2D.Size = new System.Drawing.Size(333, 457);
            this.propertyGridSprite2D.TabIndex = 35;
            this.propertyGridSprite2D.PropertyValueChanged += new System.Windows.Forms.PropertyValueChangedEventHandler(this.propertyGridSprite2D_PropertyValueChanged);
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.splitContainerCollision);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(333, 457);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Collisions";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // splitContainerCollision
            // 
            this.splitContainerCollision.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerCollision.Location = new System.Drawing.Point(3, 3);
            this.splitContainerCollision.Name = "splitContainerCollision";
            this.splitContainerCollision.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainerCollision.Panel1
            // 
            this.splitContainerCollision.Panel1.Controls.Add(this.buttonAddLine);
            this.splitContainerCollision.Panel1.Controls.Add(this.buttonAddRectangle);
            this.splitContainerCollision.Panel1.Controls.Add(this.buttonAddPoly);
            this.splitContainerCollision.Panel1.Controls.Add(this.buttonAddCircle);
            this.splitContainerCollision.Panel1.Controls.Add(this.buttonAddColl);
            this.splitContainerCollision.Panel1.Controls.Add(this.listBoxCollision);
            this.splitContainerCollision.Panel1.Controls.Add(this.label6);
            this.splitContainerCollision.Panel1.Controls.Add(this.buttonDelColl);
            // 
            // splitContainerCollision.Panel2
            // 
            this.splitContainerCollision.Panel2.Controls.Add(this.propertyGridCollision);
            this.splitContainerCollision.Size = new System.Drawing.Size(327, 451);
            this.splitContainerCollision.SplitterDistance = 265;
            this.splitContainerCollision.TabIndex = 3;
            // 
            // buttonAddLine
            // 
            this.buttonAddLine.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonAddLine.Image = global::Editor.Properties.Resources.icon_line_add_16x16;
            this.buttonAddLine.Location = new System.Drawing.Point(298, 217);
            this.buttonAddLine.Name = "buttonAddLine";
            this.buttonAddLine.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.buttonAddLine.Size = new System.Drawing.Size(26, 26);
            this.buttonAddLine.TabIndex = 33;
            this.buttonAddLine.UseVisualStyleBackColor = true;
            this.buttonAddLine.Click += new System.EventHandler(this.buttonAddLine_Click);
            // 
            // buttonAddRectangle
            // 
            this.buttonAddRectangle.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonAddRectangle.Image = global::Editor.Properties.Resources.icon_rectangle_add_16x16;
            this.buttonAddRectangle.Location = new System.Drawing.Point(298, 185);
            this.buttonAddRectangle.Name = "buttonAddRectangle";
            this.buttonAddRectangle.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.buttonAddRectangle.Size = new System.Drawing.Size(26, 26);
            this.buttonAddRectangle.TabIndex = 32;
            this.buttonAddRectangle.UseVisualStyleBackColor = true;
            this.buttonAddRectangle.Click += new System.EventHandler(this.buttonAddRectangle_Click);
            // 
            // buttonAddPoly
            // 
            this.buttonAddPoly.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonAddPoly.Image = global::Editor.Properties.Resources.icon_polygone_add_16x16;
            this.buttonAddPoly.Location = new System.Drawing.Point(298, 153);
            this.buttonAddPoly.Name = "buttonAddPoly";
            this.buttonAddPoly.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.buttonAddPoly.Size = new System.Drawing.Size(26, 26);
            this.buttonAddPoly.TabIndex = 31;
            this.buttonAddPoly.UseVisualStyleBackColor = true;
            this.buttonAddPoly.Click += new System.EventHandler(this.buttonAddPoly_Click);
            // 
            // buttonAddCircle
            // 
            this.buttonAddCircle.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonAddCircle.Image = global::Editor.Properties.Resources.icon_circle_add_16x16;
            this.buttonAddCircle.Location = new System.Drawing.Point(298, 121);
            this.buttonAddCircle.Name = "buttonAddCircle";
            this.buttonAddCircle.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.buttonAddCircle.Size = new System.Drawing.Size(26, 26);
            this.buttonAddCircle.TabIndex = 30;
            this.buttonAddCircle.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this.buttonAddCircle.UseVisualStyleBackColor = true;
            this.buttonAddCircle.Click += new System.EventHandler(this.buttonAddCircle_Click);
            // 
            // buttonAddColl
            // 
            this.buttonAddColl.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonAddColl.Image = global::Editor.Properties.Resources.icon_Plus_16x16;
            this.buttonAddColl.Location = new System.Drawing.Point(298, 25);
            this.buttonAddColl.Name = "buttonAddColl";
            this.buttonAddColl.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.buttonAddColl.Size = new System.Drawing.Size(26, 26);
            this.buttonAddColl.TabIndex = 28;
            this.buttonAddColl.UseVisualStyleBackColor = true;
            this.buttonAddColl.Click += new System.EventHandler(this.buttonAddColl_Click);
            // 
            // listBoxCollision
            // 
            this.listBoxCollision.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listBoxCollision.ContextMenuStrip = this.contextMenuStripTreeView;
            this.listBoxCollision.FormattingEnabled = true;
            this.listBoxCollision.Location = new System.Drawing.Point(12, 25);
            this.listBoxCollision.Name = "listBoxCollision";
            this.listBoxCollision.Size = new System.Drawing.Size(280, 212);
            this.listBoxCollision.TabIndex = 26;
            this.listBoxCollision.SelectedIndexChanged += new System.EventHandler(this.listBoxCollision_SelectedIndexChanged);
            // 
            // contextMenuStripTreeView
            // 
            this.contextMenuStripTreeView.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.copyToolStripMenuItem,
            this.pasteToolStripMenuItem});
            this.contextMenuStripTreeView.Name = "contextMenuStripTreeView";
            this.contextMenuStripTreeView.Size = new System.Drawing.Size(145, 48);
            // 
            // copyToolStripMenuItem
            // 
            this.copyToolStripMenuItem.Name = "copyToolStripMenuItem";
            this.copyToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.C)));
            this.copyToolStripMenuItem.Size = new System.Drawing.Size(144, 22);
            this.copyToolStripMenuItem.Text = "Copy";
            this.copyToolStripMenuItem.Click += new System.EventHandler(this.copyToolStripMenuItem_Click);
            // 
            // pasteToolStripMenuItem
            // 
            this.pasteToolStripMenuItem.Name = "pasteToolStripMenuItem";
            this.pasteToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.V)));
            this.pasteToolStripMenuItem.Size = new System.Drawing.Size(144, 22);
            this.pasteToolStripMenuItem.Text = "Paste";
            this.pasteToolStripMenuItem.Click += new System.EventHandler(this.pasteToolStripMenuItem_Click);
            // 
            // label6
            // 
            this.label6.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label6.Location = new System.Drawing.Point(12, 9);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(280, 13);
            this.label6.TabIndex = 27;
            this.label6.Text = "List of collisions";
            this.label6.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // buttonDelColl
            // 
            this.buttonDelColl.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonDelColl.Image = global::Editor.Properties.Resources.icon_Minus_16x16;
            this.buttonDelColl.Location = new System.Drawing.Point(298, 57);
            this.buttonDelColl.Name = "buttonDelColl";
            this.buttonDelColl.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.buttonDelColl.Size = new System.Drawing.Size(26, 26);
            this.buttonDelColl.TabIndex = 29;
            this.buttonDelColl.UseVisualStyleBackColor = true;
            this.buttonDelColl.Click += new System.EventHandler(this.buttonDelColl_Click);
            // 
            // propertyGridCollision
            // 
            this.propertyGridCollision.Dock = System.Windows.Forms.DockStyle.Fill;
            this.propertyGridCollision.Location = new System.Drawing.Point(0, 0);
            this.propertyGridCollision.Name = "propertyGridCollision";
            this.propertyGridCollision.Size = new System.Drawing.Size(327, 182);
            this.propertyGridCollision.TabIndex = 34;
            this.propertyGridCollision.PropertyValueChanged += new System.Windows.Forms.PropertyValueChangedEventHandler(this.propertyGridCollision_PropertyValueChanged);
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.splitContainer2);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(333, 457);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Sockets";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(3, 3);
            this.splitContainer2.Name = "splitContainer2";
            this.splitContainer2.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.buttonAddSocket);
            this.splitContainer2.Panel1.Controls.Add(this.listBoxSockets);
            this.splitContainer2.Panel1.Controls.Add(this.label2);
            this.splitContainer2.Panel1.Controls.Add(this.buttonDelSocket);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.propertyGridSocket);
            this.splitContainer2.Size = new System.Drawing.Size(327, 451);
            this.splitContainer2.SplitterDistance = 265;
            this.splitContainer2.TabIndex = 4;
            // 
            // buttonAddSocket
            // 
            this.buttonAddSocket.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonAddSocket.Image = global::Editor.Properties.Resources.icon_Plus_16x16;
            this.buttonAddSocket.Location = new System.Drawing.Point(298, 25);
            this.buttonAddSocket.Name = "buttonAddSocket";
            this.buttonAddSocket.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.buttonAddSocket.Size = new System.Drawing.Size(26, 26);
            this.buttonAddSocket.TabIndex = 28;
            this.buttonAddSocket.UseVisualStyleBackColor = true;
            this.buttonAddSocket.Click += new System.EventHandler(this.buttonAddSocket_Click);
            // 
            // listBoxSockets
            // 
            this.listBoxSockets.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listBoxSockets.ContextMenuStrip = this.contextMenuStripTreeView;
            this.listBoxSockets.FormattingEnabled = true;
            this.listBoxSockets.Location = new System.Drawing.Point(12, 25);
            this.listBoxSockets.Name = "listBoxSockets";
            this.listBoxSockets.Size = new System.Drawing.Size(280, 212);
            this.listBoxSockets.TabIndex = 26;
            this.listBoxSockets.SelectedIndexChanged += new System.EventHandler(this.listBoxSockets_SelectedIndexChanged);
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.Location = new System.Drawing.Point(12, 9);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(280, 13);
            this.label2.TabIndex = 27;
            this.label2.Text = "List of sockets";
            this.label2.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // buttonDelSocket
            // 
            this.buttonDelSocket.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonDelSocket.Image = global::Editor.Properties.Resources.icon_Minus_16x16;
            this.buttonDelSocket.Location = new System.Drawing.Point(298, 57);
            this.buttonDelSocket.Name = "buttonDelSocket";
            this.buttonDelSocket.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.buttonDelSocket.Size = new System.Drawing.Size(26, 26);
            this.buttonDelSocket.TabIndex = 29;
            this.buttonDelSocket.UseVisualStyleBackColor = true;
            this.buttonDelSocket.Click += new System.EventHandler(this.buttonDelSocket_Click);
            // 
            // propertyGridSocket
            // 
            this.propertyGridSocket.Dock = System.Windows.Forms.DockStyle.Fill;
            this.propertyGridSocket.Location = new System.Drawing.Point(0, 0);
            this.propertyGridSocket.Name = "propertyGridSocket";
            this.propertyGridSocket.Size = new System.Drawing.Size(327, 182);
            this.propertyGridSocket.TabIndex = 34;
            this.propertyGridSocket.PropertyValueChanged += new System.Windows.Forms.PropertyValueChangedEventHandler(this.propertyGridSocket_PropertyValueChanged);
            // 
            // panelXna
            // 
            this.panelXna.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelXna.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.panelXna.Location = new System.Drawing.Point(0, 25);
            this.panelXna.Name = "panelXna";
            this.panelXna.Size = new System.Drawing.Size(507, 512);
            this.panelXna.TabIndex = 1;
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.editionToolStripMenuItem,
            this.optionToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(854, 24);
            this.menuStrip1.TabIndex = 1;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // editionToolStripMenuItem
            // 
            this.editionToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.undoToolStripMenuItem,
            this.redoToolStripMenuItem,
            this.dToolStripMenuItem,
            this.couperToolStripMenuItem,
            this.copyToolStripMenuItem1,
            this.pasteToolStripMenuItem1});
            this.editionToolStripMenuItem.Name = "editionToolStripMenuItem";
            this.editionToolStripMenuItem.Size = new System.Drawing.Size(56, 20);
            this.editionToolStripMenuItem.Text = "Edition";
            // 
            // undoToolStripMenuItem
            // 
            this.undoToolStripMenuItem.Name = "undoToolStripMenuItem";
            this.undoToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Z)));
            this.undoToolStripMenuItem.Size = new System.Drawing.Size(144, 22);
            this.undoToolStripMenuItem.Text = "Undo";
            this.undoToolStripMenuItem.Click += new System.EventHandler(this.undoToolStripMenuItem_Click);
            // 
            // redoToolStripMenuItem
            // 
            this.redoToolStripMenuItem.Name = "redoToolStripMenuItem";
            this.redoToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Y)));
            this.redoToolStripMenuItem.Size = new System.Drawing.Size(144, 22);
            this.redoToolStripMenuItem.Text = "Redo";
            this.redoToolStripMenuItem.Click += new System.EventHandler(this.redoToolStripMenuItem_Click);
            // 
            // dToolStripMenuItem
            // 
            this.dToolStripMenuItem.Name = "dToolStripMenuItem";
            this.dToolStripMenuItem.Size = new System.Drawing.Size(141, 6);
            // 
            // couperToolStripMenuItem
            // 
            this.couperToolStripMenuItem.Name = "couperToolStripMenuItem";
            this.couperToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.X)));
            this.couperToolStripMenuItem.Size = new System.Drawing.Size(144, 22);
            this.couperToolStripMenuItem.Text = "Cut";
            this.couperToolStripMenuItem.Click += new System.EventHandler(this.cutToolStripMenuItem_Click);
            // 
            // copyToolStripMenuItem1
            // 
            this.copyToolStripMenuItem1.Name = "copyToolStripMenuItem1";
            this.copyToolStripMenuItem1.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.C)));
            this.copyToolStripMenuItem1.Size = new System.Drawing.Size(144, 22);
            this.copyToolStripMenuItem1.Text = "Copy";
            this.copyToolStripMenuItem1.Click += new System.EventHandler(this.copyToolStripMenuItem1_Click);
            // 
            // pasteToolStripMenuItem1
            // 
            this.pasteToolStripMenuItem1.Name = "pasteToolStripMenuItem1";
            this.pasteToolStripMenuItem1.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.V)));
            this.pasteToolStripMenuItem1.Size = new System.Drawing.Size(144, 22);
            this.pasteToolStripMenuItem1.Text = "Paste";
            this.pasteToolStripMenuItem1.Click += new System.EventHandler(this.pasteToolStripMenuItem1_Click);
            // 
            // optionToolStripMenuItem
            // 
            this.optionToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.backgroundToolStripMenuItem});
            this.optionToolStripMenuItem.Name = "optionToolStripMenuItem";
            this.optionToolStripMenuItem.Size = new System.Drawing.Size(56, 20);
            this.optionToolStripMenuItem.Text = "Option";
            // 
            // backgroundToolStripMenuItem
            // 
            this.backgroundToolStripMenuItem.Name = "backgroundToolStripMenuItem";
            this.backgroundToolStripMenuItem.Size = new System.Drawing.Size(170, 22);
            this.backgroundToolStripMenuItem.Text = "Background Color";
            this.backgroundToolStripMenuItem.Click += new System.EventHandler(this.backgroundToolStripMenuItem_Click);
            // 
            // Sprite2DEditorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(854, 537);
            this.Controls.Add(this.menuStrip1);
            this.Controls.Add(this.splitContainer1);
            this.KeyPreview = true;
            this.Name = "Sprite2DEditorForm";
            this.Text = "Sprite 2D Editor";
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Sprite2DEditorForm_KeyDown);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.Sprite2DEditorForm_KeyUp);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.tabPage3.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.splitContainerCollision.Panel1.ResumeLayout(false);
            this.splitContainerCollision.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerCollision)).EndInit();
            this.splitContainerCollision.ResumeLayout(false);
            this.contextMenuStripTreeView.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }


        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Panel panelXna;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem optionToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem backgroundToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editionToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem undoToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem redoToolStripMenuItem;
        private System.Windows.Forms.ContextMenuStrip contextMenuStripTreeView;
        private System.Windows.Forms.ToolStripMenuItem copyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pasteToolStripMenuItem;
        private System.Windows.Forms.SplitContainer splitContainerCollision;
        private System.Windows.Forms.Button buttonAddLine;
        private System.Windows.Forms.Button buttonAddRectangle;
        private System.Windows.Forms.Button buttonAddPoly;
        private System.Windows.Forms.Button buttonAddCircle;
        private System.Windows.Forms.Button buttonAddColl;
        private System.Windows.Forms.ListBox listBoxCollision;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Button buttonDelColl;
        private System.Windows.Forms.PropertyGrid propertyGridCollision;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.Button buttonAddSocket;
        private System.Windows.Forms.ListBox listBoxSockets;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button buttonDelSocket;
        private System.Windows.Forms.PropertyGrid propertyGridSocket;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.PropertyGrid propertyGridSprite2D;
        private System.Windows.Forms.ToolStripSeparator dToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem couperToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem pasteToolStripMenuItem1;
        private System.Windows.Forms.ComboBox comboBoxSelectedSprite2D;
        private System.Windows.Forms.Label label1;
    }
}