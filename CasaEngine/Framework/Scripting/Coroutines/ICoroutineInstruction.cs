namespace CasaEngine.Framework.Scripting.Coroutines;

public interface ICoroutineInstruction
{
    bool IsCompleted(CoroutineUpdateContext context);
}