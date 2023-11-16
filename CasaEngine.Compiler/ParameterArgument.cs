namespace CasaEngine.Compiler;

public class ParameterArgument
{
    public string Key { get; }
    public object Value { get; }

    public ParameterArgument(string key, object value = null)
    {
        Key = key;
        Value = value;
    }
}