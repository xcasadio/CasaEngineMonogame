using System.Text.Json;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Remote;
using CasaEngine.Editor.Remote.Protocol;
using CasaEngine.EditorServices.History;
using CasaEngine.EditorServices.ScreenEditor.Commands;
using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.Framework.Assets;
using Xunit;

namespace CasaEngine.Tests.Editor.Remote;

/// <summary>ADR-0093, task T2.4: the UI screen tree and the property set restricted to the inspector registry (P12).</summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class EditorRemoteScreenCommandsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "casa-remote-screen-" + Guid.NewGuid().ToString("N"));
    private readonly RemoteTestHost _host;
    private readonly EditorRemoteControl _control;
    private readonly List<Guid> _catalogEntries = new();
    private readonly EditorHistoryContext _context;
    private readonly Guid _screenId;
    private readonly UIScreenNode _title;
    private readonly UIScreenNode _image;

    public EditorRemoteScreenCommandsTests()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "project"));
        _host = new RemoteTestHost { ProjectDirectory = Path.Combine(_directory, "project") };
        _control = new EditorRemoteControl(_host, Path.Combine(_directory, "discovery"));

        var root = new UIScreenNode("StackPanel") { Name = "Root" };
        _title = new UIScreenNode("TextBlock") { Name = "Title" };
        _title.SetProperty("Text", "Hello");
        _title.SetProperty("Grid.Row", "1");
        _image = new UIScreenNode("Image") { Name = "Logo" };
        root.AddChild(_title);
        root.AddChild(_image);
        var document = new UIScreenDocument();
        document.SetRoot(root);
        _host.ScreenDocument = document;

        _screenId = Catalog("Screens/Menu.uiscreen");
        _context = new EditorHistoryContext(EditorHistoryContextKind.UIScreen, "remote-screen-test-" + _screenId.ToString("N"));
        EditorHistoryService.Current.IsSuspended = false;
        _host.ScreenStack = new UICommandStack(EditorHistoryService.Current.GetOrCreate(_context));
        _host.AssetContexts[_screenId] = _context;
    }

    public void Dispose()
    {
        _control.Dispose();
        EditorHistoryService.Current.Remove(_context);
        foreach (Guid id in _catalogEntries)
        {
            AssetCatalog.RemoveInternal(id, out _);
        }

        Directory.Delete(_directory, recursive: true);
    }

    private Guid Catalog(string fileName)
    {
        Guid id = Guid.NewGuid();
        AssetCatalog.AddInternal(id, "remote-screen-test-" + id.ToString("N"), fileName);
        _catalogEntries.Add(id);
        string path = Path.Combine(_host.ProjectDirectory!, fileName.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "<Window />");
        return id;
    }

    private RemoteResponse Call(string method, object parameters = null)
    {
        var call = new EditorRemoteCall(new RemoteRequest
        {
            Id = 1,
            Method = method,
            Params = parameters == null ? null : RemoteMessageCodec.ToElement(parameters),
        });
        _control.Dispatcher.Enqueue(call);
        _control.Pump();
        Assert.True(call.IsCompleted);
        return call.Completion.Result;
    }

    private static JsonElement Ok(RemoteResponse response)
    {
        Assert.True(response.Error == null, response.Error?.ToString());
        return response.Result!.Value;
    }

    private RemoteResponse Set(string node, string property, object value)
        => Call("screen.set_property", new { asset = _screenId.ToString(), node, property, value });

    [Fact]
    public void Get_ListsTheNodesWithTheirInspectorProperties()
    {
        JsonElement result = Ok(Call("screen.get", new { asset = _screenId.ToString() }));

        JsonElement[] nodes = result.GetProperty("nodes").EnumerateArray().ToArray();
        Assert.Equal(3, nodes.Length);
        Assert.False(result.GetProperty("truncated").GetBoolean());
        JsonElement title = nodes[1];
        Assert.Equal("Title", title.GetProperty("name").GetString());
        Assert.Equal(nodes[0].GetProperty("id").GetGuid(), title.GetProperty("parent_id").GetGuid());
        JsonElement text = title.GetProperty("properties").EnumerateArray().Single(p => p.GetProperty("name").GetString() == "Text");
        Assert.Equal("Hello", text.GetProperty("value").GetString());
        Assert.Equal("Grid.Row", Assert.Single(title.GetProperty("other_properties").EnumerateArray()).GetString());
        JsonElement source = nodes[2].GetProperty("properties").EnumerateArray().Single(p => p.GetProperty("name").GetString() == "SourceName");
        Assert.Equal("asset", source.GetProperty("type").GetString());
    }

    [Fact]
    public void SetProperty_GoesThroughTheScreenHistory_AndIsUndoable()
    {
        Ok(Set("Title", "Text", "Play"));
        Ok(Set(_title.Id.Value.ToString(), "FontSize", 24));
        Ok(Set("Title", "IsBold", true));

        Assert.Equal("Play", _title.Properties["Text"].SerializedValue);
        Assert.Equal("24", _title.Properties["FontSize"].SerializedValue);
        Assert.Equal("True", _title.Properties["IsBold"].SerializedValue);

        Assert.True(Ok(Call("history.undo", new { context = _screenId.ToString() })).GetProperty("done").GetBoolean());
        Assert.Null(_title.Properties["IsBold"].SerializedValue);
        Ok(Call("history.undo", new { context = _screenId.ToString() }));
        Ok(Call("history.undo", new { context = _screenId.ToString() }));
        Assert.Equal("Hello", _title.Properties["Text"].SerializedValue);
    }

    [Fact]
    public void SetProperty_NullClearsTheValue()
    {
        Ok(Set("Title", "Text", null));

        Assert.Null(_title.Properties["Text"].SerializedValue);
    }

    [Fact]
    public void SetProperty_RefusesAMissingOrAmbiguousNode()
    {
        Assert.Equal(RemoteErrorReasons.NotFound, Set("Nope", "Text", "x").Error!.Data!.Reason);
        Assert.Equal(RemoteErrorReasons.NotFound, Set(Guid.NewGuid().ToString(), "Text", "x").Error!.Data!.Reason);

        _image.Name = "Title";
        Assert.Equal(RemoteErrorCodes.InvalidParams, Set("Title", "Text", "x").Error!.Code);
    }

    /// <summary>Plan P12: only editable inspector properties, never markup, attached properties or over-long text.</summary>
    [Theory]
    [InlineData("Unknown", "1")]
    [InlineData("Grid.Row", "1")]
    [InlineData("Text{x}", "1")]
    [InlineData("x:Name", "1")]
    [InlineData("Text", "{Binding Score}")]
    [InlineData("Text", "  {StaticResource Accent}")]
    [InlineData("FontSize", "1.5")]
    [InlineData("FontSize", "big")]
    [InlineData("IsBold", "yes")]
    [InlineData("Opacity", "NaN")]
    [InlineData("Opacity", "1e400")]
    public void Validate_RefusesWhatTheRegistryDoesNotAllow(string property, string value)
    {
        Assert.NotNull(EditorRemoteScreenCommands.Validate("TextBlock", property, value));
    }

    [Theory]
    [InlineData("Text", "Hello")]
    [InlineData("FontSize", "-3")]
    [InlineData("Opacity", "0.5")]
    [InlineData("IsBold", "False")]
    [InlineData("Text", null)]
    public void Validate_AcceptsRegistryValues(string property, string value)
    {
        Assert.Null(EditorRemoteScreenCommands.Validate("TextBlock", property, value));
    }

    [Fact]
    public void SetProperty_RefusesTooLongTextAndUnchangedNodeStays()
    {
        RemoteResponse response = Set("Title", "Text", new string('a', EditorRemoteValidation.MaxPropertyTextLength + 1));

        Assert.Equal(RemoteErrorCodes.InvalidParams, response.Error!.Code);
        Assert.Equal("Hello", _title.Properties["Text"].SerializedValue);
        Assert.Equal(RemoteErrorCodes.InvalidParams, Set("Title", "Text", "{Binding Score}").Error!.Code);
        Assert.Equal("Hello", _title.Properties["Text"].SerializedValue);
    }

    [Fact]
    public void SetProperty_ImageSourceTakesASpriteOfTheCatalog()
    {
        Guid sprite = Catalog("Sprites/hero.sprite");
        Guid material = Catalog("Materials/Stone.material");

        Ok(Set("Logo", "SourceName", sprite.ToString("N")));

        Assert.Equal(sprite.ToString("D"), _image.Properties["SourceName"].SerializedValue);
        Assert.Equal(RemoteErrorCodes.InvalidParams, Set("Logo", "SourceName", material.ToString()).Error!.Code);
        Assert.Equal(RemoteErrorCodes.InvalidParams, Set("Logo", "SourceName", Guid.NewGuid().ToString()).Error!.Code);
        Assert.Equal(RemoteErrorCodes.InvalidParams, Set("Logo", "SourceName", "hero").Error!.Code);
    }
}
