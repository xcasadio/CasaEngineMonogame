using System;


using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Drawing;

namespace CasaEngine.Editor.Tools
{
    /// <summary>
    /// 
    /// </summary>
    public interface IContentObject
    {
        string ContentObjectName { get; }
        Bitmap ContentObjectImage { get; }
    }
}
