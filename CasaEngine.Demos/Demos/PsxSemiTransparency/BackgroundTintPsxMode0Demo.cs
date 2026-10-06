using CasaEngine.Framework.Assets.Sprites;
using Microsoft.Xna.Framework;

namespace CasaEngine.Demos.Demos.PsxSemiTransparency;

/// <summary>
/// E19.g G2d case 2: a tint of mode 0 (average), the mode of the 15 other tinted maps of the original and the former
/// behaviour of the engine. Tint (40, 40, 40) over the background (100, 150, 200) gives (70, 95, 120) at every probe point.
/// Run with <c>CASAENGINE_START_DEMO="Background tint PSX mode 0"</c> from the <c>CasaEngine.Demos</c> folder.
/// </summary>
public class BackgroundTintPsxMode0Demo : BackgroundTintPsxDemoBase
{
    public BackgroundTintPsxMode0Demo() : base("Background tint PSX mode 0")
    {
    }

    public override string Description =>
        "A full-view tint of PSX mode 0 over a plain background layer: the colour (40, 40, 40) is averaged with the scene " +
        "(alpha 128). The scene checks its own back-buffer against the PSX formula.";

    protected override Color TintColor => new(40, 40, 40, 255);

    protected override SpritePsxSemiTransparency TintMode => SpritePsxSemiTransparency.Mode0;

    protected override Color ExpectedColor => new(70, 95, 120);
}
