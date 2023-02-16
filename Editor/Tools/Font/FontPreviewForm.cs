using CasaEngine.Editor.Tools;
using CasaEngine.Entities;
using CasaEngine.Game;
using CasaEngineCommon.Logger;

namespace Editor.Tools.Font
{
    public partial class FontPreviewForm
        : Form, IEditorForm, IExternalTool
    {

        XnaEditorForm m_XnaEditorForm;
        FontPreviewEditorComponent m_FontPreviewEditorComponent;



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
        public FontPreviewForm()
        {
            InitializeComponent();

            ExternalTool = new ExternalTool(this);

            m_XnaEditorForm = new XnaEditorForm(this);
            m_FontPreviewEditorComponent = new FontPreviewEditorComponent(m_XnaEditorForm.Game);
            m_XnaEditorForm.Game.Content.RootDirectory = Engine.Instance.ProjectManager.ProjectPath;
            m_XnaEditorForm.StartGame();
        }




        /// <summary>
        /// 
        /// </summary>
        /// <param name="path_"></param>
        /// <param name="obj_"></param>
        public void SetCurrentObject(string path_, BaseObject obj_)
        {
            if (obj_ is CasaEngine.Assets.Fonts.Font)
            {
                //m_FontPreviewEditorComponent.Font = obj_ as CasaEngine.Assets.Fonts.Font;
                //this.Text = "Font Preview - " + m_Font.;
            }
            else
            {
                LogManager.Instance.WriteLineError("FontPreviewForm.SetCurrentObject() : BaseObject is not Font");
            }
        }


    }
}
