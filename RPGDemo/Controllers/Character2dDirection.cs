using System;

namespace RPGDemo.Controllers;

[Flags]
public enum Character2dDirection
{
    Up = 1,
    Down = 2,
    Left = 4,
    Right = 8,

    //UpLeft      = Left | Up,
    //DownLeft    = Left | Down,
    //UpRight     = Right | Up,
    //DownRight   = Right | Down
}