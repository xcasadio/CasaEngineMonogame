using System.Collections;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting.Coroutines;

namespace CasaEngine.Framework.Cutscenes;

internal static class CutsceneActionCoroutineFactory
{
    public static IEnumerator Create(CutsceneActionData action, World world, object owner)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(owner);

        return ExecuteAction(action, world, owner);
    }

    private static IEnumerator ExecuteAction(CutsceneActionData action, World world, object owner)
    {
        switch (action)
        {
            case WaitCutsceneActionData waitAction:
                if (waitAction.Seconds > 0f)
                {
                    yield return new WaitForSeconds(waitAction.Seconds);
                }

                break;

            case SequenceCutsceneActionData sequenceAction:
                for (int index = 0; index < sequenceAction.Actions.Count; index++)
                {
                    yield return ExecuteAction(sequenceAction.Actions[index], world, owner);
                }

                break;

            case ParallelCutsceneActionData parallelAction:
                if (parallelAction.Actions.Count == 0)
                {
                    yield break;
                }

                var handles = new List<CoroutineHandle>(parallelAction.Actions.Count);
                for (int index = 0; index < parallelAction.Actions.Count; index++)
                {
                    CoroutineHandle handle = world.CoroutineManager.StartCoroutine(
                        ExecuteAction(parallelAction.Actions[index], world, owner),
                        owner,
                        $"Cutscene.Parallel[{index}]");
                    handles.Add(handle);
                }

                for (int index = 0; index < handles.Count; index++)
                {
                    yield return handles[index];
                }

                break;

            default:
                throw new InvalidOperationException($"Unsupported cutscene action data type: {action.GetType().FullName}");
        }
    }
}