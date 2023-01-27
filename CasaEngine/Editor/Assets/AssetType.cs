using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Content;
using System.Xml;
using CasaEngineCommon.Extension;
using System.IO;
using CasaEngine.Game;
using CasaEngineCommon.Logger;
using Microsoft.Xna.Framework;
using CasaEngine;
using CasaEngine.Editor.Builder;


namespace CasaEngine.Editor.Assets
{
    /// <summary>
    /// 
    /// </summary>
    public enum AssetType
    {
        Animation,
        Audio,
        Effect,
        SkinnedMesh,
        StaticMesh,
        Texture,
        Video,

        None,
        All //UseFull ??
    }
}
