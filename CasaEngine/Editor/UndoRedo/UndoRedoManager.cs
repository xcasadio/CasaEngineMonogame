using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngineCommon.Logger;

namespace CasaEngine.Editor.UndoRedo
{
    public class UndoRedoManager
    {
        /// <summary>
        /// 
        /// </summary>
        struct UndoRedoParam
        {
            public object arg;
            public ICommand command;
        }


        Stack<UndoRedoParam> m_Undo = new Stack<UndoRedoParam>();
        Stack<UndoRedoParam> m_Redo = new Stack<UndoRedoParam>();

        public event EventHandler UndoRedoCommandAdded;
        public event EventHandler EventCommandDone;





        /// <summary>
        /// Gets if can undo
        /// </summary>
        public bool CanUndo
        {
            get { return m_Undo.Count == 0 ? false : true; }
        }

        /// <summary>
        /// Gets if can redo
        /// </summary>
        public bool CanRedo
        {
            get { return m_Redo.Count == 0 ? false : true; }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="command_"></param>
        /// <param name="arg_"></param>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="command_"></param>
        /// <param name="arg_"></param>
        public void Clear()
        {
            m_Undo.Clear();
            m_Redo.Clear();

            if (UndoRedoCommandAdded != null)
            {
                UndoRedoCommandAdded(null, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 
        /// </summary>
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

        /// <summary>
        /// 
        /// </summary>
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

        /// <summary>
        /// 
        /// </summary>
        public void RemoveLastCommand()
        {
            RemoveLastCommands(0);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="num_"></param>
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
