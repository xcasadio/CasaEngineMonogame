﻿using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Logger;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Assets;

public class AssetLoader<T> : IAssetLoader where T : ISaveLoad, new()
{
    public object LoadAsset(string fileName, GraphicsDevice device)
    {
        var jsonDocument = JsonDocument.Parse(File.ReadAllText(fileName));
        var entity = new T();
        entity.Load(jsonDocument.RootElement, SaveOption.Editor);
        return entity;
    }

    public bool IsFileSupported(string fileName)
    {
        return false; // no import from other project
    }
}