namespace CasaEngine.EditorUI.Manipulation;

public interface IDockable
{
    bool Selected
    {
        get;
        set;
    }

    void DrawArchor();
}