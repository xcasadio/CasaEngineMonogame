namespace CasaEngine.Framework.Assets;

/// <summary>
/// While a scope is open on the current thread, a script type name that does not resolve is logged as a warning
/// and the object is created without its script, instead of throwing <see cref="ScriptTypeResolutionException"/>.
/// The world opens it when its execution policy says so (<c>ThrowOnScriptTypeResolution</c> false: the editor
/// preview), so a project whose gameplay assembly is not built yet, or whose data names a wrong script, still
/// opens and the inspector can show and fix the script (ADR-0075, D9 and D11). No allocation: a counter and a
/// reference on the thread.
/// </summary>
internal static class ScriptTypeResolutionTolerance
{
    [ThreadStatic]
    private static int _depth;

    [ThreadStatic]
    private static string _fallbackFileName;

    public static bool IsActive => _depth > 0;

    /// <summary>The file given when the scope was opened (the world's), used by a warning that knows no file of its own.</summary>
    public static string FallbackFileName => _fallbackFileName;

    /// <summary>
    /// Opens a scope; dispose it (a <c>using</c>) to close it, also after an exception. <c>default(Scope)</c> is a
    /// closed scope whose disposal does nothing, so a caller can write <c>using var scope = condition ? Open(file) : default;</c>.
    /// </summary>
    public static Scope Open(string fallbackFileName)
    {
        var scope = new Scope(_fallbackFileName);
        _depth++;
        _fallbackFileName = fallbackFileName;
        return scope;
    }

    internal readonly struct Scope : IDisposable
    {
        private readonly string _previousFallbackFileName;
        private readonly bool _isOpen;

        internal Scope(string previousFallbackFileName)
        {
            _previousFallbackFileName = previousFallbackFileName;
            _isOpen = true;
        }

        public void Dispose()
        {
            if (!_isOpen)
            {
                return;
            }

            _depth--;
            _fallbackFileName = _previousFallbackFileName;
        }
    }
}
