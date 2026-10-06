using CasaEngine.Framework.Audio.Mixing;
using Xunit;

namespace CasaEngine.Tests.Audio;

/// <summary>Tolerant validation of a mixer asset (plan decision P47).</summary>
public class AudioMixerAssetValidatorTests
{
    private static AudioMixerBusData Bus(string name, string parent = "Master", float volume = 1f)
        => new() { Name = name, Parent = parent, Volume = volume };

    private static AudioMixerAsset Asset(params AudioMixerBusData[] buses)
    {
        var asset = new AudioMixerAsset { Name = "Project mixer" };
        asset.Buses.AddRange(buses);
        return asset;
    }

    private static string[] Names(AudioMixerPlan plan) => plan.Buses.Select(b => b.Name).ToArray();

    [Fact]
    public void AValidAsset_IsRetainedAsWritten_WithoutProblems()
    {
        var asset = Asset(Bus("Reverb"), Bus("Ambience", "Music", 0.4f));

        var plan = AudioMixerAssetValidator.Validate(asset, null, 32);

        Assert.Empty(plan.Problems);
        Assert.Equal(new[] { "Reverb", "Ambience" }, Names(plan));
        Assert.Equal("Music", plan.Buses[1].Parent);
        Assert.Equal(0.4f, plan.Buses[1].Volume);
        Assert.False(plan.Buses[1].ExistsLive);
        Assert.Same(asset.Buses[0], plan.Buses[0].Data);
    }

