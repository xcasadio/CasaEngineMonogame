#if EDITOR

using System.Windows;

namespace CasaEngine.EditorUI.Inputs;

/// <summary>
/// Cache thread-safe des coordonnees-ecran du viewport.
/// Mis a jour sur le thread WPF (LayoutUpdated / SizeChanged),
/// consomme depuis n'importe quel thread (game loop) sans appel WPF.
/// </summary>
internal sealed class ViewportBoundsCache
{
    private double _left, _top, _right, _bottom;
    private readonly object _lock = new();

    /// <summary>
    /// Met a jour le cache. DOIT etre appele depuis le thread WPF dispatcher.
    /// </summary>
    public void Update(FrameworkElement viewport)
    {
        if (PresentationSource.FromVisual(viewport) == null) return;
        try
        {
            var tl = viewport.PointToScreen(new Point(0, 0));
            var br = viewport.PointToScreen(new Point(viewport.ActualWidth, viewport.ActualHeight));
            lock (_lock)
            {
                _left   = tl.X;
                _top    = tl.Y;
                _right  = br.X;
                _bottom = br.Y;
            }
        }
        catch { /* visual pas encore dans l'arbre */ }
    }

    /// <summary>
    /// Retourne true si le point en coordonnees-ecran est dans le viewport.
    /// Thread-safe — ne requiert pas le dispatcher WPF.
    /// </summary>
    public bool Contains(int screenX, int screenY)
    {
        lock (_lock)
        {
            return _right > _left   // cache initialise
                && screenX >= _left && screenX < _right
                && screenY >= _top  && screenY < _bottom;
        }
    }

    /// <summary>
    /// Convertit des coordonnees-ecran en coordonnees locales au viewport.
    /// Thread-safe — ne requiert pas le dispatcher WPF.
    /// </summary>
    public (int localX, int localY) ToLocal(int screenX, int screenY)
    {
        lock (_lock)
        {
            return ((int)(screenX - _left), (int)(screenY - _top));
        }
    }
}

#endif
