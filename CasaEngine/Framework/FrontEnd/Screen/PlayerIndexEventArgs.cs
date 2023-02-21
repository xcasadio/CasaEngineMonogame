//-----------------------------------------------------------------------------
// PlayerIndexEventArgs.cs
//
// XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------


using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.FrontEnd.Screen
{
    public class PlayerIndexEventArgs : EventArgs
    {
        public PlayerIndexEventArgs(PlayerIndex playerIndex)
        {
            _playerIndex = playerIndex;
        }


        public PlayerIndex PlayerIndex => _playerIndex;

        private readonly PlayerIndex _playerIndex;
    }
}
