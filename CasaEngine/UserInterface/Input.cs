
#region License
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/
#endregion

#region Using directives
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

using XNAFinalEngine.Helpers;
using Keyboard = XNAFinalEngine.Input.Keyboard;
using Mouse = XNAFinalEngine.Input.Mouse;
using CasaEngine.Game;
#endregion

namespace XNAFinalEngine.UserInterface
{

    #region Enumerators
    
    public enum MouseButton
    {
        None = 0,
        Left,
        Right,
        Middle,
        XButton1,
        XButton2
    } // MouseButton

    #endregion

    public class Input
    {

        #region Classes

        private class InputKey
        {
            public Keys Key = Keys.None;
            public bool Pressed;
            public double Countdown = repeatDelay;
        } // InputKey

        private class InputMouseButton
        {
            public MouseButton Button = MouseButton.None;
            public bool Pressed;
        } // InputMouseButton

        #endregion

        #region Constants

        private const int repeatDelay = 500;
        private const int repeatRate = 50;

        #endregion

        #region Variables

        private readonly List<InputKey> keys = new List<InputKey>();
        private readonly List<InputMouseButton> mouseButtons = new List<InputMouseButton>();
        private Rectangle m_GameWindowClientBounds;

        #endregion

        #region Events
	        
        public event KeyEventHandler KeyDown;
        public event KeyEventHandler KeyPress;
        public event KeyEventHandler KeyUp;

        public event MouseEventHandler MouseDown;
        public event MouseEventHandler MousePress;
        public event MouseEventHandler MouseUp;
        public event MouseEventHandler MouseMove;

        #endregion

        #region Constructors

        /// <summary>
        /// Initialice the user interface input system.
        /// </summary>
        public Input(Rectangle gameWindowClientBounds_)
        {
            m_GameWindowClientBounds = gameWindowClientBounds_;

            #if (WINDOWS)
                foreach (string keyName in Enum.GetNames(typeof(Keys)))
                {
                    InputKey key = new InputKey { Key = (Keys)Enum.Parse(typeof(Keys), keyName) };
                    keys.Add(key);
                }

                foreach (string mouseButtonName in Enum.GetNames(typeof(MouseButton)))
                {
                    InputMouseButton mouseButton = new InputMouseButton
                    {
                        Button = (MouseButton)Enum.Parse(typeof(MouseButton), mouseButtonName)
                    };
                    mouseButtons.Add(mouseButton);
                }
            #endif
        } // InputSystem

        #endregion

        #region Update

        /// <summary>
        /// Update.
        /// </summary>
        /// <param name="elapsedTime_"></param>
        public virtual void Update(float elapsedTime_)
        {
            #if (WINDOWS)
                UpdateMouse();
                UpdateKeys(elapsedTime_);
            #endif
        } // Update

        #endregion

        #region Update Keyboard

        /// <summary>
        /// Update keyboard.
        /// </summary>
        /// <param name="elapsedTime_"></param>
        private void UpdateKeys(float elapsedTime_)
        {
            Keyboard.Update();

            KeyEventArgs e = new KeyEventArgs { Caps = (((ushort) GetKeyState(0x14)) & 0xffff) != 0 };

            foreach (Keys key in Keyboard.State.GetPressedKeys())
            {
                if      (key == Keys.LeftAlt     || key == Keys.RightAlt)     e.Alt = true;
                else if (key == Keys.LeftShift   || key == Keys.RightShift)   e.Shift = true;
                else if (key == Keys.LeftControl || key == Keys.RightControl) e.Control = true;
            }

            foreach (InputKey key in keys)
            {
                if (key.Key == Keys.LeftAlt     || key.Key == Keys.RightAlt   ||
                    key.Key == Keys.LeftShift   || key.Key == Keys.RightShift ||
                    key.Key == Keys.LeftControl || key.Key == Keys.RightControl)
                {
                    continue;
                }

                bool pressed = Keyboard.State.IsKeyDown(key.Key);

                double frameTimeInMilliseconds = elapsedTime_; // From seconds to milliseconds.
                if (pressed) key.Countdown -= frameTimeInMilliseconds;

                if ((pressed) && (!key.Pressed))
                {
                    key.Pressed = true;
                    e.Key = key.Key;

                    if (KeyDown  != null) KeyDown.Invoke(this, e);
                    if (KeyPress != null) KeyPress.Invoke(this, e);
                }
                else if ((!pressed) && (key.Pressed))
                {
                    key.Pressed = false;
                    key.Countdown = repeatDelay;
                    e.Key = key.Key;

                    if (KeyUp != null) KeyUp.Invoke(this, e);
                }
                else if (key.Pressed && key.Countdown < 0)
                {
                    key.Countdown = repeatRate;
                    e.Key = key.Key;

                    if (KeyPress != null) KeyPress.Invoke(this, e);
                }
            }
        } // UpdateKeys
        
