using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.EditorServices;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Animation;

public class Animation2dAuthoringDataTests
{
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
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "attack",
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
        var spriteId = Guid.NewGuid();
        var animationEvent = new AnimationEventAsset(0.25f, Animation2dEventNames.ChangeSprite, spriteId);

        var eventNode = AnimationEventAssetJsonSerializer.Save(animationEvent);
        var loadedEvent = AnimationEventAssetJsonSerializer.Load(eventNode);

        Assert.Equal(animationEvent, loadedEvent);
    }

    [Fact]
    public void Load_LoopAnimationType_DoesNotAppendRestartEvent()
    {
        var spriteId = Guid.NewGuid();
        var animation = new Animation2dData();

        animation.Load(new JObject
        {
            ["animation_type"] = AnimationType.Loop.ToString(),
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "legacy_loop",
            ["parts"] = new JArray
            {
                new JObject
                {
                    ["id"] = "body",
                    ["default_sprite_id"] = spriteId.ToString(),
                    ["default_position"] = new JObject
                    {
                        ["x"] = 0f,
                        ["y"] = 0f,
                    },
                },
            },
            ["tracks"] = new JArray
            {
                new JObject
                {
                    ["target_part_id"] = "body",
                    ["property"] = Animation2dTrackProperty.Sprite.ToString(),
                    ["sprite_keyframes"] = new JArray
                    {
                        new JObject
                        {
                            ["time_seconds"] = 0f,
                            ["value"] = spriteId.ToString(),
                        },
                        new JObject
                        {
                            ["time_seconds"] = 0.5f,
                            ["value"] = spriteId.ToString(),
                        },
                    },
                },
            },
        });

        Assert.Equal(AnimationType.Loop, animation.AnimationType);
        Assert.Empty(animation.Events);
        Assert.Single(animation.Tracks);
        Assert.Equal("track 01", animation.Tracks[0].Name);
    }

    [Fact]
    public void EditorAssetJsonSerializer_RoundTripsComposedAnimationData()
    {
        var spriteId = Guid.NewGuid();
        var nextSpriteId = Guid.NewGuid();
        var animation = new Animation2dData
        {
            Name = "composed_attack",
            AnimationType = AnimationType.Once,
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
        Assert.Equal(AnimationType.Once.ToString(), document["animation_type"]?.Value<string>());

        var loadedAnimation = new Animation2dData();
        loadedAnimation.Load(document);

        Assert.Equal(AnimationType.Once, loadedAnimation.AnimationType);
        Assert.Single(loadedAnimation.Parts);
        Assert.Equal("body", loadedAnimation.Parts[0].Id);
        Assert.Equal(spriteId, loadedAnimation.Parts[0].DefaultSpriteId);
        Assert.Equal(new Vector2(2f, 3f), loadedAnimation.Parts[0].DefaultPosition);
        Assert.True(loadedAnimation.Parts[0].DefaultFlipY);
        Assert.Single(loadedAnimation.Tracks);
        Assert.Equal("track 01", loadedAnimation.Tracks[0].Name);
        Assert.Equal(nextSpriteId, loadedAnimation.Tracks[0].SpriteKeyframes[1].Value);
        Assert.Single(loadedAnimation.Events);
        Assert.Equal(new AnimationEventAsset(0.25f, "Hit"), loadedAnimation.Events[0]);
    }

    [Fact]
    public void EditorAssetJsonSerializer_CanonicalizesLegacyLoopAnimationData()
    {
        var spriteId = Guid.NewGuid();
        var animation = new Animation2dData();

        animation.Load(new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "legacy_loop",
            ["parts"] = new JArray
            {
                new JObject
                {
                    ["id"] = "sprite",
                    ["name"] = "Sprite",
                    ["default_sprite_id"] = spriteId.ToString(),
                    ["default_position"] = new JObject
                    {
                        ["x"] = 0f,
                        ["y"] = 0f,
                    },
                },
            },
            ["tracks"] = new JArray
            {
                new JObject
                {
                    ["target_part_id"] = "sprite",
                    ["property"] = Animation2dTrackProperty.Sprite.ToString(),
                    ["sprite_keyframes"] = new JArray
                    {
                        new JObject
                        {
                            ["time_seconds"] = 0f,
                            ["value"] = spriteId.ToString(),
                        },
                        new JObject
                        {
                            ["time_seconds"] = 0.5f,
                            ["value"] = spriteId.ToString(),
                        },
                    },
                },
            },
            ["events"] = new JArray
            {
                new JObject
                {
                    ["time_seconds"] = 0.5f,
                    ["event_name"] = Animation2dEventNames.Restart,
                },
            },
        });

        Assert.True(EditorAssetJsonSerializer.TrySerialize(animation, out var document));
        Assert.Equal(AnimationType.Loop.ToString(), document["animation_type"]?.Value<string>());
        Assert.Null(document["events"]);
    }

    [Fact]
    public void EditorAssetJsonSerializer_RoundTripsAllPropertyTrackTypes()
    {
        var bodySpriteId = Guid.NewGuid();
        var nextSpriteId = Guid.NewGuid();
        var animation = new Animation2dData
        {
            Name = "all_property_tracks",
        };
        animation.Parts.Add(new Animation2dPartData
        {
            Id = "body",
            Name = "Body",
            DefaultSpriteId = bodySpriteId,
            DefaultPosition = new Vector2(1f, 2f),
            DefaultDrawOrder = 4,
            DefaultVisible = true,
        });

        var spriteTrack = new Animation2dTrackData { TargetPartId = "body", Property = Animation2dTrackProperty.Sprite };
        spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0.1f, nextSpriteId));
        animation.Tracks.Add(spriteTrack);

        var positionTrack = new Animation2dTrackData { TargetPartId = "body", Property = Animation2dTrackProperty.Position };
        positionTrack.PositionKeyframes.Add(new Animation2dVector2KeyframeData(0.2f, new Vector2(3f, 4f)));
        animation.Tracks.Add(positionTrack);

        var visibleTrack = new Animation2dTrackData { TargetPartId = "body", Property = Animation2dTrackProperty.Visible };
        visibleTrack.VisibleKeyframes.Add(new Animation2dBoolKeyframeData(0.3f, false));
        animation.Tracks.Add(visibleTrack);

        var drawOrderTrack = new Animation2dTrackData { TargetPartId = "body", Property = Animation2dTrackProperty.DrawOrder };
        drawOrderTrack.DrawOrderKeyframes.Add(new Animation2dIntKeyframeData(0.4f, 12));
        animation.Tracks.Add(drawOrderTrack);

        var flipXTrack = new Animation2dTrackData { TargetPartId = "body", Property = Animation2dTrackProperty.FlipX };
        flipXTrack.FlipKeyframes.Add(new Animation2dBoolKeyframeData(0.5f, true));
        animation.Tracks.Add(flipXTrack);

        var flipYTrack = new Animation2dTrackData { TargetPartId = "body", Property = Animation2dTrackProperty.FlipY };
        flipYTrack.FlipKeyframes.Add(new Animation2dBoolKeyframeData(0.6f, true));
        animation.Tracks.Add(flipYTrack);

        var rotationTrack = new Animation2dTrackData { TargetPartId = "body", Property = Animation2dTrackProperty.Rotation };
        rotationTrack.RotationKeyframes.Add(new Animation2dFloatKeyframeData(0.7f, 1.25f));
        animation.Tracks.Add(rotationTrack);

        Assert.True(EditorAssetJsonSerializer.TrySerialize(animation, out var document));

        var loadedAnimation = new Animation2dData();
        loadedAnimation.Load(document);

        Assert.Equal(7, loadedAnimation.Tracks.Count);
        Assert.Equal(AnimationType.Once, loadedAnimation.AnimationType);
        Assert.Equal("track 01", loadedAnimation.Tracks[0].Name);
        Assert.Equal(Animation2dTrackProperty.Sprite, loadedAnimation.Tracks[0].Property);
        Assert.Equal(nextSpriteId, loadedAnimation.Tracks[0].SpriteKeyframes[0].Value);
        Assert.Equal(Animation2dTrackProperty.Position, loadedAnimation.Tracks[1].Property);
        Assert.Equal(new Vector2(3f, 4f), loadedAnimation.Tracks[1].PositionKeyframes[0].Value);
        Assert.Equal(Animation2dTrackProperty.Visible, loadedAnimation.Tracks[2].Property);
        Assert.False(loadedAnimation.Tracks[2].VisibleKeyframes[0].Value);
        Assert.Equal(Animation2dTrackProperty.DrawOrder, loadedAnimation.Tracks[3].Property);
        Assert.Equal(12, loadedAnimation.Tracks[3].DrawOrderKeyframes[0].Value);
        Assert.Equal(Animation2dTrackProperty.FlipX, loadedAnimation.Tracks[4].Property);
        Assert.True(loadedAnimation.Tracks[4].FlipKeyframes[0].Value);
        Assert.Equal(Animation2dTrackProperty.FlipY, loadedAnimation.Tracks[5].Property);
        Assert.True(loadedAnimation.Tracks[5].FlipKeyframes[0].Value);
        Assert.Equal(Animation2dTrackProperty.Rotation, loadedAnimation.Tracks[6].Property);
        Assert.Equal(1.25f, loadedAnimation.Tracks[6].RotationKeyframes[0].Value, 5);
    }

    [Fact]
    public void GetDurationSeconds_UsesAllPropertyTrackTypes()
    {
        var animation = new Animation2dData();
        animation.Parts.Add(new Animation2dPartData { Id = "body" });

        var visibleTrack = new Animation2dTrackData { TargetPartId = "body", Property = Animation2dTrackProperty.Visible };
        visibleTrack.VisibleKeyframes.Add(new Animation2dBoolKeyframeData(0.2f, false));
        animation.Tracks.Add(visibleTrack);

        var drawOrderTrack = new Animation2dTrackData { TargetPartId = "body", Property = Animation2dTrackProperty.DrawOrder };
        drawOrderTrack.DrawOrderKeyframes.Add(new Animation2dIntKeyframeData(0.4f, 10));
        animation.Tracks.Add(drawOrderTrack);

        var flipXTrack = new Animation2dTrackData { TargetPartId = "body", Property = Animation2dTrackProperty.FlipX };
        flipXTrack.FlipKeyframes.Add(new Animation2dBoolKeyframeData(0.6f, true));
        animation.Tracks.Add(flipXTrack);

        var flipYTrack = new Animation2dTrackData { TargetPartId = "body", Property = Animation2dTrackProperty.FlipY };
        flipYTrack.FlipKeyframes.Add(new Animation2dBoolKeyframeData(0.8f, true));
        animation.Tracks.Add(flipYTrack);

        var rotationTrack = new Animation2dTrackData { TargetPartId = "body", Property = Animation2dTrackProperty.Rotation };
        rotationTrack.RotationKeyframes.Add(new Animation2dFloatKeyframeData(1.0f, 0.5f));
        animation.Tracks.Add(rotationTrack);

        Assert.Equal(1.0f, animation.GetDurationSeconds(), 5);
    }

    [Fact]
    public void LegacyDemoAnim2d_CanBeSerializedToCanonicalComposedFormat()
    {
        var assetPath = Path.Combine(
            FindRepositoryRoot(),
            "CasaEngine.Demos",
            "Content",
            "TileSets",
            "swordman_walk_up.anim2d");
        var animation = new Animation2dData();

        animation.Load(JObject.Parse(File.ReadAllText(assetPath)));

        Assert.True(EditorAssetJsonSerializer.TrySerialize(animation, out var document));
        Assert.Equal(AnimationType.Loop.ToString(), document["animation_type"]?.Value<string>());
        Assert.NotNull(document["parts"]);
        Assert.NotNull(document["tracks"]);
        Assert.Null(document["events"]);
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
        var animation = new Animation2dData();
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
        var rotationTrack = new Animation2dTrackData { TargetPartId = "body", Property = Animation2dTrackProperty.Rotation };
        rotationTrack.RotationKeyframes.Add(new Animation2dFloatKeyframeData(0.2f, 0.75f));
        animation.Tracks.Add(spriteTrack);
        animation.Tracks.Add(positionTrack);
        animation.Tracks.Add(visibleTrack);
        animation.Tracks.Add(drawOrderTrack);
        animation.Tracks.Add(flipXTrack);
        animation.Tracks.Add(flipYTrack);
        animation.Tracks.Add(rotationTrack);
        var sampler = new Animation2dCompositionSampler(Animation2dCompositionAdapter.Create(animation));

        sampler.Seek(0.2f);
        Assert.True(sampler.RuntimeState.TryGetPart("body", out var bodyState));

        Assert.Equal(sampledSpriteId, bodyState.SpriteId);
        Assert.Equal(new Vector2(4f, 5f), bodyState.Position);
        Assert.False(bodyState.Visible);
        Assert.Equal(12, bodyState.DrawOrder);
        Assert.True(bodyState.FlipX);
        Assert.True(bodyState.FlipY);
        Assert.Equal(0.75f, bodyState.Rotation, 5);
    }

    [Fact]
    public void CompositionSampler_LoopsSingleSpriteComposition()
    {
        var firstSpriteId = Guid.NewGuid();
        var secondSpriteId = Guid.NewGuid();
        var animation = new Animation2dData();
        animation.AnimationType = AnimationType.Loop;
        animation.Parts.Add(new Animation2dPartData { Id = "sprite", DefaultSpriteId = firstSpriteId });
        var spriteTrack = new Animation2dTrackData { TargetPartId = "sprite", Property = Animation2dTrackProperty.Sprite };
        spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0f, firstSpriteId));
        spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0.1f, secondSpriteId));
        spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0.3f, secondSpriteId));
        animation.Tracks.Add(spriteTrack);
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
        var animation = new Animation2dData();
        animation.Parts.Add(new Animation2dPartData { Id = "sprite", DefaultSpriteId = firstSpriteId });
        var spriteTrack = new Animation2dTrackData { TargetPartId = "sprite", Property = Animation2dTrackProperty.Sprite };
        spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0f, firstSpriteId));
        spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0.1f, secondSpriteId));
        spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0.3f, secondSpriteId));
        animation.Tracks.Add(spriteTrack);
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
        var animation = new Animation2dData();
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
        var animation = new Animation2dData();
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
        var animation = new Animation2dData();
        animation.AnimationType = AnimationType.Loop;
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

    [Fact]
    public void SpriteReferenceCollector_CollectsComposedSpriteIdsOnce()
    {
        var defaultSpriteId = Guid.NewGuid();
        var trackedSpriteId = Guid.NewGuid();
        var animation = new Animation2dData();
        animation.Parts.Add(new Animation2dPartData { Id = "body", DefaultSpriteId = defaultSpriteId });
        var spriteTrack = new Animation2dTrackData { TargetPartId = "body", Property = Animation2dTrackProperty.Sprite };
        spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0f, defaultSpriteId));
        spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0.2f, trackedSpriteId));
        animation.Tracks.Add(spriteTrack);
        var spriteIds = new List<Guid>();

        Animation2dSpriteReferenceCollector.Collect(animation, spriteIds);

        Assert.Equal(2, spriteIds.Count);
        Assert.Equal(defaultSpriteId, spriteIds[0]);
        Assert.Equal(trackedSpriteId, spriteIds[1]);
    }

    [Fact]
    public void CompositionSampler_UpdatesStableDrawOrder()
    {
        var animation = new Animation2dData();
        animation.Parts.Add(new Animation2dPartData { Id = "body", DefaultDrawOrder = 10 });
        animation.Parts.Add(new Animation2dPartData { Id = "weapon", DefaultDrawOrder = 5 });
        animation.Parts.Add(new Animation2dPartData { Id = "fx", DefaultDrawOrder = 5 });
        var drawOrderTrack = new Animation2dTrackData
        {
            TargetPartId = "body",
            Property = Animation2dTrackProperty.DrawOrder,
        };
        drawOrderTrack.DrawOrderKeyframes.Add(new Animation2dIntKeyframeData(0.5f, 0));
        animation.Tracks.Add(drawOrderTrack);
        var sampler = new Animation2dCompositionSampler(Animation2dCompositionAdapter.Create(animation));

        Assert.Equal(new[] { 1, 2, 0 }, sampler.RuntimeState.DrawPartIndices);

        sampler.Seek(0.5f);

        Assert.Equal(new[] { 0, 1, 2 }, sampler.RuntimeState.DrawPartIndices);
    }

    [Fact]
    public void BoundsCalculator_CoversVisibleComposedParts()
    {
        var bodySpriteId = Guid.NewGuid();
        var weaponSpriteId = Guid.NewGuid();
        var hiddenSpriteId = Guid.NewGuid();
        var animation = new Animation2dData();
        animation.Parts.Add(new Animation2dPartData
        {
            Id = "body",
            DefaultSpriteId = bodySpriteId,
            DefaultPosition = Vector2.Zero,
        });
        animation.Parts.Add(new Animation2dPartData
        {
            Id = "weapon",
            DefaultSpriteId = weaponSpriteId,
            DefaultPosition = new Vector2(10f, 3f),
        });
        animation.Parts.Add(new Animation2dPartData
        {
            Id = "hidden",
            DefaultSpriteId = hiddenSpriteId,
            DefaultVisible = false,
            DefaultPosition = new Vector2(100f, 100f),
        });
        var sampler = new Animation2dCompositionSampler(Animation2dCompositionAdapter.Create(animation));
        var spriteDataById = new Dictionary<Guid, SpriteData>
        {
            [bodySpriteId] = new()
            {
                PositionInTexture = new Rectangle(0, 0, 10, 20),
                Origin = new Point(5, 10),
            },
            [weaponSpriteId] = new()
            {
                PositionInTexture = new Rectangle(0, 0, 4, 6),
                Origin = Point.Zero,
            },
            [hiddenSpriteId] = new()
            {
                PositionInTexture = new Rectangle(0, 0, 200, 200),
                Origin = Point.Zero,
            },
        };

        var hasBounds = Animation2dBoundsCalculator.TryCalculateLocalBounds(sampler.RuntimeState, spriteDataById, out var bounds);

        Assert.True(hasBounds);
        Assert.Equal(new Vector3(-5f, -10f, 0f), bounds.Min);
        Assert.Equal(new Vector3(14f, 10f, 0.1f), bounds.Max);
    }

    [Fact]
    public void ConvertedAnim2d_LoadsSingleSpritePartAndTrack()
    {
        var assetPath = Path.Combine(
            FindRepositoryRoot(),
            "Projects",
            "RPGDemo",
            "TileSets",
            "swordman_stand_right.anim2d");
        var animation = new Animation2dData();

        animation.Load(JObject.Parse(File.ReadAllText(assetPath)));
        var composition = Animation2dCompositionAdapter.Create(animation);

        Assert.Equal("swordman_stand_right", animation.Name);
        Assert.Equal(AnimationType.Loop, animation.AnimationType);
        Assert.Single(animation.Parts);
        Assert.Equal("sprite", animation.Parts[0].Id);
        Assert.Equal("Sprite", animation.Parts[0].Name);
        Assert.True(animation.Parts[0].DefaultVisible);
        Assert.Empty(animation.Events);
        Assert.Single(composition.Parts);
        Assert.Equal("sprite", composition.Parts[0].Id);
        Assert.Equal(animation.Parts[0].DefaultSpriteId, composition.Parts[0].DefaultSpriteId);
        Assert.Single(composition.Tracks);
        Assert.Equal("track 01", composition.Tracks[0].Name);
        Assert.Equal(3, composition.Tracks[0].SpriteKeyframes.Count);
        Assert.Equal(animation.Parts[0].DefaultSpriteId, composition.Tracks[0].SpriteKeyframes[0].Value);
        Assert.Equal(composition.Tracks[0].SpriteKeyframes[1].Value, composition.Tracks[0].SpriteKeyframes[2].Value);
        Assert.Equal(0.3f, composition.DurationSeconds, 5);
    }

    [Fact]
    public void SampleComposedAnim2d_LoadsPartsTracksAndEvents()
    {
        var assetPath = Path.Combine(
            FindRepositoryRoot(),
            "Projects",
            "RPGDemo",
            "TileSets",
            "swordman_composed_demo.anim2d");
        var animation = new Animation2dData();

        animation.Load(JObject.Parse(File.ReadAllText(assetPath)));
        var sampler = new Animation2dCompositionSampler(Animation2dCompositionAdapter.Create(animation));

        Assert.Equal("swordman_composed_demo", animation.Name);
        Assert.Equal(AnimationType.Loop, animation.AnimationType);
        Assert.Equal(2, animation.Parts.Count);
        Assert.Equal(2, animation.Tracks.Count);
        Assert.Equal("track 01", animation.Tracks[0].Name);
        Assert.Equal("track 02", animation.Tracks[1].Name);
        Assert.Single(animation.Events);
        Assert.Equal("WeaponSwapLayer", animation.Events[0].EventName);

        sampler.Seek(0.3f);

        Assert.True(sampler.RuntimeState.TryGetPart("weapon", out var weaponState));
        Assert.Equal(new Vector2(26f, -4f), weaponState.Position);
        Assert.Equal(-5, weaponState.DrawOrder);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CasaEngine.MonoGame.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("CasaEngine repository root was not found.");
    }
}