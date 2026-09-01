using System.Collections;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Scene.CharacterMotion;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Rendering.ScreenEffects;
using CasaEngine.Framework.Scripting.Coroutines;

namespace CasaEngine.Framework.Cutscenes;

internal static class CutsceneActionCoroutineFactory
{
    public static IEnumerator Create(CutsceneActionData action, World world, CutsceneDirector owner)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(owner);

        return ExecuteAction(action, world, owner);
    }

    private static IEnumerator ExecuteAction(CutsceneActionData action, World world, CutsceneDirector owner)
    {
        switch (action)
        {
            case WaitCutsceneActionData waitAction:
                if (waitAction.Seconds > 0f)
                {
                    yield return new WaitForSeconds(waitAction.Seconds);
                }

                break;

            case MoveToCutsceneActionData moveToAction:
                ICharacterMotionHandle moveToHandle = StartMoveTo(moveToAction, world, owner);

                while (moveToHandle.IsActive)
                {
                    yield return null;
                }

                if (moveToHandle.HasTimedOut)
                {
                    throw new TimeoutException($"MoveTo action timed out for entity '{moveToAction.EntityName}'.");
                }

                break;

            case NavigateToCutsceneActionData navigateToAction:
                if (!TryStartNavigateTo(navigateToAction, world, out NavigationAgentComponent navigationAgent, out string failureReason))
                {
                    owner.MarkRuntimeFailure(failureReason);
                    yield break;
                }

                var navigationInstruction = new NavigateToCutsceneInstruction(navigationAgent, navigateToAction.TimeoutSeconds);
                owner.BeginNavigationAction(navigateToAction, navigationAgent);
                try
                {
                    yield return navigationInstruction;
                    owner.UpdateNavigationAction(navigationAgent, navigationInstruction.State, navigationInstruction.StopReason);
                    if (!navigationInstruction.ReachedDestination)
                    {
                        owner.MarkRuntimeFailure(navigationInstruction.StopReason);
                        yield break;
                    }
                }
                finally
                {
                    owner.EndNavigationAction(navigationAgent);
                }

                break;

            case PlaySoundCutsceneActionData playSoundAction:
                PlaySound(playSoundAction, world);
                break;

            case PlayMusicCutsceneActionData playMusicAction:
                PlayMusic(playMusicAction, world);
                break;

            case StopMusicCutsceneActionData stopMusicAction:
                GetAudioService(world)?.Music.StopAll(stopMusicAction.FadeOutSeconds);
                break;

            case FadeMusicCutsceneActionData fadeMusicAction:
                AudioService fadeMusicService = GetAudioService(world);
                if (fadeMusicService != null)
                {
                    yield return new FadeMusicBusInstruction(
                        fadeMusicService.Mixer.GetBus(AudioBusNames.Music),
                        fadeMusicAction.TargetVolume,
                        fadeMusicAction.DurationSeconds);
                }

                break;

            case FadeScreenCutsceneActionData fadeScreenAction:
                ScreenEffectService fadeScreenService = GetScreenEffectService(world);
                if (fadeScreenService != null)
                {
                    yield return new FadeScreenInstruction(fadeScreenService, fadeScreenAction);
                }

                break;

            case SequenceCutsceneActionData sequenceAction:
                for (int index = 0; index < sequenceAction.Actions.Count; index++)
                {
                    yield return ExecuteAction(sequenceAction.Actions[index], world, owner);
                    if (owner.HasRuntimeFailure)
                    {
                        yield break;
                    }
                }

                break;

            case ParallelCutsceneActionData parallelAction:
                if (parallelAction.Actions.Count == 0)
                {
                    yield break;
                }

                var handles = new List<CoroutineHandle>(parallelAction.Actions.Count);
                for (int index = 0; index < parallelAction.Actions.Count; index++)
                {
                    CoroutineHandle handle = world.RuntimeSystems.CoroutineManager.StartCoroutine(
                        ExecuteAction(parallelAction.Actions[index], world, owner),
                        owner,
                        $"Cutscene.Parallel[{index}]");
                    handles.Add(handle);
                }

                for (int index = 0; index < handles.Count; index++)
                {
                    yield return handles[index];
                    if (owner.HasRuntimeFailure)
                    {
                        yield break;
                    }
                }

                break;

            default:
                throw new InvalidOperationException($"Unsupported cutscene action data type: {action.GetType().FullName}");
        }
    }

    private static AudioService GetAudioService(World world) => world.Game?.AudioSystemComponent?.Service;

    private static ScreenEffectService GetScreenEffectService(World world) => world.Game?.ScreenEffectComponent?.Service;

    private static SoundAsset LoadSoundAsset(World world, Guid soundAssetId)
    {
        if (soundAssetId == Guid.Empty || world.Game == null)
        {
            return null;
        }

        try
        {
            return world.Game.AssetContentManager.Load<SoundAsset>(soundAssetId);
        }
        catch (Exception exception)
        {
            Logs.WriteException(new Exception($"Cutscene cannot load sound asset '{soundAssetId}'.", exception));
            return null;
        }
    }

    private static void PlaySound(PlaySoundCutsceneActionData action, World world)
    {
        AudioService audioService = GetAudioService(world);
        SoundAsset asset = LoadSoundAsset(world, action.SoundAssetId);

        if (audioService == null || asset == null)
        {
            return;
        }

        var overrides = new SoundPlaybackOverrides(
            volume: asset.Volume * action.Volume,
            busName: action.BusName);

        audioService.PlaySound(asset, overrides, world);
    }

    private static void PlayMusic(PlayMusicCutsceneActionData action, World world)
    {
        AudioService audioService = GetAudioService(world);
        SoundAsset asset = LoadSoundAsset(world, action.SoundAssetId);

        if (audioService == null || asset == null)
        {
            return;
        }

        if (action.Crossfade)
        {
            // Fading the previous tracks out over the same duration is what makes it a crossfade.
            audioService.Music.StopAll(action.FadeInSeconds);
        }

        audioService.Music.Play(asset, action.FadeInSeconds, world);
    }

    private static bool TryStartNavigateTo(NavigateToCutsceneActionData action, World world, out NavigationAgentComponent navigationAgent, out string failureReason)
    {
        navigationAgent = null;
        failureReason = string.Empty;

        Entity entity = FindEntityByName(world, action.EntityName);
        if (entity == null)
        {
            failureReason = $"NavigateTo action target entity '{action.EntityName}' was not found.";
            return false;
        }

        navigationAgent = entity.GetComponent<NavigationAgentComponent>();
        if (navigationAgent == null)
        {
            failureReason = $"NavigateTo action target entity '{action.EntityName}' has no NavigationAgentComponent.";
            return false;
        }

        if (navigationAgent.NavigationMap == null)
        {
            failureReason = $"NavigateTo action target entity '{action.EntityName}' has no NavigationMap.";
            return false;
        }

        if (entity.GetComponent<CharacterControllerNavigationDriverComponent>() == null)
        {
            failureReason = $"NavigateTo action target entity '{action.EntityName}' has no CharacterControllerNavigationDriverComponent.";
            return false;
        }

        if (entity.GetComponent<CharacterControllerComponent>() == null)
        {
            failureReason = $"NavigateTo action target entity '{action.EntityName}' has no CharacterControllerComponent.";
            return false;
        }

        navigationAgent.StoppingDistance = action.StoppingDistance;
        navigationAgent.MoveTo(action.Destination);
        if (!navigationAgent.RequestPath())
        {
            navigationAgent.Cancel();
            failureReason = $"NavigateTo action target entity '{action.EntityName}' could not find a navigation path.";
            return false;
        }

        return true;
    }

    private static ICharacterMotionHandle StartMoveTo(MoveToCutsceneActionData action, World world, CutsceneDirector owner)
    {
        Entity entity = FindEntityByName(world, action.EntityName)
            ?? throw new InvalidOperationException($"MoveTo action target entity '{action.EntityName}' was not found.");

        if (entity.GetComponent<CharacterControllerComponent>() == null)
        {
            throw new InvalidOperationException($"MoveTo action target entity '{action.EntityName}' has no CharacterControllerComponent.");
        }

        return world.RuntimeSystems.CharacterMotion.MoveTo(
            entity,
            action.Destination,
            new CharacterMoveToOptions
            {
                StoppingDistance = action.StoppingDistance,
                TimeoutSeconds = action.TimeoutSeconds,
                ControlMode = CharacterControlMode.Cutscene,
            },
            owner);
    }

    private static Entity FindEntityByName(World world, string entityName)
    {
        for (int index = 0; index < world.Entities.Count; index++)
        {
            Entity entity = world.Entities[index];
            if (string.Equals(entity.Name, entityName, StringComparison.Ordinal))
            {
                return entity;
            }
        }

        return null;
    }

    /// <summary>
    /// Ramps the Music bus volume with the real frame delta, and completes when it reaches the
    /// target. Blocking on purpose: the next cutscene action must start on the new level.
    /// </summary>
    private sealed class FadeMusicBusInstruction : ICoroutineInstruction
    {
        private readonly AudioBus _bus;
        private readonly float _startVolume;
        private readonly float _targetVolume;
        private readonly float _duration;
        private float _elapsed;

        public FadeMusicBusInstruction(AudioBus bus, float targetVolume, float durationSeconds)
        {
            _bus = bus;
            _startVolume = bus.Volume;
            _targetVolume = Math.Clamp(targetVolume, AudioVoiceParameters.MinVolume, AudioVoiceParameters.MaxVolume);
            _duration = durationSeconds;
        }

        public bool IsCompleted(CoroutineUpdateContext context)
        {
            if (_duration <= 0f)
            {
                _bus.Volume = _targetVolume;
                return true;
            }

            _elapsed += context.DeltaTime;
            float progress = Math.Clamp(_elapsed / _duration, 0f, 1f);
            _bus.Volume = _startVolume + ((_targetVolume - _startVolume) * progress);

            return progress >= 1f;
        }
    }

    /// <summary>
    /// Starts the service's ramp on its first tick, then waits for <see cref="_duration"/> to
    /// elapse. Blocking on purpose: the next cutscene action must start once the screen has
    /// reached the new colour. The ramp itself is advanced by <see cref="ScreenEffectService.Update"/>
    /// every frame (driven by <c>ScreenEffectComponent</c>), not by this instruction - it only
    /// decides when to stop yielding, exactly like <see cref="FadeMusicBusInstruction"/> decides from
    /// its own elapsed time.
    /// </summary>
    private sealed class FadeScreenInstruction : ICoroutineInstruction
    {
        private readonly ScreenEffectService _service;
        private readonly byte _fromR;
        private readonly byte _fromG;
        private readonly byte _fromB;
        private readonly FadeScreenCutsceneActionData _action;
        private readonly float _duration;
        private bool _started;
        private float _elapsed;

        public FadeScreenInstruction(ScreenEffectService service, FadeScreenCutsceneActionData action)
        {
            _service = service;
            _fromR = service.R;
            _fromG = service.G;
            _fromB = service.B;
            _action = action;
            _duration = Math.Max(0f, action.DurationSeconds);
        }

        public bool IsCompleted(CoroutineUpdateContext context)
        {
            if (!_started)
            {
                _started = true;
                _service.StartFade(_fromR, _fromG, _fromB, _action.R, _action.G, _action.B, _duration, _action.BlendMode);

                if (_duration <= 0f)
                {
                    return true;
                }
            }

            _elapsed += context.DeltaTime;
            return _elapsed >= _duration;
        }
    }

    private sealed class NavigateToCutsceneInstruction : ICoroutineInstruction
    {
        private const string ReachedDestinationReason = "ReachedDestination";
        private const string CancelledReason = "Cancelled";
        private const string TimeoutReason = "Timeout";

        private readonly NavigationAgentComponent _navigationAgent;
        private readonly float _timeoutSeconds;
        private float _elapsedSeconds;

        public NavigateToCutsceneInstruction(NavigationAgentComponent navigationAgent, float timeoutSeconds)
        {
            _navigationAgent = navigationAgent ?? throw new ArgumentNullException(nameof(navigationAgent));
            _timeoutSeconds = timeoutSeconds;
            State = "Moving";
            StopReason = string.Empty;
        }

        public bool ReachedDestination { get; private set; }

        public string State { get; private set; }

        public string StopReason { get; private set; }

        public bool IsCompleted(CoroutineUpdateContext context)
        {
            if (_navigationAgent.ReachedDestination)
            {
                ReachedDestination = true;
                State = "Completed";
                StopReason = ReachedDestinationReason;
                return true;
            }

            if (!_navigationAgent.HasDestination && !_navigationAgent.HasPath && !_navigationAgent.IsPathRequestPending)
            {
                State = "Cancelled";
                StopReason = CancelledReason;
                return true;
            }

            if (_timeoutSeconds > 0f)
            {
                _elapsedSeconds += Math.Max(0f, context.DeltaTime);
                if (_elapsedSeconds >= _timeoutSeconds)
                {
                    _navigationAgent.Cancel();
                    State = "TimedOut";
                    StopReason = TimeoutReason;
                    return true;
                }
            }

            State = _navigationAgent.IsPathRequestPending ? "PathPending" : "Moving";
            return false;
        }
    }
}