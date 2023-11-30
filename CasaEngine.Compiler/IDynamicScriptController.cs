using CasaEngine.Compiler.Results;

namespace CasaEngine.Compiler;

public interface IDynamicScriptController<T, C>
    where T : IDynamicScriptParameter
    where C : CallArguments
{
    object CreateInstance(string namespaceName, string type);

    EvaluationResult Evaluate(T t);
    ExecutionResult Execute(C callingArgs = null, List<ParameterArgument> methodArgs = null);
}