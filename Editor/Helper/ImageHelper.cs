
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.IO;

namespace Editor.Helper
{
    /// <summary>
    /// 
    /// </summary>
    static public class ImageHelper
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="file_"></param>
        /// <returns></returns>
        static public Image LoadImageFromFileAndRelease(string file_)
        {
            Image img;
            FileStream stream = new FileStream(file_, FileMode.Open, FileAccess.Read);
            img = Image.FromStream(stream);
            stream.Close();
            return img;
        }
    }
}
