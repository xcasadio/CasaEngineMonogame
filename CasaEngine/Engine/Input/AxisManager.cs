namespace CasaEngine.Engine.Input;

public class AxisManager
{
    private readonly List<Axis> _axes = new();

    public void AddAxis(Axis axis)
    {
        _axes.Add(axis);
    }

    public void RemoveAxis(Axis axis)
    {
        _axes.Remove(axis);
    }

    public float Value(string axisName)
    {
        float maxValue = 0;
        var foundAxis = false;
        foreach (var axis in _axes)
        {
            if (axis.Name == axisName && Math.Abs(axis.Value) >= Math.Abs(maxValue))
            {
                foundAxis = true;
                maxValue = axis.Value;
            }
        }
        if (!foundAxis)
        {
            throw new InvalidOperationException("Input: the axis named " + axisName + " does not exist.");
        }

        return maxValue;
    }

    public float ValueRaw(string axisName)
    {
        float maxValue = 0;
        var foundAxis = false;
        foreach (var axis in _axes)
        {
            if (axis.Name == axisName && Math.Abs(axis.ValueRaw) >= Math.Abs(maxValue))
            {
                foundAxis = true;
                maxValue = axis.ValueRaw;
            }
        }
        if (!foundAxis)
        {
            throw new InvalidOperationException("Input: the axis named " + axisName + " does not exist.");
        }

        return maxValue;
    }

    public void Update(KeyboardManager keyboardManager, MouseManager mouseManager, GamePadManager gamePadManager, float elapsedTime)
    {
        foreach (var axis in _axes)
        {
            axis.Update(keyboardManager, mouseManager, gamePadManager.GetGamePad(axis.GamePadNumber), elapsedTime);
        }
    }
}