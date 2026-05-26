using System.Collections;

namespace CasaEngine.Framework.Scripting.Coroutines;

internal sealed class CoroutineInstance : IDisposable
{
    public CoroutineInstance(CoroutineHandle handle, IEnumerator routine, object owner)
    {
        Handle = handle;
        Owner = owner;
        Stack.Push(routine);
    }

    public CoroutineHandle Handle { get; }

    public object Owner { get; }

    public Stack<IEnumerator> Stack { get; } = new();

    public object CurrentYield { get; set; }

    public ICoroutineInstruction CurrentInstruction { get; set; }

    public CoroutineHandle WaitingHandle { get; set; } = CoroutineHandle.Invalid;

    public long ResumeFrameIndex { get; set; } = -1;

    public bool IsStopped { get; set; }

    public bool IsCompleted { get; set; }

    public Exception Fault { get; set; }

    public string Name { get; set; }

    public void Dispose()
    {
        while (Stack.Count > 0)
        {
            DisposeEnumerator(Stack.Pop());
        }

        CurrentYield = null;
        CurrentInstruction = null;
        WaitingHandle = CoroutineHandle.Invalid;
    }

    public static void DisposeEnumerator(IEnumerator enumerator)
    {
        if (enumerator is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}