
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
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
#endregion

namespace XNAFinalEngine.UserInterface
{

    #region Event

    public delegate void EventHandler(object sender, EventArgs e);
    public class EventArgs : System.EventArgs
    {
        public bool Handled;
    } //EventArgs

    #endregion

    #region Skin Event

    public delegate void SkinEventHandler(EventArgs e);

    #endregion

    #region Key Event

    public delegate void KeyEventHandler(object sender, KeyEventArgs e);
    public class KeyEventArgs : EventArgs
    {

        #region Variables
                
        public Keys Key = Keys.None;
        public bool Control;
        public bool Shift;
        public bool Alt;
        public bool Caps;

        #endregion

        #region Constructors
                
        public KeyEventArgs() { } 
                
        public KeyEventArgs(Keys key)
        {
            Key = key;
            Control = false;
            Shift = false;
            Alt = false;
            Caps = false;
        } // KeyEventArgs
        
        public KeyEventArgs(Keys key, bool control, bool shift, bool alt, bool caps)
        {
            Key = key;
            Control = control;
            Shift = shift;
            Alt = alt;
            Caps = caps;
        } // KeyEventArgs
        
        #endregion

    } // KeyEventArgs

    #endregion

    #region Mouse Event

    public delegate void MouseEventHandler(object sender, MouseEventArgs e);
    public class MouseEventArgs : EventArgs
    {

        #region Variables

        public MouseState State;
        public MouseButton Button = MouseButton.None;
        public Point Position = new Point(0, 0);
        public Point Difference = new Point(0, 0);
        
        #endregion

        #region Constructors

        public MouseEventArgs() { }

        public MouseEventArgs(MouseState state, MouseButton button, Point position)
        {
            State = state;
            Button = button;
            Position = position;
        } // MouseEventArgs

        #endregion

    } // MouseEventArgs

    #endregion

    #region Draw Event

    public delegate void DrawEventHandler(object sender, DrawEventArgs e);
    public class DrawEventArgs : EventArgs
    {

        #region Variables
                
        public Rectangle Rectangle = Rectangle.Empty;
        
        #endregion

        #region Constructors

        public DrawEventArgs() { }
        
        public DrawEventArgs(Rectangle rectangle)
        {
            Rectangle = rectangle;
        }  // DrawEventArgs

        #endregion

    } // DrawEventArgs

    #endregion

    #region Resize Event

    public delegate void ResizeEventHandler(object sender, ResizeEventArgs e);
    public class ResizeEventArgs : EventArgs
    {

        #region Variables

        public int Width;
        public int Height;
        public int OldWidth;
        public int OldHeight;

        #endregion

        #region Constructors

        public ResizeEventArgs() { }
        
        public ResizeEventArgs(int width, int height, int oldWidth, int oldHeight)
        {
            Width = width;
            Height = height;
            OldWidth = oldWidth;
            OldHeight = oldHeight;
        } // ResizeEventArgs

        #endregion

    } // ResizeEventArgs

    #endregion

    #region Move Event

    public delegate void MoveEventHandler(object sender, MoveEventArgs e);
    public class MoveEventArgs : EventArgs
    {

        #region Variables

        public int Left;
        public int Top;
        public int OldLeft;
        public int OldTop;

        #endregion

        #region Constructors

        public MoveEventArgs() { }
        
        public MoveEventArgs(int left, int top, int oldLeft, int oldTop)
        {
            Left = left;
            Top = top;
            OldLeft = oldLeft;
            OldTop = oldTop;
        } // MoveEventArgs

        #endregion

    } // MoveEventArgs

    #endregion

    #region Device Event

    public delegate void DeviceEventHandler(DeviceEventArgs e);
    public class DeviceEventArgs : EventArgs
    {

        #region Variables

        public PreparingDeviceSettingsEventArgs DeviceSettings;
        
        #endregion

        #region Constructors
        
        public DeviceEventArgs() { }
        
        public DeviceEventArgs(PreparingDeviceSettingsEventArgs deviceSettings)
        {
            DeviceSettings = deviceSettings;
        } // DeviceEventArgs

        #endregion

    } // DeviceEventArgs

    #endregion

    #region Window Closing Event

    public delegate void WindowClosingEventHandler(object sender, WindowClosingEventArgs e);
    public class WindowClosingEventArgs : EventArgs
    {
        public bool Cancel;
    } // WindowClosingEventArgs

    #endregion

    #region Window Closed Event

    public delegate void WindowClosedEventHandler(object sender, WindowClosedEventArgs e);
    public class WindowClosedEventArgs : EventArgs
    {
        public bool Dispose = true;
    } // WindowClosedEventArgs

    #endregion

    #region Console Message Event

    public delegate void ConsoleMessageEventHandler(object sender, ConsoleMessageEventArgs e);
    public class ConsoleMessageEventArgs : EventArgs
    {

        #region Variables
        
        public ConsoleMessage Message;        

        #endregion
        
        #region Constructors
        
        public ConsoleMessageEventArgs() { }
        
        public ConsoleMessageEventArgs(ConsoleMessage message)
        {
            Message = message;
        } // ConsoleMessageEventArgs

        #endregion

    } // ConsoleMessageEventArgs

    #endregion

} // XNAFinalEngine.UserInterface