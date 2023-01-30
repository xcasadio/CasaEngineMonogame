namespace CasaEngine.Editor.Manipulation
{
    public interface IDockable
    {
        bool Selected
        {
            get;
            set;
        }

        void DrawArchor();
    }
}
