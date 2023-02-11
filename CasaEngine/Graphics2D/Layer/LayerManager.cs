namespace CasaEngine.Graphics2D.Layer
{
    public class LayerManager
    {
        readonly List<Layer> _layerList = new List<Layer>();







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
            foreach (Layer l in _layerList.ToArray())
            {
                l.Update();
            }
        }

    }
}
