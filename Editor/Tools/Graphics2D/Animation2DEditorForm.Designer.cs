namespace Editor.Tools.Graphics2D
{
    partial class Animation2DEditorForm
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.splitContainerCentral = new System.Windows.Forms.SplitContainer();
            this.splitContainerAnimation2DCentral = new System.Windows.Forms.SplitContainer();
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.buttonModifyDelay = new System.Windows.Forms.Button();
            this.buttonEventForListViewEx = new System.Windows.Forms.Button();
            this.numericUpDownFrameDelayForListViewEx = new System.Windows.Forms.NumericUpDown();
            this.comboBoxFrameSprite2DName = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.listViewFrame = new Editor.WinForm.ListViewEx();
            this.columnHeader3 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader4 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.label4 = new System.Windows.Forms.Label();
            this.buttonAddFrame = new System.Windows.Forms.Button();
            this.buttonDeleteFrame = new System.Windows.Forms.Button();
            this.buttonFrameUp = new System.Windows.Forms.Button();
            this.buttonFrameDown = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.hScrollBarCurrentFrame = new System.Windows.Forms.HScrollBar();
            this.buttonPlay = new System.Windows.Forms.Button();
            this.labelCurrentFrame2 = new System.Windows.Forms.Label();
            this.propertyGridSprite2D = new System.Windows.Forms.PropertyGrid();
            this.splitContainerRight = new System.Windows.Forms.SplitContainer();
            this.checkBoxShowPreviousFrame = new System.Windows.Forms.CheckBox();
            this.panelXna = new System.Windows.Forms.Panel();
            this.curveControl1 = new Editor.Tools.CurveEditor.CurveControl();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.editionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.undoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.redoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.optionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.backgroundToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.checkBoxShowSpriteOrigin = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerCentral)).BeginInit();
            this.splitContainerCentral.Panel1.SuspendLayout();
            this.splitContainerCentral.Panel2.SuspendLayout();
            this.splitContainerCentral.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerAnimation2DCentral)).BeginInit();
            this.splitContainerAnimation2DCentral.Panel1.SuspendLayout();
            this.splitContainerAnimation2DCentral.Panel2.SuspendLayout();
            this.splitContainerAnimation2DCentral.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownFrameDelayForListViewEx)).BeginInit();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerRight)).BeginInit();
            this.splitContainerRight.Panel1.SuspendLayout();
            this.splitContainerRight.Panel2.SuspendLayout();
            this.splitContainerRight.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainerCentral
            // 
            this.splitContainerCentral.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerCentral.Location = new System.Drawing.Point(0, 24);
            this.splitContainerCentral.Name = "splitContainerCentral";
            // 
            // splitContainerCentral.Panel1
            // 
            this.splitContainerCentral.Panel1.Controls.Add(this.splitContainerAnimation2DCentral);
            // 
            // splitContainerCentral.Panel2
            // 
            this.splitContainerCentral.Panel2.Controls.Add(this.splitContainerRight);
            this.splitContainerCentral.Size = new System.Drawing.Size(952, 697);
            this.splitContainerCentral.SplitterDistance = 357;
            this.splitContainerCentral.TabIndex = 0;
            // 
            // splitContainerAnimation2DCentral
            // 
            this.splitContainerAnimation2DCentral.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.splitContainerAnimation2DCentral.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerAnimation2DCentral.Location = new System.Drawing.Point(0, 0);
            this.splitContainerAnimation2DCentral.Name = "splitContainerAnimation2DCentral";
            this.splitContainerAnimation2DCentral.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainerAnimation2DCentral.Panel1
            // 
            this.splitContainerAnimation2DCentral.Panel1.Controls.Add(this.comboBox1);
            this.splitContainerAnimation2DCentral.Panel1.Controls.Add(this.buttonModifyDelay);
            this.splitContainerAnimation2DCentral.Panel1.Controls.Add(this.buttonEventForListViewEx);
            this.splitContainerAnimation2DCentral.Panel1.Controls.Add(this.numericUpDownFrameDelayForListViewEx);
            this.splitContainerAnimation2DCentral.Panel1.Controls.Add(this.comboBoxFrameSprite2DName);
            this.splitContainerAnimation2DCentral.Panel1.Controls.Add(this.label3);
            this.splitContainerAnimation2DCentral.Panel1.Controls.Add(this.listViewFrame);
            this.splitContainerAnimation2DCentral.Panel1.Controls.Add(this.label4);
            this.splitContainerAnimation2DCentral.Panel1.Controls.Add(this.buttonAddFrame);
            this.splitContainerAnimation2DCentral.Panel1.Controls.Add(this.buttonDeleteFrame);
            this.splitContainerAnimation2DCentral.Panel1.Controls.Add(this.buttonFrameUp);
            this.splitContainerAnimation2DCentral.Panel1.Controls.Add(this.buttonFrameDown);
            this.splitContainerAnimation2DCentral.Panel1.Controls.Add(this.groupBox1);
            // 
            // splitContainerAnimation2DCentral.Panel2
            // 
            this.splitContainerAnimation2DCentral.Panel2.Controls.Add(this.propertyGridSprite2D);
            this.splitContainerAnimation2DCentral.Size = new System.Drawing.Size(357, 697);
            this.splitContainerAnimation2DCentral.SplitterDistance = 475;
            this.splitContainerAnimation2DCentral.TabIndex = 34;
            // 
            // comboBox1
            // 
            this.comboBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Location = new System.Drawing.Point(98, 3);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(250, 21);
            this.comboBox1.TabIndex = 45;
            // 
            // buttonModifyDelay
            // 
            this.buttonModifyDelay.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonModifyDelay.Image = global::Editor.Properties.Resources.icon_edit_16x16;
            this.buttonModifyDelay.Location = new System.Drawing.Point(322, 180);
            this.buttonModifyDelay.Name = "buttonModifyDelay";
            this.buttonModifyDelay.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.buttonModifyDelay.Size = new System.Drawing.Size(26, 26);
            this.buttonModifyDelay.TabIndex = 43;
            this.buttonModifyDelay.UseVisualStyleBackColor = true;
            this.buttonModifyDelay.Click += new System.EventHandler(this.buttonModifyDelay_Click);
            // 
            // buttonEventForListViewEx
            // 
            this.buttonEventForListViewEx.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.buttonEventForListViewEx.Font = new System.Drawing.Font("Lucida Fax", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonEventForListViewEx.Location = new System.Drawing.Point(136, 337);
            this.buttonEventForListViewEx.Name = "buttonEventForListViewEx";
            this.buttonEventForListViewEx.Size = new System.Drawing.Size(75, 16);
            this.buttonEventForListViewEx.TabIndex = 44;
            this.buttonEventForListViewEx.Text = "button1";
            this.buttonEventForListViewEx.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            this.buttonEventForListViewEx.UseVisualStyleBackColor = true;
            this.buttonEventForListViewEx.Visible = false;
            this.buttonEventForListViewEx.Click += new System.EventHandler(this.buttonEventForListViewEx_Click);
            // 
            // numericUpDownFrameDelayForListViewEx
            // 
            this.numericUpDownFrameDelayForListViewEx.DecimalPlaces = 3;
            this.numericUpDownFrameDelayForListViewEx.Increment = new decimal(new int[] {
            1,
            0,
            0,
            196608});
            this.numericUpDownFrameDelayForListViewEx.Location = new System.Drawing.Point(136, 313);
            this.numericUpDownFrameDelayForListViewEx.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            262144});
            this.numericUpDownFrameDelayForListViewEx.Name = "numericUpDownFrameDelayForListViewEx";
            this.numericUpDownFrameDelayForListViewEx.Size = new System.Drawing.Size(108, 20);
            this.numericUpDownFrameDelayForListViewEx.TabIndex = 41;
            this.numericUpDownFrameDelayForListViewEx.ThousandsSeparator = true;
            this.numericUpDownFrameDelayForListViewEx.Value = new decimal(new int[] {
            1,
            0,
            0,
            196608});
            this.numericUpDownFrameDelayForListViewEx.Visible = false;
            // 
            // comboBoxFrameSprite2DName
            // 
            this.comboBoxFrameSprite2DName.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Append;
            this.comboBoxFrameSprite2DName.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.comboBoxFrameSprite2DName.FormattingEnabled = true;
            this.comboBoxFrameSprite2DName.ItemHeight = 13;
            this.comboBoxFrameSprite2DName.Location = new System.Drawing.Point(136, 285);
            this.comboBoxFrameSprite2DName.Name = "comboBoxFrameSprite2DName";
            this.comboBoxFrameSprite2DName.Size = new System.Drawing.Size(121, 21);
            this.comboBoxFrameSprite2DName.TabIndex = 38;
            this.comboBoxFrameSprite2DName.Visible = false;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(4, 6);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(88, 13);
            this.label3.TabIndex = 14;
            this.label3.Text = "List of animations";
            this.label3.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // listViewFrame
            // 
            this.listViewFrame.AllowColumnReorder = true;
            this.listViewFrame.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listViewFrame.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader3,
            this.columnHeader1,
            this.columnHeader2,
            this.columnHeader4});
            this.listViewFrame.DoubleClickActivation = true;
            this.listViewFrame.FullRowSelect = true;
            this.listViewFrame.GridLines = true;
            this.listViewFrame.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.listViewFrame.HideSelection = false;
            this.listViewFrame.Location = new System.Drawing.Point(3, 52);
            this.listViewFrame.Name = "listViewFrame";
            this.listViewFrame.ShowGroups = false;
            this.listViewFrame.Size = new System.Drawing.Size(313, 359);
            this.listViewFrame.TabIndex = 42;
            this.listViewFrame.UseCompatibleStateImageBehavior = false;
            this.listViewFrame.View = System.Windows.Forms.View.Details;
            this.listViewFrame.SelectedIndexChanged += new System.EventHandler(this.listViewFrame_SelectedIndexChanged);
            // 
            // columnHeader3
            // 
            this.columnHeader3.Text = "Index";
            this.columnHeader3.Width = 38;
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "Sprite 2D";
            this.columnHeader1.Width = 131;
            // 
            // columnHeader2
            // 
            this.columnHeader2.Text = "Delay";
            this.columnHeader2.Width = 52;
            // 
            // columnHeader4
            // 
            this.columnHeader4.Text = "Event";
            // 
            // label4
            // 
            this.label4.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label4.Location = new System.Drawing.Point(2, 36);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(314, 13);
            this.label4.TabIndex = 37;
            this.label4.Text = "List of frames";
            this.label4.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // buttonAddFrame
            // 
            this.buttonAddFrame.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonAddFrame.Image = global::Editor.Properties.Resources.icon_Plus_16x16;
            this.buttonAddFrame.Location = new System.Drawing.Point(322, 52);
            this.buttonAddFrame.Name = "buttonAddFrame";
            this.buttonAddFrame.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.buttonAddFrame.Size = new System.Drawing.Size(26, 26);
            this.buttonAddFrame.TabIndex = 35;
            this.buttonAddFrame.UseVisualStyleBackColor = true;
            this.buttonAddFrame.Click += new System.EventHandler(this.buttonAddFrame_Click);
            // 
            // buttonDeleteFrame
            // 
            this.buttonDeleteFrame.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonDeleteFrame.Image = global::Editor.Properties.Resources.icon_Minus_16x16;
            this.buttonDeleteFrame.Location = new System.Drawing.Point(322, 84);
            this.buttonDeleteFrame.Name = "buttonDeleteFrame";
            this.buttonDeleteFrame.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.buttonDeleteFrame.Size = new System.Drawing.Size(26, 26);
            this.buttonDeleteFrame.TabIndex = 36;
            this.buttonDeleteFrame.UseVisualStyleBackColor = true;
            this.buttonDeleteFrame.Click += new System.EventHandler(this.buttonDeleteFrame_Click);
            // 
            // buttonFrameUp
            // 
            this.buttonFrameUp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonFrameUp.Image = global::Editor.Properties.Resources.icon_arrowUp_16x16;
            this.buttonFrameUp.Location = new System.Drawing.Point(322, 116);
            this.buttonFrameUp.Name = "buttonFrameUp";
            this.buttonFrameUp.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.buttonFrameUp.Size = new System.Drawing.Size(26, 26);
            this.buttonFrameUp.TabIndex = 39;
            this.buttonFrameUp.UseVisualStyleBackColor = true;
            this.buttonFrameUp.Click += new System.EventHandler(this.buttonFrameUp_Click);
            // 
            // buttonFrameDown
            // 
            this.buttonFrameDown.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonFrameDown.Image = global::Editor.Properties.Resources.icon_arrowDown_16x16;
            this.buttonFrameDown.Location = new System.Drawing.Point(322, 148);
            this.buttonFrameDown.Name = "buttonFrameDown";
            this.buttonFrameDown.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.buttonFrameDown.Size = new System.Drawing.Size(26, 26);
            this.buttonFrameDown.TabIndex = 40;
            this.buttonFrameDown.UseVisualStyleBackColor = true;
            this.buttonFrameDown.Click += new System.EventHandler(this.buttonFrameDown_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.hScrollBarCurrentFrame);
            this.groupBox1.Controls.Add(this.buttonPlay);
            this.groupBox1.Controls.Add(this.labelCurrentFrame2);
            this.groupBox1.Location = new System.Drawing.Point(5, 417);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(347, 51);
            this.groupBox1.TabIndex = 17;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Player";
            // 
            // hScrollBarCurrentFrame
            // 
            this.hScrollBarCurrentFrame.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.hScrollBarCurrentFrame.Cursor = System.Windows.Forms.Cursors.Default;
            this.hScrollBarCurrentFrame.LargeChange = 1;
            this.hScrollBarCurrentFrame.Location = new System.Drawing.Point(49, 27);
            this.hScrollBarCurrentFrame.Maximum = 0;
            this.hScrollBarCurrentFrame.Name = "hScrollBarCurrentFrame";
            this.hScrollBarCurrentFrame.Size = new System.Drawing.Size(294, 18);
            this.hScrollBarCurrentFrame.TabIndex = 0;
            this.hScrollBarCurrentFrame.Scroll += new System.Windows.Forms.ScrollEventHandler(this.hScrollBarCurrentFrame_Scroll);
            // 
            // buttonPlay
            // 
            this.buttonPlay.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.buttonPlay.Image = global::Editor.Properties.Resources.icon_play_16x16;
            this.buttonPlay.Location = new System.Drawing.Point(6, 19);
            this.buttonPlay.Name = "buttonPlay";
            this.buttonPlay.Size = new System.Drawing.Size(26, 26);
            this.buttonPlay.TabIndex = 11;
            this.buttonPlay.UseVisualStyleBackColor = true;
            this.buttonPlay.Click += new System.EventHandler(this.buttonPlay_Click);
            // 
            // labelCurrentFrame2
            // 
            this.labelCurrentFrame2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.labelCurrentFrame2.Location = new System.Drawing.Point(49, 12);
            this.labelCurrentFrame2.Name = "labelCurrentFrame2";
            this.labelCurrentFrame2.Size = new System.Drawing.Size(294, 13);
            this.labelCurrentFrame2.TabIndex = 15;
            this.labelCurrentFrame2.Text = "0 / 0";
            this.labelCurrentFrame2.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // propertyGridSprite2D
            // 
            this.propertyGridSprite2D.Dock = System.Windows.Forms.DockStyle.Fill;
            this.propertyGridSprite2D.Location = new System.Drawing.Point(0, 0);
            this.propertyGridSprite2D.Name = "propertyGridSprite2D";
            this.propertyGridSprite2D.Size = new System.Drawing.Size(355, 216);
            this.propertyGridSprite2D.TabIndex = 32;
            // 
            // splitContainerRight
            // 
            this.splitContainerRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerRight.Location = new System.Drawing.Point(0, 0);
            this.splitContainerRight.Name = "splitContainerRight";
            this.splitContainerRight.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainerRight.Panel1
            // 
            this.splitContainerRight.Panel1.Controls.Add(this.checkBoxShowSpriteOrigin);
            this.splitContainerRight.Panel1.Controls.Add(this.checkBoxShowPreviousFrame);
            this.splitContainerRight.Panel1.Controls.Add(this.panelXna);
            // 
            // splitContainerRight.Panel2
            // 
            this.splitContainerRight.Panel2.Controls.Add(this.curveControl1);
            this.splitContainerRight.Size = new System.Drawing.Size(591, 697);
            this.splitContainerRight.SplitterDistance = 431;
            this.splitContainerRight.TabIndex = 0;
            // 
            // checkBoxShowPreviousFrame
            // 
            this.checkBoxShowPreviousFrame.AutoSize = true;
            this.checkBoxShowPreviousFrame.Location = new System.Drawing.Point(3, 6);
            this.checkBoxShowPreviousFrame.Name = "checkBoxShowPreviousFrame";
            this.checkBoxShowPreviousFrame.Size = new System.Drawing.Size(125, 17);
            this.checkBoxShowPreviousFrame.TabIndex = 1;
            this.checkBoxShowPreviousFrame.Text = "Show previous frame";
            this.checkBoxShowPreviousFrame.UseVisualStyleBackColor = true;
            this.checkBoxShowPreviousFrame.CheckedChanged += new System.EventHandler(this.checkBoxShowPreviousFrame_CheckedChanged);
            // 
            // panelXna
            // 
            this.panelXna.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelXna.Location = new System.Drawing.Point(0, 29);
            this.panelXna.Name = "panelXna";
            this.panelXna.Size = new System.Drawing.Size(588, 402);
            this.panelXna.TabIndex = 0;
            // 
            // curveControl1
            // 
            this.curveControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.curveControl1.Location = new System.Drawing.Point(0, 0);
            this.curveControl1.Name = "curveControl1";
            this.curveControl1.Size = new System.Drawing.Size(591, 262);
            this.curveControl1.TabIndex = 0;
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.editionToolStripMenuItem,
            this.optionToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(952, 24);
            this.menuStrip1.TabIndex = 1;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // editionToolStripMenuItem
            // 
            this.editionToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.undoToolStripMenuItem,
            this.redoToolStripMenuItem});
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
            this.backgroundToolStripMenuItem.Size = new System.Drawing.Size(138, 22);
            this.backgroundToolStripMenuItem.Text = "Background";
            // 
            // checkBoxShowSpriteOrigin
            // 
            this.checkBoxShowSpriteOrigin.AutoSize = true;
            this.checkBoxShowSpriteOrigin.Location = new System.Drawing.Point(138, 6);
            this.checkBoxShowSpriteOrigin.Name = "checkBoxShowSpriteOrigin";
            this.checkBoxShowSpriteOrigin.Size = new System.Drawing.Size(109, 17);
            this.checkBoxShowSpriteOrigin.TabIndex = 2;
            this.checkBoxShowSpriteOrigin.Text = "Show sprite origin";
            this.checkBoxShowSpriteOrigin.UseVisualStyleBackColor = true;
            this.checkBoxShowSpriteOrigin.CheckedChanged += new System.EventHandler(this.checkBoxShowSpriteOrigin_CheckedChanged);
            // 
            // Animation2DEditorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(952, 721);
            this.Controls.Add(this.splitContainerCentral);
            this.Controls.Add(this.menuStrip1);
            this.Name = "Animation2DEditorForm";
            this.Text = "Animation 2D Editor";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Animation2DEditorFormClosed);
            this.Load += new System.EventHandler(this.Animation2DEditorForm_Load);
            this.splitContainerCentral.Panel1.ResumeLayout(false);
            this.splitContainerCentral.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerCentral)).EndInit();
            this.splitContainerCentral.ResumeLayout(false);
            this.splitContainerAnimation2DCentral.Panel1.ResumeLayout(false);
            this.splitContainerAnimation2DCentral.Panel1.PerformLayout();
            this.splitContainerAnimation2DCentral.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerAnimation2DCentral)).EndInit();
            this.splitContainerAnimation2DCentral.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownFrameDelayForListViewEx)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.splitContainerRight.Panel1.ResumeLayout(false);
            this.splitContainerRight.Panel1.PerformLayout();
            this.splitContainerRight.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerRight)).EndInit();
            this.splitContainerRight.ResumeLayout(false);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainerCentral;
        private System.Windows.Forms.SplitContainer splitContainerAnimation2DCentral;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.HScrollBar hScrollBarCurrentFrame;
        private System.Windows.Forms.Button buttonPlay;
        private System.Windows.Forms.Label labelCurrentFrame2;
        private System.Windows.Forms.PropertyGrid propertyGridSprite2D;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem editionToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem optionToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem backgroundToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem undoToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem redoToolStripMenuItem;
        private System.Windows.Forms.SplitContainer splitContainerRight;
        private Editor.Tools.CurveEditor.CurveControl curveControl1;
        private System.Windows.Forms.ComboBox comboBox1;
        private System.Windows.Forms.Button buttonModifyDelay;
        private System.Windows.Forms.Button buttonEventForListViewEx;
        private System.Windows.Forms.NumericUpDown numericUpDownFrameDelayForListViewEx;
        private System.Windows.Forms.ComboBox comboBoxFrameSprite2DName;
        private WinForm.ListViewEx listViewFrame;
        private System.Windows.Forms.ColumnHeader columnHeader3;
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.ColumnHeader columnHeader2;
        private System.Windows.Forms.ColumnHeader columnHeader4;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button buttonAddFrame;
        private System.Windows.Forms.Button buttonDeleteFrame;
        private System.Windows.Forms.Button buttonFrameUp;
        private System.Windows.Forms.Button buttonFrameDown;
        private System.Windows.Forms.Panel panelXna;
        private System.Windows.Forms.CheckBox checkBoxShowPreviousFrame;
        private System.Windows.Forms.CheckBox checkBoxShowSpriteOrigin;
    }
}