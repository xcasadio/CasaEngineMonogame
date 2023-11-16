namespace CasaEngine.Compiler.Results;

public class MethodReturnValue : ExecutionResultEntry
{
    public MethodReturnValue(object value) : base("*RETURN_TYPE*", value)
    {
    }
}