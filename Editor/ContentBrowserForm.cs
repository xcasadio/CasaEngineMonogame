
using System.ComponentModel;
using CasaEngine.Game;
using CasaEngine.Graphics2D;
using CasaEngine.Project;
using CasaEngineCommon.Logger;
using Editor.Sprite2DEditor.SpriteSheetPacker.sspack;
using Editor.WinForm;
using Microsoft.Xna.Framework;
using WeifenLuo.WinFormsUI.Docking;
using CasaEngine.Gameplay;
using CasaEngine.Audio;
using Microsoft.Xna.Framework.Audio;
using Editor.Tools.Graphics2D;
using CasaEngine.Assets.Graphics2D;
using System.Diagnostics;
using Editor.Tools.Font;
using CasaEngine.Assets.UI;
using CasaEngine.Core_Systems.Math.Shape2D;
using CasaEngine.Front_End.Screen;
using CasaEngine.Gameplay.Actor;
using BaseObject = CasaEngine.Gameplay.Actor.BaseObject;
using Font = CasaEngine.Assets.Fonts.Font;

namespace Editor
{
    public partial class ContentBrowserForm
        : DockContent
    {

        static private List<string> m_CustomObjectNames;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="collec_"></param>
        static public void SetCustomObjectNames(IEnumerable<string> collec_)
        {
            m_CustomObjectNames = new List<string>(collec_);
        }


        private BackgroundWorker m_BackgroundWorker;
        private int m_Percent, m_PercentTotal;
        private ImageList m_ImageListListView;

        /// <summary>
        /// 
        /// </summary>
        public ContentBrowserForm()
        {
            InitializeComponent();

            m_ImageListListView = new ImageList();
            m_ImageListListView.ColorDepth = ColorDepth.Depth32Bit;
            m_ImageListListView.ImageSize = new Size(64, 64);
            //m_ImageListListView.ImageStream
            //ImageListStreamer

            //GameInfo.Instance.Asset2DManager.SpriteAdded += new EventHandler<Asset2DEventArg>(OnSpriteAdded);
            //GameInfo.Instance.PackageManager.Refresh();

            Engine.Instance.ObjectManager.FolderCreated += new EventHandler(OnFolderCreated);
            Engine.Instance.ObjectManager.FolderDeleted += new EventHandler(OnFolderDeleted);
            Engine.Instance.ObjectManager.ObjectAdded += new EventHandler(OnObjectAdded);
            Engine.Instance.ObjectManager.ObjectMoved += new EventHandler(OnObjectMoved);
            Engine.Instance.ObjectManager.ObjectRemoved += new EventHandler(OnObjectAdded);
            Engine.Instance.ObjectManager.ObjectRenamed += new EventHandler(OnObjectRenamed);
            Engine.Instance.ObjectManager.ObjectModified += new EventHandler(OnObjectModified);
            Engine.Instance.ObjectManager.AllObjectSaved += new EventHandler(OnAllObjectSaved);

            treeViewFolder.ItemDrag += new ItemDragEventHandler(treeView1_ItemDrag);

            if (m_CustomObjectNames != null)
            {
                foreach (string c in m_CustomObjectNames)
                {
                    ToolStripMenuItem t = new ToolStripMenuItem(c);
                    t.Click += new EventHandler(CustomObjectMenuClick);
                    toolStripMenuItemAddItem.DropDownItems.Add(t);
                }
            }

            RefreshTreeViewFolder();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void CustomObjectMenuClick(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem)
            {
                string objectPath = "";
                InputBox form = new InputBox("Create object", "Choose a name for the new object", "object name");

                while (form.ShowDialog(this) == DialogResult.OK)
                {
                    objectPath = form.InputText;

                    if (treeViewFolder.SelectedNode != null)
                    {
                        objectPath = treeViewFolder.SelectedNode.FullPath + "\\" + objectPath;
                    }

                    if (Engine.Instance.ObjectManager.IsValidObjectPath(objectPath) == true)
                    {
                        break;
                    }

                    form.LabelText = "An object named '" + form.InputText + "' already exist!";
                }

                if (form.DialogResult == DialogResult.OK)
                {
                    ToolStripMenuItem t = sender as ToolStripMenuItem;
                    BaseObject obj = Engine.Instance.ExternalToolManager.CreateCustomObjectByName(t.Text);
                    Engine.Instance.ObjectManager.Add(objectPath, obj);
                }

                form.Dispose();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnObjectAdded(object sender, EventArgs e)
        {
            if (treeViewFolder.InvokeRequired)
            {
                listViewItems.Invoke(new Action(() => OnObjectAdded(sender, e)));
            }
            else
            {
                try
                {
                    UpdateListView(treeViewFolder.SelectedNode != null ? treeViewFolder.SelectedNode.FullPath : "");
                }
                catch (Exception ex)
                {
                    LogManager.Instance.WriteException(ex);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnObjectRenamed(object sender, EventArgs e)
        {
            if (treeViewFolder.InvokeRequired)
            {
                listViewItems.Invoke(new Action(() => OnObjectRenamed(sender, e)));
            }
            else
            {
                try
                {
                    UpdateListView(treeViewFolder.SelectedNode != null ? treeViewFolder.SelectedNode.FullPath : "");
                }
                catch (Exception ex)
                {
                    LogManager.Instance.WriteException(ex);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnObjectModified(object sender, EventArgs e)
        {
            if (treeViewFolder.InvokeRequired)
            {
                listViewItems.Invoke(new Action(() => OnObjectModified(sender, e)));
            }
            else
            {
                try
                {
                    if (sender is string)
                    {
                        string path = sender as string;

                        foreach (ListViewItem item in listViewItems.Items)
                        {
                            if (path.Equals(item.Tag) == true && item.Text.EndsWith("*") == false)
                            {
                                item.Text += "*";
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Instance.WriteException(ex);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnObjectMoved(object sender, EventArgs e)
        {
            if (treeViewFolder.InvokeRequired)
            {
                listViewItems.Invoke(new Action(() => OnObjectMoved(sender, e)));
            }
            else
            {
                try
                {
                    UpdateListView(treeViewFolder.SelectedNode != null ? treeViewFolder.SelectedNode.FullPath : "");
                }
                catch (Exception ex)
                {
                    LogManager.Instance.WriteException(ex);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnAllObjectSaved(object sender, EventArgs e)
        {
            if (treeViewFolder.InvokeRequired)
            {
                listViewItems.Invoke(new Action(() => OnAllObjectSaved(sender, e)));
            }
            else
            {
                try
                {
                    foreach (ListViewItem item in listViewItems.Items)
                    {
                        if (item.Text.EndsWith("*") == true)
                        {
                            item.Text = item.Text.Substring(0, item.Text.Length - 1);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Instance.WriteException(ex);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnFolderCreated(object sender, EventArgs e)
        {
            try
            {
                if (sender is string)
                {
                    if (treeViewFolder.InvokeRequired)
                    {
                        treeViewFolder.Invoke(new CreateFolderDelegate(CreateFolder), sender as string);
                    }
                    else
                    {
                        CreateFolder(sender as string);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Instance.WriteException(ex);
            }
        }

        delegate void CreateFolderDelegate(string path_);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path_"></param>
        public void CreateFolder(string path_)
        {
            treeViewFolder.BeginUpdate();

            string[] folders = path_.Split(Path.DirectorySeparatorChar);
            TreeNodeCollection nodes;
            TreeNode node = null;

            foreach (string f in folders)
            {
                if (node == null)
                {
                    nodes = treeViewFolder.Nodes;
                }
                else
                {
                    nodes = node.Nodes;
                }

                node = FindOrCreateTreeNode(f, nodes);
            }

            treeViewFolder.EndUpdate();

            treeViewFolder.Refresh();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_"></param>
        /// <param name="node_"></param>
        /// <returns></returns>
        public TreeNode FindOrCreateTreeNode(string name_, TreeNodeCollection nodes_)
        {
            foreach (TreeNode n in nodes_)
            {
                if (n.Text.Equals(name_))
                {
                    return n;
                }
            }

            TreeNode nd = new TreeNode(name_, 0, 0);
            nodes_.Add(nd);
            return nd;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnFolderDeleted(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 
        /// </summary>
        public void RefreshTreeViewFolder()
        {
            treeViewFolder.BeginUpdate();

            treeViewFolder.Nodes.Clear();

            string[] directories = Engine.Instance.ObjectManager.GetAllDirectories();

            foreach (string dir in directories)
            {
                GetNodeFromPath(dir);
            }

            treeViewFolder.EndUpdate();
            treeViewFolder.Refresh();
        }

        /// <summary>
        /// 
        /// </summary>
        public void RefreshItem()
        {

        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender_"></param>
        /// <param name="e"></param>
        private void OnSpriteAdded(object sender_, Asset2DEventArg e)
        {
            if (listViewItems.InvokeRequired == true)
            {
                listViewItems.Invoke(new Action(() => OnSpriteAdded(sender_, e)));
            }
            else
            {
                Sprite2D sprite = Engine.Instance.Asset2DManager.GetSprite2DByName(e.AssetName);
                string assetName = sprite.AssetFileNames[0];

                AddItemOnListView(assetName);
            }
        }



        //delegate void SetAddItemInControlCallback(string spriteSheetName_);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        private void AddItemOnListView(string name_)
        {
            ListViewItem item = new ListViewItem(name_);
            listViewItems.Items.Add(item);

            //create thumbnail
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fullpath_"></param>
        private void UpdateListView(string fullpath_)
        {
            listViewItems.BeginUpdate();
            listViewItems.Items.Clear();

            //imageListPreviewObject.Images.Add("key", Image);

            ObjectManager.ObjectData[] objects = Engine.Instance.ObjectManager.GetAllItemsFromPath(fullpath_);

            foreach (ObjectManager.ObjectData o in objects)
            {
                ListViewItem item = new ListViewItem(o.Name);

                if (o.MustBeSaved == true)
                {
                    item.Text += "*";
                }

                if (typeof(Sprite2D).FullName.Equals(o.ClassName) == true)
                {
                    item.ImageKey = "sprite";
                }
                else if (typeof(Animation2D).FullName.Equals(o.ClassName) == true)
                {
                    item.ImageKey = "animation";
                }
                else if (typeof(UiScreen).FullName.Equals(o.ClassName) == true)
                {
                    item.ImageKey = "menu";
                }
                else
                {
                    item.ImageKey = "unknown";
                }

                item.ToolTipText = fullpath_ + Path.DirectorySeparatorChar + o.Name + "\nLoaded?\nCheckout?";
                item.Tag = string.IsNullOrWhiteSpace(fullpath_) ? o.Name : fullpath_ + Path.DirectorySeparatorChar + o.Name;
                listViewItems.Items.Add(item);
            }

            listViewItems.EndUpdate();
            //listViewItems.Invalidate();
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="package_"></param>
        /// <returns></returns>
        TreeNode CreateNodeFromPackage(Package package_)
        {
            TreeNode node = new TreeNode(package_.Name);
            node.Tag = package_;

            foreach (Package p in package_.Children)
            {
                node.Nodes.Add(CreateNodeFromPackage(p));
            }

            return node;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="path_"></param>
        /// <returns></returns>
        TreeNode GetNodeFromPath(string path_)
        {
            TreeNode ret = null;

            string[] dirs = path_.Split(Path.DirectorySeparatorChar);
            TreeNode node = null;

            foreach (string dir in dirs)
            {
                if (string.IsNullOrWhiteSpace(dir) == false)
                {
                    node = FindOrCreateNode(dir, node);
                }
            }

            return ret;
        }

        /// <summary>
        /// Search a node named 'name_' in the child nodes of node_.
        /// </summary>
        /// <param name="name_"></param>
        /// <param name="node_">can be null then search in treeView root child's nodes</param>
        /// <returns></returns>
        TreeNode FindOrCreateNode(string name_, TreeNode node_)
        {
            TreeNodeCollection collect;

            if (node_ == null)
            {
                collect = treeViewFolder.Nodes;
            }
            else
            {
                collect = node_.Nodes;
            }

            foreach (TreeNode n in collect)
            {
                if (n.Text.Equals(name_) == true)
                {
                    return n;
                }
            }

            TreeNode nd = new TreeNode(name_, 0, 0);
            collect.Add(nd);
            return nd;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeView1_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node != null)
            {
                UpdateListView(e.Node.FullPath);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if ((e.Action == TreeViewAction.ByKeyboard
                || e.Action == TreeViewAction.ByMouse)
                && e.Node != null)
            {
                e.Node.Expand();
            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void addAnimSetToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void addAnimTreeToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void addSoundToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog form = new OpenFileDialog();
            form.Filter = "All Files (*.*)|*.*";
            FileStream fs = null;

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    fs = new FileStream(form.FileName, FileMode.Open);
                    Sound s = new Sound(SoundEffect.FromStream(fs));
                    //GameInfo.Instance.ObjectManager.Add(treeView1.SelectedNode.FullPath, s);
                }
                catch (Exception ex)
                {
                    LogManager.Instance.WriteException(ex);
                    MessageBox.Show("Can't add sound.");
                }
                finally
                {
                    if (fs != null)
                    {
                        fs.Close();
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void newSpritesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string currentPath = treeViewFolder.SelectedNode != null ? treeViewFolder.SelectedNode.FullPath : "";

            AddSpriteAndAnimForm form = new AddSpriteAndAnimForm(currentPath);

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                AddSprites(form.PackageName, form.Files, form.DetectAnimation2D);
            }

            form.Dispose();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="packageName_"></param>
        /// <param name="files_"></param>
        /// <param name="detectAnimation2D_"></param>
        public void AddSprites(string packageName_, string[] files_, bool detectAnimation2D_)
        {
            object[] args = new object[3] { packageName_, files_, detectAnimation2D_ };
            BgWorkerForm form = new BgWorkerForm(GenerateSprites, args);
            form.Text = "Build sprite sheet";
            form.ShowDialog(this);
            form.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GenerateSprites(object sender, DoWorkEventArgs e)
        {
            try
            {
                m_BackgroundWorker = sender as BackgroundWorker;
                object[] args = e.Argument as object[];

                string packageName = args[0] as string;
                packageName = packageName.Replace('/', Path.DirectorySeparatorChar);
                string[] files = args[1] as string[];
                bool detectAnimation2D = (bool)args[2];
                List<string> filesAdded = new List<string>();

                m_PercentTotal = files.Length;
                m_Percent = 0;

                //crop image and copy it in the project
                foreach (string name in files)
                {
                    OnProgressChanged("Analysing... " + Path.GetFileName(name));

                    string sprite2DFileName = Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + ProjectManager.AssetDirPath;
                    sprite2DFileName += Path.DirectorySeparatorChar + packageName + Path.DirectorySeparatorChar + Path.GetFileName(name);

                    //TODO : in a function : return bool
                    string objectPath = packageName + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(name);
                    bool nameIsOK = true, replace = false;

                    while (Engine.Instance.ObjectManager.IsValidObjectPath(objectPath) == false)
                    {
                        ConflicForm form = new ConflicForm(objectPath);

                        if (form.ShowDialog() == DialogResult.Cancel
                            || form.Action == ConflicForm.ConflictAction.Skip)
                        {
                            nameIsOK = false;
                            break;
                        }
                        else
                        {
                            replace = form.Action == ConflicForm.ConflictAction.Replace;

                            if (form.Action == ConflicForm.ConflictAction.Rename)
                            {
                                int i = 1;
                                nameIsOK = true;

                                do
                                {
                                    objectPath = packageName + Path.DirectorySeparatorChar + "Copy (" + i + ") of" + Path.GetFileNameWithoutExtension(name);
                                    i++;
                                }
                                while (Engine.Instance.ObjectManager.IsValidObjectPath(objectPath) == false);
                            }
                        }

                        form.Dispose();
                    }

                    if (nameIsOK)
                    {
                        //Create Asset
                        System.Drawing.Rectangle rect;
                        System.Drawing.Point point;
                        Bitmap bitmap = new Bitmap(name);
                        Sprite2D sprite2D;

                        if (ImagePacker.ShrinkBitmap(bitmap, out rect, out point) == true)
                        {
                            //TODO : build asset

                            sprite2D = new Sprite2D(null, sprite2DFileName);
                            sprite2D.AssetFileNames.Add(packageName + Path.DirectorySeparatorChar + Path.GetFileName(name));
                            sprite2D.HotSpot = new Microsoft.Xna.Framework.Point(point.X, point.Y);
                            sprite2D.PositionInTexture = new Microsoft.Xna.Framework.Rectangle(0, 0, rect.Width, rect.Height);
                            ShapePolygone poly = new ShapePolygone(
                                new ShapePolygone(
                                    new Vector2(0, 0),
                                    new Vector2(sprite2D.PositionInTexture.Width, 0),
                                    new Vector2(sprite2D.PositionInTexture.Width, sprite2D.PositionInTexture.Height)));
                            poly.AddPoint(new Vector2(0, sprite2D.PositionInTexture.Height));
                            poly.PointList.Reverse(); // Points must be reverse to be manage by Farseer physics
                            sprite2D.AddCollision(poly);

                            //save new file
                            Bitmap newBitmap = bitmap.Clone(rect, bitmap.PixelFormat);

                            if (Directory.CreateDirectory(Path.GetDirectoryName(sprite2DFileName)) == null)
                            {
                                LogManager.Instance.WriteLineError("Can't create the directory " + Path.GetDirectoryName(sprite2DFileName));
                            }

                            newBitmap.Save(sprite2DFileName);
                            newBitmap.Dispose();
                        }
                        else
                        {
                            LogManager.Instance.WriteLineError("AddSprites() : can't analyzed the image : '" + name + "'");
                            bitmap.Dispose();
                            continue;
                        }

                        bitmap.Dispose();

                        //Add object
                        if (replace == true)
                        {
                            Engine.Instance.ObjectManager.Replace(objectPath, sprite2D);
                        }
                        else
                        {
                            Engine.Instance.ObjectManager.Add(objectPath, sprite2D);
                        }

                        //Animations
                        if (detectAnimation2D == true)
                        {
                            filesAdded.Add(objectPath);
                        }
                    }
                }

                //Animations
                if (filesAdded.Count > 0)
                {
                    DetectAnimation2D(filesAdded.ToArray());
                }
            }
            catch (Exception ex)
            {
                LogManager.Instance.WriteException(ex);
            }
            finally
            {
                m_BackgroundWorker = null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void DetectAnimation2D(string[] sprite2DNames_)
        {
            Dictionary<string, SortedDictionary<int, string>> anims = new Dictionary<string, SortedDictionary<int, string>>();
            int index;

            foreach (string name in sprite2DNames_)
            {
                index = name.LastIndexOf('_');

                if (index != -1)
                {
                    string n = name.Substring(0, index);
                    if (anims.ContainsKey(n) == false)
                    {
                        anims.Add(n, new SortedDictionary<int, string>());
                    }
                    /*else
                    {
                        LogManager.Instance.WriteLineDebug("Sprite2DEditor.DetectAnimation2DInSprite2DArray() : animation named '" + n + "' already detected");
                    }*/
                }
            }

            string number = string.Empty;
            int num;

            foreach (KeyValuePair<string, SortedDictionary<int, string>> pair in anims)
            {
                foreach (string name in sprite2DNames_)
                {
                    if (name.StartsWith(pair.Key) == true)
                    {
                        number = name.Substring(pair.Key.Length + 1, name.Length - pair.Key.Length - 1);

                        if (int.TryParse(number, out num) == true)
                        {
                            pair.Value.Add(num, name);
                        }
                    }
                }

                if (pair.Value.Count > 1)
                {
                    if (Engine.Instance.ObjectManager.IsValidObjectPath(pair.Key) == true)
                    {
                        Animation2D anim2D = new Animation2D();
                        anim2D.Name = pair.Key;

                        foreach (KeyValuePair<int, string> pair2 in pair.Value)
                        {
                            anim2D.AddFrame(Engine.Instance.ObjectManager.GetObjectByPath(pair2.Value).Id, 0.1f, null);
                        }

                        //Add object
                        /*if (replace == true)
                        {
                            GameInfo.Instance.ObjectManager.Replace(objectPath, anim2D);
                        }
                        else
                        {*/
                        Engine.Instance.ObjectManager.Add(pair.Key, anim2D);
                        //}

                        LogManager.Instance.WriteLineDebug("Animation added : " + anim2D.Name);
                    }
                    else
                    {
                        LogManager.Instance.WriteLineDebug("Sprite2DEditorForm.DetectAnimation2DInSprite2DArray() : the animation named " + pair.Key + " already exist.");
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnProgressChanged(string txt_)
        {
            LogManager.Instance.WriteLineDebug(txt_);
            SetProgress(1, "Packing image...\n" + txt_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="percent_"></param>
        /// <param name="msg_"></param>
        private void SetProgress(int percent_, string msg_ = null)
        {
            m_Percent += percent_;

            if (m_BackgroundWorker != null)
            {
                m_BackgroundWorker.ReportProgress(
                    (int)((float)m_Percent / (float)m_PercentTotal * 100.0f),
                    msg_);
            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void addScreenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string objectPath = "";
            InputBox form = new InputBox("Create object", "Choose a name for the new screen", "screen name");

            while (form.ShowDialog(this) == DialogResult.OK)
            {
                objectPath = form.InputText;

                if (treeViewFolder.SelectedNode != null)
                {
                    objectPath = treeViewFolder.SelectedNode.FullPath + "\\" + objectPath;
                }

                if (Engine.Instance.ObjectManager.IsValidObjectPath(objectPath) == true)
                {
                    break;
                }

                form.LabelText = "An object named '" + form.InputText + "' already exist!";
            }

            if (form.DialogResult == DialogResult.OK)
            {
                BaseObject obj = new UiScreen(form.InputText);
                Engine.Instance.ObjectManager.Add(objectPath, obj);
            }

            form.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void addSkinUIToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string objectPath = "";
            InputBox form = new InputBox("Create object", "Choose a name for the UI skin", "Skin name");

            while (form.ShowDialog(this) == DialogResult.OK)
            {
                objectPath = form.InputText;

                if (treeViewFolder.SelectedNode != null)
                {
                    objectPath = treeViewFolder.SelectedNode.FullPath + "\\" + objectPath;
                }

                if (Engine.Instance.ObjectManager.IsValidObjectPath(objectPath) == true)
                {
                    break;
                }

                form.LabelText = "An object named '" + form.InputText + "' already exist!";
            }

            if (form.DialogResult == DialogResult.OK)
            {
                BaseObject obj = new SkinUi();
                Engine.Instance.ObjectManager.Add(objectPath, obj);
            }

            form.Dispose();
        }







        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listView1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (listViewItems.SelectedIndices.Count > 0
                && listViewItems.SelectedItems[0].Tag is string)
            {
                string fullpath = listViewItems.SelectedItems[0].Tag as string;
                BaseObject obj = Engine.Instance.ObjectManager.GetObjectByPath(fullpath);

                //EditorInfo.LaunchEditor(obj.GetType(), fullpath, obj);
                Engine.Instance.ExternalToolManager.RunSubEditor(fullpath, obj);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripMenuItemDelete_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in listViewItems.SelectedItems)
            {
                string fullpath = (string)item.Tag;
                Engine.Instance.ObjectManager.Remove(fullpath);
                listViewItems.Items.Remove(item);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripMenuItemRename_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in listViewItems.SelectedItems)
            {
                item.BeginEdit();
                break;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listViewItems_BeforeLabelEdit(object sender, LabelEditEventArgs e)
        {
            string txt = listViewItems.Items[e.Item].Text;

            if (txt.EndsWith("*") == true)
            {
                listViewItems.Items[e.Item].Text = txt.Substring(0, txt.Length - 1);
            }
            //TODO ; source control
            /*if (SourceControlManager.Instance.SourceControl.IsValidConnection() == true)
            {
                string fullpath = (string) listViewItems.Items[e.Item].Tag;

                Dictionary<string, Dictionary<SourceControlKeyWord, string>> status = SourceControlManager.Instance.SourceControl.FileStatus(
                    new string[] { fullpath });

                if (status.ContainsKey(fullpath)
                    && status[fullpath].ContainsKey(SourceControlKeyWord.Action) == true)
                {
                    e.CancelEdit = status[fullpath][SourceControlKeyWord.Action].Equals("lock");
                }
            }*/
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listViewItems_AfterLabelEdit(object sender, LabelEditEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Label) == false)
            {
                string fullpath = (string)listViewItems.Items[e.Item].Tag;

                if (Engine.Instance.ObjectManager.IsValidObjectPath(Path.GetDirectoryName(fullpath) + Path.DirectorySeparatorChar + e.Label))
                {
                    Engine.Instance.ObjectManager.Rename(fullpath, e.Label);

                    //TODO : source control
                    /*if (SourceControlManager.Instance.SourceControl.IsValidConnection() == true)
                    {

                    }*/
                }
                else
                {
                    e.CancelEdit = true;
                    MessageBox.Show(this, "An object with the name '" + e.Label + "' already exist!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listViewItems_ItemDrag(object sender, ItemDragEventArgs e)
        {
            DoDragDrop(typeof(BaseObject).FullName + "#" + ((ListViewItem)e.Item).Tag, DragDropEffects.Move);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listViewItems_DragDrop(object sender, DragEventArgs e)
        {
            Debug.WriteLine("listViewItems_DragDrop");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listViewItems_DragEnter(object sender, DragEventArgs e)
        {
            Debug.WriteLine("listViewItems_DragEnter");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listViewItems_DragLeave(object sender, EventArgs e)
        {
            Debug.WriteLine("listViewItems_DragLeave");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listViewItems_DragOver(object sender, DragEventArgs e)
        {
            Debug.WriteLine("listViewItems_DragOver");
            e.Effect = DragDropEffects.Move;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeView1_ItemDrag(object sender, ItemDragEventArgs e)
        {
            Debug.WriteLine("treeView1_ItemDrag");
            DoDragDrop("Path#" + ((TreeNode)e.Item).FullPath, DragDropEffects.Move);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeView1_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(string)) == true)
            {
                string data = (string)e.Data.GetData(typeof(string));
                string[] split = data.Split('#');

                if (split.Length > 0)
                {
                    if (split[0].Equals(typeof(BaseObject).FullName) == true)
                    {
                        TreeNode node = treeViewFolder.GetNodeAt(treeViewFolder.PointToClient(new System.Drawing.Point(e.X, e.Y)));

                        if (node == null)
                        {
                            node = treeViewFolder.Nodes[0];
                        }

                        Engine.Instance.ObjectManager.Move(split[1], node.FullPath);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeView1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(string)) == true)
            {
                string data = (string)e.Data.GetData(typeof(string));
                string[] split = data.Split('#');

                if (split.Length > 0)
                {
                    if (split[0].Equals(typeof(BaseObject).FullName) == true)
                    {
                        //split[1] // fullpath
                        //GameInfo.Instance.ObjectManager.GetObjectByPath()
                        e.Effect = DragDropEffects.Move;
                        return;
                    }
                }
            }

            e.Effect = DragDropEffects.None;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeView1_DragLeave(object sender, EventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("treeView1_DragLeave");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeView1_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(string)) == true)
            {
                e.Effect = DragDropEffects.Move;
                TreeNode node = treeViewFolder.GetNodeAt(treeViewFolder.PointToClient(new System.Drawing.Point(e.X, e.Y)));

                if (node == null)
                {
                    node = treeViewFolder.Nodes[0];
                }

                treeViewFolder.SelectedNode = node;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void contextMenuStripListView_Opening(object sender, CancelEventArgs e)
        {
            //add toolItem from plugin
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripMenuItemFullpathToClipboard_Click(object sender, EventArgs e)
        {
            if (treeViewFolder.SelectedNode != null)
            {
                string path = treeViewFolder.SelectedNode.FullPath;

                if (string.IsNullOrWhiteSpace(path) == false)
                {
                    if (path[path.Length - 1] == Path.DirectorySeparatorChar == false)
                    {
                        path += Path.DirectorySeparatorChar;
                    }

                    if (listViewItems.SelectedItems.Count > 0)
                    {
                        path += listViewItems.SelectedItems[0].Text;
                        Clipboard.SetText(path);
                    }
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void addFontToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddFontForm form = null;

            try
            {
                form = new AddFontForm();
                form.PackageFile = treeViewFolder.SelectedNode != null ? treeViewFolder.SelectedNode.FullPath : "";

                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    List<string> newAssetFiles = new List<string>();
                    Font font = new Font(form.BmFile);

                    foreach (string p in font.AssetFileNames)
                    {
                        string texFile = Path.GetDirectoryName(form.BmFile) + Path.DirectorySeparatorChar + p;
                        string destFile = Engine.Instance.ProjectManager.ProjectPath + Path.DirectorySeparatorChar + form.PackageFile + Path.DirectorySeparatorChar + p;

                        if (Directory.CreateDirectory(Path.GetDirectoryName(destFile)) != null)
                        {
                            File.Copy(texFile, destFile, true);
                        }
                        else
                        {
                            LogManager.Instance.WriteLineError("Can't create the directory '" + destFile + "'");
                        }

                        newAssetFiles.Add(form.PackageFile + Path.DirectorySeparatorChar + p);
                    }

                    font.AssetFileNames.Clear();
                    font.AssetFileNames.AddRange(newAssetFiles);

                    Engine.Instance.ObjectManager.Add(form.PackageFile + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(form.BmFile), font);
                }
            }
            catch (Exception ex)
            {
                LogManager.Instance.WriteException(ex);
            }
            finally
            {
                if (form != null)
                {
                    form.Dispose();
                }
            }
        }

    }
}
