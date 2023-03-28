using CasaEngine.Core.Design;

namespace CasaEngine.Framework.Graphics2D.Layer;

public class Layer
{

    private readonly List<IRenderable> _objectList = new();
    private int _min, _max;







    public void AddObject(IRenderable r)
    {
        _objectList.Add(r);
    }

    public void RemoveObject(IRenderable r)
    {
        _objectList.Remove(r);
    }

    public void Update()
    {
        int min, max;

        if (GetMinMax(out min, out max))
        {
            //_ObjectList.Sort();

            foreach (var d in _objectList.ToArray())
            {
                if (d.Visible)
                {
                    d.ZOrder = (d.Depth - min) / (float)max;
                }
            }
        }
    }

    private bool GetMinMax(out int min, out int max)
    {
        min = int.MaxValue;
        max = int.MinValue;

        foreach (var d in _objectList.ToArray())
        {
            if (d.Visible)
            {
                if (min > d.Depth)
                {
                    min = d.Depth;
                }

                if (max < d.Depth)
                {
                    max = d.Depth;
                }
            }
        }

        return min < max;
    }

}