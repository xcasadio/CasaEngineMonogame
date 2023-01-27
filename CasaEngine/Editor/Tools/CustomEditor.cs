using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CasaEngine.Editor.Tools
{
    /// <summary>
    /// 
    /// </summary>
    public class CustomEditor
        : Attribute
    {
        private Type m_Type;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name_"></param>
        public CustomEditor(Type type_)
        {
            m_Type = type_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return m_Type.FullName;
        }
    }
}
