using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Gameplay.Actor;
using CasaEngine.Design;
using CasaEngineCommon.Design;

namespace CasaEngine.Graphics2D.Layer
{
    /// <summary>
    /// 
    /// </summary>
    public class Layer
    {

        private List<IRenderable> m_ObjectList = new List<IRenderable>();
        private int m_Min, m_Max;







        /// <summary>
        /// 
        /// </summary>
        /// <param name="r_"></param>
        public void AddObject(IRenderable r_)
        {
            m_ObjectList.Add(r_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="r_"></param>
        public void RemoveObject(IRenderable r_)
        {
            m_ObjectList.Remove(r_);
        }

        /// <summary>
        /// 
        /// </summary>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="min_"></param>
        /// <param name="max_"></param>
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
