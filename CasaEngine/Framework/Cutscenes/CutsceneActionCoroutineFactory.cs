using System.Collections;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Scene.CharacterMotion;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using CasaEngine.Framework.Input;
using CasaEngine.Framework.Rendering.ScreenEffects;
using CasaEngine.Framework.UI;
using CasaEngine.Framework.Scripting.Coroutines;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Cutscenes;

internal static class CutsceneActionCoroutineFactory
{
    /// <summary>The asset type the catalogue infers for a <c>.sprite</c> file.</summary>
    private const string SpriteAssetType = "sprite";

    public static IEnumerator Create(CutsceneActionData action, World world, CutsceneDirector owner, int playToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(owner);

        return ExecuteAction(action, world, owner, playToken);
    }

    private static IEnumerator ExecuteAction(CutsceneActionData action, World world, CutsceneDirector owner, int playToken)
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
                if (!TryStartMoveTo(moveToAction, world, owner, out ICharacterMotionHandle moveToHandle, out string moveToFailure))
                {
                    FailPlay(owner, moveToFailure, playToken);
                    yield break;
                }

                while (moveToHandle.IsActive)
                {
                    yield return null;
                }

                if (moveToHandle.HasTimedOut)
                {
                    FailPlay(owner, $"MoveTo action timed out for entity '{moveToAction.EntityName}'.", playToken);
                    yield break;
                }

                break;