    [Fact]
    public void ABlankParent_IsMaster()
    {
        var plan = AudioMixerAssetValidator.Validate(Asset(Bus("A", "")), null, 32);

        Assert.Equal("Master", plan.Buses[0].Parent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ABlankName_IsIgnored(string name)
    {
        var plan = AudioMixerAssetValidator.Validate(Asset(Bus(name), Bus("Kept")), null, 32);

        Assert.Equal(new[] { "Kept" }, Names(plan));
        Assert.Single(plan.Problems);
    }

    [Theory]
    [InlineData("Master")]
    [InlineData("master")]
    [InlineData("Editor")]
    [InlineData("EDITOR")]
    public void AReservedName_IsIgnored_WithAProblemNamingAssetAndBus(string name)
    {
        var plan = AudioMixerAssetValidator.Validate(Asset(Bus(name)), null, 32);

        Assert.Empty(plan.Buses);
        var problem = Assert.Single(plan.Problems);
        Assert.Contains("Project mixer", problem);
        Assert.Contains(name, problem);
    }

    [Fact]
    public void ADuplicateName_FirstWins_CaseInsensitively()
    {
        var plan = AudioMixerAssetValidator.Validate(Asset(Bus("Fx", "Master", 0.2f), Bus("FX", "Music", 0.9f)), null, 32);

        Assert.Equal(new[] { "Fx" }, Names(plan));
        Assert.Equal(0.2f, plan.Buses[0].Volume);
        Assert.Single(plan.Problems);
    }

    [Fact]
    public void AParentEditor_IsRefused()
    {
        var plan = AudioMixerAssetValidator.Validate(Asset(Bus("A", "Editor"), Bus("B", "A")), null, 32);

        Assert.Empty(plan.Buses);
        Assert.Equal(2, plan.Problems.Count);
    }

    [Fact]
    public void AnUnknownParent_IgnoresTheBusAndItsDescendants()
    {
        var plan = AudioMixerAssetValidator.Validate(Asset(Bus("A", "Nowhere"), Bus("B", "A"), Bus("C", "B"), Bus("Ok")), null, 32);

        Assert.Equal(new[] { "Ok" }, Names(plan));
        Assert.Equal(3, plan.Problems.Count);
        Assert.Contains("Nowhere", plan.Problems[0]);
    }

    [Fact]
    public void AParentCycle_IgnoresEveryBusOfTheCycle_AndTheirDescendants()
    {
        var plan = AudioMixerAssetValidator.Validate(
            Asset(Bus("A", "B"), Bus("B", "C"), Bus("C", "A"), Bus("D", "A"), Bus("Self", "Self"), Bus("Ok")), null, 32);

        Assert.Equal(new[] { "Ok" }, Names(plan));
        Assert.Contains(plan.Problems, p => p.Contains("cycle") && p.Contains("'A'"));
        Assert.Contains(plan.Problems, p => p.Contains("'Self'"));
    }

    [Fact]
    public void TheOrderIsParentsFirst_EvenWhenAChildPrecedesItsParentInTheFile()
    {
        var plan = AudioMixerAssetValidator.Validate(Asset(Bus("Child", "Parent"), Bus("Grand", "Child"), Bus("Parent"), Bus("Other")), null, 32);

        Assert.Equal(new[] { "Parent", "Child", "Grand", "Other" }, Names(plan));
        Assert.Empty(plan.Problems);
    }

    [Fact]
    public void TheOrderIsStable_ForBusesThatDoNotDependOnEachOther()
    {
        var plan = AudioMixerAssetValidator.Validate(Asset(Bus("Z"), Bus("A"), Bus("M")), null, 32);

        Assert.Equal(new[] { "Z", "A", "M" }, Names(plan));
    }

    [Fact]
    public void ALiveBus_IsFlaggedExistsLive_AndAParentMismatchIsReported()
    {
        var plan = AudioMixerAssetValidator.Validate(Asset(Bus("Music", "Master", 0.5f), Bus("Sfx", "Music")), null, 32);

        Assert.True(plan.Buses[0].ExistsLive);
        Assert.True(plan.Buses[1].ExistsLive);
        var problem = Assert.Single(plan.Problems);
        Assert.Contains("bus 'Sfx' is live under 'Master'", problem);
    }

    [Fact]
    public void AParentThatIsALiveBus_IsAccepted()
    {
        var mixer = AudioBusNames.CreateDefaultMixer();
        mixer.CreateBus("Custom", "Sfx");

        var plan = AudioMixerAssetValidator.Validate(Asset(Bus("Under", "Custom")), mixer, 32);

        Assert.Equal(new[] { "Under" }, Names(plan));
        Assert.Equal("Custom", plan.Buses[0].Parent);
    }

    [Fact]
    public void Capacity_CountsNewBusesOnTopOfTheLiveOnes()
    {
        var asset = new AudioMixerAsset { Name = "Big" };
        for (var i = 0; i < 40; i++)
        {
            asset.Buses.Add(Bus("Bus" + i));
        }

        var plan = AudioMixerAssetValidator.Validate(asset, null, 32);

        Assert.Equal(26, plan.Buses.Count);
        Assert.Equal("Bus0", plan.Buses[0].Name);
        Assert.Equal("Bus25", plan.Buses[^1].Name);
        Assert.Equal(14, plan.Problems.Count);
        Assert.All(plan.Problems, p => Assert.Contains("32", p));
    }

    [Fact]
    public void Capacity_DoesNotCountABusThatIsAlreadyLive_AndRefusesTheChildrenOfARefusedBus()
    {
        var mixer = AudioBusNames.CreateDefaultMixer();
        var plan = AudioMixerAssetValidator.Validate(
            Asset(Bus("Music", "Master", 0.5f), Bus("New", "Master"), Bus("Child", "New")), mixer, 7);

        Assert.Equal(new[] { "Music", "New" }, Names(plan));
        Assert.Single(plan.Problems);
    }

    [Fact]
    public void Effects_AreLimitedToFour()
    {
        var bus = Bus("A");
        for (var i = 0; i < 6; i++)
        {
            bus.Effects.Add(new AudioMixerBiquadEffectData());
        }

        var plan = AudioMixerAssetValidator.Validate(Asset(bus), null, 32);

        Assert.Equal(4, plan.Buses[0].Effects.Count);
        Assert.Single(plan.Problems);
    }

    [Fact]
    public void ADucking_NeedsAnExistingSource_DifferentFromTheBus()
    {
        var a = Bus("A");
        a.Effects.Add(new AudioMixerDuckingEffectData("Ghost"));
        a.Effects.Add(new AudioMixerDuckingEffectData("A"));
        a.Effects.Add(new AudioMixerDuckingEffectData(""));
        a.Effects.Add(new AudioMixerDuckingEffectData("Voice"));
        a.Effects.Add(new AudioMixerDuckingEffectData("Later"));

        var plan = AudioMixerAssetValidator.Validate(Asset(a, Bus("Later")), null, 32);

        Assert.Equal(2, plan.Buses[0].Effects.Count);
        Assert.Equal("Voice", ((AudioMixerDuckingEffectData)plan.Buses[0].Effects[0]).Source);
        Assert.Equal(3, plan.Problems.Count);
    }

    [Fact]
    public void ADucking_ThatMakesACycle_IsIgnored()
    {
        // A is a child of B, so A reaches B: a ducking of A by B (edge B -> A) closes the loop.
        var a = Bus("A", "B");
        a.Effects.Add(new AudioMixerDuckingEffectData("B"));

        var plan = AudioMixerAssetValidator.Validate(Asset(a, Bus("B")), null, 32);

        Assert.Empty(plan.Buses.Single(b => b.Name == "A").Effects);
        Assert.Contains(plan.Problems, p => p.Contains("cycle"));
    }

    [Fact]
    public void TwoDuckings_FormingACycleBetweenThemselves_KeepOnlyTheFirst()
    {
        var a = Bus("A");
        a.Effects.Add(new AudioMixerDuckingEffectData("B"));
        var b = Bus("B");
        b.Effects.Add(new AudioMixerDuckingEffectData("A"));

        var plan = AudioMixerAssetValidator.Validate(Asset(a, b), null, 32);

        Assert.Single(plan.Buses[0].Effects);
        Assert.Empty(plan.Buses[1].Effects);
    }

    [Fact]
    public void Sends_AreLimitedToFour_ClampedAndDeduplicated()
    {
        var a = Bus("A");
        a.Sends.Add(new AudioMixerSendData("Music", 2f));
        a.Sends.Add(new AudioMixerSendData("music", 0.1f));
        a.Sends.Add(new AudioMixerSendData("Sfx", 0f));
        a.Sends.Add(new AudioMixerSendData("Voice", 0.5f));
        a.Sends.Add(new AudioMixerSendData("Ui", 0.25f));
        a.Sends.Add(new AudioMixerSendData("B", 0.3f));
        a.Sends.Add(new AudioMixerSendData("C", 0.3f));

        var plan = AudioMixerAssetValidator.Validate(Asset(a, Bus("B"), Bus("C")), null, 32);

        var sends = plan.Buses[0].Sends;
        Assert.Equal(4, sends.Count);
        Assert.Equal(new AudioMixerSendData("Music", 1f), sends[0]);
        Assert.Equal("Voice", sends[1].Target);
        Assert.Equal("Ui", sends[2].Target);
        Assert.Equal("B", sends[3].Target);
        Assert.Equal(3, plan.Problems.Count); // duplicate, level 0, more than four
    }

    [Fact]
    public void ASend_NeedsAnExistingDifferentTarget_WithoutCycle()
    {
        var a = Bus("A");
        a.Sends.Add(new AudioMixerSendData("Ghost", 1f));
        a.Sends.Add(new AudioMixerSendData("A", 1f));
        a.Sends.Add(new AudioMixerSendData("B", 1f));
        a.Sends.Add(new AudioMixerSendData("Child", 1f)); // the child reaches A through its parent
        var b = Bus("B");
        b.Sends.Add(new AudioMixerSendData("A", 1f)); // closes the loop A -> B -> A
        var child = Bus("Child", "A");

        var plan = AudioMixerAssetValidator.Validate(Asset(a, b, child), null, 32);

        Assert.Equal(new[] { "B" }, plan.Buses[0].Sends.Select(s => s.Target).ToArray());
        Assert.Empty(plan.Buses[1].Sends);
        Assert.Empty(plan.Buses[2].Sends);
        Assert.Equal(4, plan.Problems.Count);
    }

    [Fact]
    public void ASend_ToABusDeclaredLater_IsRetained()
    {
        var a = Bus("A");
        a.Sends.Add(new AudioMixerSendData("Return", 0.5f));

        var plan = AudioMixerAssetValidator.Validate(Asset(a, Bus("Return")), null, 32);

        Assert.Single(plan.Buses[0].Sends);
        Assert.Empty(plan.Problems);
    }

    [Fact]
    public void ASend_ToAnIgnoredBus_IsIgnored()
    {
        var a = Bus("A");
        a.Sends.Add(new AudioMixerSendData("Lost", 0.5f));

        var plan = AudioMixerAssetValidator.Validate(Asset(a, Bus("Lost", "Nowhere")), null, 32);

        Assert.Empty(plan.Buses[0].Sends);
    }

    [Fact]
    public void Validate_NeverThrows_OnNullEntries()
    {
        var asset = Asset(null, Bus("A"));
        asset.Buses[1].Effects.Add(null);
        asset.Buses[1].Sends.Add(null);

        var plan = AudioMixerAssetValidator.Validate(asset, null, 32);
        var nothing = AudioMixerAssetValidator.Validate(null, null, 32);

        Assert.Equal(new[] { "A" }, Names(plan));
        Assert.Empty(nothing.Buses);
        Assert.Single(nothing.Problems);
    }

    [Fact]
    public void WouldMakeCycle_ReadsParentsSendsAndDuckings()
    {
        var a = Bus("A");
        a.Sends.Add(new AudioMixerSendData("B", 1f));
        var b = Bus("B");
        var c = Bus("C", "B");
        var d = Bus("D");
        d.Effects.Add(new AudioMixerDuckingEffectData("C"));
        var asset = Asset(a, b, c, d);

        Assert.True(AudioMixerAssetValidator.WouldMakeCycle(asset, "A", "A"));
        Assert.True(AudioMixerAssetValidator.WouldMakeCycle(asset, "B", "A"));   // A -> B exists
        Assert.False(AudioMixerAssetValidator.WouldMakeCycle(asset, "A", "B"));  // A -> B again, B does not reach A
        Assert.True(AudioMixerAssetValidator.WouldMakeCycle(asset, "B", "C"));   // C is under B
        Assert.True(AudioMixerAssetValidator.WouldMakeCycle(asset, "D", "C"));   // ducking C -> D exists
        Assert.False(AudioMixerAssetValidator.WouldMakeCycle(asset, "C", "D"));  // D does not reach C
        Assert.False(AudioMixerAssetValidator.WouldMakeCycle(asset, "D", "A"));
        Assert.False(AudioMixerAssetValidator.WouldMakeCycle(asset, "", "A"));
    }
}
