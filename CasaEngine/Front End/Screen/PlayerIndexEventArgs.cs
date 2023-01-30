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
            this.playerIndex = playerIndex;
        }


        public PlayerIndex PlayerIndex => playerIndex;

        readonly PlayerIndex playerIndex;
    }
}
