using System;
using System.IO;
using FontStashSharp;
using MGUI.FontStashSharp;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CasaEngine.Tests.UI;

/// <summary>
/// Covers the generic BMFont registration path added to <see cref="FontStashSharpTextEngine"/>
/// for E12.a (① dialogue choice UI, ② bitmap font path). No Alundra-specific knowledge is
/// exercised here: <c>font3</c> itself is exported by the converter and consumed by the DLL
/// slice; the engine only needs to register and resolve *some* fixed-size font by name —
/// <see cref="FontStashSharpTextEngine.AddStaticFont"/> takes a plain <see cref="SpriteFontBase"/>
/// and has no idea what content is behind it.
/// <para/>
/// <b>Why this test does not build a real <c>FontStashSharp.StaticSpriteFont</c> via
/// <c>StaticSpriteFont.FromBMFont</c>, nor a hand-rolled fake:</b> two constructibility walls,
/// both confirmed by reflecting FontStashSharp.MonoGame 1.5.6 rather than assumed:
/// <list type="bullet">
/// <item><description><c>StaticSpriteFont.FromBMFont</c>'s own texture-loading-injectable overload
/// — <c>FromBMFont(string, Func&lt;string, TextureWithOffset&gt;)</c> — still requires
/// <see cref="TextureWithOffset"/> to wrap a <b>non-null</b> <see cref="Texture2D"/> (its
/// constructor throws <see cref="ArgumentNullException"/> otherwise), and <see cref="Texture2D"/>
/// cannot be constructed without a live <see cref="GraphicsDevice"/>.</description></item>
/// <item><description>A hand-written <see cref="SpriteFontBase"/> subclass cannot be built either:
/// <c>PreDraw</c> and <c>GetKerning</c> are <c>internal abstract</c> (reflection:
/// <c>IsAssembly == true</c>, no <c>InternalsVisibleTo</c> naming this project), so this project
/// cannot provide the bodies C# requires to subclass <see cref="SpriteFontBase"/> at all.</description></item>
/// </list>
/// The one way to hold a real, concrete <see cref="SpriteFontBase"/> headless is the TTF path
/// this codebase already exercises elsewhere headless (see
/// <c>MGUI.Tests.Text.FSSMeasureDrawConsistencyTests</c>): <see cref="FontSystem.GetFont(float)"/>
/// rasterizes purely on the CPU (StbTrueType) and needs no <see cref="GraphicsDevice"/>. Using it
/// here does not test BMFont parsing (that is FontStashSharp's own concern, exercised by its own
/// test suite) — it tests exactly the seam these tests are about: that
/// <see cref="FontStashSharpTextEngine.AddStaticFont"/> registers a caller-supplied
/// <see cref="SpriteFontBase"/> by name, and <see cref="FontStashSharpTextEngine.ResolveFont"/>
/// returns that same fixed-size instance regardless of the requested <see cref="FontSpec.Size"/>
/// — never re-rasterizing it — which is precisely the behavior a bitmap font needs.
/// </summary>
public sealed class FontStashSharpBitmapFontRegistrationTests
{
    private static SpriteFontBase BuildFixedSizeFont(int pixelSize)
    {
        string ttfPath = Path.Combine(AppContext.BaseDirectory, "Content", "Fonts", "ttf", "arial.ttf");
        var fontSystem = new FontSystem();
        fontSystem.AddFont(File.ReadAllBytes(ttfPath));
        return fontSystem.GetFont(pixelSize);
    }

    [Fact]
    public void AddStaticFont_ThenResolveFont_ResolvesTheRegisteredFontByFamilyNameRegardlessOfRequestedSize()
    {
        var engine = new FontStashSharpTextEngine();
        SpriteFontBase registeredFont = BuildFixedSizeFont(pixelSize: 40);

        engine.AddStaticFont("font3", CustomFontStyles.Normal, registeredFont);

        // Request a *different* size than the font was built at: a static/bitmap font must not
        // be re-rasterized per request — it's the whole point of registering it as "static".
        ResolvedFont resolved = engine.ResolveFont(new FontSpec("font3", 16, CustomFontStyles.Normal));

        Assert.False(resolved.IsFallback);
        Assert.Equal(registeredFont.LineHeight, resolved.LineHeight);
        Assert.Equal(registeredFont.LineHeight, engine.GetLineHeight(resolved));
    }

    [Fact]
    public void ResolveFont_WithoutRegistration_FallsBackAndDoesNotMatchTheStaticFont()
    {
        var engine = new FontStashSharpTextEngine();
        SpriteFontBase neverRegistered = BuildFixedSizeFont(pixelSize: 40);

        // No AddStaticFont call: "font3" is never wired up. This is the mutation this test
        // guards against — skip/forget the registration and this assertion fails.
        ResolvedFont resolved = engine.ResolveFont(new FontSpec("font3", 16, CustomFontStyles.Normal));

        Assert.True(resolved.IsFallback);
        Assert.NotEqual(neverRegistered.LineHeight, resolved.LineHeight);
    }

    [Fact]
    public void ResolveFont_FallsBackToTheStaticFontsNormalStyleWhenTheRequestedStyleWasNotRegistered()
    {
        var engine = new FontStashSharpTextEngine();
        SpriteFontBase registeredFont = BuildFixedSizeFont(pixelSize: 40);
        engine.AddStaticFont("font3", CustomFontStyles.Normal, registeredFont);

        // Only Normal was registered; Bold falls back to it, mirroring AddFontSystem's own
        // "try exact match, then Normal for the same family" precedent.
        ResolvedFont resolved = engine.ResolveFont(new FontSpec("font3", 16, CustomFontStyles.Bold));

        Assert.False(resolved.IsFallback);
        Assert.Equal(registeredFont.LineHeight, resolved.LineHeight);
    }
}