            case NavigateToCutsceneActionData navigateToAction:
                if (!TryStartNavigateTo(navigateToAction, world, out NavigationAgentComponent navigationAgent, out string failureReason))
                {
                    owner.MarkRuntimeFailure(failureReason, playToken);
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
                        owner.MarkRuntimeFailure(navigationInstruction.StopReason, playToken);
                        yield break;
                    }
                }
                finally
                {
                    owner.EndNavigationAction(navigationAgent);
                }

                break;

            case PlaySoundCutsceneActionData playSoundAction:
                PlaySound(playSoundAction, world, GetAudioService(world, owner));
                break;

            case PlayMusicCutsceneActionData playMusicAction:
                PlayMusic(playMusicAction, world, owner);
                break;

            case StopMusicCutsceneActionData stopMusicAction:
                GetAudioService(world, owner)?.Music.StopAll(stopMusicAction.FadeOutSeconds);
                break;

            case FadeMusicCutsceneActionData fadeMusicAction:
                AudioService fadeMusicService = GetAudioService(world, owner);
                if (fadeMusicService != null)
                {
                    yield return new FadeMusicBusInstruction(
                        fadeMusicService.Mixer.GetBus(AudioBusNames.Music),
                        fadeMusicAction.TargetVolume,
                        fadeMusicAction.DurationSeconds);
                }

                break;

            case FadeScreenCutsceneActionData fadeScreenAction:
                ScreenEffectService fadeScreenService = GetScreenEffectService(world, owner);
                if (fadeScreenService != null)
                {
                    yield return new FadeScreenInstruction(fadeScreenService, fadeScreenAction);
                }

                break;

            case WaitFramesCutsceneActionData waitFramesAction:
                if (waitFramesAction.Frames is > 0)
                {
                    yield return new WaitForFrames(waitFramesAction.Frames.Value);
                }

                break;

            case FadeScreenFramesCutsceneActionData fadeFramesAction:
                ScreenEffectService fadeFramesService = GetScreenEffectService(world, owner);
                if (fadeFramesService != null)
                {
                    // Unlike FadeScreen, the layer is set and the ramp started during this very update, so the
                    // first value drawn already belongs to the ramp.
                    Color fadeFrom = fadeFramesAction.From.GetValueOrDefault();
                    Color fadeTo = fadeFramesAction.To.GetValueOrDefault();
                    int fadeFrames = fadeFramesAction.Frames.GetValueOrDefault();
                    fadeFramesService.Layer = fadeFramesAction.Layer.GetValueOrDefault();
                    fadeFramesService.StartFadeFrames(
                        fadeFrom.R, fadeFrom.G, fadeFrom.B, fadeFrom.A,
                        fadeTo.R, fadeTo.G, fadeTo.B, fadeTo.A,
                        fadeFrames,
                        fadeFramesAction.BlendMode.GetValueOrDefault());

                    if (fadeFrames > 0)
                    {
                        yield return new WaitForFrames(fadeFrames);
                    }
                }

                break;

            case OpenScreenCutsceneActionData openScreenAction:
                {
                    if (IsGameRequestRefused(owner, openScreenAction))
                    {
                        break;
                    }

                    GameManager openScreenManager = world.Game?.GameManager;
                    string openScreenFailure = openScreenManager == null
                        ? "OpenScreen needs a running game, and this world has none."
                        : openScreenManager.OpenScreenFromCutscene(openScreenAction.Target);
                    if (openScreenFailure != null)
                    {
                        FailPlay(owner, openScreenFailure, playToken);
                        yield break;
                    }

                    break;
                }

            case PlayVideoCutsceneActionData playVideoAction:
                {
                    if (IsGameRequestRefused(owner, playVideoAction))
                    {
                        break;
                    }

                    GameManager playVideoManager = world.Game?.GameManager;
                    if (playVideoManager == null)
                    {
                        FailPlay(owner, "PlayVideo needs a running game, and this world has none.", playToken);
                        yield break;
                    }

                    yield return PlayVideoScreen(
                        playVideoAction.Target,
                        screenName =>
                        {
                            string failure = playVideoManager.OpenVideoScreenFromCutscene(screenName, out IUIScreen screen, out IUIViewRuntime view);
                            return (failure, screen, view);
                        },
                        owner,
                        playToken);
                    break;
                }

            case ChangeWorldCutsceneActionData changeWorldAction:
                {
                    if (IsGameRequestRefused(owner, changeWorldAction))
                    {
                        break;
                    }

                    GameManager changeWorldManager = world.Game?.GameManager;
                    string changeWorldFailure = changeWorldManager == null
                        ? "ChangeWorld needs a running game, and this world has none."
                        : changeWorldManager.RequestWorldFromCutscene(changeWorldAction.Target);
                    if (changeWorldFailure != null)
                    {
                        FailPlay(owner, changeWorldFailure, playToken);
                        yield break;
                    }

                    break;
                }

            case ShowSpriteCutsceneActionData showSpriteAction:
                {
                    string showSpriteFailure = ShowSprite(showSpriteAction, world);
                    if (showSpriteFailure != null)
                    {
                        FailPlay(owner, showSpriteFailure, playToken);
                        yield break;
                    }

                    break;
                }

            case HideSpriteCutsceneActionData hideSpriteAction:
                {
                    if (!TryGetSpriteEntity(world, hideSpriteAction.EntityName, "HideSprite", out Entity hideEntity, out StaticSpriteComponent hideSprite, out string hideSpriteFailure))
                    {
                        FailPlay(owner, hideSpriteFailure, playToken);
                        yield break;
                    }

                    hideEntity.IsVisible = false;
                    hideSprite.StopMotionAndTint();
                    break;
                }

            case PlayVideoSpriteCutsceneActionData playVideoSpriteAction:
                {
                    if (!TryGetVideoSpriteEntity(world, playVideoSpriteAction.EntityName, "PlayVideoSprite", out Entity videoEntity, out VideoSpriteComponent videoSprite, out string playVideoSpriteFailure))
                    {
                        FailPlay(owner, playVideoSpriteFailure, playToken);
                        yield break;
                    }

                    // Shown and started in the same update; the last picture of the film, or the cleanup below, hides it.
                    videoEntity.IsVisible = true;
                    videoSprite.Play();
                    int playGeneration = videoSprite.PlayGeneration;
                    try
                    {
                        // A later play of the same film (another cutscene, or this one replaced) takes it over.
                        while (videoSprite.IsPlaying && videoSprite.PlayGeneration == playGeneration)
                        {
                            yield return null;
                        }
                    }
                    finally
                    {
                        // A cleared world has already released the component and dropped the entity, and a later play
                        // owns the film it started: in both cases there is nothing to undo here. A stopped coroutine
                        // runs this at the end of a later coroutine update, after a replacing play may have started.
                        if (videoSprite.PlayGeneration == playGeneration
                            && videoSprite.HasPlayer
                            && ReferenceEquals(FindEntityByName(world, videoEntity.Name), videoEntity))
                        {
                            videoSprite.Stop();
                            videoEntity.IsVisible = false;
                        }
                    }

                    break;
                }

            case StartVideoSpriteCutsceneActionData startVideoSpriteAction:
                {
                    if (!TryGetVideoSpriteEntity(world, startVideoSpriteAction.EntityName, "StartVideoSprite", out Entity startEntity, out VideoSpriteComponent startVideo, out string startVideoSpriteFailure))
                    {
                        FailPlay(owner, startVideoSpriteFailure, playToken);
                        yield break;
                    }

                    // ADR-0093: nothing of the cutscene stays with the film. The component hides the entity when the film
                    // ends; a stop, a replacement or a failure of the play leaves the film running.
                    startEntity.IsVisible = true;
                    startVideo.Play(hideWhenEnded: true);
                    break;
                }

            case MoveSpriteCutsceneActionData moveSpriteAction:
                {
                    if (!TryGetSpriteEntity(world, moveSpriteAction.EntityName, "MoveSprite", out _, out StaticSpriteComponent moveSprite, out string moveSpriteFailure))
                    {
                        FailPlay(owner, moveSpriteFailure, playToken);
                        yield break;
                    }

                    moveSprite.StartMove(new Vector2(moveSpriteAction.X.Value, moveSpriteAction.Y.Value), moveSpriteAction.Speed.Value);
                    break;
                }

            case TintSpriteCutsceneActionData tintSpriteAction:
                {
                    if (!TryGetSpriteEntity(world, tintSpriteAction.EntityName, "TintSprite", out _, out StaticSpriteComponent tintSprite, out string tintSpriteFailure))
                    {
                        FailPlay(owner, tintSpriteFailure, playToken);
                        yield break;
                    }

                    tintSprite.StartTint(tintSpriteAction.Color.Value, tintSpriteAction.Duration.Value);
                    break;
                }

            case ExitOnInputCutsceneActionData exitOnInputAction:
                {
                    if (IsGameRequestRefused(owner, exitOnInputAction))
                    {
                        break;
                    }

                    (CutsceneInputWatch watch, string exitOnInputFailure) = owner.ExitOnInputResolver != null
                        ? owner.ExitOnInputResolver(exitOnInputAction)
                        : ResolveExitOnInput(exitOnInputAction, world, owner);
                    if (watch == null)
                    {
                        FailPlay(owner, exitOnInputFailure, playToken);
                        yield break;
                    }

                    owner.ArmInputWatch(watch, playToken);
                    break;
                }

            case SequenceCutsceneActionData sequenceAction:
                for (int index = 0; index < sequenceAction.Actions.Count; index++)
                {
                    yield return ExecuteAction(sequenceAction.Actions[index], world, owner, playToken);
                    if (owner.IsPlayAborted(playToken))
                    {
                        yield break;
                    }
                }

                break;

            case RepeatCutsceneActionData repeatAction:
                {
                    int repeatCount = repeatAction.Count.Value;
                    bool endless = repeatCount == 0;
                    if (!endless && repeatAction.Actions.Count == 0)
                    {
                        break;
                    }

                    for (int lap = 0; endless || lap < repeatCount; lap++)
                    {
                        bool framePassed = false;
                        for (int index = 0; index < repeatAction.Actions.Count; index++)
                        {
                            // The child is driven here, to see whether it yields to the coroutine manager: a nested
                            // enumerator is followed, anything else it yields lets a frame pass.
                            var stack = new Stack<IEnumerator>();
                            stack.Push(ExecuteAction(repeatAction.Actions[index], world, owner, playToken));
                            try
                            {
                                while (stack.Count > 0)
                                {
                                    IEnumerator current = stack.Peek();
                                    if (!current.MoveNext())
                                    {
                                        (stack.Pop() as IDisposable)?.Dispose();
                                        continue;
                                    }

                                    if (current.Current is IEnumerator nested)
                                    {
                                        stack.Push(nested);
                                        continue;
                                    }

                                    framePassed = true;
                                    yield return current.Current;
                                }
                            }
                            finally
                            {
                                while (stack.Count > 0)
                                {
                                    (stack.Pop() as IDisposable)?.Dispose();
                                }
                            }

                            if (owner.IsPlayAborted(playToken))
                            {
                                yield break;
                            }
                        }

                        if (endless && !framePassed)
                        {
                            FailPlay(owner, "Repeat: a lap of an endless repeat ended without a frame passing; add a Wait or another action that takes time.", playToken);
                            yield break;
                        }
                    }

                    break;
                }

            case ParallelCutsceneActionData parallelAction:
                if (parallelAction.Actions.Count == 0)
                {
                    yield break;
                }

                var handles = new List<CoroutineHandle>(parallelAction.Actions.Count);
                for (int index = 0; index < parallelAction.Actions.Count; index++)
                {
                    CoroutineHandle handle = world.RuntimeSystems.CoroutineManager.StartCoroutine(
                        ExecuteAction(parallelAction.Actions[index], world, owner, playToken),
                        owner,
                        $"Cutscene.Parallel[{index}]");
                    handles.Add(handle);
                }

                for (int index = 0; index < handles.Count; index++)
                {
                    yield return handles[index];
                    if (owner.IsPlayAborted(playToken))
                    {
                        yield break;
                    }
                }

                break;

            default:
                throw new InvalidOperationException($"Unsupported cutscene action data type: {action.GetType().FullName}");
        }
    }

    /// <summary>
    /// The <c>PlayVideo</c> action: opens the screen, waits until none of its videos is playing, then removes it. The
    /// removal is in a <c>finally</c> so that a stopped cutscene takes its screen away too. When the world is cleared
    /// the views go with it and the removal finds nothing to do.
    /// </summary>
    /// <param name="open">Opens the screen: why it cannot, then the screen and the view it went on (both null when nothing opened).</param>
    internal static IEnumerator PlayVideoScreen(
        string target,
        Func<string, (string Failure, IUIScreen Screen, IUIViewRuntime View)> open,
        CutsceneDirector owner,
        int playToken)
    {
        var (failure, screen, view) = open(target);
        if (failure != null)
        {
            FailPlay(owner, failure, playToken);
            yield break;
        }

        // Under external view management nothing opened, so there is nothing to wait for.
        if (screen == null)
        {
            yield break;
        }

        try
        {
            if (screen is not DataDrivenScreen { VideoCount: > 0 } videoScreen)
            {
                FailPlay(owner, $"PlayVideo: screen '{target}' has no Video element.", playToken);
                yield break;
            }

            yield return new WaitForVideosInstruction(videoScreen);
        }
        finally
        {
            view.RemoveScreen(screen);
        }
    }

    private static AudioService GetAudioService(World world, CutsceneDirector owner)
        => owner?.EffectHost != null ? owner.EffectHost.Audio : world.Game?.AudioSystemComponent?.Service;

    /// <summary>True when the host of the play refuses a request to the running game: the action is then skipped.</summary>
    private static bool IsGameRequestRefused(CutsceneDirector owner, CutsceneActionData action)
        => owner.EffectHost != null && !owner.EffectHost.AllowGameRequest(action);

    /// <summary>Fails the play and logs the reason once: the action ends here, so this runs once per play.</summary>
    private static void FailPlay(CutsceneDirector owner, string reason, int playToken)
    {
        Logs.WriteError($"Cutscene action failed: {reason}");
        owner.MarkRuntimeFailure(reason, playToken);
    }

    private static ScreenEffectService GetScreenEffectService(World world, CutsceneDirector owner)
        => owner?.EffectHost != null ? owner.EffectHost.ScreenEffects : world.Game?.ScreenEffectComponent?.Service;

    /// <summary>
    /// Acquires the <see cref="SoundAsset"/> for the duration of the action alone (ADR-0037): the handle
    /// is given back before this method returns, once <paramref name="useAsset"/> has read from it
    /// everything <c>AudioService.PlaySound</c> and <c>MusicPlayer.Play</c> need - both only read the
    /// asset synchronously to start playback, they do not keep it.
    /// </summary>
    private static void WithSoundAsset(World world, Guid soundAssetId, Action<SoundAsset> useAsset)
    {
        if (soundAssetId == Guid.Empty || world.Game == null)
        {
            return;
        }

        try
        {
            using var soundAssetHandle = world.Game.AssetContentManager.Acquire<SoundAsset>(soundAssetId);
            useAsset(soundAssetHandle.Asset);
        }
        catch (Exception exception)
        {
            Logs.WriteException(new Exception($"Cutscene cannot load sound asset '{soundAssetId}'.", exception));
        }
    }

    /// <summary>Plays the sound on the service of the game, with no host involved (kept for the tests that reach it by reflection).</summary>
    private static void PlaySound(PlaySoundCutsceneActionData action, World world)
        => PlaySound(action, world, GetAudioService(world, null));

    private static void PlaySound(PlaySoundCutsceneActionData action, World world, AudioService audioService)
    {
        if (audioService == null)
        {
            return;
        }

        WithSoundAsset(world, action.SoundAssetId, asset =>
        {
            var overrides = new SoundPlaybackOverrides(
                volume: asset.Volume * action.Volume,
                busName: action.BusName);

            audioService.PlaySound(asset, overrides, world);
        });
    }

    private static void PlayMusic(PlayMusicCutsceneActionData action, World world, CutsceneDirector owner)
    {
        AudioService audioService = GetAudioService(world, owner);
        if (audioService == null)
        {
            return;
        }

        WithSoundAsset(world, action.SoundAssetId, asset =>
        {
            if (action.Crossfade)
            {
                // Fading the previous tracks out over the same duration is what makes it a crossfade.
                audioService.Music.StopAll(action.FadeInSeconds);
            }

            audioService.Music.Play(asset, action.FadeInSeconds, world);
        });
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

    private static bool TryStartMoveTo(MoveToCutsceneActionData action, World world, CutsceneDirector owner, out ICharacterMotionHandle handle, out string failureReason)
    {
        handle = null;
        failureReason = null;

        Entity entity = FindEntityByName(world, action.EntityName);
        if (entity == null)
        {
            failureReason = $"MoveTo action target entity '{action.EntityName}' was not found.";
            return false;
        }

        if (entity.GetComponent<CharacterControllerComponent>() == null)
        {
            failureReason = $"MoveTo action target entity '{action.EntityName}' has no CharacterControllerComponent.";
            return false;
        }

        handle = world.RuntimeSystems.CharacterMotion.MoveTo(
            entity,
            action.Destination,
            new CharacterMoveToOptions
            {
                StoppingDistance = action.StoppingDistance,
                TimeoutSeconds = action.TimeoutSeconds,
                ControlMode = CharacterControlMode.Cutscene,
            },
            owner);
        return true;
    }

    /// <summary>Finds the entity and its root sprite component, or says why it cannot.</summary>
    private static bool TryGetSpriteEntity(World world, string entityName, string actionName, out Entity entity, out StaticSpriteComponent sprite, out string failure)
    {
        sprite = null;
        entity = FindEntityByName(world, entityName);
        if (entity == null)
        {
            failure = $"{actionName}: entity '{entityName}' was not found in the world.";
            return false;
        }

        sprite = entity.RootComponent as StaticSpriteComponent;
        if (sprite == null)
        {
            failure = $"{actionName}: the root component of entity '{entityName}' is not a StaticSpriteComponent.";
            return false;
        }

        failure = null;
        return true;
    }

    /// <summary>Finds the entity and its root video component, or says why it cannot.</summary>
    private static bool TryGetVideoSpriteEntity(World world, string entityName, string actionName, out Entity entity, out VideoSpriteComponent video, out string failure)
    {
        video = null;
        entity = FindEntityByName(world, entityName);
        if (entity == null)
        {
            failure = $"{actionName}: entity '{entityName}' was not found in the world.";
            return false;
        }

        video = entity.RootComponent as VideoSpriteComponent;
        if (video == null)
        {
            failure = $"{actionName}: the root component of entity '{entityName}' is not a VideoSpriteComponent.";
            return false;
        }

        if (!video.HasPlayer)
        {
            failure = $"{actionName}: the video of entity '{entityName}' is not loaded (the component was not initialized in a world).";
            return false;
        }

        failure = null;
        return true;
    }

    /// <summary>Applies a ShowSprite action. Everything is checked before anything changes. Returns why it failed, or null.</summary>
    private static string ShowSprite(ShowSpriteCutsceneActionData action, World world)
    {
        if (!TryGetSpriteEntity(world, action.EntityName, "ShowSprite", out Entity entity, out StaticSpriteComponent sprite, out string failure))
        {
            return failure;
        }

        if (action.Sprite != null)
        {
            AssetInfo spriteInfo = AssetCatalog.Get(action.Sprite);
            if (spriteInfo == null)
            {
                return $"ShowSprite: sprite '{action.Sprite}' is not in the asset catalog.";
            }

            if (!string.Equals(spriteInfo.AssetType, SpriteAssetType, StringComparison.OrdinalIgnoreCase))
            {
                return $"ShowSprite: sprite '{action.Sprite}' is not a sprite asset (it is catalogued as '{spriteInfo.AssetType}').";
            }

            try
            {
                // A sprite that fails to load leaves the current sprite of the entity in place.
                sprite.TryLoadSpriteData(action.Sprite);
            }
            catch (Exception exception)
            {
                return $"ShowSprite: sprite '{action.Sprite}' cannot be loaded. {exception.Message}";
            }
        }

        if (action.X is { } x && action.Y is { } y)
        {
            sprite.Position = new Vector3(x, y, sprite.Position.Z);
        }

        if (action.Color is { } color)
        {
            sprite.Color = color;
        }

        if (action.BlendMode is { } blendMode)
        {
            sprite.BlendMode = blendMode;
        }

        sprite.StopMotionAndTint();
        entity.IsVisible = true;
        return null;
    }

    /// <summary>Reads the devices of the running game, the mapping and the next cutscene of an ExitOnInput action, or says why it cannot.</summary>
    private static (CutsceneInputWatch Watch, string Failure) ResolveExitOnInput(ExitOnInputCutsceneActionData action, World world, CutsceneDirector owner)
    {
        CasaEngineGame game = world.Game;
        if (game?.InputComponent == null)
        {
            return (null, "ExitOnInput needs a running game, and this world has none.");
        }

        ButtonsMapping mapping;
        try
        {
            mapping = game.AssetContentManager.LoadCopy<ButtonsMapping>(GameManager.FindButtonsMappingAsset(action.Mapping).Id);
        }
        catch (InvalidOperationException exception)
        {
            return (null, $"ExitOnInput: mapping '{action.Mapping}' cannot be used. {exception.Message}");
        }

        var inputs = new List<InputMapping>(action.Inputs.Count);
        for (int index = 0; index < action.Inputs.Count; index++)
        {
            InputMapping found = null;
            for (int buttonIndex = 0; buttonIndex < mapping.Buttons.Count && found == null; buttonIndex++)
            {
                if (string.Equals(mapping.Buttons[buttonIndex].Name, action.Inputs[index], StringComparison.Ordinal))
                {
                    found = mapping.Buttons[buttonIndex];
                }
            }

            if (found == null)
            {
                return (null, $"ExitOnInput: input '{action.Inputs[index]}' is not in mapping '{action.Mapping}'.");
            }

            inputs.Add(found);
        }

        CutsceneAsset next;
        try
        {
            // ADR-0037: a cutscene is read as a copy that the director's play owns.
            next = game.AssetContentManager.LoadCopy<CutsceneAsset>(GameManager.FindCutsceneAsset(action.NextCutscene).Id);
        }
        catch (InvalidOperationException exception)
        {
            return (null, $"ExitOnInput: next_cutscene '{action.NextCutscene}' cannot be used. {exception.Message}");
        }

        return (new CutsceneInputWatch(owner, inputs, game.InputComponent.KeyboardManager, game.InputComponent.MouseManager, game.InputComponent.GamePadManager, next), null);
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

    /// <summary>Completes once no video of the screen is playing: each has ended or been skipped.</summary>
    private sealed class WaitForVideosInstruction : ICoroutineInstruction
    {
        private readonly DataDrivenScreen _screen;

        public WaitForVideosInstruction(DataDrivenScreen screen) => _screen = screen;

        public bool IsCompleted(CoroutineUpdateContext context) => !_screen.IsAnyVideoPlaying;
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