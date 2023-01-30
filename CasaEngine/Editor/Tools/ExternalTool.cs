namespace CasaEngine.Editor.Tools
{
    public class ExternalTool
    {



        public Form Window
        {
            get;
            private set;
        }



        public ExternalTool(Form form_)
        {
            if (form_ == null)
            {
                throw new ArgumentNullException("ExternalTool() : Form is null");
            }

            Window = form_;
        }





    }
}
