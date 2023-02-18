using System.ComponentModel;
using Editor.Game;
using Editor.WinForm;
using Microsoft.Xna.Framework;
using CasaEngine.Editor.UndoRedo;
using Editor.UndoRedo;
using CasaEngine.Editor.Tools;
using CasaEngine.Core.Maths.Shape2D;
using CasaEngine.Framework.Entities;
using CasaEngine.Core.Logger;
using CasaEngine.Framework.Assets.Graphics2D;
using CasaEngine.Framework.Game;

namespace Editor.Tools.Graphics2D
{
    /// <summary>
    /// 
    /// </summary>
    public partial class Sprite2DEditorForm
        : Form, IEditorForm, IExternalTool
    {

        XnaEditorForm m_XnaEditorForm;
        Sprite2DEditorComponent m_Sprite2DComponent;
        UndoRedoManager m_UndoRedoManager;
        int m_LastCollisonIndex = -1, m_LastSocketIndex = -1;
        int m_TabSelectedIndex = -1;
        bool m_OnUndoRedoManagerAction = false;



        /// <summary>
        /// 
        /// </summary>
        public Control XnaPanel
        {
            get { return panelXna; }
        }

        /// <summary>
        /// 
        /// </summary>
        public Sprite2DEditorComponent Sprite2DComponent
        {
            get { return m_Sprite2DComponent; }
        }

        /// <summary>
        /// 
        /// </summary>
        public int CurrentCollisonIndex
        {
            get { return m_Sprite2DComponent.CurrentCollisonIndex; }
            set { SetCurrentCollisonIndex(value); }
        }

        /// <summary>
        /// Gets
        /// </summary>
        public Sprite2D CurrentSprite2D
        {
            get { return m_Sprite2DComponent.CurrentSprite2D; }
        }



        /// <summary>
        /// 
        /// </summary>
        public Sprite2DEditorForm()
        {
            InitializeComponent();

            ExternalTool = new ExternalTool(this);

            m_UndoRedoManager = new UndoRedoManager();
            m_UndoRedoManager.EventCommandDone += new EventHandler(UndoRedoEventCommandDone);
            m_UndoRedoManager.UndoRedoCommandAdded += new EventHandler(UndoRedoCommandAdded);

            m_XnaEditorForm = new XnaEditorForm(this);
            m_Sprite2DComponent = new Sprite2DEditorComponent(m_XnaEditorForm.Game);
            m_Sprite2DComponent.UndoRedoManager = m_UndoRedoManager;
            m_XnaEditorForm.Game.Content.RootDirectory = Engine.Instance.ProjectManager.ProjectPath;
            m_XnaEditorForm.StartGame();

            FormClosed += new FormClosedEventHandler(Sprite2DEditorFormClosed);
            Text = "Sprite2D Editor";
        }




        /// <summary>
        /// 
        /// </summary>
        private void FillSpriteControl(Sprite2D sprite2D_)
        {
            propertyGridSprite2D.SelectedObject = sprite2D_;
            FillCollisionControl(sprite2D_);
            FillSocketControl(sprite2D_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sprite2D_"></param>
        private void FillCollisionControl(Sprite2D sprite2D_)
        {
            listBoxCollision.Items.Clear();

            if (sprite2D_ != null)
            {
                for (int i = 0; i < sprite2D_.Collisions.Length; i++)
                {
                    listBoxCollision.Items.Add(sprite2D_.Collisions[i].ToString());
                }
            }

            SetCurrentCollisonIndex(listBoxCollision.Items.Count == 0 ? -1 : 0, false);
            buttonDelColl.Enabled = listBoxCollision.Items.Count > 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sprite2D_"></param>
        private void FillSocketControl(Sprite2D sprite2D_)
        {
            listBoxSockets.Items.Clear();

            if (sprite2D_ != null)
            {
                foreach (var pair in sprite2D_.GetSockets())
                {
                    listBoxSockets.Items.Add(pair.Key);
                }
            }

            //TODO
            //SetCurrentSocketIndex(listBoxSockets.Items.Count == 0 ? -1 : 0, false);
            propertyGridSocket.SelectedObject = listBoxSockets.SelectedItem;
            ///
            buttonDelSocket.Enabled = listBoxSockets.Items.Count > 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listBoxCollision_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (m_Sprite2DComponent.CurrentCollisonIndex != listBoxCollision.SelectedIndex
                && m_OnUndoRedoManagerAction == false)
            {
                UndoRedoListControlSelection c = new UndoRedoListControlSelection(m_Sprite2DComponent.CurrentCollisonIndex);
                m_UndoRedoManager.Add(c, listBoxCollision);
            }

            SetCollisionIndex();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index_"></param>
        private void SetCurrentCollisonIndex(int index_, bool applyModification = true)
        {
            if (listBoxCollision.InvokeRequired == true)
            {
                listBoxCollision.Invoke(new Action(() => SetCurrentCollisonIndex(index_, applyModification)));
            }
            else
            {
                listBoxCollision.SelectedIndexChanged -= new EventHandler(listBoxCollision_SelectedIndexChanged);
                listBoxCollision.SelectedIndex = index_;
                SetCollisionIndex(applyModification);
                listBoxCollision.SelectedIndexChanged += new EventHandler(listBoxCollision_SelectedIndexChanged);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void SetCollisionIndex(bool applyModification_ = true)
        {
            int index = listBoxCollision.SelectedIndex;

            m_LastCollisonIndex = m_Sprite2DComponent.CurrentCollisonIndex;

            if (applyModification_ == true)
            {
                m_Sprite2DComponent.ApplyCollisionChanges();
            }

            m_Sprite2DComponent.CurrentCollisonIndex = index;

            if (index != -1 && m_Sprite2DComponent.CurrentSprite2D != null)
            {
                propertyGridCollision.SelectedObject = m_Sprite2DComponent.CurrentSprite2D.Collisions[index];
                m_Sprite2DComponent.CurrentSprite2D.Collisions[index].PropertyChanged -= new PropertyChangedEventHandler(CurrentCollision_PropertyValueChanged);
                m_Sprite2DComponent.CurrentSprite2D.Collisions[index].PropertyChanged += new PropertyChangedEventHandler(CurrentCollision_PropertyValueChanged);
            }
            else
            {
                propertyGridCollision.SelectedObject = null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="s"></param>
        /// <param name="e"></param>
        private void CurrentCollision_PropertyValueChanged(object sender, PropertyChangedEventArgs e)
        {
            if (propertyGridCollision.InvokeRequired)
            {
                SetPropertyChangedCallback c = new SetPropertyChangedCallback(CollisionPropertyChangedCallback);
                propertyGridCollision.Invoke(c, new object[] { sender, e });
            }
            else
            {
                CollisionPropertyChangedCallback(sender, e);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void CollisionPropertyChangedCallback(object sender, PropertyChangedEventArgs e)
        {
            propertyGridCollision.Refresh();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="s"></param>
        /// <param name="e"></param>
        private void propertyGridCollision_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            if (m_Sprite2DComponent.CurrentShape2DManipulator == null)
            {
                return;
            }

            UndoRedoPropertyValueChangedCommand c = new UndoRedoPropertyValueChangedCommand(e.ChangedItem.PropertyDescriptor, e.OldValue);
            m_UndoRedoManager.Add(c, m_Sprite2DComponent.CurrentShape2DManipulator.Shape2DObject);
            //refresh xna screen
            m_Sprite2DComponent.CurrentCollisonIndex = m_LastCollisonIndex;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonAddCircle_Click(object sender, EventArgs e)
        {
            AddCollisionInSprite(Shape2DType.Circle);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonAddPoly_Click(object sender, EventArgs e)
        {
            AddCollisionInSprite(Shape2DType.Polygone);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonAddLine_Click(object sender, EventArgs e)
        {
            AddCollisionInSprite(Shape2DType.Line);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonAddRectangle_Click(object sender, EventArgs e)
        {
            AddCollisionInSprite(Shape2DType.Rectangle);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonAddColl_Click(object sender, EventArgs e)
        {
            Sprite2D sprite = m_Sprite2DComponent.CurrentSprite2D;

            if (sprite == null)
            {
                return;
            }

            InputComboBox form = new InputComboBox("Select a shape type", "Create a shape", Enum.GetNames(typeof(Shape2DType)));

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                AddCollisionInSprite((Shape2DType)Enum.Parse(typeof(Shape2DType), form.SelectedItem));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type_"></param>
        private void AddCollisionInSprite(Shape2DType type_)
        {
            Sprite2D sprite = m_Sprite2DComponent.CurrentSprite2D;

            if (sprite == null)
            {
                return;
            }

            Shape2DObject ob = null;

            switch (type_)
            {
                case Shape2DType.Circle:
                    ob = new ShapeCircle(
                        new Microsoft.Xna.Framework.Point(
                            sprite.PositionInTexture.Width / 2,
                            sprite.PositionInTexture.Height / 2),
                        30);
                    break;

                case Shape2DType.Line:
                    ob = new ShapeLine(
                        Microsoft.Xna.Framework.Point.Zero,
                        new Microsoft.Xna.Framework.Point(sprite.PositionInTexture.Width, sprite.PositionInTexture.Height));
                    break;

                case Shape2DType.Polygone:
                    ob = new ShapePolygone(
                        new Vector2(0, 0),
                        new Vector2(sprite.PositionInTexture.Width, 0),
                        new Vector2(sprite.PositionInTexture.Width / 2, sprite.PositionInTexture.Height));
                    break;

                case Shape2DType.Rectangle:
                    ob = new ShapeRectangle(0, 0, sprite.PositionInTexture.Width, sprite.PositionInTexture.Height);
                    break;
            }

            UndoRedoAddCollisionCommand command = new UndoRedoAddCollisionCommand(ob);
            m_UndoRedoManager.Add(command, this);

            AddCollision(ob, sprite);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj_"></param>
        public void AddCollision(Shape2DObject obj_, Sprite2D sprite_)
        {
            if (listBoxCollision.InvokeRequired == true)
            {
                listBoxCollision.Invoke(new Action(() => AddCollision(obj_, sprite_)));
            }
            else
            {
                sprite_.AddCollision(obj_);
                listBoxCollision.Items.Add(obj_);
                listBoxCollision.SelectedIndex = listBoxCollision.Items.Count - 1;
                buttonDelColl.Enabled = true;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonDelColl_Click(object sender, EventArgs e)
        {
            Sprite2D sprite = m_Sprite2DComponent.CurrentSprite2D;

            if (sprite == null)
            {
                return;
            }

            int index = listBoxCollision.SelectedIndex;

            if (index == -1)
            {
                return;
            }

            Shape2DObject ob = sprite.Collisions[index];

            UndoRedoAddCollisionCommand command = new UndoRedoAddCollisionCommand(ob, false);
            m_UndoRedoManager.Add(command, this);

            listBoxCollision.SelectedIndexChanged -= new EventHandler(listBoxCollision_SelectedIndexChanged);

            RemoveCollisionAt(index, sprite);

            if (listBoxCollision.Items.Count > 0)
            {
                index--;
                listBoxCollision.SelectedIndex = index == -1 ? 0 : index;
            }
            else
            {
                buttonDelColl.Enabled = false;
            }

            listBoxCollision.SelectedIndexChanged += new EventHandler(listBoxCollision_SelectedIndexChanged);

            SetCollisionIndex(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj_"></param>
        public void RemoveCollisionAt(int index_, Sprite2D sprite_)
        {
            if (listBoxCollision.InvokeRequired == true)
            {
                listBoxCollision.Invoke(new Action(() => RemoveCollisionAt(index_, sprite_)));
            }
            else
            {
                sprite_.RemoveCollisionAt(index_);
                listBoxCollision.Items.RemoveAt(index_);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj_"></param>
        public void RemoveCollision(Shape2DObject obj_, Sprite2D sprite_)
        {
            if (listBoxCollision.InvokeRequired == true)
            {
                listBoxCollision.Invoke(new Action(() => RemoveCollision(obj_, sprite_)));
            }
            else
            {
                sprite_.RemoveCollision(obj_);
                listBoxCollision.Items.Remove(obj_);
            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void undoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_OnUndoRedoManagerAction = true;
            Thread t = new Thread(new ThreadStart(delegate { m_UndoRedoManager.Undo(); }));
            t.Start();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void redoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_OnUndoRedoManagerAction = true;
            Thread t = new Thread(new ThreadStart(delegate { m_UndoRedoManager.Redo(); }));
            t.Start();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UndoRedoEventCommandDone(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnUndoRedoEventCommandDone()));
            }
            else
            {
                OnUndoRedoEventCommandDone();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnUndoRedoEventCommandDone()
        {
            undoToolStripMenuItem.Enabled = m_UndoRedoManager.CanUndo;
            redoToolStripMenuItem.Enabled = m_UndoRedoManager.CanRedo;
            m_OnUndoRedoManagerAction = false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UndoRedoCommandAdded(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnUndoRedoCommandAdded()));
            }
            else
            {
                OnUndoRedoCommandAdded();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnUndoRedoCommandAdded()
        {
            undoToolStripMenuItem.Enabled = m_UndoRedoManager.CanUndo;
            redoToolStripMenuItem.Enabled = m_UndoRedoManager.CanRedo;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cutToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void copyToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (tabControl1.SelectedIndex == 1
                && m_Sprite2DComponent.CurrentShape2DManipulator != null
                && m_Sprite2DComponent.CurrentShape2DManipulator.Shape2DObject != null)
            {
                Clipboard.SetData(typeof(Shape2DObject).FullName, m_Sprite2DComponent.CurrentShape2DManipulator.Shape2DObject);
            }
            else if (tabControl1.SelectedIndex == 2
                /*&& m_Sprite2DComponent.CurrentShape2DManipulator != null
                && m_Sprite2DComponent.CurrentShape2DManipulator.CurrentSocket2D != null*/)
            {
                //Clipboard.SetData("Socket2D", );
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void pasteToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (Clipboard.ContainsData(typeof(Shape2DObject).FullName) == true
                && m_Sprite2DComponent.CurrentSprite2D != null)
            {
                if (tabControl1.SelectedIndex != 1)
                {
                    tabControl1.SelectedIndex = 1;
                }

                Shape2DObject obj = (Shape2DObject)Clipboard.GetData(typeof(Shape2DObject).FullName);
                if (obj != null)
                {
                    AddCollision(obj, m_Sprite2DComponent.CurrentSprite2D);
                }
            }
            else if (Clipboard.ContainsData("Socket2D") == true)
            {
                //KeyValuePair<string, Vector2> socket = (KeyValuePair<string, Vector2>)Clipboard.GetData("Socket2D");
            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Sprite2DEditorForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Shift)
            {
                m_Sprite2DComponent.ShiftKeyPressed = true;
            }

            if (e.Control)
            {
                m_Sprite2DComponent.ControlKeyPressed = true;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Sprite2DEditorForm_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Shift == false)
            {
                m_Sprite2DComponent.ShiftKeyPressed = false;
            }

            if (e.Control == false)
            {
                m_Sprite2DComponent.ControlKeyPressed = false;
            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonAddSocket_Click(object sender, EventArgs e)
        {
            if (m_Sprite2DComponent.CurrentSprite2D != null)
            {
                InputBox inputForm = new InputBox("Create a new socket", "Choose a name for the new socket", "socket name");

                while (inputForm.ShowDialog(this) == DialogResult.OK)
                {
                    if (m_Sprite2DComponent.CurrentSprite2D.IsValidSocketName(inputForm.InputText) == true)
                    {
                        UndoRedoAddDeleteSocketCommand command = new UndoRedoAddDeleteSocketCommand(inputForm.InputText, Vector2.Zero, true);
                        m_UndoRedoManager.Add(command, m_Sprite2DComponent);

                        m_Sprite2DComponent.CurrentSprite2D.AddSocket(inputForm.InputText, Vector2.Zero);
                        FillSocketControl(m_Sprite2DComponent.CurrentSprite2D);
                        listBoxSockets.SelectedIndex = listBoxSockets.Items.Count - 1;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonDelSocket_Click(object sender, EventArgs e)
        {
            if (m_Sprite2DComponent.CurrentSprite2D != null
                && listBoxSockets.SelectedIndex != -1)
            {
                m_Sprite2DComponent.CurrentSprite2D.RemoveSocket(listBoxSockets.SelectedItem as string);
                listBoxSockets.Items.RemoveAt(listBoxSockets.SelectedIndex);
                FillSocketControl(m_Sprite2DComponent.CurrentSprite2D);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listBoxSockets_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (m_Sprite2DComponent.CurrentSprite2D != null)
            {
                if (m_Sprite2DComponent.CurrentSocketIndex != listBoxSockets.SelectedIndex
                    && m_OnUndoRedoManagerAction == false)
                {
                    UndoRedoListControlSelection c = new UndoRedoListControlSelection(m_Sprite2DComponent.CurrentSocketIndex);
                    m_UndoRedoManager.Add(c, listBoxSockets);
                }

                SetSocketIndex();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void SetSocketIndex()
        {
            int index = listBoxSockets.SelectedIndex;

            m_LastSocketIndex = m_Sprite2DComponent.CurrentSocketIndex;
            m_Sprite2DComponent.CurrentSocketIndex = index;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="s"></param>
        /// <param name="e"></param>
        private void propertyGridSocket_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            UndoRedoPropertyValueChangedCommand c = new UndoRedoPropertyValueChangedCommand(e.ChangedItem.PropertyDescriptor, e.OldValue);
            m_UndoRedoManager.Add(c, m_Sprite2DComponent.CurrentSprite2D);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Sprite2DEditorFormClosed(object sender, FormClosedEventArgs e)
        {
            CheckSpriteChanges();
        }


        /// <summary>
        /// 
        /// </summary>
        public void CheckSpriteChanges()
        {
            if (m_Sprite2DComponent.IsSprite2DChange() == true)
            {
                if (MessageBox.Show(this, "Do you want to apply changes ?", "Apply changes", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    m_Sprite2DComponent.ApplySprite2DChanges();
                }
            }
        }

        delegate void SetPropertyChangedCallback(object sender, PropertyChangedEventArgs e);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void sprite2DPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (propertyGridSprite2D.InvokeRequired)
            {
                SetPropertyChangedCallback c = new SetPropertyChangedCallback(Sprite2DPropertyChangedCallback);
                propertyGridSprite2D.Invoke(c, new object[] { sender, e });
            }
            else
            {
                Sprite2DPropertyChangedCallback(sender, e);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Sprite2DPropertyChangedCallback(object sender, PropertyChangedEventArgs e)
        {
            propertyGridSprite2D.Refresh();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="s"></param>
        /// <param name="e"></param>
        private void propertyGridSprite2D_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            UndoRedoPropertyValueChangedCommand c = new UndoRedoPropertyValueChangedCommand(e.ChangedItem.PropertyDescriptor, e.OldValue);
            m_UndoRedoManager.Add(c, e.ChangedItem.Parent.Value);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBoxSelectedSprite2D_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void backgroundToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.FullOpen = true;

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                //GameHelper.GetGameComponent<Grid2DComponent>(this.m_XnaEditorForm.Game).ColorLine = dialog.Color;
                //GameHelper.GetGameComponent<Grid2DComponent>(this.m_XnaEditorForm.Game).GridColor = dialog.Color;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tabControl1_TabIndexChanged(object sender, EventArgs e)
        {
            if (m_TabSelectedIndex != tabControl1.SelectedIndex)
            {
                switch (tabControl1.SelectedIndex)
                {
                    case 0:
                        m_Sprite2DComponent.Mode = Sprite2DEditorComponent.EditonMode.HotSpot;
                        break;

                    case 1:
                        m_Sprite2DComponent.Mode = Sprite2DEditorComponent.EditonMode.Collision;
                        break;

                    case 2:
                        m_Sprite2DComponent.Mode = Sprite2DEditorComponent.EditonMode.Socket;
                        break;
                }

                m_TabSelectedIndex = tabControl1.SelectedIndex;
                m_UndoRedoManager.Clear();
            }
        }



        /// <summary>
        /// 
        /// </summary>
        public ExternalTool ExternalTool
        {
            get;
            private set;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path_"></param>
        /// <param name="obj_"></param>
        public void SetCurrentObject(string path_, Entity obj_)
        {
            if (obj_ is Sprite2D)
            {
                Sprite2D sprite2D = obj_ as Sprite2D;

                CheckSpriteChanges();

                m_Sprite2DComponent.SetCurrentSprite2D(path_, sprite2D);
                //propertyGridSprite2D.SelectedObject = sprite2D;
                sprite2D.PropertyChanged += new PropertyChangedEventHandler(sprite2DPropertyChanged);
                FillSpriteControl(sprite2D);

                Text = "Sprite2D Editor - " + path_;
            }
            else
            {
                LogManager.Instance.WriteLineError("Sprite2DEditorForm.SetCurrentObject() : Entity is not a Sprite2D");
            }
        }

    }
}
