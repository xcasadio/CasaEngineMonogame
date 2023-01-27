namespace Editor.SoundEditor
{
    partial class SoundEditorForm
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
            this.listBoxSoundAsset = new System.Windows.Forms.ListBox();
            this.buttonBuildNewAsset = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // listBoxSoundAsset
            // 
            this.listBoxSoundAsset.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listBoxSoundAsset.FormattingEnabled = true;
            this.listBoxSoundAsset.Location = new System.Drawing.Point(12, 24);
            this.listBoxSoundAsset.Name = "listBoxSoundAsset";
            this.listBoxSoundAsset.Size = new System.Drawing.Size(249, 290);
            this.listBoxSoundAsset.TabIndex = 0;
            // 
            // buttonBuildNewAsset
            // 
            this.buttonBuildNewAsset.Image = global::Editor.Properties.Resources.icon_Plus_16x16;
            this.buttonBuildNewAsset.Location = new System.Drawing.Point(267, 24);
            this.buttonBuildNewAsset.Name = "buttonBuildNewAsset";
            this.buttonBuildNewAsset.Size = new System.Drawing.Size(26, 26);
            this.buttonBuildNewAsset.TabIndex = 1;
            this.buttonBuildNewAsset.UseVisualStyleBackColor = true;
            this.buttonBuildNewAsset.Click += new System.EventHandler(this.buttonBuildNewAsset_Click);
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.Location = new System.Drawing.Point(12, 8);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(249, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "sound asset";
            this.label1.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // SoundEditorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(305, 326);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.buttonBuildNewAsset);
            this.Controls.Add(this.listBoxSoundAsset);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Name = "SoundEditorForm";
            this.Text = "SoundEditorForm";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListBox listBoxSoundAsset;
        private System.Windows.Forms.Button buttonBuildNewAsset;
        private System.Windows.Forms.Label label1;
    }
}