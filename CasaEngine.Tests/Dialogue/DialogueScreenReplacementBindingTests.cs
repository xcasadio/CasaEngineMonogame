using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.Dialogue.UI;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.UI.MGUI;
using CasaEngine.Tests.UI;
using MGUI.Core.UI;
using MGUI.Core.UI.DataBinding;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Dialogue;

/// <summary>
/// A replacement dialogue markup that breaks the element contract is loaded, then dropped for the built-in markup:
/// the bindings its window created must leave MGUI's static registry with it (gap G10). Loading bound XAML creates
/// bindings, so this class runs in <see cref="MguiDataBindingCollection"/>.
/// </summary>
[Collection(MguiDataBindingCollection.Name)]
public sealed class DialogueScreenReplacementBindingTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("dialogue-replacement-binding-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private sealed class ScreenEnvelopeLoader : IAssetLoader
    {
        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            var asset = new UIScreenAsset();
            asset.Load(JObject.Parse(File.ReadAllText(fileName)));
            return asset;
        }

        public bool IsFileSupported(string fileName) => true;
    }

    [Fact]
    public void ARejectedReplacement_LeavesNoBindingBehind()
    {
        // A name no other test uses, to find this window's binding in the shared registry.
        string boundName = "lblBound" + Guid.NewGuid().ToString("N");
        var screenId = Guid.NewGuid();
        var info = new AssetInfo(screenId) { Name = "ProjectDialogue", FileName = "ProjectDialogue.uiscreen" };
        File.WriteAllText(Path.Combine(_root, "ProjectDialogue.xaml"), $$"""
            <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                    xmlns:dataBinding="clr-namespace:MGUI.Core.UI.DataBinding;assembly=MGUI.Core"
                    Left="0" Top="0" Height="150">
              <StackPanel Name="pnlContent" Orientation="Vertical">
                <TextBlock Name="{{boundName}}" Text="{dataBinding:MGBinding Path=Title}" />
                <StackPanel Name="pnlChoices" Orientation="Vertical" />
              </StackPanel>
            </Window>
            """);
        File.WriteAllText(Path.Combine(_root, info.FileName), $$"""
            { "id": "{{screenId}}", "name": "ProjectDialogue", "source_xaml_file": "ProjectDialogue.xaml" }
            """);
        var assets = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(
                new ProjectSettings { DialogueScreenAsset = screenId.ToString() }, _root, id => id == screenId ? info : null),
        };
        assets.RegisterAssetLoader(typeof(UIScreenAsset), new ScreenEnvelopeLoader());

        var screen = new DialogueScreen(new DialogueService(), static () => { }, null, assets) { ShowCloseButton = false };
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        screen.BuildWindow(desktop);

        Assert.False(screen.UsesReplacementMarkupForTests); // no lblLine: rejected
        Assert.DoesNotContain(DataBindingManager.Bindings, b => b.TargetObject is MGElement e && e.Name == boundName);
        screen.Dispose();
    }
}
