using System;



namespace CasaEngine.Gameplay
{
    /// <summary>
    /// 
    /// </summary>
    [Flags]
    public enum CharacterActor2DOrientation
    {
        Up = 1,
        Down = 2,
        Left = 4,
        Right = 8,

        UpLeft = Left | Up,
        DownLeft = Left | Down,
        UpRight = Right | Up,
        DownRight = Right | Down
    }
}
