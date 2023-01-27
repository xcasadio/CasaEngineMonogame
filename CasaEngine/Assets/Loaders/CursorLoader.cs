using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Windows.Forms;
using System.Reflection;

namespace CasaEngine.Asset
{
    class CursorLoader
        : IAssetLoader
    {
        #region IAssetLoader Membres

        [DllImport("User32.dll", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        private static extern IntPtr LoadCursorFromFile(String str);

        // Thanks Hans Passant!
        // http://stackoverflow.com/questions/4305800/using-custom-colored-cursors-in-a-c-windows-application
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName_"></param>
        /// <param name="device_"></param>
        /// <returns></returns>
        public object LoadAsset(string fileName_, GraphicsDevice device_)
        {
            IntPtr handle = LoadCursorFromFile(fileName_);

            if (handle == null)
            {
                throw new Win32Exception("CursorLoader.LoadAsset() : can't load the file " + fileName_);
            }

            System.Windows.Forms.Cursor curs = new System.Windows.Forms.Cursor(handle);
            // Note: force the cursor to own the handle so it gets released properly
            var fi = typeof(Cursor).GetField("ownHandle", BindingFlags.NonPublic | BindingFlags.Instance);
            fi.SetValue(curs, true);

            return curs;
        }

        #endregion
    }
}
