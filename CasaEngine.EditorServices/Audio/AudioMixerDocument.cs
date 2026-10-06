using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.EditorServices.Audio;

/// <summary>
/// Editing model of one <c>.audioMixer</c> asset in the editor (plan decisions P49 to P53, author decisions D16 and D17).
/// The asset is the single source of truth: every edit changes the asset and is then applied to the live mixer through an
/// <see cref="AudioMixerAssetApplier"/>, always asset to mixer, never the reverse (nothing reads the live mixer back).
/// </summary>
/// <remarks>
/// History is snapshot based, like the particle inspector: an operation serializes the asset, changes it, serializes it again
/// and, when the two texts differ, hands one <see cref="IEditorCommand"/> (undo and redo reload a snapshot, then apply it to
/// the live mixer) to the command sink. The change is already applied (to the asset and to the live mixer) when the sink
/// receives the command, so the first <see cref="IEditorCommand.Execute"/> does nothing; a sink that executes the command
/// (for example <c>EditorHistoryStack.Execute</c>) and one that only records it both work. Without a sink the change is
/// simply applied, without history. A fader gesture is one history entry, written when the gesture ends.
/// Mute and solo are transient: they live in the document, never in the asset nor in the history, and are written to
/// <see cref="AudioBus.IsMuted"/> only on the buses of the asset (a live bus the asset does not name is never muted; Master
/// and Editor never are). Game thread only.
/// </remarks>
public sealed class AudioMixerDocument
{
    private static readonly string[] DefaultBusNames =
    {
        AudioBusNames.Music, AudioBusNames.Sfx, AudioBusNames.Voice, AudioBusNames.Ui,
    };

    private readonly Action<IEditorCommand> _commandSink;
    private readonly int? _explicitBusCapacity;
    private readonly HashSet<string> _muted = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _soloed = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _ownedMutes = new(StringComparer.OrdinalIgnoreCase);
    private AudioMixerAssetApplier _applier;
    private AudioMixer _defaultMixer;
    private string _snapshot;
    private string _savedSnapshot;
    private bool _isDirty;
    private IReadOnlyList<string> _problems = Array.Empty<string>();
    private bool _gestureOpen;
    private string _gestureDescription;
    private string _gestureBefore;
    private bool _gestureNeedsSync;

    /// <param name="asset">The asset to edit (changed in place).</param>
    /// <param name="relativePath">Path of the asset file relative to the project, used by <see cref="TrySave"/>.</param>
    /// <param name="liveApplier">The applier of the live mixer the asset drives, or null for a document that is not live.</param>
    /// <param name="commandSink">Receives one command per user intent; null applies the edits without history.</param>
    /// <param name="busCapacity">Number of buses the backend holds (Master included); the default takes the applier's capacity.</param>
    public AudioMixerDocument(
        AudioMixerAsset asset,
        string relativePath,
        AudioMixerAssetApplier liveApplier = null,
        Action<IEditorCommand> commandSink = null,
        int busCapacity = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        Asset = asset;
        RelativePath = relativePath;
        _applier = liveApplier;
        _commandSink = commandSink;
        _explicitBusCapacity = busCapacity == int.MaxValue ? null : busCapacity;

        for (int index = 0; index < DefaultBusNames.Length; index++)
        {
            if (FindBus(DefaultBusNames[index]) == null)
            {
                Asset.Buses.Add(new AudioMixerBusData { Name = DefaultBusNames[index], Parent = AudioBusNames.Master, Volume = 1f });
            }
        }

        _snapshot = Serialize();
        _savedSnapshot = _snapshot;
        RefreshState();
    }

    public AudioMixerAsset Asset { get; }

    public string RelativePath { get; }

    /// <summary>True while the document drives the live mixer.</summary>
    public bool IsLive => _applier != null;

    /// <summary>True when the asset differs from the last saved (or opened) state. Cached, recomputed after each change.</summary>
    public bool IsDirty => _isDirty;

