using CasaEngine.Framework.Assets.Sprites;
using Microsoft.Xna.Framework;

namespace CasaEngine.Demos.Demos.PsxSemiTransparency;

/// <summary>
/// E19.g G2d case 1: a tint of mode 1 (additive), the mode of the burning Inoa map of the original. Tint (50, 0, 0) over the
/// background (100, 150, 200) gives (150, 150, 200) at every probe point. Run with
/// <c>CASAENGINE_START_DEMO="Background tint PSX mode 1"</c> from the <c>CasaEngine.Demos</c> folder.
/// </summary>
public class BackgroundTintPsxMode1Demo : BackgroundTintPsxDemoBase
{
    public BackgroundTintPsxMode1Demo() : base("Background tint PSX mode 1")
    {
    }

    public override string Description =>
        "A full-view tint of PSX mode 1 over a plain background layer: the colour (50, 0, 0) is added to the scene " +
        "(additive), where the engine used to average it. The scene checks its own back-buffer against the PSX formula.";

    protected override Color TintColor => new(50, 0, 0, 255);

    protected override SpritePsxSemiTransparency TintMode => SpritePsxSemiTransparency.Mode1;

    protected override Color ExpectedColor => new(150, 150, 200);
}
