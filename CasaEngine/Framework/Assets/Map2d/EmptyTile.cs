﻿namespace CasaEngine.Framework.Assets.Map2d;

public class EmptyTile : Tile
{
    public EmptyTile() : base(null)
    { }

    public override void Update(float elapsedTime)
    {
        //do nothing
    }

    public override void Draw(float x, float y, float z)
    {
        //do nothing
    }

    public override void Draw(float x, float y, float z, Rectangle uvOffset)
    {
        //do nothing
    }
}