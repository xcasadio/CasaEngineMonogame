using CasaEngineCommon.Design;

namespace CasaEngine.Graphics2D.Layer
{
    public class Layer
    {

        private readonly List<IRenderable> m_ObjectList = new List<IRenderable>();
        private int m_Min, m_Max;







        public void AddObject(IRenderable r_)
        {
            m_ObjectList.Add(r_);
        }

        public void RemoveObject(IRenderable r_)
        {
            m_ObjectList.Remove(r_);
        }

        public void Update()
        {
            int min, max;

            if (GetMinMax(out min, out max) == true)
            {
                //m_ObjectList.Sort();

                foreach (IRenderable d in m_ObjectList.ToArray())
                {
                    if (d.Visible == true)
                    {
                        d.ZOrder = (float)(d.Depth - min) / (float)max;
                    }
                }
            }
        }

        private bool GetMinMax(out int min_, out int max_)
        {
            min_ = int.MaxValue;
            max_ = int.MinValue;

            foreach (IRenderable d in m_ObjectList.ToArray())
            {
                if (d.Visible == true)
                {
                    if (min_ > d.Depth)
                    {
                        min_ = d.Depth;
                    }

                    if (max_ < d.Depth)
                    {
                        max_ = d.Depth;
                    }
                }
            }

            return min_ < max_;
        }

    }
}
