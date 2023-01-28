

using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace CasaEngine.Design.Event
{
    /// <summary>
    /// 
    /// </summary>
    public class TriggerManager
    {

        List<Trigger> m_Triggers = new List<Trigger>();



        /// <summary>
        /// Gets
        /// </summary>
        public List<Trigger> Triggers
        {
            get { return m_Triggers; }
        }





        /// <summary>
        /// 
        /// </summary>
        public void Update(float elapsedTime_)
        {
            foreach (Trigger t in m_Triggers.ToArray())
            {
                t.Update(elapsedTime_);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void Reset()
        {
            foreach (Trigger t in m_Triggers.ToArray())
            {
                t.Reset();
            }
        }

    }
}
