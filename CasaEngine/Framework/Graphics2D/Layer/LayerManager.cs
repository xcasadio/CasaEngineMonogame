namespace CasaEngine.Framework.Graphics2D.Layer;

public class LayerManager
{
    private readonly List<Layer> _layerList = new();







    public void AddLayer(Layer l)
    {
        _layerList.Add(l);
    }

    public void AddLayer(int index, Layer l)
    {
        _layerList.Insert(index, l);
    }

    public void Update()
    {
        foreach (var l in _layerList.ToArray())
        {
            l.Update();
        }
    }

}