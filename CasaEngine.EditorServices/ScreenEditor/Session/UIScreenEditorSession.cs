using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.EditorServices.ScreenEditor.Preview;
using CasaEngine.EditorServices.ScreenEditor.Xaml;
using CasaEngine.Framework.UI.MGUI;
using Newtonsoft.Json.Linq;

namespace CasaEngine.EditorServices.ScreenEditor.Session;

public sealed class UIScreenEditorSession
{
    private readonly UIScreenXamlParser _parser;
    private readonly UIScreenXamlSerializer _serializer;
    private readonly UIScreenPreviewBuilder _previewBuilder;

    public UIScreenAsset? CurrentAsset { get; private set; }

    public string? CurrentAssetFilePath { get; private set; }

    public string? SourceXamlFilePath { get; private set; }

    public UIScreenDocument? Document { get; private set; }

    public bool IsDirty { get; private set; }

    public DocumentNodeId? SelectedNodeId { get; private set; }

    public string? PreviewMarkup { get; private set; }

    public string? LastErrorMessage { get; private set; }

    public UIScreenEditorSession()
        : this(new UIScreenXamlParser(), new UIScreenXamlSerializer(), new UIScreenPreviewBuilder())
    {
    }

    public UIScreenEditorSession(UIScreenXamlParser parser, UIScreenXamlSerializer serializer, UIScreenPreviewBuilder previewBuilder)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _previewBuilder = previewBuilder ?? throw new ArgumentNullException(nameof(previewBuilder));
    }

    public void Open(UIScreenAsset asset, string assetFilePath)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetFilePath);

        CurrentAsset = asset;
        CurrentAssetFilePath = Path.GetFullPath(assetFilePath);
        SourceXamlFilePath = ResolveSourceXamlPath(asset, CurrentAssetFilePath);
        SelectedNodeId = null;
        IsDirty = false;

        try
        {
            Document = _parser.ParseFile(SourceXamlFilePath);
            RebuildPreview();
            LastErrorMessage = null;
        }
        catch (Exception ex)
        {
            Document = null;
            PreviewMarkup = null;
            LastErrorMessage = ex.Message;
        }
    }

    public void Reload()
    {
        EnsureAssetFilePath();
        var asset = ReadAssetFromFile(CurrentAssetFilePath!);
        Open(asset, CurrentAssetFilePath!);
    }

    public void Save()
    {
        EnsureDocumentLoaded();
        EnsureSourceXamlPath();

        File.WriteAllText(SourceXamlFilePath!, _serializer.Serialize(Document!));
        IsDirty = false;
        RebuildPreview();
        LastErrorMessage = null;
    }

    public void UpdateDocument(UIScreenDocument document, bool markDirty = true)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        if (markDirty)
        {
            IsDirty = true;
        }

        RebuildPreview();
    }

    public void MarkDirty() => IsDirty = true;

    public void SetSelection(DocumentNodeId? nodeId)
    {
        if (nodeId.HasValue && Document?.FindNode(nodeId.Value) == null)
        {
            throw new InvalidOperationException("Cannot select a node that does not exist in the current screen document.");
        }

        SelectedNodeId = nodeId;
    }

    private void RebuildPreview()
    {
        if (Document == null)
        {
            PreviewMarkup = null;
            return;
        }

        try
        {
            PreviewMarkup = _previewBuilder.CreatePreviewMarkup(Document);
            LastErrorMessage = null;
        }
        catch (Exception ex)
        {
            PreviewMarkup = null;
            LastErrorMessage = ex.Message;
        }
    }

    private void EnsureAssetFilePath()
    {
        if (string.IsNullOrWhiteSpace(CurrentAssetFilePath))
        {
            throw new InvalidOperationException("UIScreenEditorSession has no current asset file path.");
        }
    }

    private void EnsureSourceXamlPath()
    {
        if (string.IsNullOrWhiteSpace(SourceXamlFilePath))
        {
            throw new InvalidOperationException("UIScreenEditorSession has no source XAML path.");
        }
    }

    private void EnsureDocumentLoaded()
    {
        if (Document == null)
        {
            throw new InvalidOperationException("UIScreenEditorSession has no loaded document.");
        }
    }

    private static UIScreenAsset ReadAssetFromFile(string assetFilePath)
    {
        var document = JObject.Parse(File.ReadAllText(assetFilePath));
        var asset = new UIScreenAsset();
        asset.Load(document);
        asset.FileName = Path.GetRelativePath(EngineEnvironment.ResolveProjectPath(EngineEnvironment.ProjectPath), assetFilePath);
        return asset;
    }

    private static string ResolveSourceXamlPath(UIScreenAsset asset, string assetFilePath)
    {
        if (string.IsNullOrWhiteSpace(asset.SourceXamlFile))
        {
            throw new InvalidOperationException("UIScreen asset is missing 'SourceXamlFile'.");
        }

        if (Path.IsPathRooted(asset.SourceXamlFile))
        {
            return asset.SourceXamlFile;
        }

        var assetDirectory = Path.GetDirectoryName(assetFilePath);
        if (!string.IsNullOrWhiteSpace(assetDirectory))
        {
            var relativeToAsset = Path.GetFullPath(Path.Combine(assetDirectory, asset.SourceXamlFile));
            if (File.Exists(relativeToAsset))
            {
                return relativeToAsset;
            }
        }

        return Path.GetFullPath(Path.Combine(EngineEnvironment.ResolveProjectPath(EngineEnvironment.ProjectPath), asset.SourceXamlFile));
    }
}