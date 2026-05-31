using CasaEngine.Framework.Assets.Animations;
using CasaEngine.EditorServices;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Animation;

public class Animation2dAuthoringDataTests
{
    [Fact]
    public void Load_LegacyFrames_PreservesFramesAndLeavesPartsEmpty()
    {
        var firstSpriteId = Guid.NewGuid();
        var secondSpriteId = Guid.NewGuid();
        var animation = new Animation2dData();

        animation.Load(new JObject
        {
            ["animation_type"] = AnimationType.Loop.ToString(),
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "legacy_walk",
            ["frames"] = new JArray
            {
                new JObject
                {
                    ["duration"] = 0.1f,
                    ["sprite_id"] = firstSpriteId.ToString(),
                },
                new JObject
                {
                    ["duration"] = 0.2f,
                    ["sprite_id"] = secondSpriteId.ToString(),
                },
            },
        });

        Assert.Equal(AnimationType.Loop, animation.AnimationType);
        Assert.Equal("legacy_walk", animation.Name);
        Assert.Equal(2, animation.Frames.Count);
        Assert.Equal(0.1f, animation.Frames[0].Duration);
        Assert.Equal(firstSpriteId, animation.Frames[0].SpriteId);
        Assert.Equal(0.2f, animation.Frames[1].Duration);
        Assert.Equal(secondSpriteId, animation.Frames[1].SpriteId);
        Assert.Empty(animation.Parts);
        Assert.Empty(animation.Events);
    }

    [Fact]
    public void Parts_CanDescribeComposedAnimationDefaults()
    {
        var bodySpriteId = Guid.NewGuid();
        var weaponSpriteId = Guid.NewGuid();
        var animation = new Animation2dData();

        animation.Parts.Add(new Animation2dPartData
        {
            Id = "body",
            Name = "Body",
            DefaultSpriteId = bodySpriteId,
            DefaultPosition = new Vector2(1f, 2f),
            DefaultDrawOrder = 10,
            DefaultVisible = true,
        });
        animation.Parts.Add(new Animation2dPartData
        {
            Id = "weapon",
            Name = "Weapon",
            DefaultSpriteId = weaponSpriteId,
            DefaultPosition = new Vector2(3f, -4f),
            DefaultDrawOrder = 20,
            DefaultVisible = false,
            DefaultFlipX = true,
            DefaultFlipY = true,
        });

        Assert.Equal(2, animation.Parts.Count);
        Assert.Equal("body", animation.Parts[0].Id);
        Assert.Equal(bodySpriteId, animation.Parts[0].DefaultSpriteId);
        Assert.Equal(new Vector2(1f, 2f), animation.Parts[0].DefaultPosition);
        Assert.Equal(10, animation.Parts[0].DefaultDrawOrder);
        Assert.True(animation.Parts[0].DefaultVisible);
        Assert.False(animation.Parts[0].DefaultFlipX);
        Assert.False(animation.Parts[0].DefaultFlipY);

        Assert.Equal("weapon", animation.Parts[1].Id);
        Assert.Equal(weaponSpriteId, animation.Parts[1].DefaultSpriteId);
        Assert.Equal(new Vector2(3f, -4f), animation.Parts[1].DefaultPosition);
        Assert.Equal(20, animation.Parts[1].DefaultDrawOrder);
        Assert.False(animation.Parts[1].DefaultVisible);
        Assert.True(animation.Parts[1].DefaultFlipX);
        Assert.True(animation.Parts[1].DefaultFlipY);
    }

