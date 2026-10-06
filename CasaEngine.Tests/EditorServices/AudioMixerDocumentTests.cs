using System.Text;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.Audio;
using CasaEngine.EditorServices.History;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Effects;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Tests.Audio;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

/// <summary>
/// The editing model of a mixer asset (plan decisions P49 to P53, D16): snapshot history, gestures, dirty state, save, the
/// one-way link to the live mixer and its detachment on a project change.
/// </summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class AudioMixerDocumentTests
{
    private const string RelativePath = "Mixer.audioMixer";

    private static readonly string[] Watched = { "Master", "Music", "Sfx", "Voice", "Ui", "Ambience", "Reverb" };

    private sealed class Rig : IDisposable
    {
        public AudioService Service = new(new FakeAudioBackend());
        public AudioMixerAssetApplier Applier;
        public EditorHistoryStack Stack = new();
        public AudioMixerDocument Document;

        public Rig(bool live = true, int busCapacity = int.MaxValue)
        {
            Applier = new AudioMixerAssetApplier(Service);
            Document = new AudioMixerDocument(BaseAsset(), RelativePath, live ? Applier : null, Stack.Execute, busCapacity);
            Document.ApplyToLive();
        }

        public string Json => AssetJson(Document.Asset);

        public string LiveState => Live(Service, Watched);

        public void Dispose() => Service.Dispose();
    }

    /// <summary>Defaults, plus Ambience (Music, 0.5, biquad then reverb), Reverb (a return), a send and a ducking.</summary>
    private static AudioMixerAsset BaseAsset()
    {
        var asset = AudioMixerAsset.CreateDefault("Mixer");
        asset.Buses.Add(new AudioMixerBusData { Name = "Ambience", Parent = "Music", Volume = 0.5f });
        asset.Buses.Add(new AudioMixerBusData { Name = "Reverb", Parent = "Master", Volume = 0.8f });
        var ambience = asset.Buses.Find(bus => bus.Name == "Ambience");
        ambience.Effects.Add(new AudioMixerBiquadEffectData(BiquadFilterType.LowPass, 800f));
        ambience.Effects.Add(new AudioMixerReverbEffectData());
        asset.Buses.Find(bus => bus.Name == "Sfx").Sends.Add(new AudioMixerSendData("Reverb", 0.3f));
        asset.Buses.Find(bus => bus.Name == "Music").Effects.Add(new AudioMixerDuckingEffectData("Voice"));
        return asset;
    }

    private static string AssetJson(AudioMixerAsset asset)
    {
        Assert.True(EditorAssetJsonSerializer.TrySerialize(asset, out var root));
        return root.ToString(Newtonsoft.Json.Formatting.None);
    }

    private static string Live(AudioService service, params string[] names)
    {
        var text = new StringBuilder();
        foreach (string name in names)
        {
            if (!service.Mixer.TryGetBus(name, out var bus))
            {
                text.AppendLine($"{name}|absent");
                continue;
            }

            text.Append(name).Append('|').Append(bus.Parent?.Name).Append('|').Append(bus.Volume).Append("|muted:").Append(bus.IsMuted).Append("|fx:");
            foreach (var effect in bus.Effects)
            {
                text.Append(effect.GetType().Name);
                if (effect is DuckingEffect ducking)
                {
                    text.Append('(').Append(ducking.Source.Name).Append(')');
                }

                if (effect is BiquadFilterEffect biquad)
                {
                    text.Append('(').Append(biquad.Type).Append(')');
                }

                text.Append(',');
            }

            text.Append("|sends:");
            foreach (var send in bus.Sends)
            {
                text.Append(send.Target.Name).Append('=').Append(send.Level).Append(',');
            }

            text.AppendLine();
        }

        return text.ToString();
    }

    // ------------------------------------------------------------------ construction

    [Fact]
    public void Construction_AddsTheMissingDefaultBuses_WithoutHistoryAndWithoutBeingDirty()
    {
        var asset = new AudioMixerAsset { Name = "Bare" };
        asset.Buses.Add(new AudioMixerBusData { Name = "Sfx", Parent = "Master", Volume = 0.4f });
        var stack = new EditorHistoryStack();

        var document = new AudioMixerDocument(asset, RelativePath, null, stack.Execute);

        Assert.Equal(new[] { "Sfx", "Music", "Voice", "Ui" }, asset.Buses.ConvertAll(bus => bus.Name));
        Assert.Equal(0.4f, asset.Buses[0].Volume);
        Assert.All(asset.Buses, bus => Assert.Equal("Master", bus.Parent));
        Assert.False(document.IsDirty);
        Assert.False(document.IsLive);
        Assert.False(stack.CanUndo);
        Assert.Same(asset, document.Asset);
        Assert.Equal(RelativePath, document.RelativePath);
        Assert.Empty(document.Problems);
    }

    [Fact]
    public void BusCapacity_FollowsTheApplierUnlessGiven()
    {
        using var rig = new Rig();
        Assert.Equal(int.MaxValue, rig.Document.BusCapacity);

        var explicitDocument = new AudioMixerDocument(AudioMixerAsset.CreateDefault("M"), RelativePath, rig.Applier, null, 12);

        Assert.Equal(12, explicitDocument.BusCapacity);
    }

    // ------------------------------------------------------------------ history

    private static IEnumerable<(string Name, Func<AudioMixerDocument, bool> Operation)> Operations()
    {
        yield return ("set volume", d => d.SetBusVolume("Sfx", 0.4f));
        yield return ("add bus", d => d.TryAddBus("Footsteps", "Sfx", out _));
        yield return ("remove bus", d => d.TryRemoveBus("Ambience", out _));
        yield return ("add effect", d => d.TryAddEffect("Voice", new AudioMixerCompressorEffectData(), out _));
        yield return ("remove effect", d => d.TryRemoveEffect("Ambience", 0));
        yield return ("move effect", d => d.TryMoveEffect("Ambience", 0, 1));
        yield return ("set effect parameters", d => d.TrySetEffect("Ambience", 0, new AudioMixerBiquadEffectData(BiquadFilterType.HighPass, 300f), out _));
        yield return ("set effect type", d => d.TrySetEffect("Ambience", 1, new AudioMixerCompressorEffectData(), out _));
        yield return ("change the ducking source", d => d.TrySetEffect("Music", 0, new AudioMixerDuckingEffectData("Ui"), out _));
        yield return ("add a send", d => d.TrySetSend("Voice", "Reverb", 0.5f, out _));
        yield return ("change a send", d => d.TrySetSend("Sfx", "Reverb", 0.9f, out _));
        yield return ("remove a send", d => d.TrySetSend("Sfx", "Reverb", 0f, out _));
        yield return ("gesture", d =>
        {
            d.BeginGesture("Fader");
            d.UpdateBusVolume("Voice", 0.7f);
            d.UpdateBusVolume("Voice", 0.2f);
            d.EndGesture();
            return true;
        });
    }

    [Fact]
    public void EveryOperation_ThenUndoThenRedo_GivesBackTheAssetAndTheLiveMixer()
    {
        foreach (var (name, operation) in Operations())
        {
            using var rig = new Rig();
            string jsonBefore = rig.Json;
            string liveBefore = rig.LiveState;

            Assert.True(operation(rig.Document), name);

            string jsonAfter = rig.Json;
            string liveAfter = rig.LiveState;
            Assert.NotEqual(jsonBefore, jsonAfter);
            Assert.True(rig.Stack.CanUndo, name);
            Assert.True(rig.Document.IsDirty, name);

            rig.Stack.Undo();
            Assert.Equal(jsonBefore, rig.Json);
            Assert.Equal(liveBefore, rig.LiveState);
            Assert.False(rig.Document.IsDirty, name);

            rig.Stack.Redo();
            Assert.Equal(jsonAfter, rig.Json);
            Assert.Equal(liveAfter, rig.LiveState);
            Assert.True(rig.Document.IsDirty, name);

            // Undo and redo are repeatable.
            rig.Stack.Undo();
            rig.Stack.Redo();
            Assert.Equal(jsonAfter, rig.Json);
            Assert.Equal(liveAfter, rig.LiveState);
        }
    }

    [Fact]
    public void EveryOperation_ChangesTheLiveMixer_ExceptAddingABus()
    {
        foreach (var (name, operation) in Operations())
        {
            using var rig = new Rig();
            string liveBefore = rig.LiveState;

            operation(rig.Document);

            if (name == "add bus")
            {
                Assert.Equal(liveBefore, rig.LiveState);
                Assert.True(rig.Service.Mixer.TryGetBus("Footsteps", out var bus));
                Assert.Equal("Sfx", bus.Parent.Name);
            }
            else
            {
                Assert.NotEqual(liveBefore, rig.LiveState);
            }
        }
    }

    [Fact]
    public void AnOperationThatChangesNothing_AddsNoEntry()
    {
        using var rig = new Rig();

        Assert.True(rig.Document.SetBusVolume("Sfx", 1f));
        Assert.True(rig.Document.TrySetSend("Voice", "Reverb", 0f, out _)); // no such send: nothing to remove
        Assert.True(rig.Document.TrySetEffect("Ambience", 1, new AudioMixerReverbEffectData(), out _));

        Assert.False(rig.Stack.CanUndo);
        Assert.False(rig.Document.IsDirty);
    }

    [Fact]
    public void WithoutASink_TheChangeIsAppliedDirectly_WithoutHistory()
    {
        using var service = new AudioService(new FakeAudioBackend());
        var applier = new AudioMixerAssetApplier(service);
        var document = new AudioMixerDocument(BaseAsset(), RelativePath, applier);
        document.ApplyToLive();

        Assert.True(document.SetBusVolume("Sfx", 0.4f));

        Assert.Equal(0.4f, service.Mixer.GetBus("Sfx").Volume);
        Assert.True(document.IsDirty);
    }

    [Fact]
    public void Changed_IsRaisedByEveryOperation()
    {
        using var rig = new Rig();
        int raised = 0;
        rig.Document.Changed += (_, _) => raised++;

        rig.Document.SetBusVolume("Sfx", 0.4f);
        Assert.True(raised >= 1);
        int afterOperation = raised;

        rig.Stack.Undo();
        Assert.True(raised > afterOperation);
    }

    // ------------------------------------------------------------------ gestures

    [Fact]
    public void FiftyUpdatesBetweenBeginAndEnd_AreOneEntry_AndTheLiveBusFollowsEachOne()
    {
        using var rig = new Rig();
        string jsonBefore = rig.Json;

        rig.Document.BeginGesture("Fader Sfx");
        Assert.True(rig.Document.IsGestureOpen);
        for (int step = 1; step <= 50; step++)
        {
            float volume = 1f - (step / 100f);
            Assert.True(rig.Document.UpdateBusVolume("Sfx", volume));
            Assert.Equal(volume, rig.Service.Mixer.GetBus("Sfx").Volume);
        }

        Assert.False(rig.Stack.CanUndo); // nothing until the gesture ends
        Assert.False(rig.Document.IsDirty);

        rig.Document.EndGesture();

        Assert.False(rig.Document.IsGestureOpen);
        Assert.True(rig.Stack.CanUndo);
        Assert.Equal("Fader Sfx", rig.Stack.UndoDescription);
        Assert.True(rig.Document.IsDirty);
        Assert.Equal(0.5f, rig.Service.Mixer.GetBus("Sfx").Volume);

        rig.Stack.Undo();

        Assert.False(rig.Stack.CanUndo); // exactly one entry
        Assert.Equal(jsonBefore, rig.Json);
        Assert.Equal(1f, rig.Service.Mixer.GetBus("Sfx").Volume);
        Assert.False(rig.Document.IsDirty);
    }

    [Fact]
    public void AGestureBackToItsStartValue_AddsNoEntry()
    {
        using var rig = new Rig();

        rig.Document.BeginGesture("Fader");
        rig.Document.UpdateBusVolume("Sfx", 0.2f);
        rig.Document.UpdateBusVolume("Sfx", 1f);
        rig.Document.EndGesture();

        Assert.False(rig.Stack.CanUndo);
        Assert.False(rig.Document.IsDirty);
        Assert.Equal(1f, rig.Service.Mixer.GetBus("Sfx").Volume);
    }

    [Fact]
    public void AnUpdateWithoutAGesture_OpensOne_AndAnotherOperationClosesIt()
    {
        using var rig = new Rig();

        rig.Document.UpdateBusVolume("Sfx", 0.6f);
        Assert.True(rig.Document.IsGestureOpen);
        rig.Document.SetBusVolume("Voice", 0.3f);

        Assert.False(rig.Document.IsGestureOpen);
        rig.Stack.Undo(); // Voice
        Assert.Equal(1f, rig.Service.Mixer.GetBus("Voice").Volume);
        Assert.Equal(0.6f, rig.Service.Mixer.GetBus("Sfx").Volume);
        rig.Stack.Undo(); // Sfx gesture
        Assert.Equal(1f, rig.Service.Mixer.GetBus("Sfx").Volume);
        Assert.False(rig.Stack.CanUndo);
    }

    [Fact]
    public void UpdateBusVolume_OfAnUnknownBus_IsRefused()
    {
        using var rig = new Rig();

        Assert.False(rig.Document.UpdateBusVolume("Nope", 0.5f));
        Assert.False(rig.Document.IsGestureOpen);
    }

    // ------------------------------------------------------------------ dirty and save

    [Fact]
    public void IsDirty_IsExact_AcrossEditSaveAndUndo()
    {
        string directory = CreateTempDirectory();
        string previous = EngineEnvironment.ProjectPath;
        try
        {
            EngineEnvironment.ProjectPath = directory;
            using var rig = new Rig();
            Assert.False(rig.Document.IsDirty);

            rig.Document.SetBusVolume("Sfx", 0.4f);
            Assert.True(rig.Document.IsDirty);

            Assert.True(rig.Document.TrySave(out string error), error);
            Assert.False(rig.Document.IsDirty);

            rig.Document.SetBusVolume("Sfx", 0.2f);
            Assert.True(rig.Document.IsDirty);

            rig.Stack.Undo(); // back to the saved state
            Assert.False(rig.Document.IsDirty);

            rig.Stack.Undo(); // before the save
            Assert.True(rig.Document.IsDirty);

            rig.Stack.Redo();
            Assert.False(rig.Document.IsDirty);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previous;
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TrySave_WritesTheFile_ThatReloadsIdentical()
    {
        string directory = CreateTempDirectory();
        string previous = EngineEnvironment.ProjectPath;
        try
        {
            EngineEnvironment.ProjectPath = directory;
            using var rig = new Rig();
            rig.Document.SetBusVolume("Sfx", 0.4f);
            rig.Document.TryAddBus("Footsteps", "Sfx", out _);

            Assert.True(rig.Document.TrySave(out string error), error);

            string path = Path.Combine(directory, RelativePath);
            Assert.True(File.Exists(path));
            var reloaded = new AudioMixerAsset();
            reloaded.Load(JObject.Parse(File.ReadAllText(path)));
            Assert.Equal(rig.Json, AssetJson(reloaded));
            Assert.Equal(0.4f, reloaded.Buses.Find(bus => bus.Name == "Sfx").Volume);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previous;
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TrySave_ToAnUnwritablePath_ReturnsTheErrorAndStaysDirty()
    {
        string directory = CreateTempDirectory();
        string previous = EngineEnvironment.ProjectPath;
        try
        {
            EngineEnvironment.ProjectPath = directory;
            var document = new AudioMixerDocument(AudioMixerAsset.CreateDefault("M"), Path.Combine("MissingFolder", RelativePath), null, null);
            document.SetBusVolume("Sfx", 0.4f);

            Assert.False(document.TrySave(out string error));

            Assert.False(string.IsNullOrWhiteSpace(error));
            Assert.True(document.IsDirty);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previous;
            Directory.Delete(directory, recursive: true);
        }
    }

    // ------------------------------------------------------------------ refusals

    [Fact]
    public void TryAddBus_RefusesWithAMessage()
    {
        using var rig = new Rig();

        Assert.False(rig.Document.TryAddBus("  ", "Master", out string empty));
        Assert.Contains("name", empty);
        Assert.False(rig.Document.TryAddBus("master", "Master", out string reservedMaster));
        Assert.Contains("reserved", reservedMaster);
        Assert.False(rig.Document.TryAddBus("Editor", "Master", out string reservedEditor));
        Assert.Contains("reserved", reservedEditor);
        Assert.False(rig.Document.TryAddBus("sfx", "Master", out string duplicate));
        Assert.Contains("already exists", duplicate);
        Assert.False(rig.Document.TryAddBus("New", "Nowhere", out string unknownParent));
        Assert.Contains("'Nowhere'", unknownParent);
        Assert.False(rig.Document.TryAddBus("New", "Editor", out string editorParent));
        Assert.Contains("Editor", editorParent);

        Assert.False(rig.Stack.CanUndo);
        Assert.False(rig.Document.IsDirty);
    }

    [Fact]
    public void TryAddBus_RefusesBeyondTheCapacity()
    {
        // Master, Music, Sfx, Voice, Ui, Editor, Ambience and Reverb: eight buses, two free slots.
        using var rig = new Rig(busCapacity: 10);

        Assert.True(rig.Document.TryAddBus("First", "Music", out _));
        Assert.True(rig.Document.TryAddBus("Second", "Master", out _));
        Assert.False(rig.Document.TryAddBus("OneMore", "Master", out string error));

        Assert.Contains("capacity of 10", error);
        Assert.Null(rig.Document.Asset.Buses.Find(bus => bus.Name == "OneMore"));
    }

    [Fact]
    public void TryAddBus_RefusesANameThatIsLiveUnderAnotherParent_AndAcceptsTheSameParent()
    {
        using var rig = new Rig();
        rig.Service.Mixer.CreateBus("GameBus", "Sfx");

        Assert.False(rig.Document.TryAddBus("GameBus", "Music", out string error));
        Assert.Contains("GameBus", error);
        Assert.Contains("'Sfx'", error);

        Assert.True(rig.Document.TryAddBus("GameBus", "Sfx", out _));
    }

    [Fact]
    public void TryRemoveBus_RefusesWithAMessage_AndRemovesACustomBus()
    {
        using var rig = new Rig();
        rig.Document.TryAddBus("Footsteps", "Ambience", out _);
        rig.Document.TryAddEffect("Ui", new AudioMixerDuckingEffectData("Reverb"), out _);

        Assert.False(rig.Document.TryRemoveBus("Sfx", out string defaultBus));
        Assert.Contains("default", defaultBus);
        Assert.False(rig.Document.TryRemoveBus("Ambience", out string children));
        Assert.Contains("child", children);
        Assert.Contains("Footsteps", children);
        Assert.False(rig.Document.TryRemoveBus("Reverb", out string target)); // send target of Sfx (and ducking source of Ui)
        Assert.Contains("send", target);
        Assert.False(rig.Document.TryRemoveBus("Nope", out string unknown));
        Assert.Contains("'Nope'", unknown);

        Assert.True(rig.Document.TrySetSend("Sfx", "Reverb", 0f, out _));
        Assert.False(rig.Document.TryRemoveBus("Reverb", out string source));
        Assert.Contains("ducking source", source);

        Assert.True(rig.Document.TryRemoveBus("Footsteps", out _));
        Assert.Null(rig.Document.Asset.Buses.Find(bus => bus.Name == "Footsteps"));
    }

    [Fact]
    public void TryRemoveBus_ADuckingSourceOfAnotherBus_IsRefused()
    {
        using var rig = new Rig();
        rig.Document.TryAddBus("Dialogue", "Voice", out _);
        rig.Document.TryAddEffect("Music", new AudioMixerDuckingEffectData("Dialogue"), out _);

        Assert.False(rig.Document.TryRemoveBus("Dialogue", out string error));
        Assert.Contains("ducking source", error);
        Assert.Contains("Music", error);
    }

    [Fact]
    public void Effects_AreRefusedWithAMessage()
    {
        using var rig = new Rig();
        var document = rig.Document;

        Assert.False(document.TryAddEffect("Nope", new AudioMixerReverbEffectData(), out string unknownBus));
        Assert.Contains("'Nope'", unknownBus);
        Assert.False(document.TryAddEffect("Voice", null, out string noEffect));
        Assert.False(string.IsNullOrEmpty(noEffect));

        Assert.False(document.TryAddEffect("Voice", new AudioMixerDuckingEffectData("Nope"), out string unknownSource));
        Assert.Contains("'Nope'", unknownSource);
        Assert.False(document.TryAddEffect("Voice", new AudioMixerDuckingEffectData("voice"), out string ownSource));
        Assert.Contains("own bus", ownSource);

        for (int index = 0; index < AudioBus.MaxEffects; index++)
        {
            Assert.True(document.TryAddEffect("Ui", new AudioMixerReverbEffectData(), out _));
        }

        Assert.False(document.TryAddEffect("Ui", new AudioMixerReverbEffectData(), out string fifth));
        Assert.Contains("4", fifth);

        Assert.False(document.TrySetEffect("Ui", 9, new AudioMixerReverbEffectData(), out string noIndex));
        Assert.Contains("index 9", noIndex);
        Assert.False(document.TryRemoveEffect("Ui", 9));
        Assert.False(document.TryRemoveEffect("Nope", 0));
        Assert.False(document.TryMoveEffect("Ui", 0, 9));
        Assert.False(document.TryMoveEffect("Ui", 1, 1));
        Assert.Equal(AudioBus.MaxEffects, document.Asset.Buses.Find(bus => bus.Name == "Ui").Effects.Count);
    }

    [Fact]
    public void ADuckingThatWouldMakeACycle_IsRefused()
    {
        using var rig = new Rig();
        // Music is already ducked by Voice (edge Voice to Music). Music as the source of Voice closes the loop.
        Assert.False(rig.Document.TryAddEffect("Voice", new AudioMixerDuckingEffectData("Music"), out string error));
        Assert.Contains("cycle", error);

        // Ui sends to Voice: Ui ducked by Voice would loop (Ui reaches Voice, which would then duck Ui).
        Assert.True(rig.Document.TrySetSend("Ui", "Voice", 0.5f, out _));
        Assert.False(rig.Document.TryAddEffect("Ui", new AudioMixerDuckingEffectData("Voice"), out string throughSend));
        Assert.Contains("cycle", throughSend);

        // Replacing the ducking source of Music by Ui is fine: its old relation no longer counts.
        Assert.True(rig.Document.TrySetEffect("Music", 0, new AudioMixerDuckingEffectData("Ui"), out string replaced), replaced);
    }

    [Fact]
    public void Sends_AreRefusedWithAMessage()
    {
        using var rig = new Rig();
        var document = rig.Document;

        Assert.False(document.TrySetSend("Nope", "Reverb", 0.5f, out string unknownBus));
        Assert.Contains("'Nope'", unknownBus);
        Assert.False(document.TrySetSend("Voice", "Nope", 0.5f, out string unknownTarget));
        Assert.Contains("'Nope'", unknownTarget);
        Assert.False(document.TrySetSend("Voice", "Voice", 0.5f, out string itself));
        Assert.Contains("itself", itself);
        Assert.False(document.TrySetSend("Voice", "Reverb", float.NaN, out string notANumber));
        Assert.Contains("number", notANumber);

        // Ambience is under Music: a send from Music to Ambience closes the loop (a child feeds its parent).
        Assert.False(document.TrySetSend("Music", "Ambience", 0.5f, out string cycle));
        Assert.Contains("cycle", cycle);

        document.TryAddBus("R1", "Master", out _);
        document.TryAddBus("R2", "Master", out _);
        document.TryAddBus("R3", "Master", out _);
        document.TryAddBus("R4", "Master", out _);
        Assert.True(document.TrySetSend("Ui", "Reverb", 0.1f, out _));
        Assert.True(document.TrySetSend("Ui", "R1", 0.1f, out _));
        Assert.True(document.TrySetSend("Ui", "R2", 0.1f, out _));
        Assert.True(document.TrySetSend("Ui", "R3", 0.1f, out _));
        Assert.False(document.TrySetSend("Ui", "R4", 0.1f, out string fifth));
        Assert.Contains("4", fifth);
        Assert.True(document.TrySetSend("Ui", "R3", 0.7f, out _)); // changing an existing one is not a fifth
        Assert.True(document.TrySetSend("Ui", "Reverb", 2f, out _)); // clamped to 1
        Assert.Equal(1f, document.Asset.Buses.Find(bus => bus.Name == "Ui").Sends[0].Level);
    }

    // ------------------------------------------------------------------ one way

    [Fact]
    public void ChangingTheLiveBus_NeverChangesTheAsset()
    {
        using var rig = new Rig();
        string jsonBefore = rig.Json;

        rig.Service.Mixer.GetBus("Sfx").Volume = 0.1f;
        rig.Service.Mixer.GetBus("Ambience").Volume = 0.9f;
        rig.Service.Mixer.GetBus("Voice").SetSend(rig.Service.Mixer.GetBus("Reverb"), 0.5f);
        rig.Document.ApplyToLive(); // the asset wins over the live change

        Assert.Equal(jsonBefore, rig.Json);
        Assert.False(rig.Document.IsDirty);
        Assert.Equal(1f, rig.Service.Mixer.GetBus("Sfx").Volume);
        Assert.Equal(0.5f, rig.Service.Mixer.GetBus("Ambience").Volume);
    }

    [Fact]
    public void WithoutAnApplier_NothingTouchesTheLiveMixer()
    {
        using var service = new AudioService(new FakeAudioBackend());
        string before = Live(service, "Master", "Music", "Sfx", "Voice", "Ui", "Editor");
        int busCount = service.Mixer.Buses.Count;
        var stack = new EditorHistoryStack();
        var document = new AudioMixerDocument(BaseAsset(), RelativePath, null, stack.Execute);

        document.SetBusVolume("Sfx", 0.4f);
        document.TryAddBus("Footsteps", "Sfx", out _);
        document.TryAddEffect("Voice", new AudioMixerCompressorEffectData(), out _);
        document.TrySetSend("Voice", "Reverb", 0.5f, out _);
        document.BeginGesture("Fader");
        document.UpdateBusVolume("Ui", 0.3f);
        document.EndGesture();
        document.SetMute("Sfx", true);
        document.SetSolo("Voice", true);
        document.ApplyToLive();
        document.RestoreSavedToLive();
        stack.Undo();
        stack.Undo();

        Assert.False(document.IsLive);
        Assert.Equal(before, Live(service, "Master", "Music", "Sfx", "Voice", "Ui", "Editor"));
        Assert.Equal(busCount, service.Mixer.Buses.Count);
    }

    [Fact]
    public void RestoreSavedToLive_PutsTheSavedStateBack_WithoutChangingTheDocument()
    {
        string directory = CreateTempDirectory();
        string previous = EngineEnvironment.ProjectPath;
        try
        {
            EngineEnvironment.ProjectPath = directory;
            using var rig = new Rig();
            rig.Document.SetBusVolume("Sfx", 0.3f);
            Assert.True(rig.Document.TrySave(out string error), error);
            rig.Document.SetBusVolume("Sfx", 0.8f);
            rig.Document.TryAddEffect("Voice", new AudioMixerCompressorEffectData(), out _);
            Assert.Equal(0.8f, rig.Service.Mixer.GetBus("Sfx").Volume);
            string jsonEdited = rig.Json;

            rig.Document.RestoreSavedToLive();

            var saved = new AudioMixerAsset();
            saved.Load(JObject.Parse(File.ReadAllText(Path.Combine(directory, RelativePath))));
            Assert.Equal(saved.Buses.Find(bus => bus.Name == "Sfx").Volume, rig.Service.Mixer.GetBus("Sfx").Volume);
            Assert.Equal(0.3f, rig.Service.Mixer.GetBus("Sfx").Volume);
            Assert.Empty(rig.Service.Mixer.GetBus("Voice").Effects);
            Assert.Equal(jsonEdited, rig.Json);
            Assert.True(rig.Document.IsDirty);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previous;
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RestoreSavedToLive_AfterAnUnsavedEdit_OfADocumentThatWasNeverSaved_RestoresTheOpenedState()
    {
        using var rig = new Rig();
        rig.Document.SetBusVolume("Sfx", 0.8f);

        rig.Document.RestoreSavedToLive();

        Assert.Equal(1f, rig.Service.Mixer.GetBus("Sfx").Volume);
    }

    // ------------------------------------------------------------------ live binding

    [Fact]
    public void DetachLive_RestoresTheMutes_DropsTheApplier_AndAppliesNothing()
    {
        using var rig = new Rig();
        rig.Document.SetSolo("Sfx", true);
        Assert.True(rig.Service.Mixer.GetBus("Music").IsMuted);
        rig.Service.Mixer.GetBus("Voice").Volume = 0.2f; // a live value the detached document must not touch

        rig.Document.DetachLive();

        Assert.False(rig.Document.IsLive);
        Assert.False(rig.Service.Mixer.GetBus("Music").IsMuted);
        Assert.Equal(0.2f, rig.Service.Mixer.GetBus("Voice").Volume);

        rig.Document.SetBusVolume("Sfx", 0.1f);
        rig.Document.SetSolo("Voice", true);
        rig.Document.ApplyToLive();
        rig.Document.RestoreSavedToLive();

        Assert.Equal(1f, rig.Service.Mixer.GetBus("Sfx").Volume);
        Assert.False(rig.Service.Mixer.GetBus("Music").IsMuted);
        Assert.Equal(0.2f, rig.Service.Mixer.GetBus("Voice").Volume);
    }

    [Fact]
    public void UpdateLiveBinding_AttachesOnlyForTheProjectAsset_AndAppliesIt()
    {
        using var rig = new Rig(live: false);
        var asset = rig.Document.Asset;
        asset.AssetId = Guid.NewGuid();
        rig.Document.SetBusVolume("Sfx", 0.4f);
        Assert.Equal(1f, rig.Service.Mixer.GetBus("Sfx").Volume);

        rig.Document.UpdateLiveBinding(Guid.Empty, rig.Applier);
        Assert.False(rig.Document.IsLive);
        rig.Document.UpdateLiveBinding(Guid.NewGuid(), rig.Applier);
        Assert.False(rig.Document.IsLive);
        rig.Document.UpdateLiveBinding(asset.AssetId, null);
        Assert.False(rig.Document.IsLive);
        Assert.Equal(1f, rig.Service.Mixer.GetBus("Sfx").Volume);

        rig.Document.UpdateLiveBinding(asset.AssetId, rig.Applier);

        Assert.True(rig.Document.IsLive);
        Assert.Equal(0.4f, rig.Service.Mixer.GetBus("Sfx").Volume);

        rig.Document.UpdateLiveBinding(Guid.NewGuid(), rig.Applier); // another asset is now the project's
        Assert.False(rig.Document.IsLive);
    }

    // ------------------------------------------------------------------ project change

    [Fact]
    public void AProjectChange_DetachesTheDocument_AndNothingOfItReachesTheNextProject()
    {
        string tempDirectory = CreateTempDirectory();
        Guid assetIdA = Guid.NewGuid();
        Guid assetIdB = Guid.NewGuid();
        string projectA = WriteProject(Path.Combine(tempDirectory, "A"), assetIdA, "Mixer A", 0.5f, withSetting: true);
        string projectB = WriteProject(Path.Combine(tempDirectory, "B"), assetIdB, "Mixer B", 0.25f, withSetting: false);
        string projectBNamed = WriteProject(Path.Combine(tempDirectory, "BNamed"), assetIdA, "Mixer A", 0.5f, withSetting: true);

        string previousProjectPath = EngineEnvironment.ProjectPath;
        var snapshot = ProjectSettingsSnapshot.Capture();
        AudioMixerDocument document = null;
        EventHandler detachOnClose = (_, _) => document?.DetachLive();
        EditorProjectAuthoringService.ProjectClosed += detachOnClose;

        try
        {
            using var service = new AudioService(new FakeAudioBackend());
            var assets = new AssetContentManager();
            AssetLoaderRegistry.RegisterLoaders(assets);
            var projectMixer = new ProjectAudioMixer(service, assets);
            using var sync = new EditorProjectAudioMixerSync(projectMixer);
            sync.ProjectMixerApplied += (_, id) => document?.UpdateLiveBinding(id, projectMixer.Applier);
            var stack = new EditorHistoryStack();

            // Project A: its mixer is live and a document of its asset follows it.
            EditorProjectAuthoringService.LoadProject(projectA);
            Assert.Equal(assetIdA, projectMixer.AppliedAssetId);
            document = new AudioMixerDocument(assets.LoadCopy<AudioMixerAsset>(assetIdA), "Project.audioMixer", projectMixer.Applier, stack.Execute);
            document.UpdateLiveBinding(projectMixer.AppliedAssetId, projectMixer.Applier);
            Assert.True(document.IsLive);
            document.SetSolo("Music", true);
            document.SetMute("Voice", true);
            Assert.True(service.Mixer.GetBus("Sfx").IsMuted);
            Assert.True(service.Mixer.GetBus("Voice").IsMuted);

            // Project B does not name that asset: the document is detached, the live mixer is B's.
            EditorProjectAuthoringService.LoadProject(projectB);
            Assert.False(document.IsLive);
            Assert.Equal(Guid.Empty, projectMixer.AppliedAssetId);
            string liveOfB = Live(service, "Master", "Music", "Sfx", "Voice", "Ui", "Editor", "Ambience");
            Assert.DoesNotContain("muted:True", liveOfB);
            Assert.Equal(1f, service.Mixer.GetBus("Sfx").Volume); // B names no asset: the default mixer volumes

            document.BeginGesture("Fader");
            document.UpdateBusVolume("Sfx", 0.1f);
            document.UpdateBusVolume("Ui", 0.2f);
            document.EndGesture();
            document.SetSolo("Ui", true);
            document.SetMute("Music", true);
            document.ApplyToLive();
            document.RestoreSavedToLive();

            Assert.Equal(liveOfB, Live(service, "Master", "Music", "Sfx", "Voice", "Ui", "Editor", "Ambience"));
            Assert.False(document.IsLive);

            // Closing the project, then loading a project that names the asset of the document: attached.
            EditorProjectAuthoringService.ClearProject();
            Assert.False(document.IsLive);
            EditorProjectAuthoringService.LoadProject(projectBNamed);
            Assert.Equal(assetIdA, projectMixer.AppliedAssetId);
            Assert.True(document.IsLive);
            // The document's own state (gesture of Sfx and Ui) is applied to the live mixer, mutes included.
            Assert.Equal(0.1f, service.Mixer.GetBus("Sfx").Volume);
            Assert.Equal(0.2f, service.Mixer.GetBus("Ui").Volume);
            Assert.True(service.Mixer.GetBus("Music").IsMuted);
            Assert.True(service.Mixer.GetBus("Sfx").IsMuted); // Ui is soloed

            EditorProjectAuthoringService.ClearProject();
            Assert.False(document.IsLive);
            Assert.DoesNotContain("muted:True", Live(service, "Master", "Music", "Sfx", "Voice", "Ui", "Editor", "Ambience"));
        }
        finally
        {
            EditorProjectAuthoringService.ProjectClosed -= detachOnClose;
            EditorProjectAuthoringService.ClearProject();
            snapshot.Restore();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    /// <summary>Writes a project folder with a mixer asset (Sfx at <paramref name="sfxVolume"/>, plus Ambience) and returns the project file.</summary>
    private static string WriteProject(string directory, Guid assetId, string mixerName, float sfxVolume, bool withSetting)
    {
        Directory.CreateDirectory(directory);

        var asset = new AudioMixerAsset { Name = mixerName };
        asset.Buses.Add(new AudioMixerBusData { Name = "Sfx", Parent = "Master", Volume = sfxVolume });
        asset.Buses.Add(new AudioMixerBusData { Name = "Ambience", Parent = "Music", Volume = 0.25f });
        var node = new JObject();
        EditorAudioMixerAssetJsonWriter.Save(asset, node);
        File.WriteAllText(Path.Combine(directory, "Project.audioMixer"), node.ToString());

        File.WriteAllText(
            Path.Combine(directory, "AssetInfos.json"),
            new JObject
            {
                ["asset_infos"] = new JArray(new JObject
                {
                    ["id"] = assetId.ToString(),
                    ["name"] = mixerName,
                    ["file_name"] = "Project.audioMixer",
                    ["asset_type"] = "audioMixer",
                }),
            }.ToString());

        var project = new JObject
        {
            ["WindowTitle"] = "Sample Project",
            ["ProjectName"] = "SampleProject",
            ["FirstScreenName"] = string.Empty,
            ["FirstWorldLoaded"] = "DefaultWorld.world",
            ["GameplayDllName"] = string.Empty,
        };
        if (withSetting)
        {
            project["AudioMixerAsset"] = assetId.ToString();
        }

        string projectFile = Path.Combine(directory, "Project.json");
        File.WriteAllText(projectFile, project.ToString());
        return projectFile;
    }

    private static string CreateTempDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CasaEngineMonogame", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private readonly record struct ProjectSettingsSnapshot(
        string WindowTitle,
        string ProjectName,
        string FirstScreenName,
        string FirstWorldLoaded,
        string GameplayDllName,
        string GameplayCsprojName,
        string DialogueScreenAsset,
        string AudioMixerAsset,
        bool IsAudioMuted,
        bool? IsMasterLimiterEnabled,
        AudioBackendKind? AudioBackend,
        VirtualResolutionSettings VirtualResolution)
    {
        public static ProjectSettingsSnapshot Capture()
        {
            var settings = GameSettings.ProjectSettings;
            return new ProjectSettingsSnapshot(
                settings.WindowTitle,
                settings.ProjectName,
                settings.FirstScreenName,
                settings.FirstWorldLoaded,
                settings.GameplayDllName,
                settings.GameplayCsprojName,
                settings.DialogueScreenAsset,
                settings.AudioMixerAsset,
                settings.IsAudioMuted,
                settings.IsMasterLimiterEnabled,
                settings.AudioBackend,
                settings.VirtualResolution);
        }

        public void Restore()
        {
            var settings = GameSettings.ProjectSettings;
            settings.WindowTitle = WindowTitle;
            settings.ProjectName = ProjectName;
            settings.FirstScreenName = FirstScreenName;
            settings.FirstWorldLoaded = FirstWorldLoaded;
            settings.GameplayDllName = GameplayDllName;
            settings.GameplayCsprojName = GameplayCsprojName;
            settings.DialogueScreenAsset = DialogueScreenAsset;
            settings.AudioMixerAsset = AudioMixerAsset;
            settings.IsAudioMuted = IsAudioMuted;
            settings.IsMasterLimiterEnabled = IsMasterLimiterEnabled;
            settings.AudioBackend = AudioBackend;
            settings.VirtualResolution = VirtualResolution;
        }
    }
}
