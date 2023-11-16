using CasaEngine.Compiler.Errors;

namespace CasaEngine.Compiler.Results;

public class EvaluationResult
{
    public bool Success => !Errors.Any();
    public IEnumerable<DynamicScriptCompilationError> Errors { get; }

    private EvaluationResult(IEnumerable<DynamicScriptCompilationError> errors = null)
    {
        Errors = errors ?? new List<DynamicScriptCompilationError>();
    }

    public static EvaluationResult WithErrors(IEnumerable<DynamicScriptCompilationError> errors)
        => new EvaluationResult(errors);

    public static EvaluationResult Ok()
        => new EvaluationResult();
}