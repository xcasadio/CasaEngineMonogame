using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.AI.Navigation;

public sealed class NavigationPath
{
    private int _currentPointIndex;

    public List<Vector3> Points { get; } = [];

    public int CurrentPointIndex
    {
        get => _currentPointIndex;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _currentPointIndex = value;
        }
    }

    public bool IsFinished => CurrentPointIndex >= Points.Count;

    public void Clear()
    {
        Points.Clear();
        CurrentPointIndex = 0;
    }

    public void AddPoint(Vector3 point)
    {
        Points.Add(point);
    }
}