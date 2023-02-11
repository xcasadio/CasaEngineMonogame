using Microsoft.Xna.Framework.Graphics;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Reflection;

namespace CasaEngine.Asset
{
    class CursorLoader
        : IAssetLoader
    {

        [DllImport("User32.dll", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        private static extern IntPtr LoadCursorFromFile(string str);

        // Thanks Hans Passant!
        // http://stackoverflow.com/questions/4305800/using-custom-colored-cursors-in-a-c-windows-application
        public object LoadAsset(string fileName, GraphicsDevice device)
        {
            IntPtr handle = LoadCursorFromFile(fileName);

            if (handle == null)
            {
                throw new Win32Exception("CursorLoader.LoadAsset() : can't load the file " + fileName);
            }

            Cursor curs = new Cursor(handle);
            // Note: force the cursor to own the handle so it gets released properly
            var fi = typeof(Cursor).GetField("ownHandle", BindingFlags.NonPublic | BindingFlags.Instance);
            fi.SetValue(curs, true);

            return curs;
        }

    }
}
