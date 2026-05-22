using CasaEngine.Framework.Cutscenes;

namespace CasaEngine.EditorServices.Cutscenes;

public sealed record CutsceneReadOnlyProperty(string Name, string Value);

public sealed record CutsceneReadOnlyCoroutineInfo(
    int Id,
    string? Name,
    string? OwnerName,
    string? CurrentInstruction,
    string State,
    bool IsPaused,
    float? RemainingTime);

public sealed class CutsceneReadOnlyActionNode
{
    private readonly List<CutsceneReadOnlyProperty> _properties = [];
    private readonly List<CutsceneReadOnlyActionNode> _children = [];

    public CutsceneReadOnlyActionNode(string type, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Type = type;
        Path = path;
    }

    public string Type { get; }

    public string Path { get; }

    public IReadOnlyList<CutsceneReadOnlyProperty> Properties => _properties;

    public IReadOnlyList<CutsceneReadOnlyActionNode> Children => _children;

    internal void AddProperty(string name, string value)
    {
        _properties.Add(new CutsceneReadOnlyProperty(name, value));
    }

    internal void AddChild(CutsceneReadOnlyActionNode child)
    {
        _children.Add(child);
    }
}

public sealed class CutsceneReadOnlyDocument
{
    public CutsceneReadOnlyDocument(
        string assetName,
        string assetFileName,
        CutsceneReadOnlyActionNode? rootAction,
        IReadOnlyList<CutsceneValidationMessage> validationMessages,
        CutsceneRuntimeState runtimeState,
        IReadOnlyList<CutsceneReadOnlyCoroutineInfo> activeCoroutines)
    {
        ArgumentNullException.ThrowIfNull(assetName);
        ArgumentNullException.ThrowIfNull(assetFileName);
        ArgumentNullException.ThrowIfNull(validationMessages);
        ArgumentNullException.ThrowIfNull(activeCoroutines);

        AssetName = assetName;
        AssetFileName = assetFileName;
        RootAction = rootAction;
        ValidationMessages = validationMessages;
        RuntimeState = runtimeState;
        ActiveCoroutines = activeCoroutines;
    }

    public string AssetName { get; }

    public string AssetFileName { get; }

    public CutsceneReadOnlyActionNode? RootAction { get; }

    public IReadOnlyList<CutsceneValidationMessage> ValidationMessages { get; }

    public CutsceneRuntimeState RuntimeState { get; }

    public IReadOnlyList<CutsceneReadOnlyCoroutineInfo> ActiveCoroutines { get; }

    public bool CanEdit => false;
}