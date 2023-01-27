using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Editor.Debugger
{
    public class DebugHelper
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ex"></param>
        public static void ShowException(Exception ex)
        {
            Exception exception = ex;
            string tab = "\t";
            string msg = ex.Message;

            while (exception.InnerException != null)
            {
                msg += Environment.NewLine + tab + exception.Message;
                exception = exception.InnerException;
                tab += "\t";
            }

            msg += Environment.NewLine + Environment.NewLine + ex.StackTrace;
            DisplayExceptionForm form = new DisplayExceptionForm(msg);
            form.ShowDialog();
            form.Dispose();
        }
    }
}
