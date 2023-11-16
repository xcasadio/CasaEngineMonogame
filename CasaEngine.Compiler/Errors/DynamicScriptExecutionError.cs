namespace CasaEngine.Compiler.Errors;

public class DynamicScriptExecutionError
{
    public string Message { get; }

    public DynamicScriptExecutionError(string message)
    {
        Message = message;
    }
}