﻿namespace Editor.Tools.Font
{
    partial class FontPreviewForm
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
            this.panelXNA = new System.Windows.Forms.Panel();
            this.SuspendLayout();
            // 
            // panelXNA
            // 
            this.panelXNA.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelXNA.Location = new System.Drawing.Point(0, 0);
            this.panelXNA.Name = "panelXNA";
            this.panelXNA.Size = new System.Drawing.Size(650, 360);
            this.panelXNA.TabIndex = 0;
            // 
            // FontPreviewForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(650, 360);
            this.Controls.Add(this.panelXNA);
            this.Name = "FontPreviewForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Font Preview";
            this.ResumeLayout(false);

        }


        private System.Windows.Forms.Panel panelXNA;
    }
}