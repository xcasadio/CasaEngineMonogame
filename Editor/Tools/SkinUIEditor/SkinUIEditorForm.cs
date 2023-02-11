using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CasaEngine.Editor.Tools;
using CasaEngine.Game;
using CasaEngineCommon.Logger;
using CasaEngine.Assets.UI;

namespace Editor.Tools.SkinUIEditor
{
    /// <summary>
    /// 
    /// </summary>
    public partial class SkinUIEditorForm
        : Form, IEditorForm, IExternalTool
    {
        XnaEditorForm m_XnaEditorForm;

        /// <summary>
        /// 
        /// </summary>
        public SkinUIEditorForm()
        {
            InitializeComponent();

            ExternalTool = new ExternalTool(this);

            m_XnaEditorForm = new XnaEditorForm(this);
            //m_FontPreviewEditorComponent = new FontPreviewEditorComponent(m_XnaEditorForm.Game);
            m_XnaEditorForm.Game.Content.RootDirectory = Engine.Instance.ProjectManager.ProjectPath;
            m_XnaEditorForm.StartGame();
        }


        /// <summary>
        /// 
        /// </summary>
        public Control XnaPanel
        {
            get { return panelXNA; }
        }



        /// <summary>
        /// 
        /// </summary>
        public ExternalTool ExternalTool
        {
            get;
            set;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path_"></param>
        /// <param name="obj_"></param>
        public void SetCurrentObject(string path_, CasaEngine.Gameplay.Actor.Object.BaseObject obj_)
        {
            if (obj_ is SkinUi)
            {
                propertyGrid1.SelectedObject = obj_;
                //m_FontPreviewEditorComponent.Font = obj_ as CasaEngine.Asset.Fonts.Font;
                //this.Text = "Font Preview - " + m_Font.;
            }
            else
            {
                LogManager.Instance.WriteLineError("FontPreviewForm.SetCurrentObject() : BaseObject is not Font");
            }
        }

    }
}
