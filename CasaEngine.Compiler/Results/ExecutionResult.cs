using CasaEngine.Compiler.Errors;

namespace CasaEngine.Compiler.Results;

public class ExecutionResult
{
    public List<ExecutionResultEntry> Results { get; }

    public List<DynamicScriptExecutionError> Errors { get; }

    public bool Success => !Errors.Any();

    private ExecutionResult(List<DynamicScriptExecutionError> errors = null)
    {
        Results = new List<ExecutionResultEntry>();
        Errors = errors ?? new List<DynamicScriptExecutionError>();
    }

    public static ExecutionResult WithError(Exception exception)
        => new ExecutionResult(new List<DynamicScriptExecutionError>()
        {
            new DynamicScriptExecutionError(exception.Message)
        });

    public static ExecutionResult Ok()
        => new ExecutionResult();

    public void Add(ExecutionResultEntry entry)
        => Results.Add(entry);

    public T ReturnValueOf<T>() => (T)Results.FirstOrDefault(x => x is MethodReturnValue)?.Value;

    public object ReturnValue => Results.FirstOrDefault(x => x is MethodReturnValue)?.Value;

    public T GetValue<T>(string key)
        => (T)Results.FirstOrDefault(x => x.OutputName == key)?.Value;

    public object this[string key] => GetValue<object>(key);
}