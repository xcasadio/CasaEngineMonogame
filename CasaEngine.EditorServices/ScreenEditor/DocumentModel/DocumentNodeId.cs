namespace CasaEngine.EditorServices.ScreenEditor.DocumentModel;

public readonly record struct DocumentNodeId(Guid Value)
{
    public static DocumentNodeId NewId()
    {
        return new(Guid.NewGuid());
    }

    public override string ToString()
    {
        return Value.ToString("D");
    }
}