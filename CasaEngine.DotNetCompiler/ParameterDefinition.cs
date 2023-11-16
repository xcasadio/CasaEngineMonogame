namespace CasaEngine.DotNetCompiler;

public class ParameterDefinition
{
    public string Key { get; }
    public ParameterDefinitionType Type { get; }
    public ParameterDirection Direction { get; }

    public ParameterDefinition(string key, ParameterDefinitionType type, ParameterDirection direction)
    {
        Key = key;
        Type = type;
        Direction = direction;
    }
}