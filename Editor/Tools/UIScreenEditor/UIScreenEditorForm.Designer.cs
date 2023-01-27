namespace Editor.Tools.UIScreenEditor
{
    partial class UIScreenEditorForm
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
            this.splitContainerMain = new System.Windows.Forms.SplitContainer();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.comboBoxControls = new System.Windows.Forms.ComboBox();
            this.propertyGrid1 = new System.Windows.Forms.PropertyGrid();
            this.panelXna = new System.Windows.Forms.Panel();
            this.stackPanel1 = new Editor.WinForm.StackPanel();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).BeginInit();
            this.splitContainerMain.Panel1.SuspendLayout();
            this.splitContainerMain.Panel2.SuspendLayout();
            this.splitContainerMain.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainerMain
            // 
            this.splitContainerMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerMain.Location = new System.Drawing.Point(0, 0);
            this.splitContainerMain.Name = "splitContainerMain";
            // 
            // splitContainerMain.Panel1
            // 
            this.splitContainerMain.Panel1.Controls.Add(this.tabControl1);
            // 
            // splitContainerMain.Panel2
            // 
            this.splitContainerMain.Panel2.Controls.Add(this.panelXna);
            this.splitContainerMain.Size = new System.Drawing.Size(971, 670);
            this.splitContainerMain.SplitterDistance = 253;
            this.splitContainerMain.TabIndex = 0;
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(253, 670);
            this.tabControl1.TabIndex = 1;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.stackPanel1);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(245, 644);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Toolbox";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.comboBoxControls);
            this.tabPage2.Controls.Add(this.propertyGrid1);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(245, 644);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Properties";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // comboBoxControls
            // 
            this.comboBoxControls.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBoxControls.FormattingEnabled = true;
            this.comboBoxControls.Location = new System.Drawing.Point(54, 6);
            this.comboBoxControls.Name = "comboBoxControls";
            this.comboBoxControls.Size = new System.Drawing.Size(185, 21);
            this.comboBoxControls.TabIndex = 3;
            this.comboBoxControls.SelectedIndexChanged += new System.EventHandler(this.comboBoxControls_SelectedIndexChanged);
            // 
            // propertyGrid1
            // 
            this.propertyGrid1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.propertyGrid1.Location = new System.Drawing.Point(6, 37);
            this.propertyGrid1.Name = "propertyGrid1";
            this.propertyGrid1.Size = new System.Drawing.Size(233, 601);
            this.propertyGrid1.TabIndex = 2;
            // 
            // panelXna
            // 
            this.panelXna.BackColor = System.Drawing.Color.Gainsboro;
            this.panelXna.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelXna.Location = new System.Drawing.Point(0, 0);
            this.panelXna.Name = "panelXna";
            this.panelXna.Size = new System.Drawing.Size(714, 670);
            this.panelXna.TabIndex = 0;
            this.panelXna.MouseDown += new System.Windows.Forms.MouseEventHandler(this.panelXna_MouseDown);
            this.panelXna.MouseEnter += new System.EventHandler(this.panelXna_MouseEnter);
            this.panelXna.MouseLeave += new System.EventHandler(this.panelXna_MouseLeave);
            this.panelXna.MouseMove += new System.Windows.Forms.MouseEventHandler(this.panelXna_MouseMove);
            this.panelXna.MouseUp += new System.Windows.Forms.MouseEventHandler(this.panelXna_MouseUp);
            // 
            // stackPanel1
            // 
            this.stackPanel1.AutoSize = true;
            this.stackPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.stackPanel1.Location = new System.Drawing.Point(3, 3);
            this.stackPanel1.Name = "stackPanel1";
            this.stackPanel1.Size = new System.Drawing.Size(239, 638);
            this.stackPanel1.TabIndex = 0;
            // 
            // UIScreenEditorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(971, 670);
            this.Controls.Add(this.splitContainerMain);
            this.Name = "UIScreenEditorForm";
            this.Text = "ScreenEditorForm";
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ScreenEditorForm_KeyDown);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.ScreenEditorForm_KeyUp);
            this.splitContainerMain.Panel1.ResumeLayout(false);
            this.splitContainerMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).EndInit();
            this.splitContainerMain.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.tabPage2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainerMain;
        private System.Windows.Forms.Panel panelXna;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.ComboBox comboBoxControls;
        private System.Windows.Forms.PropertyGrid propertyGrid1;
        private WinForm.StackPanel stackPanel1;
    }
}