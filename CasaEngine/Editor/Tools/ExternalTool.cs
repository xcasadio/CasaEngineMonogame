namespace CasaEngine.Editor.Tools
{
    public class ExternalTool
    {



        public Form Window
        {
            get;
            private set;
        }



        public ExternalTool(Form @for)
        {
            if (@for == null)
            {
                throw new ArgumentNullException("ExternalTool() : Form is null");
            }

            Window = @for;
        }





    }
}
