using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.Xna.Framework.Input;

namespace Editor.Game
{
    public class MouseWindowed
    {

        System.Windows.Forms.Control m_Control;



        /// <summary>
        /// Gets
        /// </summary>
        public int X
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public int Y
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public bool LeftButton
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public bool MiddleButton
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public bool RightButton
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public bool XButton1
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets
        /// </summary>
        public bool XButton2
        {
            get;
            private set;
        }


        /// <summary>
        /// Gets
        /// </summary>
        public int ScrollWheelValue
        {
            get;
            private set;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="controlHandle_"></param>
        public MouseWindowed(IntPtr controlHandle_)
        {
            m_Control = System.Windows.Forms.Control.FromHandle(controlHandle_);

            //m_Control.MouseClick += new System.Windows.Forms.MouseEventHandler(MouseClick);
            //m_Control.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(MouseDoubleClick);
            m_Control.MouseDown += new System.Windows.Forms.MouseEventHandler(MouseDown);
            m_Control.MouseMove += new System.Windows.Forms.MouseEventHandler(MouseMove);
            m_Control.MouseUp += new System.Windows.Forms.MouseEventHandler(MouseUp);
            m_Control.MouseWheel += new System.Windows.Forms.MouseEventHandler(MouseWheel);
            m_Control.MouseEnter += new EventHandler(m_Control_MouseEnter);

            X = 0;
            Y = 0;
        }

        void m_Control_MouseEnter(object sender, EventArgs e)
        {
            m_Control.Focus();
        }



        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public MouseState GetState()
        {
            return new MouseState(X, Y, ScrollWheelValue, 
                LeftButton ? Microsoft.Xna.Framework.Input.ButtonState.Pressed : Microsoft.Xna.Framework.Input.ButtonState.Released,
                MiddleButton ? Microsoft.Xna.Framework.Input.ButtonState.Pressed : Microsoft.Xna.Framework.Input.ButtonState.Released, 
                RightButton ? Microsoft.Xna.Framework.Input.ButtonState.Pressed : Microsoft.Xna.Framework.Input.ButtonState.Released, 
                XButton1 ? Microsoft.Xna.Framework.Input.ButtonState.Pressed : Microsoft.Xna.Framework.Input.ButtonState.Released, 
                XButton2 ? Microsoft.Xna.Framework.Input.ButtonState.Pressed : Microsoft.Xna.Framework.Input.ButtonState.Released);
        }

        /*public void Update()
        {
            m_Control.Invoke(new UpdateMouseDelegate(UpdateMouse));
        }

        private delegate void UpdateMouseDelegate();

        /// <summary>
        /// 
        /// </summary>
        private void UpdateMouse()
        {
            System.Drawing.Point p = m_Control.PointToScreen(System.Drawing.Point.Empty);
            m_MouseState = Mouse.GetState();
            m_MousePosition.X = m_MouseState.X - p.X;
            m_MousePosition.X = m_MouseState.Y - p.Y;
        }*/

        void MouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            ScrollWheelValue = e.Delta;
        }

        void MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            switch (e.Button)
            {
                case MouseButtons.Left:
                    LeftButton = false;
                    break;

                case MouseButtons.Right:
                    RightButton = false;
                    break;

                case MouseButtons.Middle:
                    MiddleButton = false;
                    break;

                case MouseButtons.XButton1:
                    XButton1 = false;
                    break;

                case MouseButtons.XButton2:
                    XButton2 = false;
                    break;
            }
        }

        void MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            X = e.X;
            Y = e.Y;
        }

        void MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            switch (e.Button)
            {
                case MouseButtons.Left:
                    LeftButton = true;
                    break;

                case MouseButtons.Right:
                    RightButton = true;
                    break;

                case MouseButtons.Middle:
                    MiddleButton = true;
                    break;

                case MouseButtons.XButton1:
                    XButton1 = true;
                    break;

                case MouseButtons.XButton2:
                    XButton2 = true;
                    break;
            }
        }

        /// <summary>
        /// the scroll wheel value is not reset
        /// the event is fired only when wheel is used
        /// SpriteSortMode we need to reset this value
        /// </summary>
        public void ScrollWheelValueReset()
        {
            ScrollWheelValue = 0;
        }

        /*void MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            throw new NotImplementedException();
        }

        void MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            throw new NotImplementedException();
        }*/

    }
}
