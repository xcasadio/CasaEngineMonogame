namespace CasaEngine.Editor.UndoRedo
{
    public class UndoRedoManager
    {
        struct UndoRedoParam
        {
            public object arg;
            public ICommand command;
        }


        readonly Stack<UndoRedoParam> m_Undo = new Stack<UndoRedoParam>();
        readonly Stack<UndoRedoParam> m_Redo = new Stack<UndoRedoParam>();

        public event EventHandler UndoRedoCommandAdded;
        public event EventHandler EventCommandDone;





        public bool CanUndo => m_Undo.Count == 0 ? false : true;

        public bool CanRedo => m_Redo.Count == 0 ? false : true;


        public void Add(ICommand command_, object arg_)
        {
            UndoRedoParam param = new UndoRedoParam();
            param.arg = arg_;
            param.command = command_;

            m_Undo.Push(param);
            m_Redo.Clear();

            if (UndoRedoCommandAdded != null)
            {
                UndoRedoCommandAdded(null, EventArgs.Empty);
            }
        }

        public void Clear()
        {
            m_Undo.Clear();
            m_Redo.Clear();

            if (UndoRedoCommandAdded != null)
            {
                UndoRedoCommandAdded(null, EventArgs.Empty);
            }
        }

        public void Undo()
        {
            if (CanUndo)
            {
                UndoRedoParam param = m_Undo.Pop();
                param.command.Undo(param.arg);
                m_Redo.Push(param);

                if (EventCommandDone != null)
                {
                    EventCommandDone(null, EventArgs.Empty);
                }
            }
        }

        public void Redo()
        {
            if (CanRedo)
            {
                UndoRedoParam param = m_Redo.Pop();
                param.command.Execute(param.arg);
                m_Undo.Push(param);

                if (EventCommandDone != null)
                {
                    EventCommandDone(null, EventArgs.Empty);
                }
            }
        }

        public void RemoveLastCommand()
        {
            RemoveLastCommands(0);
        }

        public void RemoveLastCommands(int num_)
        {
            for (int i = 0; i < num_; i++)
            {
                if (CanUndo)
                {
                    m_Undo.Pop();
                }
                else
                {
                    break;
                }
            }

            if (UndoRedoCommandAdded != null)
            {
                UndoRedoCommandAdded(null, EventArgs.Empty);
            }
        }

    }
}
