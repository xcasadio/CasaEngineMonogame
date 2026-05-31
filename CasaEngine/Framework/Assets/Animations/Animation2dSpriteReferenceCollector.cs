namespace CasaEngine.Framework.Assets.Animations;

public static class Animation2dSpriteReferenceCollector
{
    public static void Collect(Animation2dData animationData, List<Guid> spriteIds)
    {
        ArgumentNullException.ThrowIfNull(animationData);
        ArgumentNullException.ThrowIfNull(spriteIds);

        foreach (var part in animationData.Parts)
        {
            AddUnique(spriteIds, part.DefaultSpriteId);
        }

        foreach (var track in animationData.Tracks)
        {
            if (track.Property != Animation2dTrackProperty.Sprite)
            {
                continue;
            }

            foreach (var keyframe in track.SpriteKeyframes)
            {
                AddUnique(spriteIds, keyframe.Value);
            }
        }
    }

    private static void AddUnique(List<Guid> spriteIds, Guid spriteId)
    {
        if (spriteId == Guid.Empty)
        {
            return;
        }

        for (var index = 0; index < spriteIds.Count; index++)
        {
            if (spriteIds[index] == spriteId)
            {
                return;
            }
        }

        spriteIds.Add(spriteId);
    }
}