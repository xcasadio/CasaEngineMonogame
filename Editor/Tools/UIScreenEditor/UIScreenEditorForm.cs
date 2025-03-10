﻿using CasaEngine.Editor.Tools;
using CasaEngine.Editor.UndoRedo;
using System.Globalization;
using Button = CasaEngine.Framework.UserInterface.Controls.Buttons.Button;
using CheckBox = CasaEngine.Framework.UserInterface.Controls.Buttons.CheckBox;
using ComboBox = CasaEngine.Framework.UserInterface.Controls.ComboBox;
using Console = CasaEngine.Framework.UserInterface.Controls.Text.Console;
using Control = CasaEngine.Framework.UserInterface.Control;
using GroupBox = CasaEngine.Framework.UserInterface.Controls.Group.GroupBox;
using Label = CasaEngine.Framework.UserInterface.Controls.Label;
using ListBox = CasaEngine.Framework.UserInterface.Controls.ListBox;
using Panel = CasaEngine.Framework.UserInterface.Controls.Panel.Panel;
using ProgressBar = CasaEngine.Framework.UserInterface.Controls.ProgressBar;
using RadioButton = CasaEngine.Framework.UserInterface.Controls.Buttons.RadioButton;
using TabControl = CasaEngine.Framework.UserInterface.Controls.TabControl;
using TextBox = CasaEngine.Framework.UserInterface.Controls.Text.TextBox;
using TrackBar = CasaEngine.Framework.UserInterface.Controls.Sliders.TrackBar;
using CasaEngine.Framework.UserInterface.Controls.Buttons;
using CasaEngine.Framework.UserInterface.Controls.Group;
using CasaEngine.Framework.UserInterface.Controls.Menu;
using CasaEngine.Framework.UserInterface.Controls.Panel;
using CasaEngine.Framework.UserInterface.Controls.Sliders;
using CasaEngine.Framework.UserInterface.Controls.Text;
using CasaEngine.Framework.UserInterface.Controls.ToolBar;
using CasaEngine.Framework.UserInterface.Controls;
using CasaEngine.Framework.Entities;
using CasaEngine.Core.Logger;
using CasaEngine.Framework;
using CasaEngine.Framework.Game;

