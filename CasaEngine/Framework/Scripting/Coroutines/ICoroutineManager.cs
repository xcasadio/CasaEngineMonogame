using System.Collections;

namespace CasaEngine.Framework.Scripting.Coroutines;

public interface ICoroutineManager
{
    CoroutineHandle StartCoroutine(IEnumerator routine);

    CoroutineHandle StartCoroutine(IEnumerator routine, object? owner);

    void StopCoroutine(CoroutineHandle handle);

    void StopAllCoroutines();

    void StopAllCoroutines(object owner);

    bool IsRunning(CoroutineHandle handle);

    void Update(CoroutineUpdateContext context);
}