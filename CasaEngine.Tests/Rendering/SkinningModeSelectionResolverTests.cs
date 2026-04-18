using CasaEngine.Framework.Rendering;
using Xunit;

namespace CasaEngine.Tests.Rendering;

public class SkinningModeSelectionResolverTests
{
    [Fact]
    public void ResolveRequested_InheritsRiggedModelDefaultMode()
    {
        var resolved = SkinningModeSelectionResolver.ResolveRequested(
            SkinningModeSelection.RiggedModelDefault,
            SkinningMode.DualQuaternion);

        Assert.Equal(SkinningMode.DualQuaternion, resolved);
    }

    [Fact]
    public void ResolveRequested_UsesExplicitLinearBlendOverride()
    {
        var resolved = SkinningModeSelectionResolver.ResolveRequested(
            SkinningModeSelection.LinearBlend,
            SkinningMode.DualQuaternion);

        Assert.Equal(SkinningMode.LinearBlend, resolved);
    }

    [Fact]
    public void ResolveEffective_FallsBackToLinearBlend_WhenDualQuaternionCannotBeUsed()
    {
        var resolved = SkinningModeSelectionResolver.ResolveEffective(
            SkinningModeSelection.DualQuaternion,
            SkinningMode.LinearBlend,
            canUseDualQuaternionSkinning: false);

        Assert.Equal(SkinningMode.LinearBlend, resolved);
    }

    [Fact]
    public void ResolveEffective_PreservesDualQuaternion_WhenSupported()
    {
        var resolved = SkinningModeSelectionResolver.ResolveEffective(
            SkinningModeSelection.RiggedModelDefault,
            SkinningMode.DualQuaternion,
            canUseDualQuaternionSkinning: true);

        Assert.Equal(SkinningMode.DualQuaternion, resolved);
    }
}