using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.Gameplay
{
    /// <summary>
    /// 
    /// </summary>
	public class GlobalVar
    {

        private static GlobalVar instance;
        private Dictionary<string, int> m_Vars;



        /// <summary>
        /// 
        /// </summary>
        public static GlobalVar Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new GlobalVar();
                }
                return instance;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public Dictionary<string, int> Vars
        {
            get { return m_Vars; }
            set { m_Vars = value; }
        }



        /// <summary>
        /// 
        /// </summary>
        private GlobalVar()
        {
            m_Vars = new Dictionary<string, int>();
        }



    }
}
