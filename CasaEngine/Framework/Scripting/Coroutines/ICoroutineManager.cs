using System.Collections;

namespace CasaEngine.Framework.Scripting.Coroutines;

public interface ICoroutineManager
{
    CoroutineHandle StartCoroutine(IEnumerator routine);

    CoroutineHandle StartCoroutine(IEnumerator routine, object? owner);

    CoroutineHandle StartCoroutine(IEnumerator routine, object? owner, string? name);

    void StopCoroutine(CoroutineHandle handle);

    void StopAllCoroutines();

    void StopAllCoroutines(object owner);

    bool IsRunning(CoroutineHandle handle);

    void SetCoroutineName(CoroutineHandle handle, string? name);

    IReadOnlyList<CoroutineDebugInfo> GetActiveCoroutines();

    void Update(CoroutineUpdateContext context);
}