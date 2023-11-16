namespace CasaEngine.Compiler;

public class CallArguments
{
    public string MethodName { get; }

    public CallArguments(string methodName = null)
    {
        MethodName = methodName;
    }
}