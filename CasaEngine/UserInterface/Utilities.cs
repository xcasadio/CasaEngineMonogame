
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using Microsoft.Xna.Framework;

namespace XNAFinalEngine.UserInterface
{
    static class Utilities
    {

        /// <summary>
        /// Control type name.
        /// </summary>
        public static string ControlTypeName(Control control)
        {
            string str = control.ToString();
            int i = str.LastIndexOf(".");
            return str.Remove(0, i + 1);
        } // ControlTypeName

        public static Color ParseColor(string str)
        {
            string[] val = str.Split(';');
            byte r = 255, g = 255, b = 255, a = 255;

            if (val.Length >= 1) r = byte.Parse(val[0]);
            if (val.Length >= 2) g = byte.Parse(val[1]);
            if (val.Length >= 3) b = byte.Parse(val[2]);
            if (val.Length >= 4) a = byte.Parse(val[3]);

            return Color.FromNonPremultiplied(r, g, b, a);
        } // ParseColor

    } // Utilities
} //  XNAFinalEngine.UserInterface
