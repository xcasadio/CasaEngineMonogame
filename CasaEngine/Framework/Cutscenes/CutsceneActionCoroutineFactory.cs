using System.Collections;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting.Coroutines;

namespace CasaEngine.Framework.Cutscenes;

internal static class CutsceneActionCoroutineFactory
{
    public static IEnumerator Create(CutsceneActionData action, World world, CutsceneDirector owner)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(owner);

        return ExecuteAction(action, world, owner);
    }

    private static IEnumerator ExecuteAction(CutsceneActionData action, World world, CutsceneDirector owner)
    {
        switch (action)
        {
            case WaitCutsceneActionData waitAction:
                if (waitAction.Seconds > 0f)
                {
                    yield return new WaitForSeconds(waitAction.Seconds);
                }

                break;

            case MoveToCutsceneActionData moveToAction:
                CharacterControllerMoveToDriverComponent moveToDriver = ResolveMoveToDriver(moveToAction, world, owner);
                moveToDriver.MoveTo(moveToAction.Destination, moveToAction.StoppingDistance, moveToAction.TimeoutSeconds);

                while (moveToDriver.IsMoving)
                {
                    yield return null;
                }

                if (moveToDriver.HasTimedOut)
                {
                    throw new TimeoutException($"MoveTo action timed out for entity '{moveToAction.EntityName}'.");
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

    private static CharacterControllerMoveToDriverComponent ResolveMoveToDriver(MoveToCutsceneActionData action, World world, CutsceneDirector owner)
    {
        Entity entity = FindEntityByName(world, action.EntityName)
            ?? throw new InvalidOperationException($"MoveTo action target entity '{action.EntityName}' was not found.");

        if (entity.GetComponent<CharacterControllerComponent>() == null)
        {
            throw new InvalidOperationException($"MoveTo action target entity '{action.EntityName}' has no CharacterControllerComponent.");
        }

        CharacterControllerMoveToDriverComponent driver = entity.GetComponent<CharacterControllerMoveToDriverComponent>();
        if (driver != null)
        {
            owner.TrackMoveToDriver(driver);
            return driver;
        }

        driver = new CharacterControllerMoveToDriverComponent();
        entity.AddComponent(driver);
        owner.TrackMoveToDriver(driver);
        return driver;
    }

    private static Entity FindEntityByName(World world, string entityName)
    {
        for (int index = 0; index < world.Entities.Count; index++)
        {
            Entity entity = world.Entities[index];
            if (string.Equals(entity.Name, entityName, StringComparison.Ordinal))
            {
                return entity;
            }
        }

        return null;
    }
}