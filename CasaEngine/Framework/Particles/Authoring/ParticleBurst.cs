namespace CasaEngine.Framework.Particles.Authoring;

/// <summary>
/// One-shot emission burst in normalized emitter playback time.
/// </summary>
public sealed class ParticleBurst
{
    private int _countMin = 1;
    private int _countMax = 1;

    public float Time { get; set; }

    public int CountMin
    {
        get => _countMin;
        set
        {
            _countMin = value;
            if (_countMax < _countMin)
            {
                _countMax = _countMin;
            }
        }
    }

    public int CountMax
    {
        get => _countMax;
        set
        {
            _countMax = value;
            if (_countMin > _countMax)
            {
                _countMin = _countMax;
            }
        }
    }
}