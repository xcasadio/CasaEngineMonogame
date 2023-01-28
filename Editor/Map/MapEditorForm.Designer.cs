namespace Editor.Map
{
    partial class MapEditorForm
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
            this.buttonAddSpriteSheet = new System.Windows.Forms.Button();
            this.buttonDeleteSpriteSheet = new System.Windows.Forms.Button();
            this.listBoxObjects = new System.Windows.Forms.ListBox();
            this.label1 = new System.Windows.Forms.Label();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.propertyGrid2 = new System.Windows.Forms.PropertyGrid();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPageWorld = new System.Windows.Forms.TabPage();
            this.propertyGrid1 = new System.Windows.Forms.PropertyGrid();
            this.buttonAddMap = new System.Windows.Forms.Button();
            this.buttonDeleteMap = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.listBoxMapList = new System.Windows.Forms.ListBox();
            this.tabPageEditor = new System.Windows.Forms.TabPage();
            this.tabPageLayer = new System.Windows.Forms.TabPage();
            this.label2 = new System.Windows.Forms.Label();
            this.button1 = new System.Windows.Forms.Button();
            this.listBoxLayers = new System.Windows.Forms.ListBox();
            this.button2 = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabPageWorld.SuspendLayout();
            this.tabPageEditor.SuspendLayout();
            this.tabPageLayer.SuspendLayout();
            this.SuspendLayout();
            // 
            // buttonAddSpriteSheet
            // 
            this.buttonAddSpriteSheet.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonAddSpriteSheet.Image = global::Editor.Properties.Resources.icon_Plus_16x16;
            this.buttonAddSpriteSheet.Location = new System.Drawing.Point(252, 20);
            this.buttonAddSpriteSheet.Name = "buttonAddSpriteSheet";
            this.buttonAddSpriteSheet.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.buttonAddSpriteSheet.Size = new System.Drawing.Size(26, 26);
            this.buttonAddSpriteSheet.TabIndex = 21;
            this.buttonAddSpriteSheet.UseVisualStyleBackColor = true;
            // 
            // buttonDeleteSpriteSheet
            // 
            this.buttonDeleteSpriteSheet.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonDeleteSpriteSheet.Enabled = false;
            this.buttonDeleteSpriteSheet.Image = global::Editor.Properties.Resources.icon_Minus_16x16;
            this.buttonDeleteSpriteSheet.Location = new System.Drawing.Point(252, 52);
            this.buttonDeleteSpriteSheet.Name = "buttonDeleteSpriteSheet";
            this.buttonDeleteSpriteSheet.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.buttonDeleteSpriteSheet.Size = new System.Drawing.Size(26, 26);
            this.buttonDeleteSpriteSheet.TabIndex = 22;
            this.buttonDeleteSpriteSheet.UseVisualStyleBackColor = true;
            // 
            // listBoxObjects
            // 
            this.listBoxObjects.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listBoxObjects.FormattingEnabled = true;
            this.listBoxObjects.Location = new System.Drawing.Point(3, 20);
            this.listBoxObjects.Name = "listBoxObjects";
            this.listBoxObjects.Size = new System.Drawing.Size(243, 173);
            this.listBoxObjects.TabIndex = 17;
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.Location = new System.Drawing.Point(3, 4);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(243, 13);
            this.label1.TabIndex = 18;
            this.label1.Text = "List of objects";
            this.label1.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(3, 3);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.listBoxObjects);
            this.splitContainer1.Panel1.Controls.Add(this.label1);
            this.splitContainer1.Panel1.Controls.Add(this.buttonDeleteSpriteSheet);
            this.splitContainer1.Panel1.Controls.Add(this.buttonAddSpriteSheet);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.propertyGrid2);
            this.splitContainer1.Size = new System.Drawing.Size(281, 463);
            this.splitContainer1.SplitterDistance = 198;
            this.splitContainer1.TabIndex = 25;
            // 
            // propertyGrid2
            // 
            this.propertyGrid2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.propertyGrid2.Location = new System.Drawing.Point(0, 0);
            this.propertyGrid2.Name = "propertyGrid2";
            this.propertyGrid2.Size = new System.Drawing.Size(281, 261);
            this.propertyGrid2.TabIndex = 0;
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPageWorld);
            this.tabControl1.Controls.Add(this.tabPageEditor);
            this.tabControl1.Controls.Add(this.tabPageLayer);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(295, 495);
            this.tabControl1.TabIndex = 23;
            // 
            // tabPageWorld
            // 
            this.tabPageWorld.Controls.Add(this.propertyGrid1);
            this.tabPageWorld.Controls.Add(this.buttonAddMap);
            this.tabPageWorld.Controls.Add(this.buttonDeleteMap);
            this.tabPageWorld.Controls.Add(this.label3);
            this.tabPageWorld.Controls.Add(this.listBoxMapList);
            this.tabPageWorld.Location = new System.Drawing.Point(4, 22);
            this.tabPageWorld.Name = "tabPageWorld";
            this.tabPageWorld.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageWorld.Size = new System.Drawing.Size(287, 469);
            this.tabPageWorld.TabIndex = 1;
            this.tabPageWorld.Text = "Worlds";
            this.tabPageWorld.UseVisualStyleBackColor = true;
            // 
            // propertyGrid1
            // 
            this.propertyGrid1.Location = new System.Drawing.Point(3, 239);
            this.propertyGrid1.Name = "propertyGrid1";
            this.propertyGrid1.Size = new System.Drawing.Size(281, 227);
            this.propertyGrid1.TabIndex = 41;
            // 
            // buttonAddMap
            // 
            this.buttonAddMap.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonAddMap.Image = global::Editor.Properties.Resources.icon_Plus_16x16;
            this.buttonAddMap.Location = new System.Drawing.Point(253, 21);
            this.buttonAddMap.Name = "buttonAddMap";
            this.buttonAddMap.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.buttonAddMap.Size = new System.Drawing.Size(26, 26);
            this.buttonAddMap.TabIndex = 39;
            this.buttonAddMap.UseVisualStyleBackColor = true;
            // 
            // buttonDeleteMap
            // 
            this.buttonDeleteMap.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonDeleteMap.Enabled = false;
            this.buttonDeleteMap.Image = global::Editor.Properties.Resources.icon_Minus_16x16;
            this.buttonDeleteMap.Location = new System.Drawing.Point(253, 53);
            this.buttonDeleteMap.Name = "buttonDeleteMap";
            this.buttonDeleteMap.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.buttonDeleteMap.Size = new System.Drawing.Size(26, 26);
            this.buttonDeleteMap.TabIndex = 40;
            this.buttonDeleteMap.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            this.label3.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label3.Location = new System.Drawing.Point(3, 3);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(244, 15);
            this.label3.TabIndex = 38;
            this.label3.Text = "List of map";
            this.label3.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // listBoxMapList
            // 
            this.listBoxMapList.FormattingEnabled = true;
            this.listBoxMapList.Location = new System.Drawing.Point(3, 21);
            this.listBoxMapList.Name = "listBoxMapList";
            this.listBoxMapList.Size = new System.Drawing.Size(244, 212);
            this.listBoxMapList.TabIndex = 37;
            // 
            // tabPageEditor
            // 
            this.tabPageEditor.Controls.Add(this.splitContainer1);
            this.tabPageEditor.Location = new System.Drawing.Point(4, 22);
            this.tabPageEditor.Name = "tabPageEditor";
            this.tabPageEditor.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageEditor.Size = new System.Drawing.Size(287, 469);
            this.tabPageEditor.TabIndex = 0;
            this.tabPageEditor.Text = "Editor";
            this.tabPageEditor.UseVisualStyleBackColor = true;
            // 
            // tabPageLayer
            // 
            this.tabPageLayer.Controls.Add(this.label2);
            this.tabPageLayer.Controls.Add(this.button1);
            this.tabPageLayer.Controls.Add(this.listBoxLayers);
            this.tabPageLayer.Controls.Add(this.button2);
            this.tabPageLayer.Location = new System.Drawing.Point(4, 22);
            this.tabPageLayer.Name = "tabPageLayer";
            this.tabPageLayer.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageLayer.Size = new System.Drawing.Size(287, 469);
            this.tabPageLayer.TabIndex = 2;
            this.tabPageLayer.Text = "Layers";
            this.tabPageLayer.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.Location = new System.Drawing.Point(3, 3);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(243, 13);
            this.label2.TabIndex = 26;
            this.label2.Text = "List of layers";
            this.label2.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // button1
            // 
            this.button1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.button1.Image = global::Editor.Properties.Resources.icon_Plus_16x16;
            this.button1.Location = new System.Drawing.Point(251, 19);
            this.button1.Name = "button1";
            this.button1.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.button1.Size = new System.Drawing.Size(26, 26);
            this.button1.TabIndex = 27;
            this.button1.UseVisualStyleBackColor = true;
            // 
            // listBoxLayers
            // 
            this.listBoxLayers.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listBoxLayers.FormattingEnabled = true;
            this.listBoxLayers.Location = new System.Drawing.Point(3, 19);
            this.listBoxLayers.Name = "listBoxLayers";
            this.listBoxLayers.Size = new System.Drawing.Size(242, 212);
            this.listBoxLayers.TabIndex = 25;
            // 
            // button2
            // 
            this.button2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.button2.Enabled = false;
            this.button2.Image = global::Editor.Properties.Resources.icon_Minus_16x16;
            this.button2.Location = new System.Drawing.Point(251, 51);
            this.button2.Name = "button2";
            this.button2.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.button2.Size = new System.Drawing.Size(26, 26);
            this.button2.TabIndex = 28;
            this.button2.UseVisualStyleBackColor = true;
            // 
            // MapEditorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(295, 495);
            this.Controls.Add(this.tabControl1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Name = "MapEditorForm";
            this.Text = "Map Editor";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.tabPageWorld.ResumeLayout(false);
            this.tabPageEditor.ResumeLayout(false);
            this.tabPageLayer.ResumeLayout(false);
            this.ResumeLayout(false);

        }


        private System.Windows.Forms.Button buttonAddSpriteSheet;
        private System.Windows.Forms.Button buttonDeleteSpriteSheet;
        private System.Windows.Forms.ListBox listBoxObjects;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPageWorld;
        private System.Windows.Forms.TabPage tabPageEditor;
        private System.Windows.Forms.PropertyGrid propertyGrid2;
        private System.Windows.Forms.PropertyGrid propertyGrid1;
        private System.Windows.Forms.Button buttonAddMap;
        private System.Windows.Forms.Button buttonDeleteMap;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ListBox listBoxMapList;
        private System.Windows.Forms.TabPage tabPageLayer;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.ListBox listBoxLayers;
        private System.Windows.Forms.Button button2;

    }
}