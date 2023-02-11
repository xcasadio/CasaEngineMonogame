namespace CasaEngine.Editor.UndoRedo
{
    public class UndoRedoManager
    {
        struct UndoRedoParam
        {
            public object Arg;
            public ICommand Command;
        }


        readonly Stack<UndoRedoParam> _undo = new Stack<UndoRedoParam>();
        readonly Stack<UndoRedoParam> _redo = new Stack<UndoRedoParam>();

        public event EventHandler UndoRedoCommandAdded;
        public event EventHandler EventCommandDone;





        public bool CanUndo => _undo.Count == 0 ? false : true;

        public bool CanRedo => _redo.Count == 0 ? false : true;


        public void Add(ICommand command, object arg)
        {
            UndoRedoParam param = new UndoRedoParam();
            param.Arg = arg;
            param.Command = command;

            _undo.Push(param);
            _redo.Clear();

            if (UndoRedoCommandAdded != null)
            {
                UndoRedoCommandAdded(null, EventArgs.Empty);
            }
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();

            if (UndoRedoCommandAdded != null)
            {
                UndoRedoCommandAdded(null, EventArgs.Empty);
            }
        }

        public void Undo()
        {
            if (CanUndo)
            {
                UndoRedoParam param = _undo.Pop();
                param.Command.Undo(param.Arg);
                _redo.Push(param);

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
                UndoRedoParam param = _redo.Pop();
                param.Command.Execute(param.Arg);
                _undo.Push(param);

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

        public void RemoveLastCommands(int nu)
        {
            for (int i = 0; i < nu; i++)
            {
                if (CanUndo)
                {
                    _undo.Pop();
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
