//-----------------------------------------------------------------------------
// PlayerIndexEventArgs.cs
//
// XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------


using Microsoft.Xna.Framework;

namespace CasaEngine.FrontEnd.Screen
{
    public class PlayerIndexEventArgs : EventArgs
    {
        public PlayerIndexEventArgs(PlayerIndex playerIndex)
        {
            _playerIndex = playerIndex;
        }


        public PlayerIndex PlayerIndex => _playerIndex;

        readonly PlayerIndex _playerIndex;
    }
}