    [Fact]
    public void Tracks_CanDescribeSpriteAndPositionChanges()
    {
        var bodySpriteId = Guid.NewGuid();
        var weaponSpriteId = Guid.NewGuid();
        var animation = new Animation2dData();
        animation.Parts.Add(new Animation2dPartData { Id = "body" });
        animation.Parts.Add(new Animation2dPartData { Id = "weapon" });

        var bodySpriteTrack = new Animation2dTrackData
        {
            TargetPartId = "body",
            Property = Animation2dTrackProperty.Sprite,
        };
        bodySpriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0f, bodySpriteId));
        bodySpriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0.25f, weaponSpriteId));

        var weaponPositionTrack = new Animation2dTrackData
        {
            TargetPartId = "weapon",
            Property = Animation2dTrackProperty.Position,
        };
        weaponPositionTrack.PositionKeyframes.Add(new Animation2dVector2KeyframeData(0f, Vector2.Zero));
        weaponPositionTrack.PositionKeyframes.Add(new Animation2dVector2KeyframeData(0.5f, new Vector2(8f, -3f)));

        animation.Tracks.Add(bodySpriteTrack);
        animation.Tracks.Add(weaponPositionTrack);

        Assert.Empty(animation.GetInvalidTrackTargetPartIds());
        Assert.Equal(Animation2dInterpolationMode.Step, bodySpriteTrack.Interpolation);
        Assert.Equal(2, bodySpriteTrack.SpriteKeyframes.Count);
        Assert.Equal(bodySpriteId, bodySpriteTrack.SpriteKeyframes[0].Value);
        Assert.Equal(0.25f, bodySpriteTrack.SpriteKeyframes[1].TimeSeconds);
        Assert.Equal(2, weaponPositionTrack.PositionKeyframes.Count);
        Assert.Equal(new Vector2(8f, -3f), weaponPositionTrack.PositionKeyframes[1].Value);
    }

    [Fact]
    public void GetInvalidTrackTargetPartIds_ReturnsMissingPartReferences()
    {
        var animation = new Animation2dData();
        animation.Parts.Add(new Animation2dPartData { Id = "body" });
        animation.Tracks.Add(new Animation2dTrackData
        {
            TargetPartId = "body",
            Property = Animation2dTrackProperty.Visible,
        });
        animation.Tracks.Add(new Animation2dTrackData
        {
            TargetPartId = "weapon",
            Property = Animation2dTrackProperty.DrawOrder,
        });
        animation.Tracks.Add(new Animation2dTrackData
        {
            TargetPartId = "weapon",
            Property = Animation2dTrackProperty.FlipX,
        });

        var invalidPartIds = animation.GetInvalidTrackTargetPartIds();

        Assert.Single(invalidPartIds);
        Assert.Equal("weapon", invalidPartIds[0]);
    }

    [Fact]
    public void Load_ReadsTimeBasedAnimationEvents()
    {
        var animation = new Animation2dData();

        animation.Load(new JObject
        {
            ["animation_type"] = AnimationType.Once.ToString(),
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "attack",
            ["frames"] = new JArray
            {
                new JObject
                {
                    ["duration"] = 0.1f,
                    ["sprite_id"] = Guid.NewGuid().ToString(),
                },
            },
            ["events"] = new JArray
            {
                new JObject
                {
                    ["time_seconds"] = 0.05f,
                    ["event_name"] = "HitStart",
                },
                new JObject
                {
                    ["time_seconds"] = 0.1f,
                    ["event_name"] = "HitEnd",
                },
            },
        });

        Assert.Equal(2, animation.Events.Count);
        Assert.Equal(new AnimationEventAsset(0.05f, "HitStart"), animation.Events[0]);
        Assert.Equal(new AnimationEventAsset(0.1f, "HitEnd"), animation.Events[1]);
    }

    [Fact]
    public void Load_ReadsComposedPartsAndTracks()
    {
        var bodySpriteId = Guid.NewGuid();
        var alternateSpriteId = Guid.NewGuid();
        var animation = new Animation2dData();

        animation.Load(new JObject
        {
            ["animation_type"] = AnimationType.Once.ToString(),
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "composed_attack",
            ["parts"] = new JArray
            {
                new JObject
                {
                    ["id"] = "body",
                    ["name"] = "Body",
                    ["default_sprite_id"] = bodySpriteId.ToString(),
                    ["default_position"] = new JObject
                    {
                        ["x"] = 1f,
                        ["y"] = -2f,
                    },
                    ["default_draw_order"] = 10,
                    ["default_visible"] = true,
                    ["default_flip_x"] = false,
                    ["default_flip_y"] = true,
                },
            },
            ["tracks"] = new JArray
            {
                new JObject
                {
                    ["target_part_id"] = "body",
                    ["property"] = Animation2dTrackProperty.Sprite.ToString(),
                    ["interpolation"] = Animation2dInterpolationMode.Step.ToString(),
                    ["sprite_keyframes"] = new JArray
                    {
                        new JObject
                        {
                            ["time_seconds"] = 0f,
                            ["value"] = bodySpriteId.ToString(),
                        },
                        new JObject
                        {
                            ["time_seconds"] = 0.25f,
                            ["value"] = alternateSpriteId.ToString(),
                        },
                    },
                },
                new JObject
                {
                    ["target_part_id"] = "body",
                    ["property"] = Animation2dTrackProperty.Position.ToString(),
                    ["position_keyframes"] = new JArray
                    {
                        new JObject
                        {
                            ["time_seconds"] = 0.5f,
                            ["value"] = new JObject
                            {
                                ["x"] = 6f,
                                ["y"] = 7f,
                            },
                        },
                    },
                },
            },
        });

        Assert.Empty(animation.Frames);
        Assert.Single(animation.Parts);
        Assert.Equal("body", animation.Parts[0].Id);
        Assert.Equal(bodySpriteId, animation.Parts[0].DefaultSpriteId);
        Assert.Equal(new Vector2(1f, -2f), animation.Parts[0].DefaultPosition);
        Assert.True(animation.Parts[0].DefaultFlipY);

        Assert.Equal(2, animation.Tracks.Count);
        Assert.Equal(Animation2dTrackProperty.Sprite, animation.Tracks[0].Property);
        Assert.Equal(alternateSpriteId, animation.Tracks[0].SpriteKeyframes[1].Value);
        Assert.Equal(Animation2dTrackProperty.Position, animation.Tracks[1].Property);
        Assert.Equal(new Vector2(6f, 7f), animation.Tracks[1].PositionKeyframes[0].Value);
    }

    [Fact]
    public void AnimationEventAssetJsonSerializer_RoundTripsEventData()
    {
        var animationEvent = new AnimationEventAsset(0.25f, "Footstep");

        var eventNode = AnimationEventAssetJsonSerializer.Save(animationEvent);
        var loadedEvent = AnimationEventAssetJsonSerializer.Load(eventNode);

        Assert.Equal(animationEvent, loadedEvent);
    }

    [Fact]
    public void EditorAssetJsonSerializer_SavesLegacyAnimationWithoutComposedFields()
    {
        var animation = new Animation2dData
        {
            AnimationType = AnimationType.Loop,
            Name = "legacy_idle",
        };
        animation.Frames.Add(new FrameData
        {
            Duration = 0.1f,
            SpriteId = Guid.NewGuid(),
        });

        Assert.True(EditorAssetJsonSerializer.TrySerialize(animation, out var document));

        Assert.NotNull(document["frames"]);
        Assert.Null(document["parts"]);
        Assert.Null(document["tracks"]);
        Assert.Null(document["events"]);
    }

    [Fact]
    public void EditorAssetJsonSerializer_RoundTripsComposedAnimationData()
    {
        var spriteId = Guid.NewGuid();
        var nextSpriteId = Guid.NewGuid();
        var animation = new Animation2dData
        {
            AnimationType = AnimationType.Once,
            Name = "composed_attack",
        };
        animation.Parts.Add(new Animation2dPartData
        {
            Id = "body",
            Name = "Body",
            DefaultSpriteId = spriteId,
            DefaultPosition = new Vector2(2f, 3f),
            DefaultDrawOrder = 10,
            DefaultVisible = true,
            DefaultFlipY = true,
        });
        var spriteTrack = new Animation2dTrackData
        {
            TargetPartId = "body",
            Property = Animation2dTrackProperty.Sprite,
        };
        spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0f, spriteId));
        spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0.5f, nextSpriteId));
        animation.Tracks.Add(spriteTrack);
        animation.Events.Add(new AnimationEventAsset(0.25f, "Hit"));

        Assert.True(EditorAssetJsonSerializer.TrySerialize(animation, out var document));

        var loadedAnimation = new Animation2dData();
        loadedAnimation.Load(document);

        Assert.Single(loadedAnimation.Parts);
        Assert.Equal("body", loadedAnimation.Parts[0].Id);
        Assert.Equal(spriteId, loadedAnimation.Parts[0].DefaultSpriteId);
        Assert.Equal(new Vector2(2f, 3f), loadedAnimation.Parts[0].DefaultPosition);
        Assert.True(loadedAnimation.Parts[0].DefaultFlipY);
        Assert.Single(loadedAnimation.Tracks);
        Assert.Equal(nextSpriteId, loadedAnimation.Tracks[0].SpriteKeyframes[1].Value);
        Assert.Single(loadedAnimation.Events);
        Assert.Equal(new AnimationEventAsset(0.25f, "Hit"), loadedAnimation.Events[0]);
    }

    [Fact]
    public void CompositionAdapter_ConvertsSingleLegacyFrameToOnePart()
    {
        var spriteId = Guid.NewGuid();
        var animation = new Animation2dData
        {
            AnimationType = AnimationType.Once,
        };
        animation.Frames.Add(new FrameData
        {
            Duration = 0.2f,
            SpriteId = spriteId,
        });

        var composition = Animation2dCompositionAdapter.Create(animation);

        Assert.Equal(AnimationType.Once, composition.AnimationType);
        Assert.Equal(0.2f, composition.DurationSeconds);
        Assert.Single(composition.Parts);
        Assert.Equal(Animation2dCompositionAdapter.LegacyPartId, composition.Parts[0].Id);
        Assert.Equal(spriteId, composition.Parts[0].DefaultSpriteId);
        Assert.Single(composition.Tracks);
        Assert.Equal(Animation2dTrackProperty.Sprite, composition.Tracks[0].Property);
        Assert.Single(composition.Tracks[0].SpriteKeyframes);
        Assert.Equal(0f, composition.Tracks[0].SpriteKeyframes[0].TimeSeconds);
        Assert.Equal(spriteId, composition.Tracks[0].SpriteKeyframes[0].Value);
    }

    [Fact]
    public void CompositionAdapter_PreservesLegacyFrameSequenceAndLoopType()
    {
        var firstSpriteId = Guid.NewGuid();
        var secondSpriteId = Guid.NewGuid();
        var thirdSpriteId = Guid.NewGuid();
        var animation = new Animation2dData
        {
            AnimationType = AnimationType.Loop,
        };
        animation.Frames.Add(new FrameData { Duration = 0.1f, SpriteId = firstSpriteId });
        animation.Frames.Add(new FrameData { Duration = 0.2f, SpriteId = secondSpriteId });
        animation.Frames.Add(new FrameData { Duration = 0.3f, SpriteId = thirdSpriteId });

        var composition = Animation2dCompositionAdapter.Create(animation);
        var spriteKeyframes = composition.Tracks[0].SpriteKeyframes;

        Assert.Equal(AnimationType.Loop, composition.AnimationType);
        Assert.Equal(0.6f, composition.DurationSeconds, 5);
        Assert.Equal(3, spriteKeyframes.Count);
        Assert.Equal(0f, spriteKeyframes[0].TimeSeconds);
        Assert.Equal(firstSpriteId, spriteKeyframes[0].Value);
        Assert.Equal(0.1f, spriteKeyframes[1].TimeSeconds);
        Assert.Equal(secondSpriteId, spriteKeyframes[1].Value);
        Assert.Equal(0.3f, spriteKeyframes[2].TimeSeconds, 5);
        Assert.Equal(thirdSpriteId, spriteKeyframes[2].Value);
    }

    [Fact]
    public void CompositionRuntimeState_ResetAppliesPartDefaults()
    {
        var bodySpriteId = Guid.NewGuid();
        var weaponSpriteId = Guid.NewGuid();
        var animation = new Animation2dData();
        animation.Parts.Add(new Animation2dPartData
        {
            Id = "body",
            DefaultSpriteId = bodySpriteId,
            DefaultPosition = new Vector2(1f, 2f),
            DefaultDrawOrder = 5,
            DefaultVisible = true,
        });
        animation.Parts.Add(new Animation2dPartData
        {
            Id = "weapon",
            DefaultSpriteId = weaponSpriteId,
            DefaultPosition = new Vector2(-3f, 4f),
            DefaultDrawOrder = 10,
            DefaultVisible = false,
            DefaultFlipX = true,
            DefaultFlipY = true,
        });
        var composition = Animation2dCompositionAdapter.Create(animation);
        var runtimeState = new Animation2dCompositionRuntimeState();

        runtimeState.Reset(composition);

        Assert.Equal(2, runtimeState.PartCount);
        Assert.True(runtimeState.TryGetPart("weapon", out var weaponState));
        Assert.Equal(1, weaponState.SourceIndex);
        Assert.Equal(weaponSpriteId, weaponState.SpriteId);
        Assert.Equal(new Vector2(-3f, 4f), weaponState.Position);
        Assert.Equal(10, weaponState.DrawOrder);
        Assert.False(weaponState.Visible);
        Assert.True(weaponState.FlipX);
        Assert.True(weaponState.FlipY);
    }

    [Fact]
    public void CompositionRuntimeState_ResetClearsPreviousParts()
    {
        var firstAnimation = new Animation2dData();
        firstAnimation.Parts.Add(new Animation2dPartData { Id = "body" });
        firstAnimation.Parts.Add(new Animation2dPartData { Id = "weapon" });
        var secondAnimation = new Animation2dData();
        secondAnimation.Parts.Add(new Animation2dPartData { Id = "body" });
        var runtimeState = new Animation2dCompositionRuntimeState();

        runtimeState.Reset(Animation2dCompositionAdapter.Create(firstAnimation));
        runtimeState.Reset(Animation2dCompositionAdapter.Create(secondAnimation));

        Assert.Single(runtimeState.Parts);
        Assert.True(runtimeState.TryGetPartIndex("body", out var bodyIndex));
        Assert.Equal(0, bodyIndex);
        Assert.False(runtimeState.TryGetPart("weapon", out _));
    }

    [Fact]
    public void CompositionSampler_AppliesStepTracksToRuntimeState()
    {
        var defaultSpriteId = Guid.NewGuid();
        var sampledSpriteId = Guid.NewGuid();
        var animation = new Animation2dData { AnimationType = AnimationType.Once };
        animation.Parts.Add(new Animation2dPartData
        {
            Id = "body",
            DefaultSpriteId = defaultSpriteId,
            DefaultPosition = Vector2.Zero,
            DefaultVisible = true,
        });
        var spriteTrack = new Animation2dTrackData { TargetPartId = "body", Property = Animation2dTrackProperty.Sprite };
        spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0.2f, sampledSpriteId));
        var positionTrack = new Animation2dTrackData { TargetPartId = "body", Property = Animation2dTrackProperty.Position };
        positionTrack.PositionKeyframes.Add(new Animation2dVector2KeyframeData(0.2f, new Vector2(4f, 5f)));
        var visibleTrack = new Animation2dTrackData { TargetPartId = "body", Property = Animation2dTrackProperty.Visible };
        visibleTrack.VisibleKeyframes.Add(new Animation2dBoolKeyframeData(0.2f, false));
        var drawOrderTrack = new Animation2dTrackData { TargetPartId = "body", Property = Animation2dTrackProperty.DrawOrder };
        drawOrderTrack.DrawOrderKeyframes.Add(new Animation2dIntKeyframeData(0.2f, 12));
        var flipXTrack = new Animation2dTrackData { TargetPartId = "body", Property = Animation2dTrackProperty.FlipX };
        flipXTrack.FlipKeyframes.Add(new Animation2dBoolKeyframeData(0.2f, true));
        var flipYTrack = new Animation2dTrackData { TargetPartId = "body", Property = Animation2dTrackProperty.FlipY };
        flipYTrack.FlipKeyframes.Add(new Animation2dBoolKeyframeData(0.2f, true));
        animation.Tracks.Add(spriteTrack);
        animation.Tracks.Add(positionTrack);
        animation.Tracks.Add(visibleTrack);
        animation.Tracks.Add(drawOrderTrack);
        animation.Tracks.Add(flipXTrack);
        animation.Tracks.Add(flipYTrack);
        var sampler = new Animation2dCompositionSampler(Animation2dCompositionAdapter.Create(animation));

        sampler.Seek(0.2f);
        Assert.True(sampler.RuntimeState.TryGetPart("body", out var bodyState));

        Assert.Equal(sampledSpriteId, bodyState.SpriteId);
        Assert.Equal(new Vector2(4f, 5f), bodyState.Position);
        Assert.False(bodyState.Visible);
        Assert.Equal(12, bodyState.DrawOrder);
        Assert.True(bodyState.FlipX);
        Assert.True(bodyState.FlipY);
    }

    [Fact]
    public void CompositionSampler_LoopsLegacyComposition()
    {
        var firstSpriteId = Guid.NewGuid();
        var secondSpriteId = Guid.NewGuid();
        var animation = new Animation2dData { AnimationType = AnimationType.Loop };
        animation.Frames.Add(new FrameData { Duration = 0.1f, SpriteId = firstSpriteId });
        animation.Frames.Add(new FrameData { Duration = 0.2f, SpriteId = secondSpriteId });
        var sampler = new Animation2dCompositionSampler(Animation2dCompositionAdapter.Create(animation));

        sampler.Update(0.15f);
        Assert.Equal(secondSpriteId, sampler.RuntimeState.Parts[0].SpriteId);

        sampler.Update(0.2f);
        Assert.Equal(firstSpriteId, sampler.RuntimeState.Parts[0].SpriteId);
    }

    [Fact]
    public void CompositionSampler_OnceClampsAtEnd()
    {
        var firstSpriteId = Guid.NewGuid();
        var secondSpriteId = Guid.NewGuid();
        var animation = new Animation2dData { AnimationType = AnimationType.Once };
        animation.Frames.Add(new FrameData { Duration = 0.1f, SpriteId = firstSpriteId });
        animation.Frames.Add(new FrameData { Duration = 0.2f, SpriteId = secondSpriteId });
        var sampler = new Animation2dCompositionSampler(Animation2dCompositionAdapter.Create(animation));

        var isFinished = sampler.Update(1f);

        Assert.True(isFinished);
        Assert.True(sampler.IsFinished);
        Assert.Equal(0.3f, sampler.CurrentTime, 5);
        Assert.Equal(secondSpriteId, sampler.RuntimeState.Parts[0].SpriteId);
    }

    [Fact]
    public void CompositionSampler_UpdateDispatchesAnimationEventsWhenCrossingKeyframes()
    {
        var animation = new Animation2dData { AnimationType = AnimationType.Once };
        animation.Parts.Add(new Animation2dPartData { Id = "body" });
        var track = new Animation2dTrackData { TargetPartId = "body", Property = Animation2dTrackProperty.Position };
        track.PositionKeyframes.Add(new Animation2dVector2KeyframeData(1f, Vector2.Zero));
        animation.Tracks.Add(track);
        animation.Events.Add(new AnimationEventAsset(0.25f, "Footstep"));
        var sampler = new Animation2dCompositionSampler(Animation2dCompositionAdapter.Create(animation));
        var triggeredEvents = new List<string>();
        sampler.AnimationEventTriggered += animationEvent => triggeredEvents.Add(animationEvent.EventName);

        sampler.Update(0.3f);

        Assert.Single(triggeredEvents);
        Assert.Equal("Footstep", triggeredEvents[0]);
    }

    [Fact]
    public void CompositionSampler_SeekDoesNotDispatchAnimationEvents()
    {
        var animation = new Animation2dData { AnimationType = AnimationType.Once };
        animation.Parts.Add(new Animation2dPartData { Id = "body" });
        var track = new Animation2dTrackData { TargetPartId = "body", Property = Animation2dTrackProperty.Position };
        track.PositionKeyframes.Add(new Animation2dVector2KeyframeData(1f, Vector2.Zero));
        animation.Tracks.Add(track);
        animation.Events.Add(new AnimationEventAsset(0.25f, "Footstep"));
        var sampler = new Animation2dCompositionSampler(Animation2dCompositionAdapter.Create(animation));
        var triggeredEvents = new List<string>();
        sampler.AnimationEventTriggered += animationEvent => triggeredEvents.Add(animationEvent.EventName);

        sampler.Seek(0.5f);

        Assert.Empty(triggeredEvents);
    }

    [Fact]
    public void CompositionSampler_UpdateDispatchesLoopedAnimationEventsAfterWrap()
    {
        var animation = new Animation2dData { AnimationType = AnimationType.Loop };
        animation.Parts.Add(new Animation2dPartData { Id = "body" });
        var track = new Animation2dTrackData { TargetPartId = "body", Property = Animation2dTrackProperty.Position };
        track.PositionKeyframes.Add(new Animation2dVector2KeyframeData(1f, Vector2.Zero));
        animation.Tracks.Add(track);
        animation.Events.Add(new AnimationEventAsset(0.1f, "LoopEvent"));
        animation.Events.Add(new AnimationEventAsset(0.9f, "EndEvent"));
        var sampler = new Animation2dCompositionSampler(Animation2dCompositionAdapter.Create(animation));
        var triggeredEvents = new List<string>();
        sampler.AnimationEventTriggered += animationEvent => triggeredEvents.Add(animationEvent.EventName);

        sampler.Update(0.95f);
        sampler.Update(0.2f);

        Assert.Equal(new[] { "LoopEvent", "EndEvent", "LoopEvent" }, triggeredEvents);
    }
}