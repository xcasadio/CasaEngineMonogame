using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using TomShane.Neoforce.Controls.Input;

namespace TomShane.Neoforce.Controls;

public class GamePadEventArgs : EventArgs
{

    public readonly PlayerIndex PlayerIndex = PlayerIndex.One;
    public GamePadState State = new();
    public GamePadButton Button = GamePadButton.None;
    public GamePadVectors Vectors;

    /*
     public GamePadEventArgs()
     {                      
     }*/

    public GamePadEventArgs(PlayerIndex playerIndex)
    {
        PlayerIndex = playerIndex;
    }

    public GamePadEventArgs(PlayerIndex playerIndex, GamePadButton button)
    {
        PlayerIndex = playerIndex;
        Button = button;
    }

}