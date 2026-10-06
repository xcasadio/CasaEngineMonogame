using CasaEngine.EditorServices.Audio;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Tests.Audio;
using Xunit;

namespace CasaEngine.Tests.EditorServices;

/// <summary>Solo of the mixing panel (plan decisions D17 and P53): the pure rule, then its effect on a live mixer through a document.</summary>
[Collection(ProjectEnvironmentCollection.Name)]
public class AudioMixerSoloTests
{
    private const int Block = 480;

    private static AudioMixerSoloNode Node(string name, string parent = "Master", bool isReturn = false, bool isReserved = false)
        => new(name, parent, isReturn, isReserved);

    private static List<AudioMixerSoloNode> DefaultNodes() => new()
    {
        Node("Master", null),
        Node("Editor", "Master", isReserved: true),
        Node("Music"),
        Node("Sfx"),
        Node("Voice"),
        Node("Ui"),
        Node("SfxChild", "Sfx"),
    };

    private static List<string> Muted(List<AudioMixerSoloNode> nodes, params string[] soloed)
    {
        var muted = new List<string>();
        AudioMixerSolo.ComputeMuted(nodes, soloed, muted);
        muted.Sort(StringComparer.Ordinal);
        return muted;
    }

    [Fact]
    public void ComputeMuted_NothingSoloed_MutesNothing()
    {
        Assert.Empty(Muted(DefaultNodes()));
    }

    [Fact]
    public void ComputeMuted_SoloOfSfx_MutesTheOtherBuses_AndKeepsMasterEditorAndTheChildren()
    {
        var muted = Muted(DefaultNodes(), "Sfx");

        Assert.Equal(new[] { "Music", "Ui", "Voice" }, muted);
    }

    [Fact]
    public void ComputeMuted_SoloOfAChild_KeepsItsAncestors()
    {
        var muted = Muted(DefaultNodes(), "SfxChild");

        Assert.Equal(new[] { "Music", "Ui", "Voice" }, muted);
        Assert.DoesNotContain("Sfx", muted);
        Assert.DoesNotContain("Master", muted);
    }

    [Fact]
    public void ComputeMuted_ABusUnderTheSoloedBusIsKept_AndASiblingOfItIsNot()
    {
        var nodes = DefaultNodes();
        nodes.Add(Node("Sibling", "Sfx"));
        nodes.Add(Node("Deep", "SfxChild"));

        var muted = Muted(nodes, "SfxChild");

        Assert.DoesNotContain("Deep", muted);
        Assert.Contains("Sibling", muted);
    }

    [Fact]
    public void ComputeMuted_AReturnBusAndItsAncestorsStayAudible()
    {
        var nodes = DefaultNodes();
        nodes.Add(Node("Reverb", "Music", isReturn: true));

        var muted = Muted(nodes, "Sfx");

        Assert.Equal(new[] { "Ui", "Voice" }, muted);
    }

    [Fact]
    public void ComputeMuted_TwoSolos_KeepBothPaths()
    {
        Assert.Equal(new[] { "Music", "Ui" }, Muted(DefaultNodes(), "Sfx", "Voice"));
    }

    [Fact]
    public void ComputeMuted_RemovingTheSolo_MutesNothingAgain()
    {
        var soloed = new List<string> { "Sfx" };
        var muted = new List<string>();
        AudioMixerSolo.ComputeMuted(DefaultNodes(), soloed, muted);
        Assert.NotEmpty(muted);

        soloed.Clear();
        muted.Clear();
        AudioMixerSolo.ComputeMuted(DefaultNodes(), soloed, muted);

        Assert.Empty(muted);
    }

    [Fact]
    public void ComputeMuted_NamesAreComparedIgnoringCase_AndAParentThatIsNoNodeIsOnlyAnAncestorName()
    {
        var nodes = new List<AudioMixerSoloNode> { Node("a", "LiveOnly"), Node("b", "LiveOnly") };

        Assert.Equal(new[] { "b" }, Muted(nodes, "A"));
    }

    [Fact]
    public void ComputeMuted_ACycleInTheParentsDoesNotLoop()
    {
        var nodes = new List<AudioMixerSoloNode> { Node("a", "b"), Node("b", "a"), Node("c") };

        Assert.Equal(new[] { "c" }, Muted(nodes, "a"));
    }

    // ------------------------------------------------------------------ through a document