        [DllImport("user32.dll")]
        internal static extern short GetKeyState(int key);

        #endregion

        #region Update Mouse

        /// <summary>
        /// Update mouse.
        /// </summary>
        private void UpdateMouse()
        {
            Mouse.Update();

            if ((Mouse.State.X != Mouse.PreviousState.X) || (Mouse.State.Y != Mouse.PreviousState.Y))
            {
                MouseEventArgs e = new MouseEventArgs();

                MouseButton btn = MouseButton.None;
                if      (Mouse.State.LeftButton   == ButtonState.Pressed) btn = MouseButton.Left;
                else if (Mouse.State.RightButton  == ButtonState.Pressed) btn = MouseButton.Right;
                else if (Mouse.State.MiddleButton == ButtonState.Pressed) btn = MouseButton.Middle;
                else if (Mouse.State.XButton1     == ButtonState.Pressed) btn = MouseButton.XButton1;
                else if (Mouse.State.XButton2     == ButtonState.Pressed) btn = MouseButton.XButton2;

                BuildMouseEvent(btn, ref e, m_GameWindowClientBounds);
                if (MouseMove != null)
                {
                    MouseMove.Invoke(this, e);
                }
            }
            UpdateButtons();
        } // UpdateMouse

        private void UpdateButtons()
        {
            MouseEventArgs e = new MouseEventArgs();

            foreach (InputMouseButton btn in mouseButtons)
            {
                ButtonState bs = ButtonState.Released;

                if      (btn.Button == MouseButton.Left)     bs = Mouse.State.LeftButton;
                else if (btn.Button == MouseButton.Right)    bs = Mouse.State.RightButton;
                else if (btn.Button == MouseButton.Middle)   bs = Mouse.State.MiddleButton;
                else if (btn.Button == MouseButton.XButton1) bs = Mouse.State.XButton1;
                else if (btn.Button == MouseButton.XButton2) bs = Mouse.State.XButton2;
                else continue;

                bool pressed = (bs == ButtonState.Pressed); // The current state

                if (pressed && !btn.Pressed) // If is pressed and the last frame wasn't pressed.
                {
                    btn.Pressed = true;
                    BuildMouseEvent(btn.Button, ref e, m_GameWindowClientBounds);

                    if (MouseDown != null) MouseDown.Invoke(this, e);
                    if (MousePress != null) MousePress.Invoke(this, e);
                }
                else if (!pressed && btn.Pressed) // If isn't pressed and the last frame was pressed.
                {
                    btn.Pressed = false;
                    BuildMouseEvent(btn.Button, ref e, m_GameWindowClientBounds);

                    if (MouseUp != null) MouseUp.Invoke(this, e);
                }
                else if (pressed && btn.Pressed) // If is pressed and was pressed.
                {
                    e.Button = btn.Button;
                    BuildMouseEvent(btn.Button, ref e, m_GameWindowClientBounds);
                    if (MousePress != null) MousePress.Invoke(this, e);
                }
            }
        } // UpdateButtons

        private static void AdjustPosition(Rectangle gameWindowClientBounds_, ref MouseEventArgs e)
        {
            Rectangle screen = gameWindowClientBounds_;

            if (e.Position.X < 0) e.Position.X = 0;
            if (e.Position.Y < 0) e.Position.Y = 0;
            if (e.Position.X >= screen.Width)  e.Position.X = screen.Width - 1;
            if (e.Position.Y >= screen.Height) e.Position.Y = screen.Height - 1;
        } // AdjustPosition

        private static void BuildMouseEvent(MouseButton button, ref MouseEventArgs e, Rectangle gameWindowClientBounds_)
        {
            e.State = Mouse.State;
            e.Button = button;

            e.Position = new Point(Mouse.State.X, Mouse.State.Y);
            AdjustPosition(gameWindowClientBounds_, ref e);

            e.State = new MouseState(e.Position.X, e.Position.Y, e.State.ScrollWheelValue, e.State.LeftButton, e.State.MiddleButton, e.State.RightButton, e.State.XButton1, e.State.XButton2);

            Point pos = new Point(Mouse.State.X, Mouse.State.Y);
            e.Difference = new Point(e.Position.X - pos.X, e.Position.Y - pos.Y);
        } // BuildMouseEvent

        #endregion

    } // Input
} // XNAFinalEngine.UserInterface