
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
-----------------------------------------------------------------------------------------------------------------------------------------------

*/


using Microsoft.Xna.Framework.Input;


//using XNAFinalEngine.EngineCore;

namespace XNAFinalEngine.Input
{

    public static class Mouse
    {

        public enum MouseButtons
        {
            LeftButton,
            MiddleButton,
            RightButton,
            XButton1,
            XButton2,
        } // MouseButtons



        // Mouse state, set every frame in the Update method.
        private static MouseState _currentState, _previousState;

        // X and Y movements of the mouse in this frame.
        private static int _deltaX, _deltaY;

        // Current mouse position.
        private static int _positionX, _positionY;

        // Mouse wheel delta. XNA does report only the total scroll value, but we usually need the current delta!
        private static int _wheelDelta, _wheelValue;

        // Start dragging pos, will be set when we just pressed the left mouse button. Used for the MouseDraggingAmount property.
        private static Point _startDraggingPosition;

        // This mode allows to track the mouse movement when the mouse reach and pass the system window border.
        private static bool _trackDeltaOutsideScreen;



        public static MouseState State => _currentState;

        public static MouseState PreviousState => _previousState;


        /*public static bool TrackDeltaOutsideScreen
	    {
	        get { return trackDeltaOutsideScreen; }
	        set
	        {
	            trackDeltaOutsideScreen = value;
                if (value)
                {
                    Microsoft.Xna.Framework.Input.Mouse.SetPosition(Screen.Width / 2 - 1, Screen.Height / 2 - 1);
                    positionX = Screen.Width / 2 - 1;
                    positionY = Screen.Height / 2 - 1;
                }
	        }
	    } // TrackDeltaOutsideScreen

		public static Point position
		{
			get
			{
                Point aux = new Point(positionX, positionY);
                if (aux.X >= Screen.Width)
                    aux.X = Screen.Width - 1;
                if (aux.X < 0)
                    aux.X = 0;
                if (aux.Y >= Screen.Height)
                    aux.Y = Screen.Height - 1;
                if (aux.Y < 0)
                    aux.Y = 0;
                return aux;
			}
			set
			{
                if (!TrackDeltaOutsideScreen)
                {
                    Microsoft.Xna.Framework.Input.Mouse.SetPosition(value.X, value.Y);
                }
                positionX = value.X;
                positionY = value.Y;
			}
        } // position
        */
        public static float DeltaX => _deltaX;

        public static float DeltaY => _deltaY;


        public static bool LeftButtonPressed => _currentState.LeftButton == ButtonState.Pressed;

        public static bool RightButtonPressed => _currentState.RightButton == ButtonState.Pressed;

        public static bool MiddleButtonPressed => _currentState.MiddleButton == ButtonState.Pressed;

        public static bool XButton1Pressed => _currentState.XButton1 == ButtonState.Pressed;

        public static bool XButton2Pressed => _currentState.XButton2 == ButtonState.Pressed;

        public static bool LeftButtonJustPressed => _currentState.LeftButton == ButtonState.Pressed && _previousState.LeftButton == ButtonState.Released;

        public static bool RightButtonJustPressed => _currentState.RightButton == ButtonState.Pressed && _previousState.RightButton == ButtonState.Released;

        public static bool MiddleButtonJustPressed => _currentState.MiddleButton == ButtonState.Pressed && _previousState.MiddleButton == ButtonState.Released;

        public static bool XButton1JustPressed => _currentState.XButton1 == ButtonState.Pressed && _previousState.XButton1 == ButtonState.Released;

        public static bool XButton2JustPressed => _currentState.XButton2 == ButtonState.Pressed && _previousState.XButton2 == ButtonState.Released;

        public static bool LeftButtonJustReleased => _currentState.LeftButton == ButtonState.Released && _previousState.LeftButton == ButtonState.Pressed;

        public static bool RightButtonJustReleased => _currentState.RightButton == ButtonState.Released && _previousState.RightButton == ButtonState.Pressed;

        public static bool MiddleButtonJustReleased => _currentState.MiddleButton == ButtonState.Released && _previousState.MiddleButton == ButtonState.Pressed;

        public static bool XButton1JustReleased => _currentState.XButton1 == ButtonState.Released && _previousState.XButton1 == ButtonState.Pressed;

