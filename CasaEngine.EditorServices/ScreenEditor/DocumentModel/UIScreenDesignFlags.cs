namespace CasaEngine.EditorServices.ScreenEditor.DocumentModel;

[Flags]
public enum UIScreenDesignFlags
{
    None = 0,
    Locked = 1 << 0,
    HiddenInHierarchy = 1 << 1,
    HiddenInPreview = 1 << 2,
    Generated = 1 << 3,
}