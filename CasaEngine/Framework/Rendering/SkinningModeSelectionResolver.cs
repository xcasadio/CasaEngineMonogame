namespace CasaEngine.Framework.Rendering;

public static class SkinningModeSelectionResolver
{
    public static SkinningMode ResolveRequested(SkinningModeSelection selection, SkinningMode riggedModelMode)
    {
        return selection switch
        {
            SkinningModeSelection.RiggedModelDefault => riggedModelMode,
            SkinningModeSelection.LinearBlend => SkinningMode.LinearBlend,
            SkinningModeSelection.DualQuaternion => SkinningMode.DualQuaternion,
            _ => throw new ArgumentOutOfRangeException(nameof(selection), selection, null),
        };
    }

    public static SkinningMode ResolveEffective(
        SkinningModeSelection selection,
        SkinningMode riggedModelMode,
        bool canUseDualQuaternionSkinning)
    {
        var requestedMode = ResolveRequested(selection, riggedModelMode);

        return requestedMode switch
        {
            SkinningMode.LinearBlend => SkinningMode.LinearBlend,
            SkinningMode.DualQuaternion => canUseDualQuaternionSkinning
                ? SkinningMode.DualQuaternion
                : SkinningMode.LinearBlend,
            _ => throw new ArgumentOutOfRangeException(nameof(requestedMode), requestedMode, null),
        };
    }
}