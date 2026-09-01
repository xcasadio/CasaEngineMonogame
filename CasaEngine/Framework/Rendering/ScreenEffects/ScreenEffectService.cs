using CasaEngine.Framework.Rendering.Depth;

namespace CasaEngine.Framework.Rendering.ScreenEffects;

/// <summary>
/// Engine-side screen fade/tint logic: a single full-viewport colour overlay, with an optional
/// ramp toward a target colour.
/// </summary>
/// <remarks>
/// Deliberately free of any MonoGame type, including <c>Color</c> and <c>Game</c>: this is what
/// makes the ramp behaviour unit testable, exactly like <see cref="Audio.AudioService"/>.
/// <see cref="Application.Components.ScreenEffectComponent"/> is the thin component that drives it
/// from the game loop and submits the overlay quad.
/// This service is a plain mechanism: it knows nothing about the PSX fidelity rules (16.16 fixed
/// point, channel swap, truncating division, persistence lock, tick-vs-frame cadence...). Those
/// stay in the game DLL, which computes its own colour machines and pushes the result here every
/// frame via <see cref="SetOverlay"/> or the <see cref="StartFade"/> convenience.
/// </remarks>
public sealed class ScreenEffectService
{
    private bool _isFading;
    private byte _fadeFromR;
    private byte _fadeFromG;
    private byte _fadeFromB;
    private byte _fadeToR;
    private byte _fadeToG;
    private byte _fadeToB;
    private float _fadeDuration;
    private float _fadeElapsed;

    /// <summary>False when no overlay should be submitted this frame.</summary>
    public bool Active { get; private set; }

    public byte R { get; private set; }

    public byte G { get; private set; }

    public byte B { get; private set; }

    /// <summary>Blend mode the overlay quad is drawn with while <see cref="Active"/>.</summary>
    public SpriteBlendMode Blend { get; private set; }

    /// <summary>True while a ramp started by <see cref="StartFade"/> has not yet reached its target.</summary>
    public bool IsFading => _isFading;

    /// <summary>
    /// Sets the overlay colour and blend mode immediately, with no ramp. Cancels any ramp in
    /// progress.
    /// </summary>
    public void SetOverlay(byte r, byte g, byte b, SpriteBlendMode blend)
    {
        R = r;
        G = g;
        B = b;
        Blend = blend;
        Active = true;
        _isFading = false;
    }

    /// <summary>Deactivates the overlay: nothing is submitted until <see cref="SetOverlay"/> or
    /// <see cref="StartFade"/> is called again.</summary>
    public void Clear()
    {
        Active = false;
        _isFading = false;
    }

    /// <summary>
    /// Ramps the overlay colour from <paramref name="fromR"/>/<paramref name="fromG"/>/
    /// <paramref name="fromB"/> to <paramref name="toR"/>/<paramref name="toG"/>/<paramref name="toB"/>
    /// over <paramref name="durationSeconds"/>, advanced by <see cref="Update"/>. A duration of zero
    /// (or NaN) applies the target immediately - no ramp is started. Calling this again while a ramp
    /// is in progress restarts cleanly from whatever the caller passes as the new "from": this
    /// service holds no memory of the caller's own colour machine, it only ramps between the two
    /// values it is given (mirrors <see cref="Audio.AudioService.FadeVoice"/>'s ramp semantics: exact
    /// target on arrival, no overshoot on a long frame).
    /// </summary>
    public void StartFade(byte fromR, byte fromG, byte fromB, byte toR, byte toG, byte toB, float durationSeconds, SpriteBlendMode blend)
    {
        Blend = blend;
        Active = true;

        if (durationSeconds <= 0f || float.IsNaN(durationSeconds))
        {
            R = toR;
            G = toG;
            B = toB;
            _isFading = false;
            return;
        }

        R = fromR;
        G = fromG;
        B = fromB;

        _fadeFromR = fromR;
        _fadeFromG = fromG;
        _fadeFromB = fromB;
        _fadeToR = toR;
        _fadeToG = toG;
        _fadeToB = toB;
        _fadeDuration = durationSeconds;
        _fadeElapsed = 0f;
        _isFading = true;
    }

    /// <summary>Advances the ramp started by <see cref="StartFade"/>, if any. Allocation free.</summary>
    public void Update(float elapsedSeconds)
    {
        if (!_isFading)
        {
            return;
        }

        _fadeElapsed += elapsedSeconds;

        var progress = _fadeElapsed >= _fadeDuration
            ? 1f
            : _fadeElapsed / _fadeDuration;

        R = Lerp(_fadeFromR, _fadeToR, progress);
        G = Lerp(_fadeFromG, _fadeToG, progress);
        B = Lerp(_fadeFromB, _fadeToB, progress);

        if (progress >= 1f)
        {
            _isFading = false;
        }
    }

    private static byte Lerp(byte from, byte to, float progress)
    {
        var value = from + ((to - from) * progress);
        return (byte)Math.Clamp(MathF.Round(value), 0f, 255f);
    }
}
