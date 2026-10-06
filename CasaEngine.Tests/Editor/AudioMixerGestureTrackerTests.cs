using CasaEngine.Editor.Controls;
using CasaEngine.EditorServices.Audio;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Audio.Mixing;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>
/// The state machine that turns the changes of a bus fader into fader gestures of the mixing document (plan T10.6, decision P50),
/// without any UI: each test feeds the values a fader took and the frames (<c>Tick</c>) the panel gives, and counts the history
/// entries the document published.
/// </summary>
public class AudioMixerGestureTrackerTests
{
    private sealed class Rig
    {
        public Rig()
        {
            // Every default bus starts at volume 1.
            var asset = AudioMixerAsset.CreateDefault("Tracker");
            Document = new AudioMixerDocument(asset, "Tracker.audioMixer", null, command =>
            {
                Descriptions.Add(command.Description);
                Stack.Execute(command);
            });
            Tracker = new AudioMixerGestureTracker(Document);
        }

        public AudioMixerDocument Document { get; }

        public AudioMixerGestureTracker Tracker { get; }

        public EditorHistoryStack Stack { get; } = new();

        public List<string> Descriptions { get; } = new();

        public float Volume(string bus) => Document.Asset.Buses.Single(data => data.Name == bus).Volume;

        /// <summary>Undoes every entry and returns how many there were.</summary>
        public int UndoAll()
        {
            int count = 0;
            while (Stack.CanUndo)
            {
                Stack.Undo();
                count++;
            }

            return count;
        }
    }

    // ───────────────────────── a drag ─────────────────────────

    [Fact]
    public void ADrag_IsOneEntry_EvenWhenTheValueMovesInTheFrameOfTheRelease()
    {
        var rig = new Rig();

        for (int step = 1; step <= 5; step++)
        {
            Assert.True(rig.Tracker.Change("Sfx", 1f - (step * 0.1f)));
            Assert.False(rig.Tracker.Tick(isHeld: true));
        }

        Assert.True(rig.Tracker.IsOpen);
        Assert.Equal("Sfx", rig.Tracker.Bus);
        Assert.Empty(rig.Descriptions);

        // The mouse stays down without moving.
        Assert.False(rig.Tracker.Tick(isHeld: true));
        Assert.True(rig.Tracker.IsOpen);

        // The release: the thumb is no longer held, but the value still moves in this frame.
        Assert.True(rig.Tracker.Change("Sfx", 0.3f));
        Assert.False(rig.Tracker.Tick(isHeld: false));
        Assert.True(rig.Tracker.IsOpen);
        Assert.Empty(rig.Descriptions);

        // The first frame without change where the thumb is not held closes it.
        Assert.True(rig.Tracker.Tick(isHeld: false));

        Assert.False(rig.Tracker.IsOpen);
        Assert.Null(rig.Tracker.Bus);
        Assert.False(rig.Document.IsGestureOpen);
        Assert.Equal(new[] { "Set volume of 'Sfx'" }, rig.Descriptions);
        Assert.Equal(0.3f, rig.Volume("Sfx"));
        Assert.Equal(1, rig.UndoAll());
        Assert.Equal(1f, rig.Volume("Sfx"));
    }

    [Fact]
    public void AThumbHeldStillForALongTime_KeepsTheGestureOpen_UntilItIsReleased()
    {
        var rig = new Rig();
        rig.Tracker.Change("Sfx", 0.6f);
        rig.Tracker.Tick(isHeld: true);

        for (int frame = 0; frame < 500; frame++)
        {
            Assert.False(rig.Tracker.Tick(isHeld: true));
        }

        Assert.True(rig.Tracker.IsOpen);
        Assert.Empty(rig.Descriptions);

        Assert.True(rig.Tracker.Tick(isHeld: false));

        Assert.Single(rig.Descriptions);
    }

    [Fact]
    public void AGestureThatComesBackToItsStartValue_AddsNoEntry()
    {
        var rig = new Rig();

        rig.Tracker.Change("Sfx", 0.2f);
        rig.Tracker.Tick(isHeld: true);
        rig.Tracker.Change("Sfx", 1f);
        rig.Tracker.Tick(isHeld: false);
        Assert.True(rig.Tracker.Tick(isHeld: false));

        Assert.Empty(rig.Descriptions);
        Assert.False(rig.Document.IsDirty);
        Assert.Equal(1f, rig.Volume("Sfx"));
    }

    [Fact]
    public void TheTwoDragsOfOneSession_AreTwoEntries()
    {
        var rig = new Rig();

        rig.Tracker.Change("Sfx", 0.8f);
        rig.Tracker.Tick(isHeld: true);
        rig.Tracker.Tick(isHeld: false);
        rig.Tracker.Tick(isHeld: false);
        rig.Tracker.Change("Sfx", 0.4f);
        rig.Tracker.Tick(isHeld: true);
        rig.Tracker.Tick(isHeld: false);
        rig.Tracker.Tick(isHeld: false);

        Assert.Equal(2, rig.Descriptions.Count);
        Assert.Equal(2, rig.UndoAll());
        Assert.Equal(1f, rig.Volume("Sfx"));
    }