        public static bool XButton2JustReleased => _currentState.XButton2 == ButtonState.Released && _previousState.XButton2 == ButtonState.Pressed;


        //public static Point DraggingAmount { get { return new Point(-startDraggingPosition.X + position.X, -startDraggingPosition.Y + position.Y); } }

        /*public static Rectangle DraggingRectangle
	    {
	        get
	        {
	            int x, y, width, height;
                if (startDraggingPosition.X <= position.X)
                {
                    x = startDraggingPosition.X;
                    width = position.X - startDraggingPosition.X;
                }
                else
                {
                    x = position.X;
                    width = startDraggingPosition.X - position.X;
                }
                if (startDraggingPosition.Y <= position.Y)
                {
                    y = startDraggingPosition.Y;
                    height = position.Y - startDraggingPosition.Y;
                }
                else
                {
                    y = position.Y;
                    height = startDraggingPosition.Y - position.Y;
                }
                return new Rectangle(x, y, width, height);
	        }
	    } // DraggingRectangle
        */
        //public static bool IsDragging { get { return Math.Abs(position.X - startDraggingPosition.X) + Math.Abs(position.Y - startDraggingPosition.Y) == 0; } }



        public static int WheelDelta => _wheelDelta;

        public static int WheelValue => _wheelValue;


        /*public static void ResetDragging()
		{
			startDraggingPosition = position;
		} // ResetDragging
        */


        public static bool MouseInsideRectangle(Rectangle rectangle)
        {
            return _positionX >= rectangle.X &&
                   _positionY >= rectangle.Y &&
                   _positionX < rectangle.Right &&
                   _positionY < rectangle.Bottom;
        } // MouseInsideRectangle



        public static bool ButtonJustPressed(MouseButtons button)
        {
            if (button == MouseButtons.LeftButton)
            {
                return LeftButtonJustPressed;
            }

            if (button == MouseButtons.MiddleButton)
            {
                return MiddleButtonJustPressed;
            }

            if (button == MouseButtons.RightButton)
            {
                return RightButtonJustPressed;
            }

            if (button == MouseButtons.XButton1)
            {
                return XButton1JustPressed;
            }

            return XButton2JustPressed;
        } // ButtonJustPressed

        public static bool ButtonPressed(MouseButtons button)
        {
            if (button == MouseButtons.LeftButton)
            {
                return LeftButtonPressed;
            }

            if (button == MouseButtons.MiddleButton)
            {
                return MiddleButtonPressed;
            }

            if (button == MouseButtons.RightButton)
            {
                return RightButtonPressed;
            }

            if (button == MouseButtons.XButton1)
            {
                return XButton1Pressed;
            }

            return XButton2Pressed;
        } // ButtonPressed



        internal static void Update()
        {
            // Update mouse state.
            _previousState = _currentState;
            _currentState = Microsoft.Xna.Framework.Input.Mouse.GetState();

            //if (!TrackDeltaOutsideScreen)
            {
                // Calculate mouse movement.
                _deltaX = _currentState.X - _positionX; // positionX is the old position.
                _deltaY = _currentState.Y - _positionY;
                // Update position.
                _positionX = _currentState.X; // Now is the new one.
                _positionY = _currentState.Y;
            }
            /*else
            {
                deltaX = _currentState.X - Screen.Width / 2;
                deltaY = _currentState.Y - Screen.Height / 2;
                positionX += deltaX;
                positionY += deltaY;
                if (positionX >= Screen.Width)
                    positionX = Screen.Width - 1;
                if (positionX < 0)
                    positionX = 0;
                if (positionY >= Screen.Height)
                    positionY = Screen.Height - 1;
                if (positionY < 0)
                    positionY = 0;
                Microsoft.Xna.Framework.Input.Mouse.SetPosition(Screen.Width / 2 - 1, Screen.Height / 2 - 1);
            }*/

            // Dragging
            /*if (LeftButtonJustPressed || (!LeftButtonPressed && !LeftButtonJustReleased))
            {
                startDraggingPosition = position;
            }*/

            // Wheel
            _wheelDelta = _currentState.ScrollWheelValue - _wheelValue;
            _wheelValue = _currentState.ScrollWheelValue;
        } // Update

    } // Mouse
} // XNAFinalEngine.Input
