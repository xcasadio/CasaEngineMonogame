
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using CasaEngine.Framework.UserInterface.Controls.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Framework.UserInterface;

public delegate void EventHandler(object sender, EventArgs e);
public class EventArgs : System.EventArgs
{
    public bool Handled;
} //EventArgs



public delegate void SkinEventHandler(EventArgs e);



public delegate void KeyEventHandler(object sender, KeyEventArgs e);
public class KeyEventArgs : EventArgs
{


    public Keys Key = Keys.None;
    public bool Control;
    public bool Shift;
    public bool Alt;
    public bool Caps;



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


} // KeyEventArgs



public delegate void MouseEventHandler(object sender, MouseEventArgs e);
public class MouseEventArgs : EventArgs
{


    public MouseState State;
    public MouseButton Button = MouseButton.None;
    public Point Position = new(0, 0);
    public Point Difference = new(0, 0);



    public MouseEventArgs() { }

    public MouseEventArgs(MouseState state, MouseButton button, Point position)
    {
        State = state;
        Button = button;
        Position = position;
    } // MouseEventArgs


} // MouseEventArgs



public delegate void DrawEventHandler(object sender, DrawEventArgs e);
public class DrawEventArgs : EventArgs
{


    public Rectangle Rectangle = Rectangle.Empty;



    public DrawEventArgs() { }

    public DrawEventArgs(Rectangle rectangle)
    {
        Rectangle = rectangle;
    }  // DrawEventArgs


} // DrawEventArgs



public delegate void ResizeEventHandler(object sender, ResizeEventArgs e);
public class ResizeEventArgs : EventArgs
{


    public int Width;
    public int Height;
    public int OldWidth;
    public int OldHeight;



    public ResizeEventArgs() { }

    public ResizeEventArgs(int width, int height, int oldWidth, int oldHeight)
    {
        Width = width;
        Height = height;
        OldWidth = oldWidth;
        OldHeight = oldHeight;
    } // ResizeEventArgs


} // ResizeEventArgs



public delegate void MoveEventHandler(object sender, MoveEventArgs e);
public class MoveEventArgs : EventArgs
{


    public int Left;
    public int Top;
    public int OldLeft;
    public int OldTop;



    public MoveEventArgs() { }

    public MoveEventArgs(int left, int top, int oldLeft, int oldTop)
    {
        Left = left;
        Top = top;
        OldLeft = oldLeft;
        OldTop = oldTop;
    } // MoveEventArgs


} // MoveEventArgs



public delegate void DeviceEventHandler(DeviceEventArgs e);
public class DeviceEventArgs : EventArgs
{


    public PreparingDeviceSettingsEventArgs DeviceSettings;



    public DeviceEventArgs() { }

    public DeviceEventArgs(PreparingDeviceSettingsEventArgs deviceSettings)
    {
        DeviceSettings = deviceSettings;
    } // DeviceEventArgs


} // DeviceEventArgs



public delegate void WindowClosingEventHandler(object sender, WindowClosingEventArgs e);
public class WindowClosingEventArgs : EventArgs
{
    public bool Cancel;
} // WindowClosingEventArgs



public delegate void WindowClosedEventHandler(object sender, WindowClosedEventArgs e);
public class WindowClosedEventArgs : EventArgs
{
    public bool Dispose = true;
} // WindowClosedEventArgs



public delegate void ConsoleMessageEventHandler(object sender, ConsoleMessageEventArgs e);
public class ConsoleMessageEventArgs : EventArgs
{


    public ConsoleMessage Message;



    public ConsoleMessageEventArgs() { }

    public ConsoleMessageEventArgs(ConsoleMessage message)
    {
        Message = message;
    } // ConsoleMessageEventArgs


} // ConsoleMessageEventArgs


// XNAFinalEngine.UserInterface