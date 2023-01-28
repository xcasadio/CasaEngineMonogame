
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


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Reflection;
using CasaEngineCommon.Logger;
using CasaEngine.Game;

namespace Editor.Sprite2DEditor.SpriteSheetPacker
{
	public partial class SpriteSheetPackerForm : Form
    {

        static private readonly Stopwatch stopWatch = new Stopwatch();
        static private readonly string tmpDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Editor.Sprite2DEditor.SpriteSheetPacker");
        static private readonly string fileList = Path.Combine(tmpDirectory, "FileList.txt");

        private Editor.Sprite2DEditor.SpriteSheetPacker.sspack.IImageExporter currentImageExporter;
        private Editor.Sprite2DEditor.SpriteSheetPacker.sspack.IMapExporter currentMapExporter;

        // a list of the files we'll build into our sprite sheet
        private readonly List<string> files = new List<string>();

        private bool m_OnlySettings;

        private EventHandler<BuildResultEventArgs> m_OnSpriteSheetBuildFinished;

        private EventHandler m_OnBuildFinished;



        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<BuildResultEventArgs> OnSpriteSheetBuildFinished
        {
            add
            {
                m_OnSpriteSheetBuildFinished += value;
            }
            remove
            {
                m_OnSpriteSheetBuildFinished -= value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public event EventHandler OnBuildFinished
        {
            add
            {
                m_OnBuildFinished += value;
            }
            remove
            {
                m_OnBuildFinished -= value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public string SpriteSheet
        {
            get;
            private set;
        }

        /// <summary>
        /// 
        /// </summary>
        public string SpriteSheetMap
        {
            get;
            private set;
        }

        /// <summary>
        /// 
        /// </summary>
        public SpriteSheetTask.SpriteSheetBuild SpriteSheetBuild
        {
            get;
            private set;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="onlySettings"></param>
        public SpriteSheetPackerForm(SpriteSheetTask.SpriteSheetBuild? build_, bool onlySettings = false)
        {
            InitializeComponent();

            m_OnlySettings = onlySettings;

            if (m_OnlySettings == true)
            {
                buildBtn.Text = "OK";
                buildBtn.Size = buttonCancel.Size;
                buildBtn.Location = buttonCancel.Location;
                buildBtn.Location = new Point(
                    buildBtn.Location.X - (buttonCancel.Size.Width + buttonCancel.Margin.Right + buildBtn.Margin.Left),
                    buildBtn.Location.Y);
            }
            else
            {
                buttonCancel.Visible = false;
            }

            // find all of the available exporters
            Editor.Sprite2DEditor.SpriteSheetPacker.sspack.Exporters.Load();

            // generate the save dialogs based on available exporters
            GenerateImageSaveDialog();
            GenerateMapSaveDialog();

            // set our open file dialog filter based on the valid extensions
            imageOpenFileDialog.Filter = "Image Files|";
            foreach (var ext in Editor.Sprite2DEditor.SpriteSheetPacker.sspack.MiscHelper.AllowedImageExtensions)
                imageOpenFileDialog.Filter += string.Format("*.{0};", ext);

            // set the UI values to our saved settings
            /*maxWidthTxtBox.Text = Editor.Sprite2DEditor.SpriteSheetPacker.Settings.Default.MaxWidth.ToString();
            maxHeightTxtBox.Text = Editor.Sprite2DEditor.SpriteSheetPacker.Settings.Default.MaxHeight.ToString();
            paddingTxtBox.Text = Editor.Sprite2DEditor.SpriteSheetPacker.Settings.Default.Padding.ToString();
            powOf2CheckBox.Checked = Editor.Sprite2DEditor.SpriteSheetPacker.Settings.Default.PowOf2;
            squareCheckBox.Checked = Editor.Sprite2DEditor.SpriteSheetPacker.Settings.Default.Square;*/
            //generateMapCheckBox.Checked = Editor.Sprite2DEditor.SpriteSheetPacker.Settings.Default.GenerateMap;

            currentMapExporter = Editor.Sprite2DEditor.SpriteSheetPacker.sspack.Exporters.MapExporters[0];

            if (build_.HasValue == true)
            {
                spriteSheetNameTxtBox.Text = build_.Value.SpriteSheetName;
                maxWidthTxtBox.Text = build_.Value.SpriteSheetWidth.ToString();
                maxHeightTxtBox.Text = build_.Value.SpriteSheetHeight.ToString();
                paddingTxtBox.Text = build_.Value.Padding.ToString();
                powOf2CheckBox.Checked = build_.Value.PowerOfTwo;
                squareCheckBox.Checked = build_.Value.Square;
                AddFiles(build_.Value.Files);
            }
        }



        // configures our image save dialog to take into account all loaded image exporters
        public void GenerateImageSaveDialog()
        {
            string filter = "";
            int filterIndex = 0;
            int i = 0;

            foreach (var exporter in Editor.Sprite2DEditor.SpriteSheetPacker.sspack.Exporters.ImageExporters)
            {
                i++;
                filter += string.Format("{0} Files|*.{1}|", exporter.ImageExtension.ToUpper(), exporter.ImageExtension);
                /*if (exporter.ImageExtension.ToLower() == Editor.Sprite2DEditor.SpriteSheetPacker.Settings.Default.ImageExt)
                {
                    filterIndex = i;
                }*/
            }
            filter = filter.Remove(filter.Length - 1);

            imageSaveFileDialog.Filter = filter;
            imageSaveFileDialog.FilterIndex = filterIndex;
        }

        // configures our map save dialog to take into account all loaded map exporters
        public void GenerateMapSaveDialog()
        {
            string filter = "";
            int filterIndex = 0;
            int i = 0;

            foreach (var exporter in Editor.Sprite2DEditor.SpriteSheetPacker.sspack.Exporters.MapExporters)
            {
                i++;
                filter += string.Format("{0} Files|*.{1}|", exporter.MapExtension.ToUpper(), exporter.MapExtension);
                /*if (exporter.MapExtension.ToLower() == Editor.Sprite2DEditor.SpriteSheetPacker.Settings.Default.MapExt)
                {
                    filterIndex = i;
                }*/
            }
            filter = filter.Remove(filter.Length - 1);

            mapSaveFileDialog.Filter = filter;
            mapSaveFileDialog.FilterIndex = filterIndex;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="paths"></param>
        private void AddFilesFromDirectory(string path)
        {
            // if the path is a directory, add all files inside the directory
            if (Directory.Exists(path))
            {
                AddFiles(Directory.GetFiles(path, "*", SearchOption.AllDirectories));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="paths"></param>
        private void AddFiles(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                // make sure the file is an image
                if (!Editor.Sprite2DEditor.SpriteSheetPacker.sspack.MiscHelper.IsImageFile(path))
                    continue;

                // prevent duplicates
                if (IsFileNameValid(path) == false)
                {
                    string msg = "The file (" + path + ") already exist or an other file as the same name.";
                    MessageBox.Show(this, msg, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    LogManager.Instance.WriteLineWarning(msg);
                    continue;
                }

                // add to both our internal list and the list box
                files.Add(path);
                listBox1.Items.Add(path);
            }
        }

        /// <summary>
        /// Check if the file as the same name that the files already added
        /// </summary>
        /// <param name="path">full path of the file</param>
        /// <returns>true if the name is unic</returns>
        private bool IsFileNameValid(string path_)
        {
            if (string.IsNullOrWhiteSpace(path_))
            {
                throw new ArgumentNullException("SpriteSheetPackerForm.IsFileNameValid() : path_ is null");
            }

            path_ = Path.GetFileName(path_);

            foreach (string f in files)
            {
                if (Path.GetFileName(f).Equals(path_) == true)
                {
                    return false;
                }
            }

            return !files.Contains(path_);
        }

        // determines if a directory contains an image we can accept
        public static bool DirectoryContainsImages(string directory)
        {
            foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                if (Editor.Sprite2DEditor.SpriteSheetPacker.sspack.MiscHelper.IsImageFile(file))
                {
                    return true;
                }
            }

            return false;
        }

        private void listBox1_DragEnter(object sender, DragEventArgs e)
        {
            // if this drag is not for a file drop, ignore it
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            // figure out if any the files being dropped are images
            bool imageFound = false;
            foreach (var f in (string[])e.Data.GetData(DataFormats.FileDrop))
            {
                // if the path is a directory and it contains images...
                if (Directory.Exists(f) && DirectoryContainsImages(f))
                {
                    imageFound = true;
                    break;
                }

                // or if the path itself is an image
                if (Editor.Sprite2DEditor.SpriteSheetPacker.sspack.MiscHelper.IsImageFile(f))
                {
                    imageFound = true;
                    break;
                }
            }

            // if an image is being added, we're going to copy them. otherwise, we don't accept them.
            e.Effect = imageFound ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void listBox1_DragDrop(object sender, DragEventArgs e)
        {
            // add all the files dropped onto the list box
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                AddFiles((string[])e.Data.GetData(DataFormats.FileDrop));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonAddDirectory_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog  form = new FolderBrowserDialog ();
            form.SelectedPath = Engine.Instance.ProjectManager.ProjectPath;

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                AddFilesFromDirectory(form.SelectedPath);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void addImageBtn_Click(object sender, EventArgs e)
        {
            // show our file dialog and add all the resulting files
            if (imageOpenFileDialog.ShowDialog() == DialogResult.OK)
                AddFiles(imageOpenFileDialog.FileNames);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void removeImageBtn_Click(object sender, EventArgs e)
        {
            // build a list of files to be removed
            List<string> filesToRemove = new List<string>();
            foreach (var i in listBox1.SelectedItems)
                filesToRemove.Add((string)i);

            // remove the files from our internal list
            files.RemoveAll(filesToRemove.Contains);

            // remove the files from our list box
            filesToRemove.ForEach(f => listBox1.Items.Remove(f));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void clearBtn_Click(object sender, EventArgs e)
        {
            // clear both lists
            files.Clear();
            listBox1.Items.Clear();
        }

        /*private void browseImageBtn_Click(object sender, EventArgs e)
        {
            // show the file dialog
            imageSaveFileDialog.FileName = spriteSheetNameTxtBox.Text;
            if (imageSaveFileDialog.ShowDialog() == DialogResult.OK)
            {
                // store the image path
                spriteSheetNameTxtBox.Text = imageSaveFileDialog.FileName;

                // figure out which image exporter to use based on the extension
                string imageExtension = Path.GetExtension(imageSaveFileDialog.FileName).Substring(1);
                foreach (var exporter in Editor.Sprite2DEditor.SpriteSheetPacker.sspack.Exporters.ImageExporters)
                {
                    if (exporter.ImageExtension.Equals(imageExtension, StringComparison.InvariantCultureIgnoreCase))
                    {
                        currentImageExporter = exporter;    
                        break;
                    }
                }
                Editor.Sprite2DEditor.SpriteSheetPacker.Settings.Default.ImageExt = imageExtension;
				
                // if there is no selected map exporter, default to the last used
                if (currentMapExporter == null)
                {
                    string mapExtension = Editor.Sprite2DEditor.SpriteSheetPacker.Settings.Default.MapExt;
                    foreach (var exporter in Editor.Sprite2DEditor.SpriteSheetPacker.sspack.Exporters.MapExporters)
                    {
                        if (exporter.MapExtension.Equals(mapExtension, StringComparison.InvariantCultureIgnoreCase))
                        {
                            currentMapExporter = exporter;
                            break;
                        }
                    }

                    // if the last used map exporter didn't exist, default to the first one
                    if (currentMapExporter == null)
                    {
                        currentMapExporter = Editor.Sprite2DEditor.SpriteSheetPacker.sspack.Exporters.MapExporters[0];
                    }
                }
				
                mapFileTxtBox.Text = imageSaveFileDialog.FileName.Remove(imageSaveFileDialog.FileName.Length - 3) + currentMapExporter.MapExtension.ToLower();
            }
        }*/

        /*private void browseMapBtn_Click(object sender, EventArgs e)
        {
            // show the file dialog
            mapSaveFileDialog.FileName = mapFileTxtBox.Text;
            if (mapSaveFileDialog.ShowDialog() == DialogResult.OK)
            {
                // store the file path
                mapFileTxtBox.Text = mapSaveFileDialog.FileName;

                // figure out which map exporter to use based on the extension
                string mapExtension = Path.GetExtension(mapSaveFileDialog.FileName).Substring(1);
                foreach (var exporter in Editor.Sprite2DEditor.SpriteSheetPacker.sspack.Exporters.MapExporters)
                {
                    if (exporter.MapExtension.Equals(mapExtension, StringComparison.InvariantCultureIgnoreCase))
                    {
                        currentMapExporter = exporter;
                        break;
                    }
                }
                Editor.Sprite2DEditor.SpriteSheetPacker.Settings.Default.MapExt = mapExtension;
            }
        }*/

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buildBtn_Click(object sender, EventArgs e)
        {
            // check our parameters
            if (files.Count == 0)
            {
                ShowBuildError("No images to pack into sheet");
                return;
            }
            if (string.IsNullOrWhiteSpace(spriteSheetNameTxtBox.Text))
            {
                ShowBuildError("No sprite sheet name given.");
                return;
            }
            /*if (string.IsNullOrEmpty(mapFileTxtBox.Text))
            {
                ShowBuildError("No text filename given.");
                return;
            }*/
            int outputWidth, outputHeight, padding;
            if (!int.TryParse(maxWidthTxtBox.Text, out outputWidth) || outputWidth < 1)
            {
                ShowBuildError("Maximum width is not a valid integer value greater than 0.");
                return;
            }
            if (!int.TryParse(maxHeightTxtBox.Text, out outputHeight) || outputHeight < 1)
            {
                ShowBuildError("Maximum height is not a valid integer value greater than 0.");
                return;
            }
            if (!int.TryParse(paddingTxtBox.Text, out padding) || padding < 0)
            {
                ShowBuildError("Image padding is not a valid non-negative integer");
                return;
            }

            /*  */
            SpriteSheetTask.SpriteSheetBuild buildTask = new SpriteSheetTask.SpriteSheetBuild();
            buildTask.Files = new List<string>();
            buildTask.Files.AddRange(files);
            buildTask.SpriteSheetWidth = int.Parse(maxWidthTxtBox.Text);
            buildTask.SpriteSheetHeight = int.Parse(maxHeightTxtBox.Text);
            buildTask.Padding = int.Parse(paddingTxtBox.Text);
            buildTask.PowerOfTwo = powOf2CheckBox.Checked;
            buildTask.Square = squareCheckBox.Checked;
            buildTask.SpriteSheetName = spriteSheetNameTxtBox.Text;
            buildTask.DetectAnimations = checkBoxDetectAnimations.Checked;

            if (m_OnlySettings == true)
            {
                this.SpriteSheetBuild = buildTask;
            }
            else
            {
                BuildSpriteSheet(buildTask);
            }

            DialogResult = DialogResult.OK;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="task_"></param>
        public void BuildSpriteSheet(SpriteSheetTask.SpriteSheetBuild task_)
        {
            foreach (Control control in Controls)
                control.Enabled = false;

            if (task_.SpriteSheetName.EndsWith(".png") == true)
            {
                task_.SpriteSheetName = task_.SpriteSheetName.Replace(".png", string.Empty);
            }

            string map = task_.SpriteSheetName + ".txt";
            task_.SpriteSheetName = task_.SpriteSheetName + ".png";

            if (!Directory.Exists(tmpDirectory))
            {
                Directory.CreateDirectory(tmpDirectory);
            }

            SpriteSheet = Path.Combine(tmpDirectory, task_.SpriteSheetName);
            SpriteSheetMap = Path.Combine(tmpDirectory, map);

            using (StreamWriter writer = new StreamWriter(fileList))
            {
                foreach (var file in task_.Files)
                {
                    writer.WriteLine(file);
                }
            }

            // construct the arguments in a list so we can pass the array cleanly to Editor.Sprite2DEditor.SpriteSheetPacker.sspack.Program.Launch()
            List<string> args = new List<string>();
            args.Add("/image:" + SpriteSheet/*image*/);
            if (true/*generateMap*/)
                args.Add("/map:" + SpriteSheetMap/*map*/);
            args.Add("/mw:" + task_.SpriteSheetWidth);
            args.Add("/mh:" + task_.SpriteSheetHeight);
            args.Add("/pad:" + task_.Padding);
            if (task_.PowerOfTwo)
                args.Add("/pow2");
            if (task_.Square)
                args.Add("/sqr");
            args.Add("/il:" + fileList);

            LogManager.Instance.WriteLineDebug("Launch sspack : " + string.Concat(args));

            Thread buildThread = new Thread(delegate(object obj)
            {
#if !DEBUG
                try
                {
#endif
                int Result = Editor.Sprite2DEditor.SpriteSheetPacker.sspack.sspack.Launch(args.ToArray());
                (obj as Control).Invoke(new Action<int>(BuildThreadComplete), new[] { (object)Result });
#if !DEBUG
                }
                catch (System.Exception ex)
                {
                    LogManager.Instance.WriteException(ex);
                }
#endif
            })
            {
                IsBackground = true,
                Name = "Sprite Sheet Packer Worker Thread",
            };

            stopWatch.Reset();
            stopWatch.Start();
            buildThread.Start(this);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="result"></param>
        private void BuildThreadComplete(int result)
        {
            if (result == 0)
            {
                if (m_OnSpriteSheetBuildFinished != null)
                {
                    BuildResultEventArgs eventArgs = new BuildResultEventArgs(SpriteSheet, SpriteSheetMap, checkBoxDetectAnimations.Checked);
                    m_OnSpriteSheetBuildFinished.Invoke(this, eventArgs);
                }

                try
                {
                    File.Delete(SpriteSheetMap);
                    File.Delete(fileList);
                    File.Delete(SpriteSheet);
                }
                catch (Exception ex)
                {
                    ShowBuildError(ex.Message);
                }

                stopWatch.Stop();
                string msg = "Build completed in " + stopWatch.Elapsed.TotalSeconds + " seconds.";
                LogManager.Instance.WriteLineDebug(msg);
                MessageBox.Show(msg, "Finished", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                stopWatch.Stop();
                ShowBuildError("Error packing images: " + SpaceErrorCode((Editor.Sprite2DEditor.SpriteSheetPacker.sspack.FailCode)result));
            }

            // re-enable all our controls
            foreach (Control control in Controls)
                control.Enabled = true;

            if (m_OnBuildFinished != null)
            {
                m_OnBuildFinished.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnClosed(EventArgs e)
        {
            // get our UI values if they are valid
            int outputWidth, outputHeight, padding;

            /*if (int.TryParse(maxWidthTxtBox.Text, out outputWidth) && outputWidth > 0)
                Editor.Sprite2DEditor.SpriteSheetPacker.Settings.Default.MaxWidth = outputWidth;

            if (int.TryParse(maxHeightTxtBox.Text, out outputHeight) && outputHeight > 0)
                Editor.Sprite2DEditor.SpriteSheetPacker.Settings.Default.MaxHeight = outputHeight;

            if (int.TryParse(paddingTxtBox.Text, out padding) && padding >= 0)
                Editor.Sprite2DEditor.SpriteSheetPacker.Settings.Default.Padding = padding;

            Editor.Sprite2DEditor.SpriteSheetPacker.Settings.Default.PowOf2 = powOf2CheckBox.Checked;
            Editor.Sprite2DEditor.SpriteSheetPacker.Settings.Default.Square = squareCheckBox.Checked;
            //Editor.Sprite2DEditor.SpriteSheetPacker.Settings.Default.GenerateMap = generateMapCheckBox.Checked;

            // save the settings
            Editor.Sprite2DEditor.SpriteSheetPacker.Settings.Default.Save();*/
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="failCode"></param>
        /// <returns></returns>
        private static string SpaceErrorCode(Editor.Sprite2DEditor.SpriteSheetPacker.sspack.FailCode failCode)
        {
            string error = failCode.ToString();

            string result = error[0].ToString();

            for (int i = 1; i < error.Length; i++)
            {
                char c = error[i];
                if (char.IsUpper(c))
                    result += " ";
                result += c;
            }

            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="error"></param>
        private static void ShowBuildError(string error)
        {
            MessageBox.Show(error, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

	}
}
