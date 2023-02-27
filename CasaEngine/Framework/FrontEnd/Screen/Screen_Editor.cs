//-----------------------------------------------------------------------------
// Screen.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------


using System.Xml;
using CasaEngine.Core.Design;

namespace CasaEngine.Framework.FrontEnd.Screen
{
    public abstract partial class Screen
    {
        public override void Save(XmlElement el, SaveOption opt)
        {

        }

        public override void Save(BinaryWriter bw, SaveOption opt)
        {

        }
    }
}