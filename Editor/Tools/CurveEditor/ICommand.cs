//-----------------------------------------------------------------------------
// ICommand.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

using System.Collections.Generic;
using System.Text;

namespace Editor.Tools.CurveEditor
{
    /// <summary>
    /// Command interface that provides Execute and Unexecute methods.
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// Execute command.
        /// </summary>
        void Execute();

        /// <summary>
        /// Unexecute (Undo) command.
        /// </summary>
        void Unexecute();
    }

    /// <summary>
    /// Command handler
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="command"></param>
    public delegate void CommandHandler(object sender, ICommand command);

}
