namespace Editor.Sprite2DEditor.Event
{
    partial class AnimationListEventForm
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
            this.buttonCancel = new System.Windows.Forms.Button();
            this.buttonOK = new System.Windows.Forms.Button();
            this.listBoxEvent = new System.Windows.Forms.ListBox();
            this.label2 = new System.Windows.Forms.Label();
            this.buttonAddEvent = new System.Windows.Forms.Button();
            this.buttonDeleteEvent = new System.Windows.Forms.Button();
            this.buttonModifyEvent = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // buttonCancel
            // 
            this.buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.Location = new System.Drawing.Point(166, 270);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(75, 23);
            this.buttonCancel.TabIndex = 2;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            // 
            // buttonOK
            // 
            this.buttonOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.buttonOK.Location = new System.Drawing.Point(85, 270);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new System.Drawing.Size(75, 23);
            this.buttonOK.TabIndex = 3;
            this.buttonOK.Text = "OK";
            this.buttonOK.UseVisualStyleBackColor = true;
            // 
            // listBoxEvent
            // 
            this.listBoxEvent.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.listBoxEvent.FormattingEnabled = true;
            this.listBoxEvent.Location = new System.Drawing.Point(12, 25);
            this.listBoxEvent.Name = "listBoxEvent";
            this.listBoxEvent.Size = new System.Drawing.Size(199, 238);
            this.listBoxEvent.TabIndex = 5;
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.Location = new System.Drawing.Point(12, 9);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(229, 13);
            this.label2.TabIndex = 6;
            this.label2.Text = "List of events";
            this.label2.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // buttonAddEvent
            // 
            this.buttonAddEvent.Image = global::Editor.Properties.Resources.icon_Plus_16x16;
            this.buttonAddEvent.ImageAlign = System.Drawing.ContentAlignment.BottomRight;
            this.buttonAddEvent.Location = new System.Drawing.Point(217, 25);
            this.buttonAddEvent.Name = "buttonAddEvent";
            this.buttonAddEvent.Size = new System.Drawing.Size(24, 24);
            this.buttonAddEvent.TabIndex = 7;
            this.buttonAddEvent.UseVisualStyleBackColor = true;
            this.buttonAddEvent.Click += new System.EventHandler(this.buttonAddEvent_Click);
            // 
            // buttonDeleteEvent
            // 
            this.buttonDeleteEvent.Image = global::Editor.Properties.Resources.icon_Minus_16x16;
            this.buttonDeleteEvent.ImageAlign = System.Drawing.ContentAlignment.BottomRight;
            this.buttonDeleteEvent.Location = new System.Drawing.Point(217, 85);
            this.buttonDeleteEvent.Name = "buttonDeleteEvent";
            this.buttonDeleteEvent.Size = new System.Drawing.Size(24, 24);
            this.buttonDeleteEvent.TabIndex = 8;
            this.buttonDeleteEvent.UseVisualStyleBackColor = true;
            this.buttonDeleteEvent.Click += new System.EventHandler(this.buttonDeleteEvent_Click);
            // 
            // buttonModifyEvent
            // 
            this.buttonModifyEvent.Image = global::Editor.Properties.Resources.icon_edit_16x16;
            this.buttonModifyEvent.ImageAlign = System.Drawing.ContentAlignment.BottomRight;
            this.buttonModifyEvent.Location = new System.Drawing.Point(217, 55);
            this.buttonModifyEvent.Name = "buttonModifyEvent";
            this.buttonModifyEvent.Size = new System.Drawing.Size(24, 24);
            this.buttonModifyEvent.TabIndex = 9;
            this.buttonModifyEvent.UseVisualStyleBackColor = true;
            this.buttonModifyEvent.Click += new System.EventHandler(this.buttonModifyEvent_Click);
            // 
            // AnimationListEventForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(253, 305);
            this.Controls.Add(this.buttonModifyEvent);
            this.Controls.Add(this.buttonDeleteEvent);
            this.Controls.Add(this.buttonAddEvent);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.listBoxEvent);
            this.Controls.Add(this.buttonOK);
            this.Controls.Add(this.buttonCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Name = "AnimationListEventForm";
            this.ShowIcon = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Animation Event Editor";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.Button buttonOK;
        private System.Windows.Forms.ListBox listBoxEvent;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button buttonAddEvent;
        private System.Windows.Forms.Button buttonDeleteEvent;
        private System.Windows.Forms.Button buttonModifyEvent;
    }
}