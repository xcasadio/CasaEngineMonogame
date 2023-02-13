using CasaEngine.Editor.UndoRedo;

namespace Editor.UndoRedo
{
    /// <summary>
    /// 
    /// </summary>
    public class UndoRedoListControlSelection
        : ICommand
    {

        int m_Selectedindex;





        /// <summary>
        /// 
        /// </summary>
        /// <param name="ob_"></param>
        public UndoRedoListControlSelection(int index_)
        {
            m_Selectedindex = index_;
        }



        delegate void DelegateCallback(ListControl c_);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="arg1_"></param>
        public void Execute(object arg1_)
        {
            if (arg1_ is ListControl)
            {
                ListControl c = arg1_ as ListControl;

                if (c.InvokeRequired == true)
                {
                    c.Invoke(new DelegateCallback(Do), c);
                }
                else
                {
                    Do(c);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="arg1_"></param>
        public void Undo(object arg1_)
        {
            Execute(arg1_);
        }

        /// <summary>
        /// 
        /// </summary>
        private void Do(ListControl c_)
        {
            int temp = c_.SelectedIndex;
            c_.SelectedIndex = m_Selectedindex;
            m_Selectedindex = temp;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "Selection " + m_Selectedindex;
        }

    }
}
