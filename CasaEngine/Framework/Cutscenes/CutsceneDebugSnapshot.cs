using CasaEngine.Framework.Scripting.Coroutines;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Cutscenes;

public sealed class CutsceneDebugSnapshot
{
    public CutsceneDebugSnapshot(
        CutsceneRuntimeState state,
        Guid assetId,
        string assetName,
        string assetFileName,
        CoroutineHandle activeHandle,
        IReadOnlyList<CutsceneValidationMessage> validationMessages,
        IReadOnlyList<CoroutineDebugInfo> activeCoroutines)
        : this(
            state,
            assetId,
            assetName,
            assetFileName,
            activeHandle,
            validationMessages,
            activeCoroutines,
            null,
            null,
            null,
            null,
            null)
    { }

    public CutsceneDebugSnapshot(
        CutsceneRuntimeState state,
        Guid assetId,
        string assetName,
        string assetFileName,
        CoroutineHandle activeHandle,
        IReadOnlyList<CutsceneValidationMessage> validationMessages,
        IReadOnlyList<CoroutineDebugInfo> activeCoroutines,
        string activeActionType,
        string activeActionEntityName,
        Vector3? activeActionDestination,
        string activeActionState,
        string activeActionStopReason)
    {
        State = state;
        AssetId = assetId;
        AssetName = assetName;
        AssetFileName = assetFileName;
        ActiveHandle = activeHandle;
        ValidationMessages = validationMessages;
        ActiveCoroutines = activeCoroutines;
        ActiveActionType = activeActionType;
        ActiveActionEntityName = activeActionEntityName;
        ActiveActionDestination = activeActionDestination;
        ActiveActionState = activeActionState;
        ActiveActionStopReason = activeActionStopReason;
    }

    public CutsceneRuntimeState State { get; }

    public Guid AssetId { get; }

    public string AssetName { get; }

    public string AssetFileName { get; }

    public CoroutineHandle ActiveHandle { get; }

    public IReadOnlyList<CutsceneValidationMessage> ValidationMessages { get; }

    public IReadOnlyList<CoroutineDebugInfo> ActiveCoroutines { get; }

    public string ActiveActionType { get; }

    public string ActiveActionEntityName { get; }

    public Vector3? ActiveActionDestination { get; }

    public string ActiveActionState { get; }

    public string ActiveActionStopReason { get; }
}