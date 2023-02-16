
/*
 * Copyright (c) 2009 Nick Gravelyn (nick@gravelyn.com), Markus Ewald (cygon@CasaEngine.org)
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a 
 * copy of this software and associated documentation files (the "Software"), 
 * to deal in the Software without restriction, including without limitation 
 * the rights to use, copy, modify, merge, publish, distribute, sublicense, 
 * and/or sell copies of the Software, and to permit persons to whom the Software 
 * is furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all 
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, 
 * INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A 
 * PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT 
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION 
 * OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE 
 * SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE. 
 * 
 */


namespace Editor.Sprite2DEditor.SpriteSheetPacker
{
	public partial class SpriteSheetPackerForm
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
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.removeImageBtn = new System.Windows.Forms.Button();
            this.addImageBtn = new System.Windows.Forms.Button();
            this.buildBtn = new System.Windows.Forms.Button();
            this.imageOpenFileDialog = new System.Windows.Forms.OpenFileDialog();
            this.clearBtn = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.paddingTxtBox = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.maxWidthTxtBox = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.maxHeightTxtBox = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.spriteSheetNameTxtBox = new System.Windows.Forms.TextBox();
            this.imageSaveFileDialog = new System.Windows.Forms.SaveFileDialog();
            this.mapSaveFileDialog = new System.Windows.Forms.SaveFileDialog();
            this.powOf2CheckBox = new System.Windows.Forms.CheckBox();
            this.squareCheckBox = new System.Windows.Forms.CheckBox();
            this.checkBoxDetectAnimations = new System.Windows.Forms.CheckBox();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.buttonAddDirectory = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // listBox1
            // 
            this.listBox1.AllowDrop = true;
            this.listBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listBox1.FormattingEnabled = true;
            this.listBox1.IntegralHeight = false;
            this.listBox1.Location = new System.Drawing.Point(9, 10);
            this.listBox1.Margin = new System.Windows.Forms.Padding(2);
            this.listBox1.Name = "listBox1";
            this.listBox1.SelectionMode = System.Windows.Forms.SelectionMode.MultiSimple;
            this.listBox1.Size = new System.Drawing.Size(529, 136);
            this.listBox1.TabIndex = 0;
            this.listBox1.TabStop = false;
            this.listBox1.DragDrop += new System.Windows.Forms.DragEventHandler(this.listBox1_DragDrop);
            this.listBox1.DragEnter += new System.Windows.Forms.DragEventHandler(this.listBox1_DragEnter);
            // 
            // removeImageBtn
            // 
            this.removeImageBtn.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.removeImageBtn.Location = new System.Drawing.Point(366, 150);
            this.removeImageBtn.Margin = new System.Windows.Forms.Padding(2);
            this.removeImageBtn.Name = "removeImageBtn";
            this.removeImageBtn.Size = new System.Drawing.Size(84, 41);
            this.removeImageBtn.TabIndex = 6;
            this.removeImageBtn.Text = "IsRemoved Selected";
            this.removeImageBtn.UseVisualStyleBackColor = true;
            this.removeImageBtn.Click += new System.EventHandler(this.removeImageBtn_Click);
            // 
            // addImageBtn
            // 
            this.addImageBtn.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.addImageBtn.Location = new System.Drawing.Point(278, 150);
            this.addImageBtn.Margin = new System.Windows.Forms.Padding(2);
            this.addImageBtn.Name = "addImageBtn";
            this.addImageBtn.Size = new System.Drawing.Size(84, 41);
            this.addImageBtn.TabIndex = 5;
            this.addImageBtn.Text = "Add Images";
            this.addImageBtn.UseVisualStyleBackColor = true;
            this.addImageBtn.Click += new System.EventHandler(this.addImageBtn_Click);
            // 
            // buildBtn
            // 
            this.buildBtn.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.buildBtn.Location = new System.Drawing.Point(366, 271);
            this.buildBtn.Margin = new System.Windows.Forms.Padding(2);
            this.buildBtn.Name = "buildBtn";
            this.buildBtn.Size = new System.Drawing.Size(172, 41);
            this.buildBtn.TabIndex = 12;
            this.buildBtn.Text = "Build Sprite Sheet";
            this.buildBtn.UseVisualStyleBackColor = true;
            this.buildBtn.Click += new System.EventHandler(this.buildBtn_Click);
            // 
            // imageOpenFileDialog
            // 
            this.imageOpenFileDialog.AddExtension = false;
            this.imageOpenFileDialog.FileName = "openFileDialog1";
            this.imageOpenFileDialog.Filter = "Image Files (png, jpg, bmp)|*.png;*.jpg;*.bmp";
            this.imageOpenFileDialog.Multiselect = true;
            // 
            // clearBtn
            // 
            this.clearBtn.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.clearBtn.Location = new System.Drawing.Point(454, 150);
            this.clearBtn.Margin = new System.Windows.Forms.Padding(2);
            this.clearBtn.Name = "clearBtn";
            this.clearBtn.Size = new System.Drawing.Size(84, 41);
            this.clearBtn.TabIndex = 7;
            this.clearBtn.Text = "IsRemoved All";
            this.clearBtn.UseVisualStyleBackColor = true;
            this.clearBtn.Click += new System.EventHandler(this.clearBtn_Click);
            // 
            // label1
            // 
            this.label1.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(8, 164);
            this.label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(81, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Image Padding:";
            // 
            // paddingTxtBox
            // 
            this.paddingTxtBox.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.paddingTxtBox.Location = new System.Drawing.Point(92, 161);
            this.paddingTxtBox.Margin = new System.Windows.Forms.Padding(2);
            this.paddingTxtBox.Name = "paddingTxtBox";
            this.paddingTxtBox.Size = new System.Drawing.Size(76, 20);
            this.paddingTxtBox.TabIndex = 0;
            this.paddingTxtBox.Text = "0";
            this.paddingTxtBox.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // label2
            // 
            this.label2.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(8, 203);
            this.label2.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(108, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Maximum Sheet Size:";
            // 
            // maxWidthTxtBox
            // 
            this.maxWidthTxtBox.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.maxWidthTxtBox.Location = new System.Drawing.Point(92, 220);
            this.maxWidthTxtBox.Margin = new System.Windows.Forms.Padding(2);
            this.maxWidthTxtBox.Name = "maxWidthTxtBox";
            this.maxWidthTxtBox.Size = new System.Drawing.Size(76, 20);
            this.maxWidthTxtBox.TabIndex = 1;
            this.maxWidthTxtBox.Text = "4096";
            this.maxWidthTxtBox.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // label3
            // 
            this.label3.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(51, 222);
            this.label3.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(38, 13);
            this.label3.TabIndex = 2;
            this.label3.Text = "Width:";
            // 
            // maxHeightTxtBox
            // 
            this.maxHeightTxtBox.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.maxHeightTxtBox.Location = new System.Drawing.Point(92, 243);
            this.maxHeightTxtBox.Margin = new System.Windows.Forms.Padding(2);
            this.maxHeightTxtBox.Name = "maxHeightTxtBox";
            this.maxHeightTxtBox.Size = new System.Drawing.Size(76, 20);
            this.maxHeightTxtBox.TabIndex = 2;
            this.maxHeightTxtBox.Text = "4096";
            this.maxHeightTxtBox.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // label4
            // 
            this.label4.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(48, 245);
            this.label4.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(41, 13);
            this.label4.TabIndex = 2;
            this.label4.Text = "Height:";
            // 
            // label5
            // 
            this.label5.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(205, 205);
            this.label5.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(98, 13);
            this.label5.TabIndex = 2;
            this.label5.Text = "Sprite sheet name :";
            this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // spriteSheetNameTxtBox
            // 
            this.spriteSheetNameTxtBox.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.spriteSheetNameTxtBox.Location = new System.Drawing.Point(307, 202);
            this.spriteSheetNameTxtBox.Margin = new System.Windows.Forms.Padding(2);
            this.spriteSheetNameTxtBox.Name = "spriteSheetNameTxtBox";
            this.spriteSheetNameTxtBox.Size = new System.Drawing.Size(231, 20);
            this.spriteSheetNameTxtBox.TabIndex = 8;
            // 
            // imageSaveFileDialog
            // 
            this.imageSaveFileDialog.DefaultExt = "png";
            this.imageSaveFileDialog.Filter = "PNG Files|*.png";
            // 
            // mapSaveFileDialog
            // 
            this.mapSaveFileDialog.DefaultExt = "txt";
            this.mapSaveFileDialog.Filter = "TXT Files|*.txt";
            // 
            // powOf2CheckBox
            // 
            this.powOf2CheckBox.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.powOf2CheckBox.AutoSize = true;
            this.powOf2CheckBox.Checked = true;
            this.powOf2CheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.powOf2CheckBox.Location = new System.Drawing.Point(11, 273);
            this.powOf2CheckBox.Margin = new System.Windows.Forms.Padding(2);
            this.powOf2CheckBox.Name = "powOf2CheckBox";
            this.powOf2CheckBox.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.powOf2CheckBox.Size = new System.Drawing.Size(173, 17);
            this.powOf2CheckBox.TabIndex = 3;
            this.powOf2CheckBox.Text = "Require Power of Two Output?";
            this.powOf2CheckBox.UseVisualStyleBackColor = true;
            // 
            // squareCheckBox
            // 
            this.squareCheckBox.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.squareCheckBox.AutoSize = true;
            this.squareCheckBox.Checked = true;
            this.squareCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.squareCheckBox.Location = new System.Drawing.Point(11, 295);
            this.squareCheckBox.Margin = new System.Windows.Forms.Padding(2);
            this.squareCheckBox.Name = "squareCheckBox";
            this.squareCheckBox.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.squareCheckBox.Size = new System.Drawing.Size(141, 17);
            this.squareCheckBox.TabIndex = 4;
            this.squareCheckBox.Text = "Require Square Output?";
            this.squareCheckBox.UseVisualStyleBackColor = true;
            // 
            // checkBoxDetectAnimations
            // 
            this.checkBoxDetectAnimations.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.checkBoxDetectAnimations.AutoSize = true;
            this.checkBoxDetectAnimations.Checked = true;
            this.checkBoxDetectAnimations.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxDetectAnimations.Location = new System.Drawing.Point(415, 228);
            this.checkBoxDetectAnimations.Name = "checkBoxDetectAnimations";
            this.checkBoxDetectAnimations.Size = new System.Drawing.Size(111, 17);
            this.checkBoxDetectAnimations.TabIndex = 13;
            this.checkBoxDetectAnimations.Text = "Detect animations";
            this.checkBoxDetectAnimations.UseVisualStyleBackColor = true;
            // 
            // buttonCancel
            // 
            this.buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.Location = new System.Drawing.Point(463, 289);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(75, 23);
            this.buttonCancel.TabIndex = 14;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            // 
            // buttonAddDirectory
            // 
            this.buttonAddDirectory.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.buttonAddDirectory.Location = new System.Drawing.Point(190, 150);
            this.buttonAddDirectory.Margin = new System.Windows.Forms.Padding(2);
            this.buttonAddDirectory.Name = "buttonAddDirectory";
            this.buttonAddDirectory.Size = new System.Drawing.Size(84, 41);
            this.buttonAddDirectory.TabIndex = 15;
            this.buttonAddDirectory.Text = "Add Directory";
            this.buttonAddDirectory.UseVisualStyleBackColor = true;
            this.buttonAddDirectory.Click += new System.EventHandler(this.buttonAddDirectory_Click);
            // 
            // SpriteSheetPackerForm
            // 
            this.AcceptButton = this.buildBtn;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.buttonCancel;
            this.ClientSize = new System.Drawing.Size(545, 323);
            this.Controls.Add(this.buttonAddDirectory);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.checkBoxDetectAnimations);
            this.Controls.Add(this.squareCheckBox);
            this.Controls.Add(this.powOf2CheckBox);
            this.Controls.Add(this.maxHeightTxtBox);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.maxWidthTxtBox);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.spriteSheetNameTxtBox);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.paddingTxtBox);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.buildBtn);
            this.Controls.Add(this.addImageBtn);
            this.Controls.Add(this.clearBtn);
            this.Controls.Add(this.removeImageBtn);
            this.Controls.Add(this.listBox1);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.MinimumSize = new System.Drawing.Size(553, 350);
            this.Name = "SpriteSheetPackerForm";
            this.ShowIcon = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Sprite Sheet Packer";
            this.ResumeLayout(false);
            this.PerformLayout();

		}


		private System.Windows.Forms.ListBox listBox1;
		private System.Windows.Forms.Button removeImageBtn;
		private System.Windows.Forms.Button addImageBtn;
		private System.Windows.Forms.Button buildBtn;
		private System.Windows.Forms.OpenFileDialog imageOpenFileDialog;
		private System.Windows.Forms.Button clearBtn;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.TextBox paddingTxtBox;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.TextBox maxWidthTxtBox;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.TextBox maxHeightTxtBox;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox spriteSheetNameTxtBox;
		private System.Windows.Forms.SaveFileDialog imageSaveFileDialog;
		private System.Windows.Forms.SaveFileDialog mapSaveFileDialog;
		private System.Windows.Forms.CheckBox powOf2CheckBox;
        private System.Windows.Forms.CheckBox squareCheckBox;
        private System.Windows.Forms.CheckBox checkBoxDetectAnimations;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.Button buttonAddDirectory;
	}
}