    private static (AudioService Service, AudioMixerAssetApplier Applier, AudioMixerDocument Document) NewLiveDocument(
        AudioMixerAsset asset = null, AudioService service = null)
    {
        service ??= new AudioService(new FakeAudioBackend());
        var applier = new AudioMixerAssetApplier(service);
        var document = new AudioMixerDocument(asset ?? AudioMixerAsset.CreateDefault("Mixer"), "Mixer.audioMixer", applier);
        document.ApplyToLive();
        return (service, applier, document);
    }

    private static bool Muted(AudioService service, string bus) => service.Mixer.GetBus(bus).IsMuted;

    [Fact]
    public void SetSolo_OfSfx_MutesMusicVoiceAndUi_AndLeavesMasterEditorAndSfx()
    {
        var (service, _, document) = NewLiveDocument();
        using (service)
        {
            Assert.True(document.SetSolo("Sfx", true));

            Assert.True(document.IsSoloed("Sfx"));
            Assert.True(Muted(service, "Music"));
            Assert.True(Muted(service, "Voice"));
            Assert.True(Muted(service, "Ui"));
            Assert.False(Muted(service, "Sfx"));
            Assert.False(Muted(service, "Master"));
            Assert.False(Muted(service, "Editor"));

            Assert.True(document.SetSolo("Sfx", false));

            Assert.False(Muted(service, "Music"));
            Assert.False(Muted(service, "Voice"));
            Assert.False(Muted(service, "Ui"));
        }
    }

    [Fact]
    public void SetSolo_KeepsTheChildrenOfTheSoloedBus_AndABusAddedLaterFollowsTheSolo()
    {
        var asset = AudioMixerAsset.CreateDefault("Mixer");
        asset.Buses.Add(new AudioMixerBusData { Name = "Footsteps", Parent = "Sfx" });
        var (service, _, document) = NewLiveDocument(asset);
        using (service)
        {
            document.SetSolo("Sfx", true);
            Assert.False(Muted(service, "Footsteps"));

            Assert.True(document.TryAddBus("Other", "Music", out _));
            Assert.True(Muted(service, "Other"));
            Assert.True(document.TryAddBus("Inner", "Sfx", out _));
            Assert.False(Muted(service, "Inner"));
        }
    }

    [Fact]
    public void SetSolo_TwoSolos_KeepBothPaths_AndRemovingOneRestoresTheOther()
    {
        var (service, _, document) = NewLiveDocument();
        using (service)
        {
            document.SetSolo("Sfx", true);
            document.SetSolo("Voice", true);

            Assert.True(Muted(service, "Music"));
            Assert.True(Muted(service, "Ui"));
            Assert.False(Muted(service, "Sfx"));
            Assert.False(Muted(service, "Voice"));

            document.SetSolo("Voice", false);

            Assert.True(Muted(service, "Voice"));
            Assert.False(Muted(service, "Sfx"));
        }
    }

    [Fact]
    public void SetSolo_AReturnBusOfTheAsset_StaysAudibleWithItsParent()
    {
        var asset = AudioMixerAsset.CreateDefault("Mixer");
        asset.Buses.Add(new AudioMixerBusData { Name = "Reverb", Parent = "Music" });
        var voice = asset.Buses.Find(bus => bus.Name == "Voice");
        voice.Sends.Add(new AudioMixerSendData("Reverb", 0.5f));
        var (service, _, document) = NewLiveDocument(asset);
        using (service)
        {
            document.SetSolo("Sfx", true);

            Assert.False(Muted(service, "Reverb"));
            Assert.False(Muted(service, "Music"));
            Assert.True(Muted(service, "Voice"));
            Assert.True(Muted(service, "Ui"));
        }
    }

    [Fact]
    public void SetSolo_AReturnBusOfTheLiveMixer_StaysAudible()
    {
        var asset = AudioMixerAsset.CreateDefault("Mixer");
        asset.Buses.Add(new AudioMixerBusData { Name = "Fx", Parent = "Ui" });
        var service = new AudioService(new FakeAudioBackend());
        var gameBus = service.Mixer.CreateBus("GameSource", "Master");
        var (_, _, document) = NewLiveDocument(asset, service);
        using (service)
        {
            // A send the game made, not in the asset: Fx is a return of the live mixer.
            gameBus.SetSend(service.Mixer.GetBus("Fx"), 0.5f);

            document.SetSolo("Sfx", true);

            Assert.False(Muted(service, "Fx"));
            Assert.False(Muted(service, "Ui"));
            Assert.True(Muted(service, "Music"));
            Assert.False(Muted(service, "GameSource")); // outside the asset: never muted
        }
    }

