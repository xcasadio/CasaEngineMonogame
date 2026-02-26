#if EDITOR

using System;
using System.Threading;
using System.Windows;

namespace CasaEngine.EditorUI.Inputs;

/// <summary>
/// Cache thread-safe des coordonnees-ecran du viewport.
/// Mis a jour sur le thread WPF (LayoutUpdated / SizeChanged),
/// consomme depuis n'importe quel thread (game loop) sans appel WPF.
/// Accumule aussi la molette de la souris depuis les events WPF MouseWheel.
/// </summary>
internal sealed class ViewportBoundsCache
{
    private double _left, _top, _right, _bottom;
    private readonly object _lock = new();

    // Molette : valeur absolue croissante (comme XNA ScrollWheelValue).
    // Ecrit depuis le thread WPF (MouseWheel), lu depuis le game thread.
    private int _scrollWheelValue;

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
    /// Ajoute un delta de molette (appele depuis WPF MouseWheel sur le thread UI).
    /// Le signe de <paramref name="delta"/> suit la convention WPF (+120 par cran vers le haut).
    /// </summary>
    public void AddScrollDelta(int delta)
    {
        Interlocked.Add(ref _scrollWheelValue, delta);
    }

    /// <summary>Valeur absolue accumulee de la molette (compatible XNA ScrollWheelValue).</summary>
    public int ScrollWheelValue => Volatile.Read(ref _scrollWheelValue);

    /// <summary>
    /// Retourne true si le point en coordonnees-ecran est dans le viewport.
    /// Thread-safe — ne requiert pas le dispatcher WPF.
    /// </summary>
    public bool Contains(int screenX, int screenY)
    {
        lock (_lock)
        {
            return _right > _left
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
