using CasaEngine.Framework.Assets.Animations;
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
}