namespace Editor.Tools.UIScreenEditor
{
    public partial class UIScreenEditorForm
        : Form, IEditorForm, IExternalTool
    {

        /// <summary>
        /// 
        /// </summary>
        /*public enum UIScreenEditorTool
        {
            Selection,
            CreateControl
        }*/



        bool m_OnUndoRedoManagerAction = false;
        XnaEditorForm m_XnaEditorForm;
        UIScreenEditorComponent m_UIScreenEditorComponent;
        UndoRedoManager m_UndoRedoManager;
        //UIScreenEditorTool m_UIScreenEditorTool = UIScreenEditorTool.Selection;
        Type m_ToolControlType = null;

        Dictionary<string, Type> m_ControlTypes = new();
        System.Windows.Forms.RadioButton m_DefaultRadioButton;
        Dictionary<string, Control> m_Controls = new();



        /// <summary>
        /// 
        /// </summary>
        public System.Windows.Forms.Control XnaPanel
        {
            get { return panelXna; }
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
        public Cursor CurrentCursor
        {
            get;
            set;
        }



        /// <summary>
        /// 
        /// </summary>
        public UIScreenEditorForm()
        {
            InitializeComponent();

            CurrentCursor = Cursors.Default;
            ExternalTool = new ExternalTool(this);

            m_UndoRedoManager = new UndoRedoManager();
            m_UndoRedoManager.EventCommandDone += new System.EventHandler(UndoRedoEventCommandDone);
            m_UndoRedoManager.UndoRedoCommandAdded += new System.EventHandler(UndoRedoCommandAdded);

            m_XnaEditorForm = new XnaEditorForm(this);
            m_UIScreenEditorComponent = new UIScreenEditorComponent(m_XnaEditorForm.Game);
            //m_UIScreenEditorComponent.UndoRedoManager = m_UndoRedoManager;
            m_XnaEditorForm.Game.Content.RootDirectory = CasaEngineGame.Game.GameManager.ProjectManager.ProjectPath;
            m_XnaEditorForm.StartGame();

            FormClosed += new FormClosedEventHandler(ScreenEditorFormClosed);

            m_ControlTypes.Add("Cursor", null);
            m_ControlTypes.Add("ComboBox", typeof(ComboBox));
            m_ControlTypes.Add("ImageBox", typeof(ImageBox));
            m_ControlTypes.Add("Label", typeof(Label));
            m_ControlTypes.Add("ListBox", typeof(ListBox));
            m_ControlTypes.Add("ProgressBar", typeof(ProgressBar));
            m_ControlTypes.Add("TabControl", typeof(TabControl));
            m_ControlTypes.Add("Vector3Box", typeof(Vector3Box));
            m_ControlTypes.Add("AssetSelector", typeof(AssetSelector));

            m_ControlTypes.Add("Button", typeof(Button));
            m_ControlTypes.Add("CheckBox", typeof(CheckBox));
            m_ControlTypes.Add("RadioButton", typeof(RadioButton));
            m_ControlTypes.Add("TreeButton", typeof(TreeButton));

            m_ControlTypes.Add("GroupBox", typeof(GroupBox));
            m_ControlTypes.Add("GroupPanel", typeof(GroupPanel));

            m_ControlTypes.Add("ContextMenu", typeof(ContextMenu));
            m_ControlTypes.Add("MainMenu", typeof(MainMenu));

            m_ControlTypes.Add("Panel", typeof(Panel));
            m_ControlTypes.Add("PanelCollapsible", typeof(PanelCollapsible));
            m_ControlTypes.Add("SideBar", typeof(SideBar));
            m_ControlTypes.Add("SideBarPanel", typeof(SideBarPanel));
            m_ControlTypes.Add("StackPanel", typeof(StackPanel));

            m_ControlTypes.Add("SliderColor", typeof(SliderColor));
            m_ControlTypes.Add("SliderNumeric", typeof(SliderNumeric));
            m_ControlTypes.Add("TrackBar", typeof(TrackBar));

            m_ControlTypes.Add("Console", typeof(Console));
            m_ControlTypes.Add("SpinBox", typeof(SpinBox));
            m_ControlTypes.Add("TextBox", typeof(TextBox));

            m_ControlTypes.Add("ToolBar", typeof(ToolBar));
            m_ControlTypes.Add("ToolBarButton", typeof(ToolBarButton));
            m_ControlTypes.Add("ToolBarPanel", typeof(ToolBarPanel));


            foreach (var pair in m_ControlTypes)
            {
                System.Windows.Forms.RadioButton button = new System.Windows.Forms.RadioButton
                {
                    AutoSize = false,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Appearance = Appearance.Button,
                    BackColor = Color.Transparent,
                    Text = pair.Key,
                    FlatStyle = FlatStyle.Flat,
                    Tag = pair.Value,
                    Padding = new Padding(1, 4, 1, 4),
                    ImageAlign = ContentAlignment.TopLeft,
                    TextAlign = ContentAlignment.MiddleCenter,
                    TextImageRelation = TextImageRelation.ImageBeforeText,
                    UseVisualStyleBackColor = false,
                    Size = new System.Drawing.Size(50, 21),
                    Margin = new Padding(1, 1, 1, 0)
                };
                button.FlatAppearance.BorderColor = Color.Black;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.CheckedBackColor = Color.Gold;
                button.FlatAppearance.MouseDownBackColor = Color.Goldenrod;
                button.FlatAppearance.MouseOverBackColor = Color.Goldenrod;
                button.CheckedChanged += new System.EventHandler(button_CheckedChanged);
                stackPanel1.Controls.Add(button);

                if (m_DefaultRadioButton == null)
                {
                    m_DefaultRadioButton = button;
                }
            }

            //m_XnaEditorForm.Game.UIManager.AutoUnfocus = false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void button_CheckedChanged(object sender, System.EventArgs e)
        {
            if (sender is System.Windows.Forms.RadioButton)
            {
                System.Windows.Forms.RadioButton button = sender as System.Windows.Forms.RadioButton;

                if (button.Checked == true)
                {
                    //Type controlType = button.Tag as Type;
                    m_ToolControlType = button.Tag as Type;
                }
            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void ScreenEditorFormClosed(object sender, FormClosedEventArgs e)
        {
            CheckScreenChanges();
        }

        /// <summary>
        /// 
        /// </summary>
        public void CheckScreenChanges()
        {
            /*if (m_Sprite2DComponent.IsSprite2DChange() == true)
            {
                if (MessageBox.Show(this, "Do you want to apply changes ?", "Apply changes", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    m_Sprite2DComponent.ApplySprite2DChanges();
                }
            }*/
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path_"></param>
        /// <param name="obj_"></param>
        public void SetCurrentObject(string path_, Entity obj_)
        {
            //throw new Exception("The method or operation is not implemented.");
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void undoToolStripMenuItem_Click(object sender, System.EventArgs e)
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
        private void redoToolStripMenuItem_Click(object sender, System.EventArgs e)
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
        private void UndoRedoEventCommandDone(object sender, System.EventArgs e)
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
            //undoToolStripMenuItem.Enabled = m_UndoRedoManager.CanUndo;
            //redoToolStripMenuItem.Enabled = m_UndoRedoManager.CanRedo;
            m_OnUndoRedoManagerAction = false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UndoRedoCommandAdded(object sender, System.EventArgs e)
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
            //undoToolStripMenuItem.Enabled = m_UndoRedoManager.CanUndo;
            //redoToolStripMenuItem.Enabled = m_UndoRedoManager.CanRedo;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScreenEditorForm_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (e.Shift)
            {
                //m_Sprite2DComponent.ShiftKeyPressed = true;
            }

            if (e.Control)
            {
                //m_Sprite2DComponent.ControlKeyPressed = true;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScreenEditorForm_KeyUp(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (e.Shift == false)
            {
                //m_Sprite2DComponent.ShiftKeyPressed = false;
            }

            if (e.Control == false)
            {
                //m_Sprite2DComponent.ControlKeyPressed = false;
            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void panelXna_MouseEnter(object sender, System.EventArgs e)
        {
            //panelXna.Cursor = CurrentCursor;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void panelXna_MouseLeave(object sender, System.EventArgs e)
        {
            //panelXna.Cursor = System.Windows.Forms.Cursors.Default;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void panelXna_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            Control control = m_UIScreenEditorComponent.GetControlAt(e.X, e.Y);

            if (control != null)
            {

            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void panelXna_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void panelXna_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            // selection / manipulation
            if (m_ToolControlType == null)
            {

            }
            else // creation
            {
                try
                {
                    Control control =
                    (Control)m_ToolControlType.Assembly.CreateInstance(
                        m_ToolControlType.FullName, false,
                        System.Reflection.BindingFlags.Default, null, new Object[] { /*m_XnaEditorForm.Game.UIManager*/ },
                    CultureInfo.InvariantCulture, null);
                    control.Left = e.X;
                    control.Top = e.Y;
                    m_UIScreenEditorComponent.AddControl(control);

                    int i = 1;
                    control.Name = m_ToolControlType.Name + i.ToString();
                    while (m_Controls.ContainsKey(control.Name) == true)
                    {
                        i++;
                        control.Name = m_ToolControlType.Name + i.ToString();
                    }
                    m_Controls.Add(control.Name, control);

                    comboBoxControls.Items.Add(control.Name);
                    comboBoxControls.SelectedItem = control.Name;
                    m_ToolControlType = null;
                    m_DefaultRadioButton.Checked = true;
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
        private void comboBoxControls_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            if (comboBoxControls.SelectedIndex != -1)
            {
                m_Controls[comboBoxControls.SelectedItem as string].Focused = true;
                propertyGrid1.SelectedObject = m_Controls[comboBoxControls.SelectedItem as string];
            }
        }

    }
}
