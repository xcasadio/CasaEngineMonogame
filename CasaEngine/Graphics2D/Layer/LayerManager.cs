using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.Graphics2D.Layer
{
    /// <summary>
    /// 
    /// </summary>
    public class LayerManager
    {
        #region Fields

        List<Layer> m_LayerList = new List<Layer>();

        #endregion

        #region Properties

        #endregion

        #region Constructors

        #endregion

        #region Methods

        /// <summary>
        /// Add layer to the front
        /// </summary>
        /// <param name="l_"></param>
        public void AddLayer(Layer l_)
        {
            m_LayerList.Add(l_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="l_"></param>
        public void AddLayer(int index_, Layer l_)
        {
            m_LayerList.Insert(index_, l_);
        }

        /// <summary>
        /// 
        /// </summary>
        public void Update()
        {
            foreach (Layer l in m_LayerList.ToArray())
            {
                l.Update();
            }
        }

        #endregion
    }
}