    /// <summary>What the validator found in the asset (against the live mixer when live), recomputed on every change.</summary>
    public IReadOnlyList<string> Problems => _problems;

    /// <summary>Number of buses the backend holds, Master included.</summary>
    public int BusCapacity => _explicitBusCapacity ?? _applier?.BusCapacity ?? int.MaxValue;

    /// <summary>True between <see cref="BeginGesture"/> and <see cref="EndGesture"/>.</summary>
    public bool IsGestureOpen => _gestureOpen;

    /// <summary>Raised after the asset, the problems, the dirty flag, the live binding or the transient state changed.</summary>
    public event EventHandler Changed;

    // ------------------------------------------------------------------ structure

    /// <summary>Adds a custom bus under <paramref name="parent"/> (Master when blank) at volume 1.</summary>
    public bool TryAddBus(string name, string parent, out string error)
    {
        error = null;
        string busName = name?.Trim();

        if (string.IsNullOrEmpty(busName))
        {
            error = "A bus needs a name.";
            return false;
        }

        if (IsReserved(busName))
        {
            error = $"'{busName}' is reserved by the engine.";
            return false;
        }

        if (FindBus(busName) != null)
        {
            error = $"A bus named '{busName}' already exists.";
            return false;
        }

        string parentName;
        if (string.IsNullOrWhiteSpace(parent) || string.Equals(parent.Trim(), AudioBusNames.Master, StringComparison.OrdinalIgnoreCase))
        {
            parentName = AudioBusNames.Master;
        }
        else if (string.Equals(parent.Trim(), AudioBusNames.Editor, StringComparison.OrdinalIgnoreCase))
        {
            error = "The Editor bus is for previews and cannot be a parent.";
            return false;
        }
        else
        {
            var parentBus = FindBus(parent.Trim());
            if (parentBus == null)
            {
                error = $"The parent bus '{parent.Trim()}' does not exist.";
                return false;
            }

            parentName = parentBus.Name;
        }

        if (CountKnownBuses() >= BusCapacity)
        {
            error = $"The bus capacity of {BusCapacity} is reached.";
            return false;
        }

        var live = _applier?.Service.Mixer;
        if (live != null && live.TryGetBus(busName, out var liveBus))
        {
            string liveParent = liveBus.Parent?.Name;
            if (!string.Equals(liveParent, parentName, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Bus '{busName}' is already live under '{liveParent}'; a live bus is never reparented.";
                return false;
            }
        }

        Commit($"Add bus '{busName}'", () => Asset.Buses.Add(new AudioMixerBusData { Name = busName, Parent = parentName, Volume = 1f }));
        return true;
    }

    /// <summary>Removes a custom bus without children, incoming send or ducking source (it stays live until the next start).</summary>
    public bool TryRemoveBus(string name, out string error)
    {
        error = null;
        var bus = FindBus(name);

        if (bus == null)
        {
            error = $"There is no bus named '{name}'.";
            return false;
        }

        for (int index = 0; index < DefaultBusNames.Length; index++)
        {
            if (string.Equals(DefaultBusNames[index], bus.Name, StringComparison.OrdinalIgnoreCase))
            {
                error = $"'{bus.Name}' is a default bus and cannot be removed.";
                return false;
            }
        }

        for (int index = 0; index < Asset.Buses.Count; index++)
        {
            var other = Asset.Buses[index];
            if (other == bus)
            {
                continue;
            }

            if (string.Equals(other.Parent, bus.Name, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Bus '{bus.Name}' has a child bus ('{other.Name}'); remove or move it first.";
                return false;
            }

            for (int sendIndex = 0; sendIndex < other.Sends.Count; sendIndex++)
            {
                if (string.Equals(other.Sends[sendIndex].Target, bus.Name, StringComparison.OrdinalIgnoreCase))
                {
                    error = $"Bus '{bus.Name}' is the target of a send from '{other.Name}'; remove the send first.";
                    return false;
                }
            }

            for (int effectIndex = 0; effectIndex < other.Effects.Count; effectIndex++)
            {
                if (other.Effects[effectIndex] is AudioMixerDuckingEffectData ducking
                    && string.Equals(ducking.Source, bus.Name, StringComparison.OrdinalIgnoreCase))
                {
                    error = $"Bus '{bus.Name}' is the ducking source of '{other.Name}'; remove the effect first.";
                    return false;
                }
            }
        }

        Commit($"Remove bus '{bus.Name}'", () => Asset.Buses.Remove(bus));
        return true;
    }

    // ------------------------------------------------------------------ volume

    /// <summary>Sets the volume of a bus of the asset as one history entry. False when the asset has no such bus.</summary>
    public bool SetBusVolume(string name, float volume)
    {
        var bus = FindBus(name);
        if (bus == null)
        {
            return false;
        }

        Commit($"Set volume of '{bus.Name}'", () => bus.Volume = volume);
        return true;
    }

    /// <summary>Opens a gesture (a fader drag, a field being typed): the changes until <see cref="EndGesture"/> are one entry.</summary>
    public void BeginGesture(string description)
    {
        EndGesture();
        _gestureOpen = true;
        _gestureDescription = string.IsNullOrWhiteSpace(description) ? "Edit mixer" : description;
        _gestureBefore = _snapshot;
        _gestureNeedsSync = false;
    }

    /// <summary>
    /// Sets the volume of a bus during a gesture: the asset and the live bus change at once, nothing is serialized. Opens a
    /// gesture when none is open. False when the asset has no such bus.
    /// </summary>
    public bool UpdateBusVolume(string name, float volume)
    {
        var bus = FindBus(name);
        if (bus == null)
        {
            return false;
        }

        if (!_gestureOpen)
        {
            BeginGesture($"Set volume of '{bus.Name}'");
        }

        bus.Volume = volume;

        if (_applier != null && !_applier.TryApplyBusVolume(bus.Name, bus.Volume))
        {
            _gestureNeedsSync = true;
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Closes the gesture: one history entry, or none when the asset is back to its state at the start.</summary>
    public void EndGesture()
    {
        if (!_gestureOpen)
        {
            return;
        }

        _gestureOpen = false;
        string before = _gestureBefore;
        string description = _gestureDescription;
        bool needsSync = _gestureNeedsSync;
        _gestureBefore = null;
        _gestureDescription = null;
        _gestureNeedsSync = false;

        string after = Serialize();
        _snapshot = after;

        if (string.Equals(before, after, StringComparison.Ordinal))
        {
            if (needsSync)
            {
                SyncLive();
            }

            RefreshState();
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        Publish(description, before, after);
    }

    // ------------------------------------------------------------------ effects

    public bool TryAddEffect(string bus, AudioMixerEffectData data, out string error)
    {
        var busData = RequireBus(bus, out error);
        if (busData == null)
        {
            return false;
        }

        if (data == null)
        {
            error = "An effect is required.";
            return false;
        }

        if (busData.Effects.Count >= AudioBus.MaxEffects)
        {
            error = $"Bus '{busData.Name}' already holds {AudioBus.MaxEffects} effects.";
            return false;
        }

        if (!CheckDucking(busData, data, -1, out error))
        {
            return false;
        }

        Commit($"Add effect to '{busData.Name}'", () => busData.Effects.Add(data));
        return true;
    }

    public bool TryRemoveEffect(string bus, int index)
    {
        var busData = FindBus(bus);
        if (busData == null || index < 0 || index >= busData.Effects.Count)
        {
            return false;
        }

        Commit($"Remove effect from '{busData.Name}'", () => busData.Effects.RemoveAt(index));
        return true;
    }

    public bool TryMoveEffect(string bus, int index, int newIndex)
    {
        var busData = FindBus(bus);
        if (busData == null || index < 0 || index >= busData.Effects.Count
            || newIndex < 0 || newIndex >= busData.Effects.Count || index == newIndex)
        {
            return false;
        }

        Commit($"Move effect of '{busData.Name}'", () =>
        {
            var effect = busData.Effects[index];
            busData.Effects.RemoveAt(index);
            busData.Effects.Insert(newIndex, effect);
        });
        return true;
    }

    /// <summary>Replaces the effect at <paramref name="index"/> (new parameters, or another type).</summary>
    public bool TrySetEffect(string bus, int index, AudioMixerEffectData data, out string error)
    {
        var busData = RequireBus(bus, out error);
        if (busData == null)
        {
            return false;
        }

        if (data == null)
        {
            error = "An effect is required.";
            return false;
        }

        if (index < 0 || index >= busData.Effects.Count)
        {
            error = $"Bus '{busData.Name}' has no effect at index {index}.";
            return false;
        }

        if (!CheckDucking(busData, data, index, out error))
        {
            return false;
        }

        Commit($"Edit effect of '{busData.Name}'", () => busData.Effects[index] = data);
        return true;
    }

    // ------------------------------------------------------------------ sends

    /// <summary>Sets the send of <paramref name="bus"/> to <paramref name="target"/>; a level of 0 removes it.</summary>
    public bool TrySetSend(string bus, string target, float level, out string error)
    {
        var busData = RequireBus(bus, out error);
        if (busData == null)
        {
            return false;
        }

        if (float.IsNaN(level))
        {
            error = "The level of a send must be a number.";
            return false;
        }

        string targetName = FindExistingBusName(target);
        if (targetName == null)
        {
            error = $"The send target '{target}' does not exist.";
            return false;
        }

        float clamped = Math.Clamp(level, 0f, 1f);
        int existing = -1;
        for (int index = 0; index < busData.Sends.Count; index++)
        {
            if (string.Equals(busData.Sends[index].Target, targetName, StringComparison.OrdinalIgnoreCase))
            {
                existing = index;
                break;
            }
        }

        if (clamped <= 0f)
        {
            if (existing >= 0)
            {
                Commit($"Remove send of '{busData.Name}'", () => busData.Sends.RemoveAt(existing));
            }

            return true;
        }

        if (string.Equals(targetName, busData.Name, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Bus '{busData.Name}' cannot send to itself.";
            return false;
        }

        if (existing < 0 && busData.Sends.Count >= AudioBus.MaxSends)
        {
            error = $"Bus '{busData.Name}' already holds {AudioBus.MaxSends} sends.";
            return false;
        }

        if (AudioMixerAssetValidator.WouldMakeCycle(Asset, busData.Name, targetName))
        {
            error = $"A send from '{busData.Name}' to '{targetName}' would make a cycle.";
            return false;
        }

        var send = new AudioMixerSendData(targetName, clamped);
        Commit($"Set send of '{busData.Name}'", () =>
        {
            if (existing >= 0)
            {
                busData.Sends[existing] = send;
            }
            else
            {
                busData.Sends.Add(send);
            }
        });
        return true;
    }

    // ------------------------------------------------------------------ save and live

    /// <summary>Writes the asset to <see cref="RelativePath"/> under the project path; the saved state becomes the current one.</summary>
    public bool TrySave(out string error)
    {
        error = null;
        EndGesture();

        try
        {
            EditorAssetWriterService.SaveAsset(RelativePath, Asset, EditorAssetSaveSource.AudioMixerEditorPanel);
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }

        _savedSnapshot = _snapshot;
        RefreshState();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Applies the current asset to the live mixer (no effect when the document is not live).</summary>
    public void ApplyToLive()
    {
        EndGesture();
        SyncLive();
        RefreshState();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Applies the saved state of the asset to the live mixer, for a closed or reloaded document that was modified without
    /// being saved. No effect when the document is not live. The document itself is not changed.
    /// </summary>
    public void RestoreSavedToLive()
    {
        if (_applier == null)
        {
            return;
        }

        var saved = new AudioMixerAsset();
        saved.Load(JObject.Parse(_savedSnapshot));
        _applier.Apply(saved, logProblems: false);
        ApplyMutes();
    }

    /// <summary>Stops driving the live mixer: gives back the mutes this document set, drops the applier, applies nothing.</summary>
    public void DetachLive()
    {
        if (_applier == null)
        {
            return;
        }

        RestoreOwnedMutes();
        _applier = null;
        RefreshState();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Follows the project's mixer: attaches to <paramref name="applier"/> and applies the asset when the asset of this document
    /// is the project's (<paramref name="projectAssetId"/> not empty and equal to <see cref="ObjectBase.AssetId"/>),
    /// otherwise detaches.
    /// </summary>
    public void UpdateLiveBinding(Guid projectAssetId, AudioMixerAssetApplier applier)
    {
        if (applier == null || projectAssetId == Guid.Empty || Asset.AssetId != projectAssetId)
        {
            DetachLive();
            return;
        }

        if (_applier != null && !ReferenceEquals(_applier, applier))
        {
            RestoreOwnedMutes();
        }

        _applier = applier;
        EndGesture();
        SyncLive();
        RefreshState();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // ------------------------------------------------------------------ transient state

    public bool IsMuted(string bus) => !string.IsNullOrEmpty(bus) && _muted.Contains(bus);

    public bool IsSoloed(string bus) => !string.IsNullOrEmpty(bus) && _soloed.Contains(bus);

    /// <summary>Mutes or unmutes a bus of the asset on the live mixer. Not saved, not in the history. False for Master, Editor and unknown buses.</summary>
    public bool SetMute(string bus, bool muted)
    {
        var busData = FindBus(bus);
        if (busData == null)
        {
            return false;
        }

        if (muted ? _muted.Add(busData.Name) : _muted.Remove(busData.Name))
        {
            ApplyMutes();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return true;
    }

    /// <summary>Solos or unsolos a bus of the asset (see <see cref="AudioMixerSolo"/>). Not saved, not in the history.</summary>
    public bool SetSolo(string bus, bool soloed)
    {
        var busData = FindBus(bus);
        if (busData == null)
        {
            return false;
        }

        if (soloed ? _soloed.Add(busData.Name) : _soloed.Remove(busData.Name))
        {
            ApplyMutes();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return true;
    }

    /// <summary>Forgets every mute and solo and gives back exactly the live mutes this document set.</summary>
    public void ClearTransientState()
    {
        _muted.Clear();
        _soloed.Clear();
        ApplyMutes();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // ------------------------------------------------------------------ internals

    private static bool IsReserved(string name)
        => string.Equals(name, AudioBusNames.Master, StringComparison.OrdinalIgnoreCase)
           || string.Equals(name, AudioBusNames.Editor, StringComparison.OrdinalIgnoreCase);

    private AudioMixerBusData FindBus(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        string trimmed = name.Trim();
        for (int index = 0; index < Asset.Buses.Count; index++)
        {
            var bus = Asset.Buses[index];
            if (bus != null && string.Equals(bus.Name, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return bus;
            }
        }

        return null;
    }

    private AudioMixerBusData RequireBus(string name, out string error)
    {
        error = null;
        var bus = FindBus(name);
        if (bus == null)
        {
            error = $"There is no bus named '{name}'.";
        }

        return bus;
    }

    private AudioMixer KnownMixer => _applier?.Service.Mixer ?? (_defaultMixer ??= AudioBusNames.CreateDefaultMixer());

    /// <summary>Distinct names of the buses the live mixer (the default mixer when not live) and the asset hold.</summary>
    private int CountKnownBuses()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var buses = KnownMixer.Buses;
        for (int index = 0; index < buses.Count; index++)
        {
            names.Add(buses[index].Name);
        }

        for (int index = 0; index < Asset.Buses.Count; index++)
        {
            if (!string.IsNullOrWhiteSpace(Asset.Buses[index]?.Name))
            {
                names.Add(Asset.Buses[index].Name);
            }
        }

        return names.Count;
    }

    /// <summary>The canonical name of a bus of the asset or of the live mixer (Master and Editor included), or null.</summary>
    private string FindExistingBusName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var bus = FindBus(name);
        if (bus != null)
        {
            return bus.Name;
        }

        return KnownMixer.TryGetBus(name.Trim(), out var live) ? live.Name : null;
    }

    private bool CheckDucking(AudioMixerBusData bus, AudioMixerEffectData data, int replacedIndex, out string error)
    {
        error = null;
        if (data is not AudioMixerDuckingEffectData ducking)
        {
            return true;
        }

        string source = FindExistingBusName(ducking.Source);
        if (source == null)
        {
            error = $"The ducking source '{ducking.Source}' does not exist.";
            return false;
        }

        if (string.Equals(source, bus.Name, StringComparison.OrdinalIgnoreCase))
        {
            error = "A ducking cannot use its own bus as source.";
            return false;
        }

        // The replaced effect no longer counts: its own relation goes away with it.
        AudioMixerEffectData replaced = null;
        if (replacedIndex >= 0)
        {
            replaced = bus.Effects[replacedIndex];
            bus.Effects.RemoveAt(replacedIndex);
        }

        bool cycle;
        try
        {
            cycle = AudioMixerAssetValidator.WouldMakeCycle(Asset, source, bus.Name);
        }
        finally
        {
            if (replacedIndex >= 0)
            {
                bus.Effects.Insert(replacedIndex, replaced);
            }
        }

        if (cycle)
        {
            error = $"A ducking of '{bus.Name}' by '{source}' would make a cycle.";
            return false;
        }

        return true;
    }

    private string Serialize()
    {
        if (!EditorAssetJsonSerializer.TrySerialize(Asset, out var root))
        {
            throw new InvalidOperationException("The audio mixer asset cannot be serialized.");
        }

        return root.ToString(Formatting.None);
    }

    /// <summary>Runs an edit as one history entry: closes any gesture, changes the asset, publishes when the asset really changed.</summary>
    private void Commit(string description, Action change)
    {
        EndGesture();
        string before = _snapshot;
        change();
        string after = Serialize();

        if (string.Equals(before, after, StringComparison.Ordinal))
        {
            return;
        }

        _snapshot = after;
        Publish(description, before, after);
    }

    private void Publish(string description, string before, string after)
    {
        SyncLive();
        RefreshState();

        if (_commandSink != null)
        {
            bool executed = false;
            _commandSink(new EditorDelegateCommand(
                description,
                () =>
                {
                    // The change is already applied when the command is created: the first execution has nothing to redo.
                    if (!executed)
                    {
                        executed = true;
                        return;
                    }

                    Reload(after);
                },
                () => Reload(before)));
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Reload(string snapshot)
    {
        _gestureOpen = false;
        _gestureBefore = null;
        _gestureNeedsSync = false;
        Asset.Load(JObject.Parse(snapshot));
        _snapshot = snapshot;
        SyncLive();
        RefreshState();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void SyncLive()
    {
        if (_applier == null)
        {
            return;
        }

        _applier.Apply(Asset, logProblems: false);
        ApplyMutes();
    }

    private void RefreshState()
    {
        _isDirty = !string.Equals(_snapshot, _savedSnapshot, StringComparison.Ordinal);
        _problems = AudioMixerAssetValidator.Validate(Asset, _applier?.Service.Mixer, BusCapacity).Problems;
    }

    // ------------------------------------------------------------------ mutes

    private HashSet<string> ComputeWantedMutes()
    {
        var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string name in _muted)
        {
            if (FindBus(name) != null)
            {
                wanted.Add(name);
            }
        }

        var soloed = new List<string>();
        foreach (string name in _soloed)
        {
            if (FindBus(name) != null)
            {
                soloed.Add(name);
            }
        }

        if (soloed.Count > 0)
        {
            var muted = new List<string>();
            AudioMixerSolo.ComputeMuted(BuildSoloNodes(), soloed, muted);
            for (int index = 0; index < muted.Count; index++)
            {
                if (FindBus(muted[index]) != null)
                {
                    wanted.Add(muted[index]);
                }
            }
        }

        wanted.RemoveWhere(IsReserved);
        return wanted;
    }

    /// <summary>The asset's buses plus Master and Editor, a bus being a return when a send of the asset or of the live mixer targets it.</summary>
    private List<AudioMixerSoloNode> BuildSoloNodes()
    {
        var returns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < Asset.Buses.Count; index++)
        {
            var bus = Asset.Buses[index];
            for (int sendIndex = 0; bus != null && sendIndex < bus.Sends.Count; sendIndex++)
            {
                if (!string.IsNullOrWhiteSpace(bus.Sends[sendIndex]?.Target))
                {
                    returns.Add(bus.Sends[sendIndex].Target);
                }
            }
        }

        var live = _applier?.Service.Mixer;
        if (live != null)
        {
            for (int index = 0; index < live.Buses.Count; index++)
            {
                var sends = live.Buses[index].Sends;
                for (int sendIndex = 0; sendIndex < sends.Count; sendIndex++)
                {
                    returns.Add(sends[sendIndex].Target.Name);
                }
            }
        }

        var nodes = new List<AudioMixerSoloNode>
        {
            new(AudioBusNames.Master, null, returns.Contains(AudioBusNames.Master), false),
            new(AudioBusNames.Editor, AudioBusNames.Master, returns.Contains(AudioBusNames.Editor), true),
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { AudioBusNames.Master, AudioBusNames.Editor };
        for (int index = 0; index < Asset.Buses.Count; index++)
        {
            var bus = Asset.Buses[index];
            if (bus == null || string.IsNullOrWhiteSpace(bus.Name) || !seen.Add(bus.Name))
            {
                continue;
            }

            string parent = string.IsNullOrWhiteSpace(bus.Parent) ? AudioBusNames.Master : bus.Parent;
            nodes.Add(new AudioMixerSoloNode(bus.Name, parent, returns.Contains(bus.Name), false));
        }

        return nodes;
    }

    /// <summary>Writes the wanted mutes to the live buses (only when they change) and gives back the ones no longer wanted.</summary>
    private void ApplyMutes()
    {
        if (_applier == null)
        {
            return;
        }

        var mixer = _applier.Service.Mixer;
        var wanted = ComputeWantedMutes();

        foreach (string name in wanted)
        {
            if (mixer.TryGetBus(name, out var bus) && !bus.IsMuted)
            {
                bus.IsMuted = true;
                _ownedMutes.Add(bus.Name);
            }
        }

        if (_ownedMutes.Count == 0)
        {
            return;
        }

        var released = new List<string>();
        foreach (string name in _ownedMutes)
        {
            if (!wanted.Contains(name))
            {
                released.Add(name);
            }
        }

        for (int index = 0; index < released.Count; index++)
        {
            if (mixer.TryGetBus(released[index], out var bus))
            {
                bus.IsMuted = false;
            }

            _ownedMutes.Remove(released[index]);
        }
    }

    private void RestoreOwnedMutes()
    {
        if (_applier != null)
        {
            var mixer = _applier.Service.Mixer;
            foreach (string name in _ownedMutes)
            {
                if (mixer.TryGetBus(name, out var bus))
                {
                    bus.IsMuted = false;
                }
            }
        }

        _ownedMutes.Clear();
    }
}
