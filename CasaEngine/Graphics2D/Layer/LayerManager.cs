namespace CasaEngine.Graphics2D.Layer
{
    public class LayerManager
    {
        readonly List<Layer> m_LayerList = new List<Layer>();







        public void AddLayer(Layer l_)
        {
            m_LayerList.Add(l_);
        }

        public void AddLayer(int index_, Layer l_)
        {
            m_LayerList.Insert(index_, l_);
        }

        public void Update()
        {
            foreach (Layer l in m_LayerList.ToArray())
            {
                l.Update();
            }
        }

    }
}
