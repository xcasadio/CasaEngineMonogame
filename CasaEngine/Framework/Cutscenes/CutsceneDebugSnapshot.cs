using CasaEngine.Framework.Scripting.Coroutines;

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
    {
        State = state;
        AssetId = assetId;
        AssetName = assetName;
        AssetFileName = assetFileName;
        ActiveHandle = activeHandle;
        ValidationMessages = validationMessages;
        ActiveCoroutines = activeCoroutines;
    }

    public CutsceneRuntimeState State { get; }

    public Guid AssetId { get; }

    public string AssetName { get; }

    public string AssetFileName { get; }

    public CoroutineHandle ActiveHandle { get; }

    public IReadOnlyList<CutsceneValidationMessage> ValidationMessages { get; }

    public IReadOnlyList<CoroutineDebugInfo> ActiveCoroutines { get; }
}