//-----------------------------------------------------------------------------
// IDebugCommandHost.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

namespace CasaEngine.Framework.Debugger
{
    public enum DebugCommandMessage
    {
        Standard = 1,
        Error = 2,
        Warning = 3
    }

    public delegate void DebugCommandExecute(IDebugCommandHost host, string command,
                                                            IList<string> arguments);

    public interface IDebugCommandExecutioner
    {
        void ExecuteCommand(string command);
    }

    public interface IDebugEchoListner
    {
        void Echo(DebugCommandMessage messageType, string text);
    }

    public interface IDebugCommandHost : IDebugEchoListner, IDebugCommandExecutioner
    {
        void RegisterCommand(string command, string description,
                                                        DebugCommandExecute callback);

        void UnregisterCommand(string command);

        void Echo(string text);

        void EchoWarning(string text);

        void EchoError(string text);

        void RegisterEchoListner(IDebugEchoListner listner);

        void UnregisterEchoListner(IDebugEchoListner listner);

        void PushExecutioner(IDebugCommandExecutioner executioner);

        void PopExecutioner();
    }

}