    // ───────────────────────── changes nobody drags ─────────────────────────

    [Fact]
    public void AClickOnTheTrack_IsOneEntry_ClosedOneFrameAfterTheChange()
    {
        var rig = new Rig();

        // The button is released on the track: one change, the control is no longer pressed.
        Assert.True(rig.Tracker.Change("Sfx", 0.7f));
        Assert.False(rig.Tracker.Tick(isHeld: false));
        Assert.True(rig.Tracker.IsOpen);

        Assert.True(rig.Tracker.Tick(isHeld: false));

        Assert.Single(rig.Descriptions);
        Assert.Equal(0.7f, rig.Volume("Sfx"));
    }

    [Fact]
    public void TwoChangesInOneFrame_AreOneEntry()
    {
        var rig = new Rig();

        rig.Tracker.Change("Sfx", 0.9f);
        rig.Tracker.Change("Sfx", 0.8f);
        Assert.False(rig.Tracker.Tick(isHeld: false));
        Assert.True(rig.Tracker.Tick(isHeld: false));

        Assert.Single(rig.Descriptions);
        Assert.Equal(1, rig.UndoAll());
        Assert.Equal(1f, rig.Volume("Sfx"));
    }

    [Fact]
    public void ABurstOfWheelNotchesOrKeyRepeats_IsOneEntry_AndTheNextBurstAnother()
    {
        var rig = new Rig();

        foreach (float value in new[] { 0.9f, 0.8f, 0.7f })
        {
            rig.Tracker.Change("Sfx", value);
            Assert.False(rig.Tracker.Tick(isHeld: false));
        }

        Assert.Empty(rig.Descriptions);
        Assert.True(rig.Tracker.Tick(isHeld: false));
        Assert.Single(rig.Descriptions);

        foreach (float value in new[] { 0.6f, 0.5f })
        {
            rig.Tracker.Change("Sfx", value);
            Assert.False(rig.Tracker.Tick(isHeld: false));
        }

        Assert.True(rig.Tracker.Tick(isHeld: false));

        Assert.Equal(2, rig.Descriptions.Count);
        Assert.Equal(2, rig.UndoAll());
        Assert.Equal(1f, rig.Volume("Sfx"));
    }

    [Fact]
    public void ATickWithNoGesture_DoesNothing()
    {
        var rig = new Rig();

        Assert.False(rig.Tracker.Tick(isHeld: false));
        Assert.False(rig.Tracker.Tick(isHeld: true));

        Assert.False(rig.Tracker.IsOpen);
        Assert.Empty(rig.Descriptions);
        Assert.False(rig.Document.IsGestureOpen);
    }

    // ───────────────────────── closing from outside ─────────────────────────

    [Fact]
    public void End_MidGesture_IsOneEntry_AndALaterEndOrTickAddsNothing()
    {
        var rig = new Rig();
        rig.Tracker.Change("Sfx", 0.5f);
        rig.Tracker.Tick(isHeld: true);
        rig.Tracker.Change("Sfx", 0.4f);

        // The panel is disposed with the thumb still held.
        rig.Tracker.End();
        rig.Tracker.End();
        rig.Tracker.Tick(isHeld: false);
        rig.Tracker.Tick(isHeld: false);

        Assert.False(rig.Tracker.IsOpen);
        Assert.False(rig.Document.IsGestureOpen);
        Assert.Equal(new[] { "Set volume of 'Sfx'" }, rig.Descriptions);
        Assert.Equal(0.4f, rig.Volume("Sfx"));
    }

    [Fact]
    public void APlaySessionStartingMidGesture_EndsIt_AndTheNextChangeIsAnotherGesture()
    {
        var rig = new Rig();
        rig.Tracker.Change("Sfx", 0.5f);
        rig.Tracker.Tick(isHeld: true);

        // The panel ends the gesture when the session starts; the thumb was still held.
        rig.Tracker.End();
        Assert.Single(rig.Descriptions);

        // A late change of the same drag (the control was not disabled yet) opens a new gesture, closed by the next idle frame.
        rig.Tracker.Change("Sfx", 0.3f);
        Assert.True(rig.Tracker.IsOpen);
        rig.Tracker.Tick(isHeld: false);
        rig.Tracker.Tick(isHeld: false);

        Assert.Equal(2, rig.Descriptions.Count);
        Assert.Equal(2, rig.UndoAll());
    }

    [Fact]
    public void EndWithNothingOpen_DoesNothing()
    {
        var rig = new Rig();

        rig.Tracker.End();

        Assert.Empty(rig.Descriptions);
        Assert.False(rig.Document.IsGestureOpen);
    }

