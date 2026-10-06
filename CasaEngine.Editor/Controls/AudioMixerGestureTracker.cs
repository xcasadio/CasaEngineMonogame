#nullable enable

using System;
using CasaEngine.EditorServices.Audio;

namespace CasaEngine.Editor.Controls;

/// <summary>
/// Turns the changes of a bus fader into fader gestures of an <see cref="AudioMixerDocument"/> (plan T10.6, decision P50): the
/// changes of one burst are one history entry. It knows nothing of the UI: the panel feeds it each value the fader took
/// (<see cref="Change"/>) and, once per frame, whether the control is still held (<see cref="Tick"/>).
/// </summary>
/// <remarks>
/// <para>
/// A gesture opens at the first change. It closes at the first <see cref="Tick"/> that saw no change since the previous one
/// while the control is not held, so a drag that pauses with the thumb still held stays one gesture, the release of a drag
/// (which may still move the value in the frame of the release) is absorbed, and a change nobody drags (keyboard, wheel,
/// click on the track) is a one-frame burst that closes one frame later. <see cref="End"/> closes it at once (the panel is
/// disposed, a play session starts).
/// </para>
/// <para>
/// The tracker never trusts its own state over the document's: a gesture the document closed by itself (an undo, a save, another
/// edit) is forgotten, and the next change opens a new one.
/// </para>
/// </remarks>
internal sealed class AudioMixerGestureTracker
{
    private readonly AudioMixerDocument _document;
    private string? _bus;
    private bool _changedSinceTick;

    public AudioMixerGestureTracker(AudioMixerDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>True while a gesture opened by this tracker is open in the document.</summary>
    public bool IsOpen => _bus != null && _document.IsGestureOpen;

    /// <summary>The bus of the open gesture, or null when none is open.</summary>
    public string? Bus => IsOpen ? _bus : null;

    /// <summary>
    /// A fader of <paramref name="bus"/> took <paramref name="volume"/>. Opens a gesture when none is open; a change on another
    /// bus first closes the gesture of the previous one (one entry per bus). False, with nothing changed, when the asset has no such bus.
    /// </summary>
    public bool Change(string bus, float volume)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bus);

        if (_bus != null && !string.Equals(_bus, bus, StringComparison.OrdinalIgnoreCase))
        {
            End();
        }

        bool opened = false;
        if (!_document.IsGestureOpen)
        {
            _document.BeginGesture($"Set volume of '{bus}'");
            opened = true;
        }

        if (!_document.UpdateBusVolume(bus, volume))
        {
            if (opened)
            {
                _document.EndGesture();
            }

            return false;
        }

        _bus = bus;
        _changedSinceTick = true;
        return true;
    }

    /// <summary>
    /// One frame went by. <paramref name="isHeld"/> is true while the control that opened the gesture is still held (its thumb is
    /// dragged, or the mouse button pressed on it). Returns true when this call closed the gesture.
    /// </summary>
    public bool Tick(bool isHeld)
    {
        bool changed = _changedSinceTick;
        _changedSinceTick = false;

        if (_bus == null)
        {
            return false;
        }

        if (!_document.IsGestureOpen)
        {
            // The document closed it (an undo, a save, another edit): nothing is left to close.
            _bus = null;
            return false;
        }

        if (changed || isHeld)
        {
            return false;
        }

        End();
        return true;
    }

    /// <summary>Closes the gesture now, if one is open: one history entry, or none when the value came back to where it started.</summary>
    public void End()
    {
        if (_bus == null)
        {
            _changedSinceTick = false;
            return;
        }

        _bus = null;
        _changedSinceTick = false;

        if (_document.IsGestureOpen)
        {
            _document.EndGesture();
        }
    }
}
