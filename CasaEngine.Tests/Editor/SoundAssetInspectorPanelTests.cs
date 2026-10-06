using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Editor.Controls;
using CasaEngine.Editor.Runtime;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Audio.Spatial;
using CasaEngine.Tests.Audio;
using CasaEngine.Tests.ContentBrowser;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>
/// The variation, range and priority rows of the sound inspector (plan T8.5, decision P31), driven without a GPU:
/// the rows are built on the headless MGUI desktop, the selectors and numeric fields are driven through their public
/// and internal surface, the two buttons through real mouse clicks, and the save goes through the real editor writer
/// to a temporary project folder. The asset catalogue and <see cref="EngineEnvironment.ProjectPath"/> are global
/// state, hence the serialized collection and the restore in <see cref="Dispose"/>.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public sealed class SoundAssetInspectorPanelTests : IDisposable
{
    private static readonly Guid FileA = Guid.Parse("c1000000-0000-0000-0000-00000000000a");
    private static readonly Guid FileB = Guid.Parse("c1000000-0000-0000-0000-00000000000b");
    private static readonly Guid FileC = Guid.Parse("c1000000-0000-0000-0000-00000000000c");
    private static readonly Guid TextureId = Guid.Parse("c2000000-0000-0000-0000-000000000001");

    private static readonly string[] OriginalKeys =
    {
        "audio_file_asset_id", "bus_name", "id", "is_looped", "is_streaming", "name", "pitch", "volume",
    };

    private readonly string _projectDirectory;
    private readonly string _previousProjectPath;

    public SoundAssetInspectorPanelTests()
    {
        _projectDirectory = Path.Combine(Path.GetTempPath(), "casa-sound-inspector-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_projectDirectory);

        _previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = _projectDirectory;

        AssetCatalog.ClearInternal();
        AssetCatalog.AddInternal(new AssetInfo(FileA) { Name = "step_a", FileName = "Sounds/step_a.wav" });
        AssetCatalog.AddInternal(new AssetInfo(FileB) { Name = "step_b", FileName = "Sounds/step_b.ogg" });
        AssetCatalog.AddInternal(new AssetInfo(FileC) { Name = "step_c", FileName = "Sounds/step_c.WAV" });
        AssetCatalog.AddInternal(new AssetInfo(TextureId) { Name = "sheet", FileName = "Textures/sheet.png" });
    }

    public void Dispose()
    {
        EngineEnvironment.ProjectPath = _previousProjectPath;
        AssetCatalog.ClearInternal();

        try
        {
            Directory.Delete(_projectDirectory, recursive: true);
        }
        catch (IOException)
        {
            // A temporary folder left behind is harmless.
        }
    }

    /// <summary>A panel on a headless window, showing a loaded asset.</summary>
    private sealed class Rig
    {
        public ContentBrowserViewTestHarness Harness { get; init; }

        public SoundAssetInspectorPanel Panel { get; init; }

        public MGElement Content { get; init; }

        public string FullPath { get; init; }

        public int DirtyEvents { get; set; }

        public SoundAsset Asset => Panel.LoadedSoundAsset;
    }

    private Rig Open(SoundAsset asset, bool buildContentFirst = false)
    {
        // Big enough for every row to be laid out inside the viewport, so the buttons can be clicked.
        var harness = ContentBrowserViewTestHarness.Create(900, 900);
        var panel = new SoundAssetInspectorPanel(harness.Window);
        var fullPath = Path.Combine(_projectDirectory, "step.sound");

        MGElement content;
        if (buildContentFirst)
        {
            content = panel.CreateContent();
            panel.LoadAsset(asset, fullPath);
        }
        else
        {
            panel.LoadAsset(asset, fullPath);
            content = panel.CreateContent();
        }

        harness.Window.SetContent(content);

        var rig = new Rig { Harness = harness, Panel = panel, Content = content, FullPath = fullPath };
        panel.DirtyStateChanged += _ => rig.DirtyEvents++;
        return rig;
    }

    private static SoundAsset CreateAsset()
    {
        return new SoundAsset { Name = "step", AudioFileAssetId = FileA };
    }

    private static List<AssetSelector> Selectors(MGElement content)
        => content.TraverseVisualTree().OfType<AssetSelector>().ToList();

    private static NumericField[] FieldsOfRow(MGElement content, string rowLabel)
    {
        var label = content.TraverseVisualTree().OfType<MGTextBlock>().First(text => text.Text == rowLabel);
        var row = Assert.IsAssignableFrom<MGStackPanel>(label.Parent);
        return row.TraverseVisualTree().OfType<NumericField>().ToArray();
    }

    private static List<MGButton> ButtonsLabelled(MGElement content, string label)
    {
        return content.TraverseVisualTree()
            .OfType<MGButton>()
            .Where(button => button.TraverseVisualTree().OfType<MGTextBlock>().Any(text => text.Text == label))
            .ToList();
    }

    private static void ClickCenterOf(Rig rig, MGElement element)
    {
        // A frame to lay out the tree, one to hover the element, then press and release on it.
        rig.Harness.AdvanceFrame(0);
        rig.Harness.AdvanceFrame(16);
        var bounds = element.ActualLayoutBounds;
        Assert.False(bounds.IsEmpty);
        rig.Harness.AdvanceFrame(32, bounds.Center);
        rig.Harness.Click(48, bounds.Center);
    }

    private static string[] KeysOf(string fullPath)
    {
        return JObject.Parse(File.ReadAllText(fullPath))
            .Properties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    // ───────────────────────── building the content ─────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildingTheContent_DoesNotMarkTheAssetDirty_AndShowsTheAssetValues(bool buildContentFirst)
    {
        var asset = CreateAsset();
        asset.VariationAudioFileAssetIds.Add(FileB);
        asset.VariationAudioFileAssetIds.Add(Guid.Empty);
        asset.VariationVolumeMin = 0.6f;
        asset.VariationVolumeMax = 0.9f;
        asset.VariationPitchMin = -0.15f;
        asset.VariationPitchMax = 0.25f;
        asset.Priority = 7;

        var rig = Open(asset, buildContentFirst);

        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(0, rig.DirtyEvents);

        var selectors = Selectors(rig.Content);
        Assert.Equal(new[] { FileA, FileB, Guid.Empty }, selectors.Select(selector => selector.AssetId));

        var volumeVariation = FieldsOfRow(rig.Content, "Volume variation");
        Assert.Equal(new[] { 0.6f, 0.9f }, volumeVariation.Select(field => field.Value));
        Assert.All(volumeVariation, field =>
        {
            Assert.Equal(AudioVoiceParameters.MinVolume, field.Min);
            Assert.Equal(AudioVoiceParameters.MaxVolume, field.Max);
            Assert.Equal(0.05f, field.Step);
        });

        var pitchVariation = FieldsOfRow(rig.Content, "Pitch variation");
        Assert.Equal(new[] { -0.15f, 0.25f }, pitchVariation.Select(field => field.Value));
        Assert.All(pitchVariation, field =>
        {
            Assert.Equal(AudioVoiceParameters.MinPitch, field.Min);
            Assert.Equal(AudioVoiceParameters.MaxPitch, field.Max);
            Assert.Equal(0.05f, field.Step);
        });

        var priority = Assert.Single(FieldsOfRow(rig.Content, "Priority"));
        Assert.Equal(7f, priority.Value);
        Assert.Equal(0f, priority.Min);
        Assert.Equal(SoundAsset.MaxPriority, priority.Max);
        Assert.Equal(1f, priority.Step);

        // The asset itself is untouched by the build.
        Assert.Equal(0.6f, asset.VariationVolumeMin);
        Assert.Equal(0.9f, asset.VariationVolumeMax);
        Assert.Equal(-0.15f, asset.VariationPitchMin);
        Assert.Equal(0.25f, asset.VariationPitchMax);
        Assert.Equal(7, asset.Priority);
        Assert.Equal(new[] { FileB, Guid.Empty }, asset.VariationAudioFileAssetIds);
    }

    [Fact]
    public void TheRows_KeepTheirOrder_AndTheHelpLinesAreThere()
    {
        var asset = CreateAsset();
        asset.VariationAudioFileAssetIds.Add(FileB);
        asset.VariationAudioFileAssetIds.Add(FileC);

        var rig = Open(asset);

        // Row labels are the 110 wide text blocks, in tree order.
        var rowLabels = rig.Content.TraverseVisualTree()
            .OfType<MGTextBlock>()
            .Where(text => text.PreferredWidth == 110)
            .Select(text => text.Text)
            .ToArray();
        Assert.Equal(
            new[]
            {
                "Audio file", "Variation 1", "Variation 2", "Variations", "Volume", "Pitch",
                "Volume variation", "Pitch variation", "Priority", "Bus", "Spatial", "Distance model",
                "Reference distance", "Max distance", "Rolloff", "Doppler factor",
            },
            rowLabels);

        var texts = rig.Content.TraverseVisualTree()
            .OfType<MGTextBlock>()
            .Select(text => text.Text)
            .Where(text => text != null)
            .ToList();
        Assert.Contains(texts, text => text.Contains("Priority 0 = none", StringComparison.Ordinal));
        Assert.Contains(texts, text => text.Contains("ignored for a streaming asset", StringComparison.Ordinal));

        Assert.Equal(2, ButtonsLabelled(rig.Content, "Remove").Count);
        Assert.Single(ButtonsLabelled(rig.Content, "Add variation file"));
    }

    [Fact]
    public void EveryAudioFileSelector_ListsTheSameFormats()
    {
        var asset = CreateAsset();
        asset.VariationAudioFileAssetIds.Add(Guid.Empty);
        asset.VariationAudioFileAssetIds.Add(Guid.Empty);

        var selectors = Selectors(Open(asset).Content);

        Assert.Equal(3, selectors.Count);
        // The png is filtered out, wav and ogg stay (case-insensitively).
        Assert.All(selectors, selector =>
            Assert.Equal(new[] { FileA, FileB, FileC }, selector.GetPickableAssets().Select(info => info.Id)));
    }

    // ───────────────────────── variation files ─────────────────────────

    [Fact]
    public void AddVariationFile_AppendsAnEmptyEntry_MarksDirty_AndAddsARow()
    {
        var rig = Open(CreateAsset());
        Assert.Single(Selectors(rig.Content));

        rig.Panel.AddVariationFile();

        Assert.Equal(new[] { Guid.Empty }, rig.Asset.VariationAudioFileAssetIds);
        Assert.True(rig.Panel.IsDirty);
        Assert.Equal(1, rig.DirtyEvents);

        var selectors = Selectors(rig.Content);
        Assert.Equal(2, selectors.Count);
        Assert.Equal(FileA, selectors[0].AssetId);
        Assert.Equal(Guid.Empty, selectors[1].AssetId);

        rig.Panel.AddVariationFile();

        Assert.Equal(new[] { Guid.Empty, Guid.Empty }, rig.Asset.VariationAudioFileAssetIds);
        Assert.Equal(3, Selectors(rig.Content).Count);
    }

    [Fact]
    public void SelectingAnAsset_InAVariationSelector_UpdatesTheRightEntry()
    {
        var asset = CreateAsset();
        asset.VariationAudioFileAssetIds.Add(FileB);
        asset.VariationAudioFileAssetIds.Add(Guid.Empty);
        asset.VariationAudioFileAssetIds.Add(FileB);
        var rig = Open(asset);
        var selectors = Selectors(rig.Content);

        // Selectors: the main audio file, then one per variation.
        selectors[2].SelectAsset(AssetCatalog.Get(FileC));

        Assert.Equal(new[] { FileB, FileC, FileB }, asset.VariationAudioFileAssetIds);
        Assert.Equal(FileA, asset.AudioFileAssetId);
        Assert.True(rig.Panel.IsDirty);
        Assert.Equal(1, rig.DirtyEvents);

        selectors[3].SelectAsset(AssetCatalog.Get(FileA));

        Assert.Equal(new[] { FileB, FileC, FileA }, asset.VariationAudioFileAssetIds);
        Assert.Equal(FileA, asset.AudioFileAssetId);
    }

    [Fact]
    public void SelectingAnAsset_InTheMainSelector_LeavesTheVariationsAlone()
    {
        var asset = CreateAsset();
        asset.VariationAudioFileAssetIds.Add(FileB);
        var rig = Open(asset);

        Selectors(rig.Content)[0].SelectAsset(AssetCatalog.Get(FileC));

        Assert.Equal(FileC, asset.AudioFileAssetId);
        Assert.Equal(new[] { FileB }, asset.VariationAudioFileAssetIds);
        Assert.True(rig.Panel.IsDirty);
    }

    [Fact]
    public void RemoveVariationFile_RemovesTheRightEntry_AndRebuildsTheRows()
    {
        var asset = CreateAsset();
        asset.VariationAudioFileAssetIds.Add(FileA);
        asset.VariationAudioFileAssetIds.Add(FileB);
        asset.VariationAudioFileAssetIds.Add(FileC);
        var rig = Open(asset);

        rig.Panel.RemoveVariationFile(1);

        Assert.Equal(new[] { FileA, FileC }, asset.VariationAudioFileAssetIds);
        Assert.True(rig.Panel.IsDirty);
        Assert.Equal(1, rig.DirtyEvents);

        var selectors = Selectors(rig.Content);
        Assert.Equal(new[] { FileA, FileA, FileC }, selectors.Select(selector => selector.AssetId));

        // The rebuilt rows write the entries they now show.
        selectors[2].SelectAsset(AssetCatalog.Get(FileB));
        Assert.Equal(new[] { FileA, FileB }, asset.VariationAudioFileAssetIds);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void RemoveVariationFile_OutOfRange_ChangesNothing(int index)
    {
        var asset = CreateAsset();
        asset.VariationAudioFileAssetIds.Add(FileB);
        asset.VariationAudioFileAssetIds.Add(FileC);
        var rig = Open(asset);

        rig.Panel.RemoveVariationFile(index);

        Assert.Equal(new[] { FileB, FileC }, asset.VariationAudioFileAssetIds);
        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(0, rig.DirtyEvents);
    }

    [Fact]
    public void TheVariationFileMethods_WithoutAnAsset_DoNothing()
    {
        var harness = ContentBrowserViewTestHarness.Create(900, 900);
        var panel = new SoundAssetInspectorPanel(harness.Window);
        harness.Window.SetContent(panel.CreateContent());

        panel.AddVariationFile();
        panel.RemoveVariationFile(0);

        Assert.False(panel.IsDirty);
        Assert.Null(panel.LoadedSoundAsset);
    }

    [Fact]
    public void TheRemoveButton_RemovesTheEntryOfItsOwnRow()
    {
        var asset = CreateAsset();
        asset.VariationAudioFileAssetIds.Add(FileA);
        asset.VariationAudioFileAssetIds.Add(FileB);
        asset.VariationAudioFileAssetIds.Add(FileC);
        var rig = Open(asset);

        var buttons = ButtonsLabelled(rig.Content, "Remove");
        Assert.Equal(3, buttons.Count);

        ClickCenterOf(rig, buttons[1]);

        Assert.Equal(new[] { FileA, FileC }, asset.VariationAudioFileAssetIds);
        Assert.True(rig.Panel.IsDirty);
        Assert.Equal(2, ButtonsLabelled(rig.Content, "Remove").Count);
    }

    [Fact]
    public void TheAddButton_AppendsAnEmptyEntry()
    {
        var rig = Open(CreateAsset());

        ClickCenterOf(rig, Assert.Single(ButtonsLabelled(rig.Content, "Add variation file")));

        Assert.Equal(new[] { Guid.Empty }, rig.Asset.VariationAudioFileAssetIds);
        Assert.True(rig.Panel.IsDirty);
        Assert.Equal(2, Selectors(rig.Content).Count);
    }

    // ───────────────────────── ranges and priority ─────────────────────────

    [Fact]
    public void TheVolumeRange_WritesTheAsset_RoundedToTwoDecimals()
    {
        var rig = Open(CreateAsset());
        var fields = FieldsOfRow(rig.Content, "Volume variation");

        // 0.35000002f is what the float field holds after a few steps.
        fields[0].Value = 0.35000002f;
        Assert.Equal(0.35f, rig.Asset.VariationVolumeMin);
        Assert.True(rig.Panel.IsDirty);

        fields[1].Value = 0.7500001f;
        Assert.Equal(0.75f, rig.Asset.VariationVolumeMax);
        Assert.Equal(0.35f, rig.Asset.VariationVolumeMin);
    }

    [Fact]
    public void TheVolumeRange_SteppedDownFromOne_StaysOnTwoDecimals()
    {
        var rig = Open(CreateAsset());
        var minField = FieldsOfRow(rig.Content, "Volume variation")[0];

        for (var i = 0; i < 8; i++)
        {
            minField.Value -= minField.Step;
        }

        Assert.Equal(0.6f, rig.Asset.VariationVolumeMin);
        Assert.Equal(1f, rig.Asset.VariationVolumeMax);
    }

    [Fact]
    public void TheVolumeRange_IsClamped_ByTheFieldAndTheAsset()
    {
        var rig = Open(CreateAsset());
        var fields = FieldsOfRow(rig.Content, "Volume variation");

        fields[0].Value = -4f;
        Assert.Equal(0f, fields[0].Value);
        Assert.Equal(0f, rig.Asset.VariationVolumeMin);

        fields[1].Value = 0.5f;
        fields[1].Value = 9f;
        Assert.Equal(1f, fields[1].Value);
        Assert.Equal(1f, rig.Asset.VariationVolumeMax);
    }

    [Fact]
    public void ThePitchRange_WritesTheAsset_RoundedAndClamped()
    {
        var rig = Open(CreateAsset());
        var fields = FieldsOfRow(rig.Content, "Pitch variation");

        fields[0].Value = -0.45000002f;
        Assert.Equal(-0.45f, rig.Asset.VariationPitchMin);

        fields[1].Value = 0.1500001f;
        Assert.Equal(0.15f, rig.Asset.VariationPitchMax);

        fields[0].Value = -7f;
        Assert.Equal(AudioVoiceParameters.MinPitch, fields[0].Value);
        Assert.Equal(AudioVoiceParameters.MinPitch, rig.Asset.VariationPitchMin);

        fields[1].Value = 7f;
        Assert.Equal(AudioVoiceParameters.MaxPitch, fields[1].Value);
        Assert.Equal(AudioVoiceParameters.MaxPitch, rig.Asset.VariationPitchMax);
        Assert.True(rig.Panel.IsDirty);
    }

    [Fact]
    public void ThePriority_WritesTheAsset_RoundedToAnIntegerAndClamped()
    {
        var rig = Open(CreateAsset());
        var field = Assert.Single(FieldsOfRow(rig.Content, "Priority"));

        field.Value = 7.4f;
        Assert.Equal(7, rig.Asset.Priority);
        Assert.True(rig.Panel.IsDirty);

        field.Value = 7.6f;
        Assert.Equal(8, rig.Asset.Priority);

        field.Value = 250f;
        Assert.Equal(SoundAsset.MaxPriority, field.Value);
        Assert.Equal(SoundAsset.MaxPriority, rig.Asset.Priority);

        field.Value = -3f;
        Assert.Equal(0f, field.Value);
        Assert.Equal(0, rig.Asset.Priority);
    }

    [Fact]
    public void ARebuildOfTheInspector_ShowsTheEditedValues()
    {
        var rig = Open(CreateAsset());
        FieldsOfRow(rig.Content, "Volume variation")[0].Value = 0.6000001f;
        FieldsOfRow(rig.Content, "Priority")[0].Value = 12f;
        Assert.Equal(1, rig.DirtyEvents);

        rig.Panel.AddVariationFile();

        Assert.Equal(1, rig.DirtyEvents);
        Assert.Equal(0.6f, rig.Asset.VariationVolumeMin);
        Assert.Equal(12, rig.Asset.Priority);
        Assert.Equal(0.6f, FieldsOfRow(rig.Content, "Volume variation")[0].Value);
        Assert.Equal(12f, FieldsOfRow(rig.Content, "Priority")[0].Value);
    }


    // ----- spatial rows (T9.3) -----

    private static MGComboBox<T> ComboOfRow<T>(MGElement content, string rowLabel)
    {
        var label = content.TraverseVisualTree().OfType<MGTextBlock>().First(text => text.Text == rowLabel);
        var row = Assert.IsAssignableFrom<MGStackPanel>(label.Parent);
        return Assert.Single(row.TraverseVisualTree().OfType<MGComboBox<T>>());
    }

    [Fact]
    public void TheSpatialRows_ShowTheAssetValues_WithTheirRanges()
    {
        var asset = CreateAsset();
        asset.SpatialMode = AudioSpatialMode.Spatial2D;
        asset.DistanceModel = AudioDistanceModel.LinearDistance;
        asset.ReferenceDistance = 4f;
        asset.MaxDistance = 250f;
        asset.RolloffFactor = 0.5f;
        asset.DopplerFactor = 2f;
        asset.SetParameterBindings(new[]
        {
            new AudioParameterBinding("a", AudioParameterTarget.Volume, 0, 1, 0, 1),
            new AudioParameterBinding("b", AudioParameterTarget.Pitch, 0, 1, 0, 1),
        });

        var rig = Open(asset);

        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(0, rig.DirtyEvents);
        Assert.Equal(AudioSpatialMode.Spatial2D, ComboOfRow<AudioSpatialMode>(rig.Content, "Spatial").SelectedItem);
        Assert.Equal(AudioDistanceModel.LinearDistance, ComboOfRow<AudioDistanceModel>(rig.Content, "Distance model").SelectedItem);

        var reference = Assert.Single(FieldsOfRow(rig.Content, "Reference distance"));
        Assert.Equal(4f, reference.Value);
        Assert.Equal(0f, reference.Min);
        Assert.Equal(100000f, reference.Max);
        Assert.Equal(1f, reference.Step);
        Assert.Equal(250f, Assert.Single(FieldsOfRow(rig.Content, "Max distance")).Value);
        var rolloff = Assert.Single(FieldsOfRow(rig.Content, "Rolloff"));
        Assert.Equal(0.5f, rolloff.Value);
        Assert.Equal(0f, rolloff.Min);
        Assert.Equal(10f, rolloff.Max);
        Assert.Equal(0.1f, rolloff.Step);
        var doppler = Assert.Single(FieldsOfRow(rig.Content, "Doppler factor"));
        Assert.Equal(2f, doppler.Value);
        Assert.Equal(0f, doppler.Min);
        Assert.Equal(10f, doppler.Max);

        var texts = rig.Content.TraverseVisualTree().OfType<MGTextBlock>().Select(text => text.Text).ToList();
        Assert.Contains("Parameter bindings: 2 (edit the .sound file)", texts);
        Assert.DoesNotContain(texts, text => text != null && text.Contains("no limit", StringComparison.Ordinal));
    }

    [Fact]
    public void TheSpatialRows_FollowTheBusRow_InOrder()
    {
        var rig = Open(CreateAsset());

        var rowLabels = rig.Content.TraverseVisualTree()
            .OfType<MGTextBlock>()
            .Where(text => text.PreferredWidth == 110)
            .Select(text => text.Text)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "Bus", "Spatial", "Distance model", "Reference distance", "Max distance", "Rolloff", "Doppler factor",
            },
            rowLabels.SkipWhile(label => label != "Bus").ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildingTheContent_WithNoLimit_DoesNotRewriteMaxDistance_NorMarkDirty(bool buildContentFirst)
    {
        var asset = CreateAsset();
        Assert.Equal(float.MaxValue, asset.MaxDistance);

        var rig = Open(asset, buildContentFirst);

        Assert.Equal(float.MaxValue, asset.MaxDistance);
        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(0, rig.DirtyEvents);
        Assert.Contains(
            rig.Content.TraverseVisualTree().OfType<MGTextBlock>().Select(text => text.Text),
            text => text != null && text.Contains("no limit", StringComparison.Ordinal));

        Assert.True(rig.Panel.TrySaveLoadedAsset(out var error), error);
        Assert.Equal(OriginalKeys, KeysOf(rig.FullPath));
    }

    [Fact]
    public void EditingTheMaxDistance_WritesTheAsset_AndMarksDirty()
    {
        var rig = Open(CreateAsset());

        FieldsOfRow(rig.Content, "Max distance")[0].Value = 120f;

        Assert.Equal(120f, rig.Asset.MaxDistance);
        Assert.True(rig.Panel.IsDirty);
    }

    [Fact]
    public void EditingTheSpatialRows_WritesTheAsset_AndMarksDirty()
    {
        var rig = Open(CreateAsset());

        ComboOfRow<AudioSpatialMode>(rig.Content, "Spatial").SelectedItem = AudioSpatialMode.Spatial3D;
        Assert.Equal(AudioSpatialMode.Spatial3D, rig.Asset.SpatialMode);
        Assert.True(rig.Panel.IsDirty);

        ComboOfRow<AudioDistanceModel>(rig.Content, "Distance model").SelectedItem = AudioDistanceModel.ExponentDistance;
        Assert.Equal(AudioDistanceModel.ExponentDistance, rig.Asset.DistanceModel);

        FieldsOfRow(rig.Content, "Reference distance")[0].Value = 3f;
        FieldsOfRow(rig.Content, "Rolloff")[0].Value = 0.35000002f;
        FieldsOfRow(rig.Content, "Doppler factor")[0].Value = 1.5f;

        Assert.Equal(3f, rig.Asset.ReferenceDistance);
        Assert.Equal(0.35f, rig.Asset.RolloffFactor);
        Assert.Equal(1.5f, rig.Asset.DopplerFactor);

        Assert.True(rig.Panel.TrySaveLoadedAsset(out var error), error);
        var document = JObject.Parse(File.ReadAllText(rig.FullPath));
        Assert.Equal("Spatial3D", (string)document["spatial_mode"]);
        Assert.Equal("ExponentDistance", (string)document["distance_model"]);
        Assert.Equal(3f, (float)document["reference_distance"]);
        Assert.Equal(0.35f, (float)document["rolloff_factor"]);
        Assert.Equal(1.5f, (float)document["doppler_factor"]);
        Assert.False(document.ContainsKey("max_distance"));
    }

    [Fact]
    public void TheSpatialFields_AreNotWrittenByTheBuild_AndTheDirtyFlagStaysClear()
    {
        var asset = CreateAsset();
        asset.SpatialMode = AudioSpatialMode.Spatial3D;
        asset.DistanceModel = AudioDistanceModel.InverseDistance;
        asset.MaxDistance = 99f;

        var rig = Open(asset);

        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(AudioSpatialMode.Spatial3D, asset.SpatialMode);
        Assert.Equal(AudioDistanceModel.InverseDistance, asset.DistanceModel);
        Assert.Equal(99f, asset.MaxDistance);
    }
    // ───────────────────────── save and load ─────────────────────────

    [Fact]
    public void Save_WithNothingNew_WritesOnlyTheOriginalKeys()
    {
        var rig = Open(CreateAsset());

        Assert.True(rig.Panel.TrySaveLoadedAsset(out var error), error);

        Assert.Equal(OriginalKeys, KeysOf(rig.FullPath));
    }

    [Fact]
    public void Save_WithAnEmptyVariationEntry_DoesNotPersistIt()
    {
        var rig = Open(CreateAsset());
        rig.Panel.AddVariationFile();

        Assert.True(rig.Panel.TrySaveLoadedAsset(out var error), error);

        Assert.Equal(OriginalKeys, KeysOf(rig.FullPath));
        Assert.False(rig.Panel.IsDirty);
    }

    [Fact]
    public void Save_WithTheNewFieldsSet_WritesTheNewKeys_AndLoadsBackIdentically()
    {
        var rig = Open(CreateAsset());

        rig.Panel.AddVariationFile();
        rig.Panel.AddVariationFile();
        rig.Panel.AddVariationFile();
        var selectors = Selectors(rig.Content);
        selectors[1].SelectAsset(AssetCatalog.Get(FileB));
        selectors[3].SelectAsset(AssetCatalog.Get(FileC));
        // The entry 2 stays empty: it is not persisted.
        FieldsOfRow(rig.Content, "Volume variation")[0].Value = 0.6000001f;
        FieldsOfRow(rig.Content, "Volume variation")[1].Value = 0.9f;
        FieldsOfRow(rig.Content, "Pitch variation")[0].Value = -0.15f;
        FieldsOfRow(rig.Content, "Pitch variation")[1].Value = 0.35000002f;
        FieldsOfRow(rig.Content, "Priority")[0].Value = 12f;
        Assert.True(rig.Panel.IsDirty);

        Assert.True(rig.Panel.TrySaveLoadedAsset(out var error), error);
        Assert.False(rig.Panel.IsDirty);

        var document = JObject.Parse(File.ReadAllText(rig.FullPath));
        Assert.Equal(
            OriginalKeys
                .Concat(new[]
                {
                    "priority", "variation_audio_file_asset_ids", "variation_pitch_max", "variation_pitch_min",
                    "variation_volume_max", "variation_volume_min",
                })
                .OrderBy(key => key, StringComparer.Ordinal),
            KeysOf(rig.FullPath));
        Assert.Equal(12, (int)document["priority"]);
        Assert.Equal(new[] { FileB.ToString(), FileC.ToString() }, document["variation_audio_file_asset_ids"].Select(token => (string)token));

        var loaded = new SoundAsset();
        loaded.Load(document);
        AssertSameAudioSettings(rig.Asset, loaded);
        Assert.Equal(new[] { FileB, FileC }, loaded.VariationAudioFileAssetIds);
        Assert.Equal(0.6f, loaded.VariationVolumeMin);
        Assert.Equal(0.9f, loaded.VariationVolumeMax);
        Assert.Equal(-0.15f, loaded.VariationPitchMin);
        Assert.Equal(0.35f, loaded.VariationPitchMax);
        Assert.Equal(12, loaded.Priority);

        // The loader the editor uses to open the file reads the same asset, and reloading shows it.
        Assert.True(SoundAssetInspectorPanel.TryLoadAsset(rig.FullPath, out var opened));
        AssertSameAudioSettings(loaded, opened);
        Assert.Equal(loaded.VariationAudioFileAssetIds, opened.VariationAudioFileAssetIds);

        Assert.True(rig.Panel.ReloadFromDisk());
        Assert.False(rig.Panel.IsDirty);
        Assert.Equal(new[] { FileA, FileB, FileC }, Selectors(rig.Content).Select(selector => selector.AssetId));
        Assert.Equal(12f, FieldsOfRow(rig.Content, "Priority")[0].Value);
        Assert.Equal(0.6f, FieldsOfRow(rig.Content, "Volume variation")[0].Value);
    }

    [Fact]
    public void Save_AfterTheFieldsAreBackToTheirDefaults_DropsTheNewKeys()
    {
        var rig = Open(CreateAsset());
        var priority = FieldsOfRow(rig.Content, "Priority")[0];
        var volumeMin = FieldsOfRow(rig.Content, "Volume variation")[0];
        var pitchMax = FieldsOfRow(rig.Content, "Pitch variation")[1];

        priority.Value = 5f;
        volumeMin.Value = 0.5f;
        pitchMax.Value = 0.2f;
        priority.Value = 0f;
        volumeMin.Value = 1f;
        pitchMax.Value = 0f;

        Assert.True(rig.Panel.TrySaveLoadedAsset(out var error), error);

        Assert.Equal(OriginalKeys, KeysOf(rig.FullPath));
    }

    private static void AssertSameAudioSettings(SoundAsset expected, SoundAsset actual)
    {
        Assert.Equal(expected.AudioFileAssetId, actual.AudioFileAssetId);
        Assert.Equal(expected.Volume, actual.Volume);
        Assert.Equal(expected.Pitch, actual.Pitch);
        Assert.Equal(expected.IsLooped, actual.IsLooped);
        Assert.Equal(expected.BusName, actual.BusName);
        Assert.Equal(expected.IsStreaming, actual.IsStreaming);
        Assert.Equal(expected.Priority, actual.Priority);
        Assert.Equal(expected.VariationVolumeMin, actual.VariationVolumeMin);
        Assert.Equal(expected.VariationVolumeMax, actual.VariationVolumeMax);
        Assert.Equal(expected.VariationPitchMin, actual.VariationPitchMin);
        Assert.Equal(expected.VariationPitchMax, actual.VariationPitchMax);
    }

    // ───────────────────────── the preview ─────────────────────────

    private static void SetBackingField(object instance, Type declaringType, string propertyName, object value)
    {
        var field = declaringType.GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    /// <summary>An editor runtime that only carries the audio service, the way <c>AudioServiceVariationTests</c> builds a game.</summary>
    private static HostedEditorGameAdapter CreateEditorRuntime(AudioService service)
    {
        var audioSystem = (AudioSystemComponent)RuntimeHelpers.GetUninitializedObject(typeof(AudioSystemComponent));
        SetBackingField(audioSystem, typeof(AudioSystemComponent), nameof(AudioSystemComponent.Service), service);

        var runtime = (HostedEditorGameAdapter)RuntimeHelpers.GetUninitializedObject(typeof(HostedEditorGameAdapter));
        SetBackingField(runtime, typeof(CasaEngineGame), nameof(CasaEngineGame.AudioSystemComponent), audioSystem);
        return runtime;
    }

    [Fact]
    public void ThePreview_PlaysWithPriorityZero_SoItNeverStealsAGameVoice()
    {
        var backend = new FakeAudioBackend(1);
        var provider = new FakeAudioClipProvider();
        using var service = new AudioService(backend) { ClipProvider = provider };
        var gameAsset = new SoundAsset { Name = "game", AudioFileAssetId = provider.Register(new FakeAudioClip("game")), Priority = 1 };
        var previewAsset = new SoundAsset
        {
            Name = "preview",
            AudioFileAssetId = provider.Register(new FakeAudioClip("preview")),
            Priority = SoundAsset.MaxPriority,
        };

        var gameVoice = service.PlaySound(gameAsset);
        Assert.True(gameVoice.IsValid);

        var harness = ContentBrowserViewTestHarness.Create(900, 900);
        var panel = new SoundAssetInspectorPanel(harness.Window, CreateEditorRuntime(service));
        panel.LoadAsset(previewAsset, Path.Combine(_projectDirectory, "preview.sound"));

        panel.PlayPreview();

        // The only voice stays with the game: the preview is refused instead of stealing it.
        Assert.True(service.IsPlaying(gameVoice));
        Assert.Equal(0, service.StolenVoiceCount);
        Assert.Equal(1, service.RefusedVoiceCount);

        // Control: the same asset played by the game with its own priority does steal the voice.
        var control = service.PlaySound(previewAsset);
        Assert.True(control.IsValid);
        Assert.False(service.IsPlaying(gameVoice));
        Assert.Equal(1, service.StolenVoiceCount);
    }

    [Fact]
    public void ThePreview_PlaysOnAFreeVoice_AndStops()
    {
        var backend = new FakeAudioBackend(2);
        var provider = new FakeAudioClipProvider();
        using var service = new AudioService(backend) { ClipProvider = provider };
        var asset = new SoundAsset
        {
            Name = "preview",
            AudioFileAssetId = provider.Register(new FakeAudioClip("preview")),
            Priority = 50,
        };

        var harness = ContentBrowserViewTestHarness.Create(900, 900);
        var panel = new SoundAssetInspectorPanel(harness.Window, CreateEditorRuntime(service));
        panel.LoadAsset(asset, Path.Combine(_projectDirectory, "preview.sound"));

        panel.PlayPreview();
        Assert.Equal(1, service.ActiveVoiceCount);

        panel.StopPreview();
        Assert.Equal(0, service.ActiveVoiceCount);
        Assert.False(panel.IsDirty);
    }
}
