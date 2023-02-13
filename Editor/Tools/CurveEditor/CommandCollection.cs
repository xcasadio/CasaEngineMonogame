//-----------------------------------------------------------------------------
// CommandCollection.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

//-----------------------------------------------------------------------------
// CommandCollection.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

namespace Editor.Tools.CurveEditor
{
    /// <summary>
    /// This class manages ICommands for Undo/Redo.
    /// Also, CommandQueue works like ICommand. So you can capture multiple
    /// commands as one command.
    /// </summary>
    public class CommandCollection : ICommand, ICollection<ICommand>
    {
        /// <summary>
        /// It returns true if it can process undo; otherwise it returns false.
        /// </summary>
        public bool CanUndo { get { return commandIndex > 0; } }

        /// <summary>
        /// It returns true if it can process redo; otherwise it returns false.
        /// </summary>
        public bool CanRedo { get { return commandIndex < commands.Count; } }

        /// <summary>
        /// Return number of commands in this queue.
        /// </summary>
        public int Count { get { return commands.Count; } }

        /// <summary>
        /// Current command index.
        /// </summary>
        public int Index { get { return commandIndex; } }

        /// <summary>
        /// Gets the element at the specfied index.
        /// </summary>
        /// <param name="index">The zero-based index of the element to get.</param>
        /// <returns>The element at the specfied index.</</returns>
        public ICommand this[int index] { get { return commands[index]; } }


        /// <summary>
        /// Add command to queue.
        /// </summary>
        /// <param name="command"></param>
        public void Add(ICommand item)
        {
            // Discard rest of commands from commandIndex.
            commands.RemoveRange(commandIndex, commands.Count - commandIndex);

            // Add command to commands.
            commands.Add(item);
            commandIndex = commands.Count;
        }

        /// <summary>
        /// Undo command.
        /// </summary>
        public bool Undo()
        {
            if (!CanUndo)
            {
                return false;
            }

            commands[--commandIndex].Unexecute();
            return true;
        }

        /// <summary>
        /// Redo command.
        /// </summary>
        public bool Redo()
        {
            if (!CanRedo)
            {
                return false;
            }

            commands[commandIndex++].Execute();
            return true;
        }

        public void Execute()
        {
            // Execute all commands.
            foreach (ICommand command in commands)
                command.Execute();
        }

        public void Unexecute()
        {
            // Unexecute all commands.
            for (int i = commands.Count - 1; i >= 0; --i)
            {
                commands[i].Unexecute();
            }
        }



        /// <summary>
        /// For store commands.
        /// </summary>
        List<ICommand> commands = new();

        /// <summary>
        /// Current command index.
        /// </summary>
        int commandIndex;



        public void Clear()
        {
            commands.Clear();
            commandIndex = 0;
        }

        public bool Contains(ICommand item)
        {
            return commands.Contains(item);
        }

        public void CopyTo(ICommand[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(ICommand item)
        {
            throw new NotImplementedException();
        }



        public IEnumerator<ICommand> GetEnumerator()
        {
            return commands.GetEnumerator();
        }



        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return commands.GetEnumerator();
        }

    }
}
