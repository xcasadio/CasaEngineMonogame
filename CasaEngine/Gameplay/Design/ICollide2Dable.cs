using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.AI.Messaging;
using CasaEngine.Math.Shape2D;
using Microsoft.Xna.Framework;
using CasaEngine.Gameplay;

namespace CasaEngine.Gameplay.Design
{
    /// <summary>
    /// 
    /// </summary>
    public interface ICollide2Dable
        : IMessageable
    {
        Shape2DObject[] Shape2DObjectList { get; }
        Vector2 Position { get; }
    }
}
