namespace CasaEngine.Compiler.Results;

public class ExecutionResultEntry
{
    public string OutputName { get; }
    public object Value { get; }

    public ExecutionResultEntry(string outputName, object value)
    {
        OutputName = outputName;
        Value = value;
    }
}