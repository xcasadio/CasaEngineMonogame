using System.ComponentModel;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Spatial;
using Microsoft.Xna.Framework;
using NumericsVector3 = System.Numerics.Vector3;

namespace CasaEngine.Framework.Scene.Entities.Components;

/// <summary>
/// The listening point of the audio: it pushes its world pose to the <see cref="AudioService"/> (decisions D8, P35).
/// </summary>
/// <remarks>
/// With several listeners the last registered one is the active one. The listener is removed when the component is
/// detached or when its entity (or an ancestor entity) is disabled, and registered again when it is enabled.
/// Without any listener the spatial sounds play neutral. The pose is read as described in <see cref="AudioScenePose"/>.
/// The component asks for a tick every frame (see <see cref="ApplyEntityPolicyDefaults"/>): the pose is pushed from
/// <see cref="Update"/>, and only when it changed (the Doppler velocity is derived from the poses pushed).
/// </remarks>
[DisplayName("Audio Listener")]
public class AudioListenerComponent : SceneComponent, IEntityPolicyDefaultsProvider
{
    private AudioService _audioService;
    private AudioService _boundService;
    private bool _isRegistered;
    private NumericsVector3 _lastPosition;
    private NumericsVector3 _lastForward;
    private NumericsVector3 _lastUp;

    public AudioListenerComponent()
    {
    }

    /// <summary>Additive constructor for callers assigning a deterministic id (see <see cref="ObjectBase(Guid)"/>).</summary>
    public AudioListenerComponent(Guid id) : base(id)
    {
    }

    public AudioListenerComponent(AudioListenerComponent other) : base(other)
    {
    }

    /// <summary>True while this component is registered as a listener of the audio service.</summary>
    public bool IsRegistered => _isRegistered;

    public override AudioListenerComponent Clone() => new(this);

    public override void InitializeWithWorld(World.World world)
    {
        base.InitializeWithWorld(world);

        _audioService = world?.Game?.AudioSystemComponent?.Service ?? _boundService;
        Synchronize();
    }

    public override void OnEnabledValueChange()
    {
        base.OnEnabledValueChange();
        Synchronize();
    }

    public override void Update(float elapsedTime)
    {
        // The children and the world matrix first, then the pose of this frame.
        base.Update(elapsedTime);
        Synchronize();
    }

    public override void Detach()
    {
        _audioService?.RemoveListener(this);
        _isRegistered = false;
        base.Detach();
    }

    public override BoundingBox GetBoundingBox() => AudioScenePose.GetBoundingBox(this);

    /// <summary>Like an entity holding a render projection, an entity holding a listener ticks every frame.</summary>
    public void ApplyEntityPolicyDefaults(Entity owner, ref EntityPolicyDefaultsBuilder defaults)
    {
        defaults.Apply(EntityPolicySet.DynamicDefault);
    }

    /// <summary>Test seam: the service used when the world has none, registering at once when the component is attached and enabled.</summary>
    internal void BindServiceForTests(AudioService service)
    {
        _boundService = service;
        _audioService = service;
        Synchronize();
    }

    /// <summary>
    /// Registers or removes the listener according to the enabled state, and pushes the pose when it is new or changed.
    /// Entity.IsEnabled does not gate Entity.Update, so the state is checked here too.
    /// </summary>
    private void Synchronize()
    {
        if (_audioService == null)
        {
            return;
        }

        if (!IsActive())
        {
            if (_isRegistered)
            {
                _audioService.RemoveListener(this);
                _isRegistered = false;
            }

            return;
        }

        AudioScenePose.GetWorldPose(this, out var position, out var forward, out var up);

        if (_isRegistered && position == _lastPosition && forward == _lastForward && up == _lastUp)
        {
            return;
        }

        _lastPosition = position;
        _lastForward = forward;
        _lastUp = up;
        _isRegistered = true;
        _audioService.SetListener(this, AudioListenerPose.Create(position, forward, up));
    }

    /// <summary>Attached to an entity that is enabled, as every ancestor entity (disabling a parent entity does not change its children).</summary>
    private bool IsActive()
    {
        if (Owner == null)
        {
            return false;
        }

        for (var entity = Owner; entity != null; entity = entity.Parent)
        {
            if (!entity.IsEnabled)
            {
                return false;
            }
        }

        return true;
    }
}
