﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.GUI.Neoforce;

public class Cursor
{
    public Texture2D CursorTexture { get; set; }

    internal readonly string CursorPath;

    public int Height { get; set; }

    public int Width { get; set; }

    public Vector2 HotSpot { get; set; }

    public Cursor(string path, Vector2 hotspot, int width, int height)
    {
        CursorPath = path;
        HotSpot = hotspot;
        Width = width;
        Height = height;
    }
}