    [Fact]
    public void MuteAndSolo_Combined_AMutedBusStaysMutedWhenTheSoloGoes()
    {
        var (service, _, document) = NewLiveDocument();
        using (service)
        {
            document.SetMute("Sfx", true);
            Assert.True(Muted(service, "Sfx"));
            Assert.False(Muted(service, "Music"));

            document.SetSolo("Sfx", true);
            Assert.True(Muted(service, "Sfx")); // soloed but muted by the panel
            Assert.True(Muted(service, "Music"));

            document.SetSolo("Sfx", false);
            Assert.True(Muted(service, "Sfx"));
            Assert.False(Muted(service, "Music"));

            document.SetMute("Sfx", false);
            Assert.False(Muted(service, "Sfx"));
            Assert.False(document.IsMuted("Sfx"));
        }
    }

    [Fact]
    public void ClearTransientState_GivesBackExactlyTheMutesTheDocumentSet()
    {
        var (service, _, document) = NewLiveDocument();
        using (service)
        {
            service.Mixer.GetBus("Voice").IsMuted = true; // muted by the game, not by the document
            document.SetSolo("Sfx", true);
            document.SetMute("Ui", true);
            Assert.True(Muted(service, "Music"));

            document.ClearTransientState();

            Assert.False(Muted(service, "Music"));
            Assert.False(Muted(service, "Ui"));
            Assert.True(Muted(service, "Voice"));
            Assert.False(document.IsSoloed("Sfx"));
            Assert.False(document.IsMuted("Ui"));
        }
    }

    [Fact]
    public void Solo_NeverMutesMasterOrEditor_AndALiveBusOutsideTheAssetIsNeverMuted()
    {
        var service = new AudioService(new FakeAudioBackend());
        service.Mixer.CreateBus("GameOnly", "Music");
        var (_, _, document) = NewLiveDocument(null, service);
        using (service)
        {
            Assert.False(document.SetMute("Master", true));
            Assert.False(document.SetMute("Editor", true));
            Assert.False(document.SetSolo("GameOnly", true));

            document.SetSolo("Sfx", true);

            Assert.False(Muted(service, "GameOnly"));
            Assert.False(Muted(service, "Master"));
            Assert.False(Muted(service, "Editor"));
        }
    }

    [Fact]
    public void SetSolo_WithoutALiveMixer_ChangesNothingButTheDocumentState()
    {
        using var service = new AudioService(new FakeAudioBackend());
        var document = new AudioMixerDocument(AudioMixerAsset.CreateDefault("Mixer"), "Mixer.audioMixer");

        document.SetSolo("Sfx", true);
        document.SetMute("Ui", true);

        Assert.True(document.IsSoloed("Sfx"));
        Assert.True(document.IsMuted("Ui"));
        Assert.False(document.IsLive);
        Assert.False(Muted(service, "Music"));
        Assert.False(Muted(service, "Ui"));
        Assert.False(document.IsDirty);
    }

    [Fact]
    public void ABusMutedByTheDocument_RendersZeroUnderTheSoftwareBackend()
    {
        var output = new OfflineAudioOutput();
        using var service = new AudioService(new SoftwareAudioBackend(output, 16));
        service.MasterLimiter.IsEnabled = false;
        var asset = AudioMixerAsset.CreateDefault("Mixer");
        asset.Buses.Add(new AudioMixerBusData { Name = "Layer", Parent = AudioBusNames.Sfx, Volume = 0.5f });
        var (_, _, document) = NewLiveDocument(asset, service);

        var samples = new short[2 * 400000];
        Array.Fill(samples, (short)16384);
        service.PlayClip(new PcmAudioClip(samples, 48000, 2), "Layer", AudioVoiceParameters.Default);
        service.Update(0.01f);
        output.Pump(Block);
        output.Pump(Block);
        Assert.Equal(0.25f, output.Pump(Block), 3);

        document.SetMute("Layer", true);
        service.Update(0.01f);
        output.Pump(Block);
        output.Pump(Block);
        Assert.Equal(0f, output.Pump(Block), 3);

        document.SetMute("Layer", false);
        service.Update(0.01f);
        output.Pump(Block);
        output.Pump(Block);
        Assert.Equal(0.25f, output.Pump(Block), 3);

        // A solo elsewhere mutes the bus too.
        document.SetSolo("Voice", true);
        service.Update(0.01f);
        output.Pump(Block);
        output.Pump(Block);
        Assert.Equal(0f, output.Pump(Block), 3);
    }
}
