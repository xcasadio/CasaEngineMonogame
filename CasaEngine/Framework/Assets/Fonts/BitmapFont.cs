using FontStashSharp;

namespace CasaEngine.Framework.Assets.Fonts;

/// <summary>
/// A fixed-size bitmap font loaded from a BMFont <c>.fnt</c> asset (ADR-0036). It holds its page
/// textures through asset handles and gives them back when it is disposed, which the asset manager does
/// when it frees the font.
/// </summary>
public sealed class BitmapFont : IDisposable
{
    private readonly IReadOnlyList<IDisposable> _pageHandles;

    /// <param name="family">The font family, the <c>face</c> of the <c>.fnt</c> file.</param>
    /// <param name="font">The built font.</param>
    /// <param name="pageHandles">Holds on the page textures, given back on <see cref="Dispose"/>.</param>
    public BitmapFont(string family, SpriteFontBase font, IReadOnlyList<IDisposable> pageHandles = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(family);
        ArgumentNullException.ThrowIfNull(font);

        Family = family;
        Font = font;
        _pageHandles = pageHandles ?? Array.Empty<IDisposable>();
    }

    /// <summary>The font family, as UI text elements name it (e.g. <c>FontFamily="font3"</c> in XAML).</summary>
    public string Family { get; }

    /// <summary>The built font.</summary>
    public SpriteFontBase Font { get; }

    public bool IsDisposed { get; private set; }

    /// <summary>Raised once, when the font is disposed.</summary>
    public event EventHandler Disposed;

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;

        for (var i = 0; i < _pageHandles.Count; i++)
        {
            _pageHandles[i].Dispose();
        }

        Disposed?.Invoke(this, EventArgs.Empty);
    }
}
