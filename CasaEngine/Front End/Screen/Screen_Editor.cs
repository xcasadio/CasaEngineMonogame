//-----------------------------------------------------------------------------
// Screen.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

using System;


using Microsoft.Xna.Framework;
using CasaEngine.Graphics2D;
using System.Xml;
using System.IO;
using CasaEngineCommon.Design;

namespace CasaEngine.FrontEnd.Screen
{
    public abstract partial class Screen
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="opt_"></param>
        public override void Save(XmlElement el_, SaveOption opt_)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="opt_"></param>
        public override void Save(BinaryWriter bw_, SaveOption opt_)
        {

        }
    }
}
