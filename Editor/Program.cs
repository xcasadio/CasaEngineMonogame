using CasaEngine.Framework.Game;
using Editor.Debugger;

namespace Editor
{
    static class Program
    {
        /// <summary>
        /// Point d'entrée principal de l'application.
        /// </summary>
        [STAThread]
        static void Main(string[] args_)
        {

#if UNITTEST
            //UNIT TEST

            //InputManager
            //InputManager inputMangerTest = new InputManager();
            //inputMangerTest.ComplexTest();

            //EditorMain Test
            EditorMainForm editorMainTestnew = new EditorMainForm();
            editorMainTestnew.CopyProjectUnitTest();
            editorMainTestnew.CreateNewProjectAndSave();
            editorMainTestnew.ClearProjectUnitTestCopy();
#else
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += new UnhandledExceptionEventHandler(MyHandler);

            //Check if XNA and .Net framework is install

            // String to hold any prerequisites error messages
            //string prerequisitesErrorMessage = "";
            //
            //// If XNA 4.0 is not installed
            //Microsoft.Win32.RegistryKey xnaKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\XNA\\Framework\\v4.0");
            //if (xnaKey == null || (int)xnaKey.GetValue("Installed") != 1)
            //{
            //    // Store the error message
            //    prerequisitesErrorMessage += "XNA 4.0 must be installed to run this program. You can download the XNA 4.0 Redistributable from http://www.microsoft.com/downloads/ \n\n";
            //}
            //
            //// If .NET 3.5 or 4 is not installed
            ////Microsoft.Win32.RegistryKey netKey35 = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\NET Framework Setup\\NDP\\v3.5");
            //Microsoft.Win32.RegistryKey netKey4 = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\NET Framework Setup\\NDP\\v4\\Client");
            ////bool net35NotInstalled = (netKey35 == null || (int)netKey35.GetValue("Install") != 1);
            //bool net4NotInstalled = (netKey4 == null || (int)netKey4.GetValue("Install") != 1);
            //if (/*net35NotInstalled && */net4NotInstalled)
            //{
            //    // Store the error message
            //    prerequisitesErrorMessage += "The .NET Framework 4.0 must be installed to run this program. You can download the .NET Framework from http://www.microsoft.com/downloads/ \n\n";
            //}
            //
            //// If not all of the prerequisites are installed
            //if (!string.IsNullOrEmpty(prerequisitesErrorMessage))
            //{
            //    // Add to the error message the option of trying to run the program anyways
            //    prerequisitesErrorMessage += "Do you want to try and run the program anyways, even though not all of the prerequisites are installed?";
            //
            //    // Display the error message and exit
            //    if (MessageBox.Show(prerequisitesErrorMessage, "Prerequisites Not Installed", MessageBoxButtons.YesNo) == DialogResult.No)
            //    {
            //        return;
            //    }
            //}

            EngineComponents.Arguments = args_;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

#if !DEBUG
            try
            {
#endif
            //Application.Run(new EditorMainForm());
            Application.Run(new MainForm());
#if !DEBUG
            }
            catch (Exception e)
            {
                DebugHelper.ShowException(e);
            }
#endif

#endif // UNITTEST
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        static void MyHandler(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception)args.ExceptionObject;
            string msg = "";

            Exception ex = e;
            while (ex != null)
            {
                msg += ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine + Environment.NewLine;
                ex = ex.InnerException;
            }

            DisplayExceptionForm form = new DisplayExceptionForm(
                msg + "Runtime terminating: " + args.IsTerminating);
            form.ShowDialog();
        }
    }
}
