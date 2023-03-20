using CasaEngine.Editor.UndoRedo;
using Editor.Game;
using Editor.Tools.CurveEditor;
using Microsoft.Xna.Framework;
using Editor.Sprite2DEditor;
using CasaEngine.Editor.Tools;
using Editor.Sprite2DEditor.Event;
using CasaEngine.Framework.Entities;
using CasaEngine.Core.Logger;
using CasaEngine.Framework;
using CasaEngine.Framework.Assets.Graphics2D;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Gameplay.Actor.Event;

namespace Editor.Tools.Graphics2D
{
    /// <summary>
    /// 
    /// </summary>
    public partial class Animation2DEditorForm
        : Form, IEditorForm, IExternalTool
    {

        XnaEditorForm m_XnaEditorForm;
        UndoRedoManager m_UndoRedoManager;
        Animation2DEditorComponent m_Animation2DComponent;

        private Control[] m_ListViewSubControls;



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
        public Animation2DEditorForm()
        {
            InitializeComponent();

            ExternalTool = new ExternalTool(this);

            m_UndoRedoManager = new UndoRedoManager();
            m_UndoRedoManager.EventCommandDone += new EventHandler(UndoRedoEventCommandDone);
            m_UndoRedoManager.UndoRedoCommandAdded += new EventHandler(UndoRedoCommandAdded);

            m_XnaEditorForm = new XnaEditorForm(this);
            m_XnaEditorForm.Game.Content.RootDirectory = EngineComponents.ProjectManager.ProjectPath;
            m_Animation2DComponent = new Animation2DEditorComponent(m_XnaEditorForm.Game);
            m_Animation2DComponent.UndoRedoManager = m_UndoRedoManager;
            m_Animation2DComponent.CurrentAnimationSetted += new EventHandler(OnCurrentAnimationSetted);
            m_XnaEditorForm.StartGame();

            listViewFrame.SubItemClicked += new WinForm.SubItemEventHandler(listViewFrame_SubItemClicked);
            listViewFrame.SubItemEndEditing += new WinForm.SubItemEndEditingEventHandler(listViewFrame_SubItemEndEditing);
        }




        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void buttonEventForListViewEx_Click(object sender, EventArgs e)
        {
            if (listViewFrame.SelectedIndices.Count > 0)
            {
                Animation2D anim2D = m_Animation2DComponent.CurrentAnimation;
                Frame2D[] frames = anim2D.GetFrames();

                List<EventActor> evts = frames[listViewFrame.SelectedIndices[0]].Events;

                AnimationListEventForm form = new AnimationListEventForm(evts);

                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    anim2D.SetFrameEvents(listViewFrame.SelectedIndices[0], form.EventActorList);
                    Control c = (Control)sender;
                    c.Text = frames[listViewFrame.SelectedIndices[0]].EventsToString();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void listViewFrame_SubItemClicked(object sender, WinForm.SubItemEventArgs e)
        {
            listViewFrame.StartEditing(m_ListViewSubControls[e.SubItem], e.Item, e.SubItem);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void listViewFrame_SubItemEndEditing(object sender, WinForm.SubItemEndEditingEventArgs e)
        {
            if (e.Cancel)
            {
                return;
            }

            Animation2D anim2D = m_Animation2DComponent.CurrentAnimation;

            if (anim2D == null)
            {
                return;
            }

            switch (e.SubItem)
            {
                //case 0 : index : readonly
                case 1: // name
                    //Sprite2D sprite2D = GameInfo.Instance.Asset2DManager.GetSprite2DByName(e.DisplayText);
                    //anim2D.SetFrameSprite2D(sprite2D.Id, e.Item.Index);
                    break;

                case 2: //delay
                    string num = e.DisplayText.Replace(".", ",");
                    try
                    {
                        anim2D.SetFrameDelay(e.Item.Index, Convert.ToSingle(num));
                    }
                    catch (Exception) { e.DisplayText = "0.1"; }
                    break;

                case 3: //event
                    //e.DisplayText = anim2D.EventsToString(e.Item.Index);
                    break;
            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void undoToolStripMenuItem_Click(object sender, EventArgs e)
        {
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
            Thread t = new Thread(new ThreadStart(delegate { m_UndoRedoManager.Redo(); }));
            t.Start();
        }

        delegate void DefaultEventDelegate();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UndoRedoEventCommandDone(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new DefaultEventDelegate(OnUndoRedoEventCommandDone));
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
                Invoke(new DefaultEventDelegate(OnUndoRedoCommandAdded));
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
        private void Animation2DEditorForm_Load(object sender, EventArgs e)
        {
            m_ListViewSubControls = new Control[] {
                                    null,
                                    comboBoxFrameSprite2DName,
                                    numericUpDownFrameDelayForListViewEx,
                                    buttonEventForListViewEx
                                    };
        }


        /// <summary>
        /// 
        /// </summary>
        void FillControlFromAnimation2D(Animation2D anim2D_)
        {
            listViewFrame.Items.Clear();
            int i = 0;
            //curveEditor
            Curve curve = new Curve();

            foreach (Frame2D f in anim2D_.GetFrames())
            {
                listViewFrame.Items.Add(
                    new ListViewItem(new string[]
                        {
                            (i + 1).ToString(),
                            EngineComponents.ObjectManager.GetObjectNameById(f.SpriteId),
                            f.Time.ToString(),
                            f.EventsToString()
                        }));

                CurveKey key = new CurveKey((float)i, anim2D_.GetFrameTime(i));
                curve.Keys.Add(key);

                i++;
            }

            hScrollBarCurrentFrame.Maximum = anim2D_.FrameCount - 1;
            labelCurrentFrame2.Text = "1/" + anim2D_.FrameCount;

            curveControl1.Curves.Clear();
            EditCurve editCurve = new EditCurve("animation", System.Drawing.Color.Red, curve, null);
            curveControl1.Curves.Add(editCurve);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Animation2DEditorFormClosed(object sender, FormClosedEventArgs e)
        {
            CheckAnimationChanges();
        }

        /// <summary>
        /// 
        /// </summary>
        public void CheckAnimationChanges()
        {
            if (m_Animation2DComponent.IsAnimation2DChange() == true)
            {
                if (MessageBox.Show(this, "Do you want to apply changes ?", "Apply changes", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    m_Animation2DComponent.ApplyAnimation2DChanges();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listBoxAnimations_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listBoxAnimations_DoubleClick(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textBoxAnimation2DFilter_TextChanged(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonDeleteAnimation_Click(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBoxAnimationType_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBoxShowPreviousFrame_CheckedChanged(object sender, EventArgs e)
        {
            m_Animation2DComponent.DisplayPreviousFrame = checkBoxShowPreviousFrame.Checked;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBoxShowSpriteOrigin_CheckedChanged(object sender, EventArgs e)
        {
            m_Animation2DComponent.DisplayOrigin = checkBoxShowSpriteOrigin.Checked;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonModifyDelay_Click(object sender, EventArgs e)
        {
            Animation2D anim2D = m_Animation2DComponent.CurrentAnimation;

            if (anim2D == null)
            {
                return;
            }

            DelayFrameOperationForm form = new DelayFrameOperationForm();

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                //for (int i=0; i<anim2D.FrameCount; i++)
                foreach (int i in listViewFrame.SelectedIndices)
                {
                    switch (form.Operation)
                    {
                        case "Addition":
                            anim2D.SetFrameDelay(i, anim2D.GetFrameTime(i) + form.Value);
                            break;

                        case "Substract":
                            anim2D.SetFrameDelay(i, anim2D.GetFrameTime(i) - form.Value);
                            break;

                        case "Divide":
                            if (form.Value != 0.0f)
                            {
                                anim2D.SetFrameDelay(i, anim2D.GetFrameTime(i) / form.Value);
                            }
                            break;

                        case "Multiply":
                            anim2D.SetFrameDelay(i, anim2D.GetFrameTime(i) * form.Value);
                            break;

                        case "Set all":
                            anim2D.SetFrameDelay(i, form.Value);
                            break;

                        default:
                            throw new InvalidOperationException("Editor.Sprite2DEditor.buttonModifyDelay_Click : operation " + form.Operation + " not supported");
                    }
                }

                FillControlFromAnimation2D(anim2D);
            }

            form.Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonAddFrame_Click(object sender, EventArgs e)
        {
            /*string[] sprite2DNames = GameInfo.Instance.Asset2DManager.GetAllSprite2DName();

            if (listBoxAnimations.SelectedIndex == -1
                || sprite2DNames.Length == 0)
            {
                return;
            }

            Sprite2D sprite2D = GameInfo.Instance.Asset2DManager.GetSprite2DByName(sprite2DNames[0]);

            Animation2D anim2D = GameInfo.Instance.Asset2DManager.GetAnimation2DByName(listBoxAnimations.SelectedItem as string);
            anim2D.AddFrame(sprite2D.Id, 0.1f, null);

            listViewFrame.SelectedItems.Clear();
            listViewFrame.Focus();
            listViewFrame.Items[listViewFrame.Items.Count - 1].Selected = true;

            UpdateAnimation2DControl();*/
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonDeleteFrame_Click(object sender, EventArgs e)
        {
            if (listViewFrame.SelectedIndices.Count != 0)
            {
                int index = listViewFrame.SelectedIndices[0];
                Animation2D anim2D = m_Animation2DComponent.CurrentAnimation;
                anim2D.DeleteFrame(index);
                listViewFrame.Items.RemoveAt(index);
                listViewFrame.SelectedIndices.Clear();

                if (index >= listViewFrame.Items.Count)
                {
                    index--;
                }

                listViewFrame.SelectedItems.Clear();
                listViewFrame.Focus();
                listViewFrame.Items[index].Selected = true;
                anim2D.SetCurrentFrame(index);

                hScrollBarCurrentFrame.Maximum = anim2D.FrameCount - 1;
                labelCurrentFrame2.Text = (hScrollBarCurrentFrame.Value + 1).ToString() + "/" + anim2D.FrameCount;

                curveControl1.Curves[0].Keys.RemoveAt(index);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonFrameUp_Click(object sender, EventArgs e)
        {
            int[] a = new int[listViewFrame.SelectedIndices.Count];
            listViewFrame.SelectedIndices.CopyTo(a, 0);

            List<int> indices = new List<int>(a);
            indices.Sort();

            foreach (int index in indices)
            {
                MoveFrame(index, -1);
            }

            listViewFrame.SelectedItems.Clear();
            listViewFrame.Focus();

            foreach (int index in indices)
            {
                if (index - 1 >= 0)
                {
                    listViewFrame.Items[index - 1].Selected = true;
                }
                else
                {
                    listViewFrame.Items[0].Selected = true;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonFrameDown_Click(object sender, EventArgs e)
        {
            if (listViewFrame.SelectedIndices.Count != 0)
            {
                int[] a = new int[listViewFrame.SelectedIndices.Count];
                listViewFrame.SelectedIndices.CopyTo(a, 0);

                List<int> indices = new List<int>(a);
                indices.Sort();

                foreach (int index in indices)
                {
                    MoveFrame(index, 1);
                }

                listViewFrame.SelectedItems.Clear();
                listViewFrame.Focus();

                foreach (int index in indices)
                {
                    if (index + 1 < listViewFrame.Items.Count)
                    {
                        listViewFrame.Items[index + 1].Selected = true;
                    }
                    else
                    {
                        listViewFrame.Items[listViewFrame.Items.Count - 1].Selected = true;
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index_"></param>
        /// <param name="dir_">-1->down, 1->up</param>
        private void MoveFrame(int index_, int dir_)
        {
            Animation2D anim = m_Animation2DComponent.CurrentAnimation;

            if (anim == null)
            {
                return;
            }

            if (dir_ == -1)
            {
                anim.MoveFrameBackward(index_);
            }
            else if (dir_ == 1)
            {
                anim.MoveFrameForward(index_);
            }

            FillControlFromAnimation2D(anim);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void hScrollBarCurrentFrame_Scroll(object sender, ScrollEventArgs e)
        {
            m_Animation2DComponent.PlayAnimation = false;

            Animation2D anim = m_Animation2DComponent.CurrentAnimation;

            if (anim != null)
            {
                anim.SetCurrentFrame(hScrollBarCurrentFrame.Value);
                labelCurrentFrame2.Text = (hScrollBarCurrentFrame.Value + 1) + "/" + anim.FrameCount;

                listViewFrame.SelectedIndexChanged -= new EventHandler(listViewFrame_SelectedIndexChanged);
                listViewFrame.SelectedIndices.Clear();
                listViewFrame.SelectedIndices.Add(hScrollBarCurrentFrame.Value);
                listViewFrame.SelectedIndexChanged += new EventHandler(listViewFrame_SelectedIndexChanged);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listViewFrame_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listViewFrame.SelectedIndices.Count != 0)
            {
                m_Animation2DComponent.PlayAnimation = false;

                Animation2D anim = m_Animation2DComponent.CurrentAnimation;

                if (anim != null)
                {
                    int index = listViewFrame.SelectedIndices[0];
                    hScrollBarCurrentFrame.Value = index;
                    anim.SetCurrentFrame(index);
                    labelCurrentFrame2.Text = (index + 1) + "/" + anim.FrameCount;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonPlay_Click(object sender, EventArgs e)
        {
            m_Animation2DComponent.PlayAnimation = !m_Animation2DComponent.PlayAnimation;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnFrameChanged(object sender, Animation2DFrameChangedEventArgs e)
        {
            if (InvokeRequired == true)
            {
                BeginInvoke(new Action(() => OnFrameChanged(sender, e)));
                return;
            }

            labelCurrentFrame2.Text = (e.NewFrame + 1) + "/" + m_Animation2DComponent.CurrentAnimation.FrameCount.ToString();
            hScrollBarCurrentFrame.Scroll -= new ScrollEventHandler(hScrollBarCurrentFrame_Scroll);
            hScrollBarCurrentFrame.Value = e.NewFrame;
            hScrollBarCurrentFrame.Scroll += new ScrollEventHandler(hScrollBarCurrentFrame_Scroll);
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
            if (obj_ is Animation2D)
            {
                Animation2D anim2D = obj_ as Animation2D;

                CheckAnimationChanges();
                m_Animation2DComponent.SetCurrentAnimation2D(path_, anim2D);
                FillControlFromAnimation2D(anim2D);
                Text = "Animation2D Editor - " + path_;
            }
            else
            {
                LogManager.Instance.WriteLineError("Sprite2DEditorForm.SetCurrentObject() : Entity is not a Animation2D");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnCurrentAnimationSetted(object sender, EventArgs e)
        {
            m_Animation2DComponent.CurrentAnimation.OnFrameChanged += new EventHandler<Animation2DFrameChangedEventArgs>(OnFrameChanged);
            propertyGridSprite2D.Invoke(new Action(() => propertyGridSprite2D.SelectedObject = m_Animation2DComponent.CurrentAnimation));
        }

    }
}
