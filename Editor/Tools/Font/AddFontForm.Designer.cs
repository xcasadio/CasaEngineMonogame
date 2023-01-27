namespace Editor.Tools.Font
{
    partial class AddFontForm
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
            this.buttonOK = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.textBoxProjectPath = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.buttonChoosePackage = new System.Windows.Forms.Button();
            this.buttonBmFile = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.textBoxBmFile = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // buttonOK
            // 
            this.buttonOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonOK.Location = new System.Drawing.Point(429, 94);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new System.Drawing.Size(75, 23);
            this.buttonOK.TabIndex = 0;
            this.buttonOK.Text = "OK";
            this.buttonOK.UseVisualStyleBackColor = true;
            this.buttonOK.Click += new System.EventHandler(this.buttonOK_Click);
            // 
            // buttonCancel
            // 
            this.buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.Location = new System.Drawing.Point(510, 94);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(75, 23);
            this.buttonCancel.TabIndex = 1;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            // 
            // textBoxProjectPath
            // 
            this.textBoxProjectPath.Location = new System.Drawing.Point(83, 31);
            this.textBoxProjectPath.Name = "textBoxProjectPath";
            this.textBoxProjectPath.Size = new System.Drawing.Size(464, 20);
            this.textBoxProjectPath.TabIndex = 2;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 34);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(64, 13);
            this.label1.TabIndex = 3;
            this.label1.Text = "Project path";
            // 
            // buttonChoosePackage
            // 
            this.buttonChoosePackage.Location = new System.Drawing.Point(553, 29);
            this.buttonChoosePackage.Name = "buttonChoosePackage";
            this.buttonChoosePackage.Size = new System.Drawing.Size(32, 23);
            this.buttonChoosePackage.TabIndex = 4;
            this.buttonChoosePackage.Text = "...";
            this.buttonChoosePackage.UseVisualStyleBackColor = true;
            // 
            // buttonBmFile
            // 
            this.buttonBmFile.Location = new System.Drawing.Point(553, 56);
            this.buttonBmFile.Name = "buttonBmFile";
            this.buttonBmFile.Size = new System.Drawing.Size(32, 23);
            this.buttonBmFile.TabIndex = 7;
            this.buttonBmFile.Text = "...";
            this.buttonBmFile.UseVisualStyleBackColor = true;
            this.buttonBmFile.Click += new System.EventHandler(this.buttonBmFile_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 61);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(66, 13);
            this.label2.TabIndex = 6;
            this.label2.Text = "File to import";
            // 
            // textBoxBmFile
            // 
            this.textBoxBmFile.Location = new System.Drawing.Point(83, 58);
            this.textBoxBmFile.Name = "textBoxBmFile";
            this.textBoxBmFile.ReadOnly = true;
            this.textBoxBmFile.Size = new System.Drawing.Size(464, 20);
            this.textBoxBmFile.TabIndex = 5;
            // 
            // AddFontForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.buttonCancel;
            this.ClientSize = new System.Drawing.Size(597, 129);
            this.Controls.Add(this.buttonBmFile);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textBoxBmFile);
            this.Controls.Add(this.buttonChoosePackage);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.textBoxProjectPath);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonOK);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "AddFontForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Create a Font";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button buttonOK;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.TextBox textBoxProjectPath;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button buttonChoosePackage;
        private System.Windows.Forms.Button buttonBmFile;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textBoxBmFile;
    }
}