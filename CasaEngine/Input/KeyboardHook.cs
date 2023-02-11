
/*
Copyright (c) 2008-2012, Laboratorio de Investigación y Desarrollo en Visualización y Computación Gráfica - 
                         Departamento de Ciencias e Ingeniería de la Computación - Universidad Nacional del Sur.
All rights reserved.
Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

•	Redistributions of source code must retain the above copyright, this list of conditions and the following disclaimer.

•	Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer
    in the documentation and/or other materials provided with the distribution.

•	Neither the name of the Universidad Nacional del Sur nor the names of its contributors may be used to endorse or promote products derived
    from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS ''AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR
CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE,
EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

-----------------------------------------------------------------------------------------------------------------------------------------------
Author: Schneider, José Ignacio (jis@cs.uns.edu.ar)
This class was based in the work of Emma Burrows. That work doesn't have a license of any kind.
-----------------------------------------------------------------------------------------------------------------------------------------------

*/


using System.Diagnostics;
using System.Runtime.InteropServices;


namespace XNAFinalEngine.Input
{
    public class KeyboardHook : IDisposable
    {


        private const int KeyboardHookId = 13;



        internal struct KeyboadHookStruct
        {
            public int VkCode;
            public int Flags;
        } // KeyboadHookStruct



        private readonly HookHandlerDelegate _proc;
        private readonly IntPtr _hookId = IntPtr.Zero;
        internal delegate IntPtr HookHandlerDelegate(int nCode, IntPtr wParam, ref KeyboadHookStruct lParam);

        private KeyboadHookStruct _lastParameter;



        public KeyboardHook()
        {
            _proc = new HookHandlerDelegate(HookCallback);
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                _hookId = NativeMethods.SetWindowsHookEx(KeyboardHookId, _proc, NativeMethods.GetModuleHandle(curModule.ModuleName), 0);
            }
        } // KeyboardHook



        private IntPtr HookCallback(int nCode, IntPtr wParam, ref KeyboadHookStruct lParam)
        {
            // This is an easy way to change from a key press behavior to a key down behavior.
            if (lParam.Flags != _lastParameter.Flags || lParam.VkCode != _lastParameter.VkCode)
            {
                // If the key is disable or has a special function.
                if (lParam.VkCode == 91 || lParam.VkCode == 92 ||  // Win
                    (lParam.VkCode == 13) ||  // Alt-Enter (does not work properly so I made a fix)
                    lParam.VkCode == 44) // Print Screen
                {
                    //if (lParam.VkCode == 44)
                    //{
                    //    ScreenshotCapturer.MakeScreenshot = true;
                    //}
                    //KeyboardState keyboardState = Microsoft.Xna.Framework.Input.Keyboard.GetState();
                    //if (keyboardState.IsKeyDown(Keys.LeftAlt) || keyboardState.IsKeyDown(Keys.RightAlt))
                    //{
                    //    Screen.ToggleFullscreen();
                    //}

                    // Store current parameter.
                    _lastParameter = lParam;
                    if (lParam.VkCode != 13)
                        return (IntPtr)1;
                }
            }
            // Store current parameter.
            _lastParameter = lParam;
            // if the key is allowed...
            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, ref lParam);
        } // HookCallback



        public void Dispose()
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
        } // Dispose



        [ComVisibleAttribute(false), System.Security.SuppressUnmanagedCodeSecurity]
        internal class NativeMethods
        {

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr GetModuleHandle(string lpModuleName);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr SetWindowsHookEx(int idHook, HookHandlerDelegate lpfn, IntPtr hMod, uint dwThreadId);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool UnhookWindowsHookEx(IntPtr hhk);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, ref KeyboadHookStruct lParam);

            [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
            public static extern short GetKeyState(int keyCode);

        } // NativeMethods


    } // KeyboardHook
} // XNAFinalEngine.Input