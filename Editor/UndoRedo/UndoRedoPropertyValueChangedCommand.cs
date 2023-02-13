using CasaEngine.Editor.UndoRedo;
using System.ComponentModel;

namespace Editor.UndoRedo
{
    class UndoRedoPropertyValueChangedCommand
        : ICommand
    {
        object m_Value;
        PropertyDescriptor m_PropertyDescriptor;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj_"></param>
        /// <param name="propDesc_"></param>
        /// <param name="oldValue_"></param>
        public UndoRedoPropertyValueChangedCommand(PropertyDescriptor propDesc_, object oldValue_)
        {
            m_PropertyDescriptor = propDesc_;
            m_Value = oldValue_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="arg1_"></param>
        public void Execute(object arg1_)
        {
            object temp = m_PropertyDescriptor.GetValue(arg1_);
            m_PropertyDescriptor.SetValue(arg1_, m_Value);
            m_Value = temp;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="arg1_"></param>
        public void Undo(object arg1_)
        {
            Execute(arg1_);
        }
    }
}
