
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using System;



namespace XNAFinalEngine.UserInterface
{


    public enum Message
    {
        Click,
        MouseDown,
        MouseUp,
        MousePress,
        MouseMove,
        MouseOver,
        MouseOut,
        KeyDown,
        KeyUp,
        KeyPress,
    } // Message

    public enum ControlState
    {
        Enabled,
        Hovered,
        Pressed,
        Focused,
        Disabled
    } // ControlState

    public enum Alignment
    {
        None,
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        MiddleCenter,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    } // Alignment

    public enum ModalResult
    {
        None,
        Ok,
        Cancel,
        Yes,
        No,
        Abort,
        Retry,
        Ignore
    } // ModalResult

    public enum Orientation
    {
        Horizontal,
        Vertical
    } // Orientation

    public enum ScrollBarType
    {
        None,
        Vertical,
        Horizontal,
        Both
    } // ScrollBarsType

    [Flags]
    public enum Anchors
    {
        None = 0x00,
        Left = 0x01,
        Top = 0x02,
        Right = 0x04,
        Bottom = 0x08,
        Horizontal = Left | Right,
        Vertical = Top | Bottom,
        All = Left | Top | Right | Bottom
    } // Anchors

    /// <summary>
    /// Specifies how an image is positioned within a control.
    /// </summary>
    public enum SizeMode
    {
        /// <summary>
        /// It preserves the pixel ratio to 1. If the destination rectangle is smaller than the source rectangle the lower-right part of the texture will be cut.
        /// </summary>
        Normal,
        /// <summary>
        /// It changes the control's width and height to the texture's dimentions. 
        /// </summary>
        Auto,
        /// <summary>
        /// It preserves the pixel ratio to 1, but the center of the texture is in the same place as the center of the control. 
        /// </summary>
        Centered,
        /// <summary>
        /// Stretch the texture onto the control. 
        /// </summary>
        Stretched,
        /// <summary>
        /// Stretch the texture onto the control but maintaining the texture's aspect ratio. 
        /// </summary>
        Fit,
    } // SizeMode



    public struct Margins
    {

        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        /// <summary>
        /// Top + Bottom.
        /// </summary>
        public int Vertical { get { return (Top + Bottom); } }

        /// <summary>
        /// Left + Right.
        /// </summary>
        public int Horizontal { get { return (Left + Right); } }

        public Margins(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        } // Margins

    }  // Margins

    public struct Size
    {
        public int Width;
        public int Height;

        public static Size Zero { get { return new Size(0, 0); } }

        public Size(int width, int height)
        {
            Width = width;
            Height = height;
        } // Size

    } // Size


} // XNAFinalEngine.UserInterface