    [Fact]
    public void AGestureTheDocumentClosedItself_IsForgotten_AndTheNextChangeOpensANewOne()
    {
        var rig = new Rig();
        rig.Tracker.Change("Sfx", 0.5f);

        // Another edit of the document closes the gesture (one entry), then adds its own.
        Assert.True(rig.Document.SetBusVolume("Voice", 0.2f));
        Assert.False(rig.Document.IsGestureOpen);
        Assert.Equal(new[] { "Set volume of 'Sfx'", "Set volume of 'Voice'" }, rig.Descriptions);

        Assert.False(rig.Tracker.IsOpen);
        Assert.False(rig.Tracker.Tick(isHeld: true));
        // Nothing was left to close: an idle frame afterwards does not claim to have closed anything.
        Assert.False(rig.Tracker.Tick(isHeld: false));
        Assert.Equal(2, rig.Descriptions.Count);

        Assert.True(rig.Tracker.Change("Sfx", 0.1f));
        Assert.True(rig.Tracker.IsOpen);
        rig.Tracker.Tick(isHeld: false);
        rig.Tracker.Tick(isHeld: false);

        Assert.Equal(3, rig.Descriptions.Count);
        Assert.Equal(0.1f, rig.Volume("Sfx"));
    }

    [Fact]
    public void AnUndoMidDrag_ClosesTheDocumentGesture_AndTheDragGoesOnAsANewEntry()
    {
        var rig = new Rig();
        Assert.True(rig.Document.SetBusVolume("Voice", 0.2f));
        rig.Tracker.Change("Sfx", 0.6f);
        rig.Tracker.Tick(isHeld: true);

        rig.Stack.Undo();

        Assert.False(rig.Document.IsGestureOpen);
        Assert.False(rig.Tracker.Tick(isHeld: true));
        Assert.False(rig.Tracker.IsOpen);

        Assert.True(rig.Tracker.Change("Sfx", 0.4f));
        rig.Tracker.Tick(isHeld: false);
        rig.Tracker.Tick(isHeld: false);

        Assert.Equal(0.4f, rig.Volume("Sfx"));
        Assert.Equal(1f, rig.Volume("Voice"));
        Assert.Equal(new[] { "Set volume of 'Voice'", "Set volume of 'Sfx'" }, rig.Descriptions);
    }

    // ───────────────────────── another bus, an unknown bus ─────────────────────────

    [Fact]
    public void AChangeOnAnotherBus_ClosesTheGestureOfThePreviousOne_AtOnce()
    {
        var rig = new Rig();

        rig.Tracker.Change("Sfx", 0.5f);
        rig.Tracker.Change("Voice", 0.25f);

        Assert.Equal(new[] { "Set volume of 'Sfx'" }, rig.Descriptions);
        Assert.Equal("Voice", rig.Tracker.Bus);

        rig.Tracker.Tick(isHeld: false);
        rig.Tracker.Tick(isHeld: false);

        Assert.Equal(new[] { "Set volume of 'Sfx'", "Set volume of 'Voice'" }, rig.Descriptions);
        Assert.Equal(0.5f, rig.Volume("Sfx"));
        Assert.Equal(0.25f, rig.Volume("Voice"));
    }

    [Fact]
    public void TheBusNameIsMatchedWithoutCase_SoOneBusIsOneGesture()
    {
        var rig = new Rig();

        rig.Tracker.Change("Sfx", 0.5f);
        rig.Tracker.Change("SFX", 0.4f);
        rig.Tracker.Tick(isHeld: false);
        rig.Tracker.Tick(isHeld: false);

        Assert.Single(rig.Descriptions);
    }

    [Fact]
    public void AnUnknownBus_IsRefused_AndLeavesNoGestureOpen()
    {
        var rig = new Rig();

        Assert.False(rig.Tracker.Change("Nope", 0.5f));

        Assert.False(rig.Tracker.IsOpen);
        Assert.False(rig.Document.IsGestureOpen);
        Assert.False(rig.Tracker.Tick(isHeld: false));
        Assert.Empty(rig.Descriptions);
    }

    [Fact]
    public void AnUnknownBusAfterAKnownOne_ClosesTheKnownGesture_AndOpensNothing()
    {
        var rig = new Rig();
        rig.Tracker.Change("Sfx", 0.5f);

        Assert.False(rig.Tracker.Change("Nope", 0.5f));

        Assert.False(rig.Tracker.IsOpen);
        Assert.Single(rig.Descriptions);
    }

    // ───────────────────────── arguments ─────────────────────────

    [Fact]
    public void TheArguments_AreChecked()
    {
        var rig = new Rig();

        Assert.Throws<ArgumentNullException>(() => new AudioMixerGestureTracker(null!));
        Assert.Throws<ArgumentNullException>(() => rig.Tracker.Change(null!, 0.5f));
        Assert.Throws<ArgumentException>(() => rig.Tracker.Change(" ", 0.5f));
    }
}
