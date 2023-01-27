
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
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CasaEngine.Asset;

using XNAFinalEngine.Helpers;
using CasaEngine.Game;
using CasaEngine.Asset.Cursors;
#endregion

namespace XNAFinalEngine.UserInterface
{

    #region Control List

    /// <summary>
    /// Defines type used as a controls collection.
    /// </summary>
    public class ControlsList : EventedList<Control>
    {
        public ControlsList() { }

        public ControlsList(int capacity) : base(capacity) { }

        public ControlsList(IEnumerable<Control> collection) : base(collection) { }
        
    } // ControlsList

    #endregion

    /// <summary>
    /// Defines the base class for all controls.
    /// </summary>    
    public class Control : Disposable
    {

        #region Constants

        /// <summary>
        /// Undefined Color.
        /// </summary>
        public static readonly Color UndefinedColor = new Color(255, 255, 255, 0);

        #endregion

        #region Variables

        // List of all controls.
        private static readonly ControlsList controlList = new ControlsList();

        // List of all child controls.
        private readonly ControlsList childrenControls = new ControlsList();

        // Specifies how many pixels is used for edges (and corners) allowing resizing of the control.
        private int resizerSize = 4;

        // Rectangular area that reacts on moving the control with the mouse.
        private Rectangle movableArea = Rectangle.Empty;

        // Parent control.
        private Control parent;

        // The root control.
        private Control root;

        // Indicates whether this control can receive focus. 
        private bool canFocus = true;

        // Indicates whether this control can be moved by the mouse.
        private bool movable;

        // Indicate whether this control can be resized by the mouse.
        private bool resizable;

        // Indicates whether this control should process mouse double-clicks.
        private bool doubleClicks = true;

        //  Indicates whether this control should use ouline resizing.
        private bool outlineResizing;

        // Indicates whether this control should use outline moving.
        private bool outlineMoving;

        // Indicates the distance from another control. Usable with StackPanel control.
        private Margins margins = new Margins(4, 4, 4, 4);

        // Indicates whether the control outline is displayed only for certain edges. 
        private bool partialOutline = true;

        // Indicates whether the control is allowed to be brought in the front.
        private bool stayOnBack;

        // Indicates that the control should stay on top of other controls.
        private bool stayOnTop;

        // Control's tool tip.
        internal ToolTip toolTip;

        // The area where is the control supposed to be drawn.
        private Rectangle drawingRect = Rectangle.Empty;

        // The skin parameters used for rendering the control.
        private SkinControlInformation skinControl;

        // Indicates whether the control can respond to user interaction.
        private bool enabled = true;

        // Indicates whether the control is rendered.
        private bool visible = true;

        // The color for the control.
        private Color color = UndefinedColor;

        // Text color for the control.
        private Color textColor = UndefinedColor;

        // The background color for the control.
        private Color backgroundColor = Color.Transparent;

        // The alpha value for this control.
        private byte alpha = 255;

        // The edges of the container to which a control is bound and determines how a control is resized with its parent.
        private Anchors anchor = Anchors.Left | Anchors.Top;

        // The width of the control.
        private int width = 64;

        // The height of the control.
        private int height = 64;

        // The distance, in pixels, between the left edge of the control and the left edge of its parent.
        private int left;

        // The distance, in pixels, between the top edge of the control and the top edge of its parent.
        private int top;

        // The minimum width in pixels the control can be sized to.
        private int minimumWidth;

        // The maximum width in pixels the control can be sized to.
        private int maximumWidth = 4096;

        // The minimum height in pixels the control can be sized to.
        private int minimumHeight;

        // The maximum height in pixels the control can be sized to.
        private int maximumHeight = 4096;

        // Stack that stores new controls.
        private static readonly Queue<Control> newControls = new Queue<Control>();

        private Anchors resizeEdge = Anchors.All;
        private string text = "Control";
        private long tooltipTimer;
        private long doubleClickTimer;
        private MouseButton doubleClickButton = MouseButton.None;
        private bool invalidated = true;
        private RenderTarget renderTarget;
        private Point pressSpot = Point.Zero;
        private readonly int[] pressDiff = new int[4];
        private Alignment resizeArea = Alignment.None;
        private bool hovered;
        private bool inside;
        private readonly bool[] pressed = new bool[32];
        private Margins anchorMargins;
        private Rectangle outlineRectangle = Rectangle.Empty;

        #endregion

        #region Properties

        /// <summary>
        /// List of all controls, even if they are not added to the manager or another control.
        /// </summary>
        public static ControlsList ControlList { get { return controlList; } }

        #region Size and Position

        #region Virtual Height, Virtual Width

        /// <summary>
        /// Get the virtual height of this control. 
        /// I.e. the height that is established by the children controls.
        /// Sometimes the control is smaller so a scroll bar is needed.
        /// </summary>
        internal virtual int VirtualHeight
        {
            get
            {
                if (Parent is Container && (Parent as Container).AutoScroll)
                {
                    // So it is a client area...
                    int maxHeight = 0;
                    foreach (Control childControl in ChildrenControls)
                    {
                        if ((childControl.Anchor & Anchors.Bottom) != Anchors.Bottom && childControl.Visible)
                        {
                            if (childControl.Top + childControl.Height > maxHeight)
                                maxHeight = childControl.Top + childControl.Height;
                        }
                    }
                    if (maxHeight < Height)
                        maxHeight = Height;

                    return maxHeight;
                }
                return Height;
            }
        } // VirtualHeight

        /// <summary>
        /// Get the virtual width of this control. 
        /// I.e. the width that is established by the children controls.
        /// Sometimes the control is smaller so a scroll bar is needed.
        /// </summary>
        internal virtual int VirtualWidth
        {
            get
            {
                if (Parent is Container && (Parent as Container).AutoScroll)
                {
                    // So it is a client area...
                    int maxWidth = 0;

                    foreach (Control c in ChildrenControls)
                    {
                        if ((c.Anchor & Anchors.Right) != Anchors.Right && c.Visible)
                        {
                            if (c.Left + c.Width > maxWidth)
                                maxWidth = c.Left + c.Width;
                        }
                    }
                    if (maxWidth < Width)
                        maxWidth = Width;

                    return maxWidth;
                }
                return Width;
            }
        } // VirtualWidth

        #endregion

        #region Left, Top, Width, Height

        /// <summary>
        /// Gets or sets the distance, in pixels, between the left edge of the control and the left edge of its parent.
        /// </summary>
        public virtual int Left
        {
            get { return left; }
            set
            {
                if (left != value)
                {
                    int old = left;
                    left = value;

                    SetAnchorMargins();

                    if (!Suspended)
                        OnMove(new MoveEventArgs(left, top, old, top));
                }
            }
        } // Left

        /// <summary>
        /// Gets or sets the distance, in pixels, between the top edge of the control and the top edge of its parent.
        /// </summary>
        public virtual int Top
        {
            get { return top; }
            set
            {
                if (top != value)
                {
                    int old = top;
                    top = value;

                    SetAnchorMargins();

                    if (!Suspended) 
                        OnMove(new MoveEventArgs(left, top, left, old));
                }
            }
        } // Top

        /// <summary>
        /// Gets or sets the width of the control.
        /// </summary>
        public virtual int Width
        {
            get
            {
                return width;
            }
            set
            {
                if (width != value)
                {
                    int old = width;
                    width = value;

                    if (skinControl != null)
                    {
                        if (width + skinControl.OriginMargins.Horizontal > MaximumWidth) 
                            width = MaximumWidth - skinControl.OriginMargins.Horizontal;
                    }
                    else
                    {
                        if (width > MaximumWidth) 
                            width = MaximumWidth;
                    }
                    if (width < MinimumWidth)
                        width = MinimumWidth;

                    if (width > MinimumWidth) 
                        SetAnchorMargins();

                    if (!Suspended) 
                        OnResize(new ResizeEventArgs(width, height, old, height));
                }
            }
        } // Width

        /// <summary>
        /// Gets or sets the height of the control.
        /// </summary>
        public virtual int Height
        {
            get
            {
                return height;
            }
            set
            {
                if (height != value)
                {
                    int old = height;

                    height = value;

                    if (skinControl != null)
                    {
                        if (height + skinControl.OriginMargins.Vertical > MaximumHeight)
                            height = MaximumHeight - skinControl.OriginMargins.Vertical;
                    }
                    else
                    {
                        if (height > MaximumHeight) height = MaximumHeight;
                    }
                    if (height < MinimumHeight) height = MinimumHeight;

                    if (height > MinimumHeight) SetAnchorMargins();

                    if (!Suspended) 
                        OnResize(new ResizeEventArgs(width, height, width, old));
                }

            }
        } // Height

        #endregion

        #region Minimum Maximum Width Height

        /// <summary>
        /// Gets or sets the minimum width in pixels the control can be sized to.
        /// </summary>
        public virtual int MinimumWidth
        {
            get { return minimumWidth; }
            set
            {
                minimumWidth = value;
                if (minimumWidth < 0)
                    minimumWidth = 0;
                if (minimumWidth > maximumWidth)
                    minimumWidth = maximumWidth;
                if (width < MinimumWidth)
                    Width = MinimumWidth;
            }
        } // MinimumWidth

        /// <summary>
        /// /// Gets or sets the minimum height in pixels the control can be sized to.
        /// </summary>
        public virtual int MinimumHeight
        {
            get { return minimumHeight; }
            set
            {
                minimumHeight = value;
                if (minimumHeight < 0) 
                    minimumHeight = 0;
                if (minimumHeight > maximumHeight) 
                    minimumHeight = maximumHeight;
                if (height < MinimumHeight) 
                    Height = MinimumHeight;
            }
        } // MinimumHeight

        /// <summary>
        /// /// Gets or sets the maximum width in pixels the control can be sized to.
        /// </summary>
        public virtual int MaximumWidth
        {
            get
            {
                int max = maximumWidth;
                if (max > UserInterfaceManager.Screen.Width)
                    max = UserInterfaceManager.Screen.Width;
                return max;
            }
            set
            {
                maximumWidth = value;
                if (maximumWidth < minimumWidth) 
                    maximumWidth = minimumWidth;
                if (width > MaximumWidth) 
                    Width = MaximumWidth;
            }
        } // MaximumWidth

        /// <summary>
        /// Gets or sets the maximum height in pixels the control can be sized to.
        /// </summary>
        public virtual int MaximumHeight
        {
            get
            {
                int max = maximumHeight;
                if (max > UserInterfaceManager.Screen.Height)
                    max = UserInterfaceManager.Screen.Height;
                return max;
            }
            set
            {
                maximumHeight = value;
                if (maximumHeight < minimumHeight)
                    maximumHeight = minimumHeight;
                if (height > MaximumHeight)
                    Height = MaximumHeight;
            }
        } // MaximumHeight

        #endregion

        #region Scrolling Amount

        /// <summary>
        /// The horizontal scrolling amount.
        /// </summary>
        internal virtual int HorizontalScrollingAmount { get; set; }

        /// <summary>
        /// The vertical scrolling amount.
        /// </summary>
        internal virtual int VerticalScrollingAmount { get; set; }

        #endregion

        #region Screen coordinate (absolute coordinates)

        /// <summary>
        /// The screen coordinates where the control begins. The margins aren't taken in consideration.
        /// </summary>
        internal virtual int ControlLeftAbsoluteCoordinate
        {
            get
            {
                if (parent == null)
                    return left + HorizontalScrollingAmount;
                if (parent.SkinInformation == null)
                    return parent.ControlLeftAbsoluteCoordinate + left + HorizontalScrollingAmount;
                return parent.ControlLeftAbsoluteCoordinate + left - parent.SkinInformation.OriginMargins.Left + HorizontalScrollingAmount;
            }
        } // ControlLeftAbsoluteCoordinate

        /// <summary>
        /// The screen coordinates where the control begins. The margins aren't taken in consideration.
        /// </summary>
        internal virtual int ControlTopAbsoluteCoordinate
        {
            get
            {
                if (parent == null)
                    return top + VerticalScrollingAmount;
                if (parent.SkinInformation == null)
                    return parent.ControlTopAbsoluteCoordinate + top + VerticalScrollingAmount;
                return parent.ControlTopAbsoluteCoordinate + top - parent.SkinInformation.OriginMargins.Top + VerticalScrollingAmount;
            }
        } // ControlTopAbsoluteCoordinate

        /// <summary>
        /// The screen coordinates where the control begins. The margins are taken in consideration.
        /// In this case the width of the control is its width and the size of the margins.
        /// </summary>
        protected virtual int ControlAndMarginsLeftAbsoluteCoordinate
        {
            get
            {
                if (skinControl == null)
                    return ControlLeftAbsoluteCoordinate;
                return ControlLeftAbsoluteCoordinate - skinControl.OriginMargins.Left;
            }
        } // ControlAndMarginsLeftAbsoluteCoordinate

        /// <summary>
        /// The screen coordinates where the control begins. The margins are taken in consideration.
        /// In this case the height of the control is its height and the size of the margins.
        /// </summary>
        protected virtual int ControlAndMarginsTopAbsoluteCoordinate
        {
            get
            {
                if (skinControl == null)
                    return ControlTopAbsoluteCoordinate;
                return ControlTopAbsoluteCoordinate - skinControl.OriginMargins.Top;
            }
        } // ControlAndMarginsTopAbsoluteCoordinate

        #endregion

        #region Control and margins size

        /// <summary>
        /// The width and the size of the left and right margins.
        /// </summary>
        internal virtual int ControlAndMarginsWidth
        {
            get
            {
                if (skinControl == null)
                    return width;
                return width + skinControl.OriginMargins.Left + skinControl.OriginMargins.Right;
            }
        } // ControlAndMarginsWidth

        /// <summary>
        /// The height and the size of the bottom and top margins.
        /// </summary>
        internal virtual int ControlAndMarginsHeight
        {
            get
            {
                if (skinControl == null)
                    return height;
                return height + skinControl.OriginMargins.Top + skinControl.OriginMargins.Bottom;
            }
        } // ControlAndMarginsHeight

        #endregion
        
        #region Client Values

        /// <summary>
        /// Get and set the control's client margins.
        /// </summary>
        public virtual Margins ClientMargins { get; set; }
        
        /// <summary>
        /// Client Left.
        /// </summary>
        public virtual int ClientLeft { get { return ClientMargins.Left; } }

        /// <summary>
        /// Client Top..
        /// </summary>
        public virtual int ClientTop { get { return ClientMargins.Top; } }

        /// <summary>
        /// Client Width.
        /// </summary>
        public virtual int ClientWidth
        {
            get { return ControlAndMarginsWidth - ClientMargins.Left - ClientMargins.Right; }
            set { Width = value + ClientMargins.Horizontal - skinControl.OriginMargins.Horizontal; }
        } // ClientWidth

        /// <summary>
        /// Client Height.
        /// </summary>
        public virtual int ClientHeight
        {
            get { return ControlAndMarginsHeight - ClientMargins.Top - ClientMargins.Bottom; }
            set { Height = value + ClientMargins.Vertical - skinControl.OriginMargins.Vertical; }
        } // ClientHeight

        #endregion

        #region Rectangles

        /// <summary>
        /// The rectangle that covers only the control dimensions (without its margins).
        /// </summary>
        internal virtual Rectangle ControlRectangle
        {
            get
            {
                return new Rectangle(ControlLeftAbsoluteCoordinate, ControlTopAbsoluteCoordinate, ControlAndMarginsWidth, ControlAndMarginsHeight);
            }
        } // ControlRectangle

        /// <summary>
        /// The rectangle that covers the control and its margins.
        /// </summary>
        protected virtual Rectangle ControlAndMarginsRectangle
        {
            get
            {
                return new Rectangle(ControlAndMarginsLeftAbsoluteCoordinate, ControlAndMarginsTopAbsoluteCoordinate, ControlAndMarginsWidth, ControlAndMarginsHeight);
            }
        } // ControlAndMarginsRectangle

        /// <summary>
        /// Client left, top, width and height.
        /// </summary>
        protected virtual Rectangle ClientRectangle
        {
            get
            {
                return new Rectangle(ClientLeft, ClientTop, ClientWidth, ClientHeight);
            }
        } // ClientRectangle

        /// <summary>
        /// The rectangle that covers the control and that begins inside the parent. Left, Top, Width and Height.
        /// </summary>
        internal virtual Rectangle ControlRectangleRelativeToParent
        {
            get
            {
                return new Rectangle(Left, Top, Width, Height);
            }
        } // ControlRectangleRelativeToParent

        /// <summary>
        /// Outline Rectangle
        /// </summary>
        private Rectangle OutlineRectangle
        {
            get { return outlineRectangle; }
            set
            {
                outlineRectangle = value;
                if (value != Rectangle.Empty)
                {
                    if (outlineRectangle.Width > MaximumWidth) outlineRectangle.Width = MaximumWidth;
                    if (outlineRectangle.Height > MaximumHeight) outlineRectangle.Height = MaximumHeight;
                    if (outlineRectangle.Width < MinimumWidth) outlineRectangle.Width = MinimumWidth;
                    if (outlineRectangle.Height < MinimumHeight) outlineRectangle.Height = MinimumHeight;
                }
            }
        } // OutlineRectangle

        #endregion

        /// <summary>
        /// Gets or sets the value indicating the distance from another control. Usable with StackPanel control.
        /// </summary>
        internal virtual Margins DefaultDistanceFromAnotherControl { get { return margins; } set { margins = value; } }

        /// <summary>
        /// Set control position without adjusting margins.
        /// </summary>
        internal void SetPosition(int _left, int _top)
        {
            left = _left;
            top = _top;
        } // SetPosition

        #endregion

        #region Others

        /// <summary>
        /// Gets the UserInterfaceManager
        /// </summary>
        public UserInterfaceManager UserInterfaceManager { get; private set; }

        #if (WINDOWS)
            /// <summary>
            /// Gets or sets the cursor displaying over the control.
            /// </summary>
            public Cursor Cursor { get; set; }
        #endif        

        /// <summary>
        /// Gets a list of all child controls.
        /// </summary>
        public virtual ControlsList ChildrenControls { get { return childrenControls; } }

        /// <summary>
        /// Gets or sets a rectangular area that reacts on moving the control with the mouse.
        /// </summary>
        public virtual Rectangle MovableArea { get { return movableArea; } set { movableArea = value; } }

        /// <summary>
        /// Gets a value indicating whether this control is a child control.
        /// </summary>
        public virtual bool IsChild { get { return (parent != null); } }

        /// <summary>
        /// Gets a value indicating whether this control is a parent control.
        /// </summary>
        public virtual bool IsParent { get { return (childrenControls != null && childrenControls.Count > 0); } }

        /// <summary>
        /// Gets a value indicating whether this control is a root control.
        /// </summary>
        public virtual bool IsRoot { get { return (root == this); } }

        /// <summary>
        /// Gets or sets a value indicating whether this control can receive focus when the user press the tab key.
        /// </summary>
        public virtual bool CanFocus { get { return canFocus; } set { canFocus = value; } }

        /// <summary>
        /// Gets or sets a value indicating whether this control is rendered off the parents texture.
        /// </summary>
        public virtual bool Detached { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this controls can receive user input events.
        /// </summary>
        public virtual bool Passive { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this control can be moved by the mouse.
        /// </summary>
        public virtual bool Movable { get { return movable; } set { movable = value; } }

        /// <summary>
        /// Gets or sets a value indicating whether this control can be resized by the mouse.
        /// </summary>
        public virtual bool Resizable { get { return resizable; } set { resizable = value; } }

        /// <summary>
        /// Gets or sets the size of the rectangular borders around the control used for resizing by the mouse.
        /// </summary>
        public virtual int ResizerSize { get { return resizerSize; } set { resizerSize = value; } }

        /// <summary>
        /// Gets or sets the ContextMenu associated with this control.
        /// </summary>
        public virtual ContextMenu ContextMenu { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this control should process mouse double-clicks.
        /// </summary>
        public virtual bool DoubleClicks { get { return doubleClicks; } set { doubleClicks = value; } }

        /// <summary>
        /// Gets or sets a value indicating whether this control should use outline resizing.
        /// </summary>
        public virtual bool OutlineResizing { get { return outlineResizing; } set { outlineResizing = value; } }

        /// <summary>
        /// Gets or sets a value indicating whether this control should use outline moving.
        /// </summary>
        public virtual bool OutlineMoving { get { return outlineMoving; } set { outlineMoving = value; } }

        /// <summary>
        /// Gets or sets the value indicating wheter control is in design mode.
        /// </summary>
        public virtual bool DesignMode { get; set; }

        /// <summary>
        /// Gets or sets the value indicating whether the control outline is displayed only for certain edges. 
        /// </summary>   
        public virtual bool PartialOutline
        {
            get { return partialOutline; }
            set { partialOutline = value; }
        } // PartialOutline

        /// <summary>
        /// Gets or sets the value indicating whether the control is allowed to be brought in the front.
        /// </summary>
        public virtual bool StayOnBack
        {
            get { return stayOnBack; }
            set
            {
                if (value && stayOnTop)
                    stayOnTop = false;
                stayOnBack = value;
            }
        } // StayOnBack

        /// <summary>
        /// Gets or sets the value indicating that the control should stay on top of other controls.
        /// </summary>
        public virtual bool StayOnTop
        {
            get { return stayOnTop; }
            set
            {
                if (value && stayOnBack) 
                    stayOnBack = false;
                stayOnTop = value;
            }
        } // StayOnTop

        /// <summary>
        /// Gets or sets a name of the control.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this control has input focus.
        /// </summary>
        public virtual bool Focused
        {
            get { return (UserInterfaceManager.FocusedControl == this); }
            set
            {
                Invalidate();
                bool previousValue = Focused;
                if (value)
                {
                    UserInterfaceManager.FocusedControl = this;
                    if (!Suspended && !previousValue)
                        OnFocusGained();
                    if (Focused && Root != null && Root is Container)
                        (Root as Container).ScrollTo(this);
                }
                else
                {
                    if (UserInterfaceManager.FocusedControl == this)
                        UserInterfaceManager.FocusedControl = null;
                    if (!Suspended && previousValue)
                        OnFocusLost();
                }
            }
        } // Focused

        /// <summary>
        /// Gets a value indicating current state of the control.
        /// </summary>
        public virtual ControlState ControlState
        {
            get
            {
                if (DesignMode)
                    return ControlState.Enabled;
                if (Suspended)
                    return ControlState.Disabled;
                if (!enabled)
                    return ControlState.Disabled;
                if ((IsPressed && inside) || (Focused && IsPressed))
                    return ControlState.Pressed;
                if (hovered && !IsPressed)
                    return ControlState.Hovered;
                if ((Focused && !inside) || (hovered && IsPressed && !inside) || (Focused && !hovered && inside))
                    return ControlState.Focused;
                return ControlState.Enabled;
            }
        } // ControlState

        /// <summary>
        /// Control's tool tip.
        /// </summary>
        public virtual ToolTip ToolTip
        {
            get { return toolTip ?? (toolTip = new ToolTip(UserInterfaceManager) { Visible = false }); }
            set { toolTip = value;  }
        } // ToolTip

        /// <summary>
        /// Is pressed?
        /// </summary>
        internal protected virtual bool IsPressed
        {
            get
            {
                for (int i = 0; i < pressed.Length - 1; i++)
                {
                    if (pressed[i]) return true;
                }
                return false;
            }
        } // IsPressed
        
        /// <summary>
        /// Gets an area where is the control supposed to be drawn.
        /// </summary>
        public Rectangle DrawingRectangle
        {
            get { return drawingRect; }
            private set { drawingRect = value; }
        } // DrawingRect

        /// <summary>
        /// Gets or sets a value indicating whether this control should receive any events.
        /// </summary>
        public virtual bool Suspended { get; set; }

        internal protected virtual bool Hovered { get { return hovered; } }

        internal protected virtual bool Inside { get { return inside; } }

        internal protected virtual bool[] Pressed { get { return pressed; } }

        /// <summary>
        /// Gets or sets a value indicating whether this controls is currently being moved.
        /// </summary>
        protected virtual bool IsMoving { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this controls is currently being resized.
        /// </summary>
        protected virtual bool IsResizing { get; set; }

        /// <summary>
        /// Gets or sets the edges of the container to which a control is bound and determines how a control is resized with its parent.
        /// </summary>
        public virtual Anchors Anchor
        {
            get { return anchor; }
            set
            {
                anchor = value;
                SetAnchorMargins();
                if (!Suspended) OnAnchorChanged(new EventArgs());
            }
        } // Anchor

        /// <summary>
        /// Gets or sets the edges of the contol which are allowed for resizing.
        /// </summary>
        public virtual Anchors ResizeEdge
        {
            get { return resizeEdge; }
            set { resizeEdge = value; }
        } // ResizeEdge

        /// <summary>
        /// Gets or sets the skin parameters used for rendering the control.
        /// </summary>
        internal virtual SkinControlInformation SkinInformation
        {
            get { return skinControl; }
            set
            {
                skinControl = value;
                ClientMargins = skinControl.ClientMargins;
            }
        } // SkinControlInformation

        /// <summary>
        /// Gets or sets the text associated with this control.
        /// </summary>
        public virtual string Text
        {
            get { return text; }
            set
            {
                text = value;
                Invalidate();
                if (!Suspended) 
                    OnTextChanged(new EventArgs());
            }
        } // Text

        /// <summary>
        /// Gets or sets the alpha value for this control.
        /// </summary>
        public virtual byte Alpha
        {
            get { return alpha; }
            set
            {
                alpha = value;
                if (!Suspended) 
                    OnAlphaChanged(new EventArgs());
            }
        } // Alpha

        /// <summary>
        /// Gets or sets the background color for the control.
        /// </summary>
        public virtual Color BackgroundColor
        {
            get { return backgroundColor; }
            set
            {
                backgroundColor = value;
                Invalidate();
                if (!Suspended) 
                    OnBackColorChanged(new EventArgs());
            }
        } // BackgroundColor

        /// <summary>
        /// Gets or sets the color for the control.
        /// </summary>
        public virtual Color Color
        {
            get { return color; }
            set
            {
                if (value != color)
                {
                    color = value;
                    Invalidate();
                    if (!Suspended) 
                        OnColorChanged(new EventArgs());
                }
            }
        } // Color

        /// <summary>
        /// Gets or sets the text color for the control.
        /// </summary>
        public virtual Color TextColor
        {
            get { return textColor; }
            set
            {
                if (value != textColor)
                {
                    textColor = value;
                    Invalidate();
                    if (!Suspended) OnTextColorChanged(new EventArgs());
                }
            }
        } // TextColor

        /// <summary>
        /// Gets or sets a value indicating whether the control can respond to user interaction.
        /// </summary>
        public virtual bool Enabled
        {
            get { return enabled; }
            set
            {
                if (Root != null && Root != this && !Root.Enabled && value) 
                    return;
                enabled = value;
                Invalidate();
                foreach (Control c in childrenControls)
                {
                    c.Enabled = value;
                }
                if (!Suspended)
                    OnEnabledChanged(new EventArgs());
            }
        } // Enabled

        /// <summary>
        /// Gets or sets a value that indicates whether the control is rendered.
        /// </summary>
        public virtual bool Visible
        {
            get { return (visible && (parent == null || parent.Visible)); }
            set
            {
                visible = value;
                Invalidate();
                if (!Suspended)
                    OnVisibleChanged(new EventArgs());
            }
        } // Visible

        /// <summary>
        /// Gets or sets the parent for the control.
        /// </summary>
        public virtual Control Parent
        {
            get { return parent; }
            set
            {
                // Remove it from old parent
                if (parent == null)
                    UserInterfaceManager.Remove(parent);
                else
                    parent.Remove(this);
                // Add this control to the new parent
                if (value != null) 
                    value.Add(this);
                else 
                    UserInterfaceManager.Add(this);
            }
        } // Parent

        /// <summary>
        /// Gets or sets the root for the control.
        /// </summary>
        public virtual Control Root
        {
            get { return root; }
            private set
            {
                if (root != value)
                {
                    root = value;
                    foreach (Control c in childrenControls)
                    {
                        c.Root = root;
                    }
                    if (!Suspended) OnRootChanged(new EventArgs());
                }
            }
        } // Root

        #endregion

        #endregion

        #region Events

        // Mouse //
        public event EventHandler Click;
        public event EventHandler DoubleClick;
        public event MouseEventHandler MouseDown;
        public event MouseEventHandler MousePress;
        public event MouseEventHandler MouseUp;
        public event MouseEventHandler MouseMove;
        public event MouseEventHandler MouseOver;
        public event MouseEventHandler MouseOut;
        // Keyboard //
        public event KeyEventHandler KeyDown;
        public event KeyEventHandler KeyPress;
        public event KeyEventHandler KeyUp;
        // Move //
        public event MoveEventHandler Move;
        public event MoveEventHandler ValidateMove;
        public event EventHandler MoveBegin;
        public event EventHandler MoveEnd;
        // Resize //
        public event ResizeEventHandler Resize;
        public event ResizeEventHandler ValidateResize;
        public event EventHandler ResizeBegin;
        public event EventHandler ResizeEnd;
        // Draw //
        public event DrawEventHandler Draw;
        // Focus //
        public event EventHandler FocusLost;
        public event EventHandler FocusGained;
        // Properties changed //
        public event EventHandler ColorChanged;
        public event EventHandler TextColorChanged;
        public event EventHandler BackColorChanged;
        public event EventHandler TextChanged;
        public event EventHandler AnchorChanged;
        public event EventHandler VisibleChanged;
        public event EventHandler EnabledChanged;
        public event EventHandler AlphaChanged;
        // Skin//
        public event EventHandler SkinChanging;
        public event EventHandler SkinChanged;
        // Parent and root
        public event EventHandler ParentChanged;
        public event EventHandler RootChanged;

        #endregion

        #region Constructor

        /// <summary>
        /// Control.
        /// </summary>
        public Control(UserInterfaceManager userInterfaceManager_)
        {
            UserInterfaceManager = userInterfaceManager_;
            Name = "Control";
            Parent = null;
            text = Utilities.ControlTypeName(this);
            root = this;
            // Load skin information for this control.
            InitSkin();
            // Check skin layer existance.
            CheckLayer(skinControl, "Control");
            
            SetDefaultSize(width, height);
            SetMinimumSize(MinimumWidth, MinimumHeight);
            ResizerSize = skinControl.ResizerSize;

            // Add control to the list of all controls.
            controlList.Add(this);
            newControls.Enqueue(this);

            // Events.
            UserInterfaceManager.DeviceReset += OnDeviceReset;
            UserInterfaceManager.SkinChanging += OnSkinChanging;
            UserInterfaceManager.SkinChanged += OnSkinChanged;
        } // Control

        #endregion

        #region Init

        /// <summary>
        /// Check that the skin layer exist.
        /// </summary>
        protected void CheckLayer(SkinControlInformation skinControl, string layer)
        {
            if (skinControl == null || skinControl.Layers == null || skinControl.Layers.Count == 0 || skinControl.Layers[layer] == null)
            {
                throw new InvalidOperationException("User Interface: Unable to read skin layer \"" + layer + "\" for control \"" + Utilities.ControlTypeName(this) + "\".");
            }
        } // CheckLayer

        /// <summary>
        /// Init some parameters of the control. 
        /// This method needs to be executed after the constructor call because is a virtual method that use information of the derived class.
        /// </summary>
        protected internal virtual void Init()
        {
            // Override if necessary.
        } // Init

        /// <summary>
        /// Load skin information for this control.
        /// </summary>
        protected internal virtual void InitSkin()
        {
            if (UserInterfaceManager.Skin.Controls != null)
            {
                SkinControlInformation _skinControl = UserInterfaceManager.Skin.Controls[Utilities.ControlTypeName(this)];
                if (_skinControl != null)
                    SkinInformation = _skinControl;
                else
                    SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["Control"]);
            }
            else
            {
                throw new InvalidOperationException("User Interface: Control's skin cannot be initialized. No skin loaded.");
            }
        } // InitSkin

        /// <summary>
        /// Set default size.
        /// </summary>
        protected void SetDefaultSize(int _width, int _height)
        {
            Width = skinControl.DefaultSize.Width > 0 ? skinControl.DefaultSize.Width : _width;
            Height = skinControl.DefaultSize.Height > 0 ? skinControl.DefaultSize.Height : _height;
        } // SetDefaultSize

        /// <summary>
        /// Set minimum size.
        /// </summary>
        protected virtual void SetMinimumSize(int _minimumWidth, int _minimumHeight)
        {
            MinimumWidth = skinControl.MinimumSize.Width > 0 ? skinControl.MinimumSize.Width : _minimumWidth;
            MinimumHeight = skinControl.MinimumSize.Height > 0 ? skinControl.MinimumSize.Height : _minimumHeight;
        } // SetMinimumSize

        #endregion

        #region Dispose
        
        /// <summary>
        /// Dispose managed resources.
        /// </summary>
        protected override void DisposeManagedResources()
        {
            // Remove events.
            UserInterfaceManager.DeviceReset -= OnDeviceReset;
            UserInterfaceManager.SkinChanging -= OnSkinChanging;
            UserInterfaceManager.SkinChanged -= OnSkinChanged;

            #region Own Events

            // A disposed object could be still generating events, because it is alive for a time, in a disposed state, but alive nevertheless.
            Click = null;
            DoubleClick = null;
            MouseDown = null;
            MousePress = null;
            MouseUp = null;
            MouseMove = null;
            MouseOver = null;
            MouseOut = null;
            KeyDown = null;
            KeyPress = null;
            KeyUp = null;
            Move = null;
            ValidateMove = null;
            MoveBegin = null;
            MoveEnd = null;
            Resize = null;
            ValidateResize = null;
            ResizeBegin = null;
            ResizeEnd = null;
            Draw = null;
            FocusLost = null;
            FocusGained = null;
            ColorChanged = null;
            TextColorChanged = null;
            BackColorChanged = null;
            TextChanged = null;
            AnchorChanged = null;
            VisibleChanged = null;
            EnabledChanged = null;
            AlphaChanged = null;
            SkinChanging = null;
            SkinChanged = null;
            ParentChanged = null;
            RootChanged = null;
            
            #endregion

            if (parent != null)
                parent.Remove(this);
            else
                UserInterfaceManager.Remove(this);

            if (UserInterfaceManager.OrderList != null)
                UserInterfaceManager.OrderList.Remove(this);

            // Possibly we added the menu to another parent than this control, 
            // so we dispose it manually, because in logic it belongs to this control.        
            if (ContextMenu != null)
                ContextMenu.Dispose();

            // Recursively disposing all children controls.
            // The collection might change from its children, so we check it on count greater than zero.
            if (childrenControls != null)
            {
                int childrenControlsCount = childrenControls.Count;
                for (int i = childrenControlsCount - 1; i >= 0; i--)
                {
                    childrenControls[i].Dispose();
                }
            }

            // Disposes tooltip owned by Manager        
            if (toolTip != null)
                toolTip.Dispose();

            // Removing this control from the global stack.
            controlList.Remove(this);
            // Remove object from queue to avoid a memory leak.
            if (newControls.Contains(this))
            {
                while(true)
                {
                    if (newControls.Peek() == this)
                    {
                        newControls.Dequeue();
                        break;
                    }
                    newControls.Enqueue(newControls.Peek());
                    newControls.Dequeue();
                }
            }

            if (renderTarget != null)
                renderTarget.Dispose();
        } // DisposeManagedResources

        #endregion

        #region Update

        /// <summary>
        /// Update control
        /// </summary>
        protected internal virtual void Update(float elapsedTime_)
        {
            ToolTipUpdate();

            if (childrenControls != null)
            {
                // The lines commented does not produce garbage.
                // I begin the process to reduce garbage in the user interface
                // but it’s too much work for something that probably won’t be necessary.
                /*
                int childrenControlsCount = childrenControls.Count;
                try
                {
                    // The list updateControlList needs to be clear each frame.
                    int j = 0;
                    while (updateControlList[j] != null)
                        j++;
                    for (int i = 0; i < childrenControlsCount; i++)
                    {
                        updateControlList[i + j] = childrenControls[i];
                    }
                    for (int i = 0; i < childrenControlsCount; i++)
                    {
                        updateControlList[j + i].Update();
                    }
                }
                catch (IndexOutOfRangeException)
                {*/
                    // This is the alternative that produces garbage but it does not have out of range problems.
                    ControlsList childrenControlsAuxList = new ControlsList(childrenControls);
                    foreach (Control control in childrenControlsAuxList)
                        control.Update(elapsedTime_);
                //}
            }
        } // Update

        #endregion

        #region Tool Tip

        /// <summary>
        /// Tool tip update.
        /// </summary> 
        private void ToolTipUpdate()
        {
            if (UserInterfaceManager.ToolTipsEnabled && toolTip != null && tooltipTimer > 0 &&
                (TimeSpan.FromTicks(DateTime.Now.Ticks).TotalMilliseconds - tooltipTimer) >= UserInterfaceManager.ToolTipDelay)
            {
                tooltipTimer = 0;
                toolTip.Visible = true;
                UserInterfaceManager.Add(toolTip);
            }
        } // ToolTipUpdate

        /// <summary>
        /// When the mouse pointer is over the control...
        /// </summary>
        private void ToolTipOver()
        {
            if (UserInterfaceManager.ToolTipsEnabled && toolTip != null && tooltipTimer == 0)
            {
                TimeSpan ts = new TimeSpan(DateTime.Now.Ticks);
                tooltipTimer = (long)ts.TotalMilliseconds;
            }
        } // ToolTipOver

        /// <summary>
        /// When the mouse pointer is out the control...
        /// </summary>
        private void ToolTipOut()
        {
            if (UserInterfaceManager.ToolTipsEnabled && toolTip != null)
            {
                tooltipTimer = 0;
                toolTip.Visible = false;
                UserInterfaceManager.Remove(toolTip);
            }
        } // ToolTipOut

        #endregion

        #region Draw

        /// <summary>
        /// Prepare render target and draw this control and its children in the control's render target.
        /// Later the control will be rendered into screen using this render target.
        /// </summary>
        internal virtual void PreDrawControlOntoOwnTexture()
        {
            if (visible && invalidated)
            {
                if (renderTarget == null || renderTarget.Width < ControlAndMarginsWidth || renderTarget.Height < ControlAndMarginsHeight)
                {
                    if (renderTarget != null)
                        renderTarget.Dispose();

                    int w = ControlAndMarginsWidth  + (UserInterfaceManager.TextureResizeIncrement - (ControlAndMarginsWidth  % UserInterfaceManager.TextureResizeIncrement));
                    int h = ControlAndMarginsHeight + (UserInterfaceManager.TextureResizeIncrement - (ControlAndMarginsHeight % UserInterfaceManager.TextureResizeIncrement));

                    if (h > UserInterfaceManager.Screen.Height) h = UserInterfaceManager.Screen.Height;
                    if (w > UserInterfaceManager.Screen.Width) w = UserInterfaceManager.Screen.Width;
                    
                    if (width > 0 && height > 0)
                    {
                        //AssetContentManager userContentManager = AssetContentManager.CurrentContentManager;
                        //AssetContentManager.CurrentContentManager = UserInterfaceManager.UserInterfaceContentManager;
                        renderTarget = new RenderTarget(
                            UserInterfaceManager.GraphicsDevice, new Helpers.Size(w, h, UserInterfaceManager.Screen), 
                            SurfaceFormat.Color, false, RenderTarget.AntialiasingType.NoAntialiasing)
                        {
                            Name = "User Interface Render Target"
                        };
                        //AssetContentManager.CurrentContentManager = userContentManager;
                    }
                    else 
                        renderTarget = null;
                }
                if (renderTarget != null)
                {
                    renderTarget.EnableRenderTarget();
                    renderTarget.Clear(backgroundColor);

                    Rectangle rect = new Rectangle(0, 0, ControlAndMarginsWidth, ControlAndMarginsHeight);
                
                    DrawControls(rect, false);

                    renderTarget.DisableRenderTarget();
                    invalidated = false;
                }
            }
        } // PreDrawControlOntoOwnTexture

        /// <summary>
        /// Render the control in the main render target.
        /// </summary>
        internal virtual void DrawControlOntoMainTexture()
        {
            // Some controls like the list box left the scissor rectangle in a not useful value. 
            // Therefore it is a good idea to reset it to fullscreen.
            UserInterfaceManager.GraphicsDevice.ScissorRectangle = new Rectangle(0, 0, UserInterfaceManager.Screen.Width, UserInterfaceManager.Screen.Height);
            if (visible && renderTarget != null)
            {
                UserInterfaceManager.Renderer.Begin();
                UserInterfaceManager.Renderer.Draw(renderTarget.Resource,
                                  ControlAndMarginsLeftAbsoluteCoordinate, ControlAndMarginsTopAbsoluteCoordinate,
                                  new Rectangle(0, 0, ControlAndMarginsWidth, ControlAndMarginsHeight),
                                  Color.FromNonPremultiplied(255, 255, 255, Alpha));
                UserInterfaceManager.Renderer.End();
                DrawDetached(this);
                DrawOutline(false);
            }
        } // DrawControlOntoMainTexture

        /// <summary>
        /// Draw control and its children.
        /// </summary>
        private void DrawControls(Rectangle rectangle, bool firstDetach)
        {
            UserInterfaceManager.Renderer.Begin();

            DrawingRectangle = rectangle;

            DrawEventArgs args = new DrawEventArgs { Rectangle = rectangle, };
            OnDraw(args);

            DrawControl(rectangle);

            UserInterfaceManager.Renderer.End();

            DrawChildControls(firstDetach);
        } // DrawControls

        /// <summary>
        /// Draw children controls.
        /// </summary>
        private void DrawChildControls(bool firstDetachedLevel)
        {
            if (childrenControls != null)
            {
                foreach (Control childControl in childrenControls)
                {
                    // We skip detached controls for first level (after root) because they are rendered separately in the Draw method.
                    if (((childControl.Root == childControl.Parent && !childControl.Detached) || childControl.Root != childControl.Parent) && ControlRectangle.Intersects(childControl.ControlRectangle) && childControl.visible)
                    {
                        UserInterfaceManager.GraphicsDevice.ScissorRectangle = ClippingRectangle(childControl);

                        // The position relative to its parent plus its width and height.
                        Rectangle rect = new Rectangle(childControl.ControlAndMarginsLeftAbsoluteCoordinate - root.ControlLeftAbsoluteCoordinate, childControl.ControlAndMarginsTopAbsoluteCoordinate - root.ControlTopAbsoluteCoordinate, childControl.ControlAndMarginsWidth, childControl.ControlAndMarginsHeight);
                        if (childControl.Root != childControl.Parent && ((!childControl.Detached && CheckDetached(childControl)) || firstDetachedLevel))
                        {
                            rect = new Rectangle(childControl.ControlAndMarginsLeftAbsoluteCoordinate, childControl.ControlAndMarginsTopAbsoluteCoordinate, childControl.ControlAndMarginsWidth, childControl.ControlAndMarginsHeight);
                            // If is off the screen there is not need for a scissor rectangle
                            if (rect.X < UserInterfaceManager.Screen.Width && rect.Y < UserInterfaceManager.Screen.Height && rect.X + rect.Width > 0 && rect.Y + rect.Height > 0)
                            {
                                // If part of it is off the screen we reduce the rectangle.
                                if (rect.X + rect.Width > UserInterfaceManager.Screen.Width)
                                {
                                    rect.Width = rect.Width - (rect.X + rect.Width - UserInterfaceManager.Screen.Width);
                                }
                                if (rect.Y + rect.Height > UserInterfaceManager.Screen.Height)
                                {
                                    rect.Height = rect.Height - (rect.Y + rect.Height - UserInterfaceManager.Screen.Height);
                                }
                                if (rect.X < 0 )
                                {
                                    rect.Width = rect.Width + rect.X;
                                    rect.X = 0;
                                }
                                if (rect.Y < 0)
                                {
                                    rect.Height = rect.Height + rect.Y;
                                    rect.Y = 0;
                                }
                                // We enable the scissor test.
                                UserInterfaceManager.GraphicsDevice.ScissorRectangle = rect;
                            }
                        }

                        UserInterfaceManager.Renderer.Begin();
                        childControl.DrawingRectangle = rect;

                        DrawEventArgs args = new DrawEventArgs { Rectangle = rect, };
                        childControl.OnDraw(args);

                        childControl.DrawControl(rect);

                        UserInterfaceManager.Renderer.End();

                        childControl.DrawChildControls(firstDetachedLevel);

                        childControl.DrawOutline(true);
                    }
                }
            }
        } // DrawChildControls

        /// <summary>
        /// Draw detached.
        /// </summary>
        private static void DrawDetached(Control control)
        {
            if (control.ChildrenControls != null)
            {
                foreach (Control c in control.ChildrenControls)
                {
                    if (c.Detached && c.Visible)
                    {
                        c.DrawControls(new Rectangle(c.ControlAndMarginsLeftAbsoluteCoordinate,
                                                     c.ControlAndMarginsTopAbsoluteCoordinate, 
                                                     c.ControlAndMarginsWidth, c.ControlAndMarginsHeight), true);
                    }
                }
            }
        } // DrawDetached

        /// <summary>
        /// Draw Outline.
        /// </summary>
        private void DrawOutline(bool child)
        {
            if (!OutlineRectangle.IsEmpty)
            {
                Rectangle r = OutlineRectangle;
                if (child)
                {
                    r = new Rectangle(OutlineRectangle.Left + (parent.ControlLeftAbsoluteCoordinate - root.ControlLeftAbsoluteCoordinate), OutlineRectangle.Top + (parent.ControlTopAbsoluteCoordinate - root.ControlTopAbsoluteCoordinate), OutlineRectangle.Width, OutlineRectangle.Height);
                }

                Texture2D t = UserInterfaceManager.Skin.Controls["Control.Outline"].Layers[0].Image.Texture.Resource;

                int s = resizerSize;
                Rectangle r1 = new Rectangle(r.Left + HorizontalScrollingAmount, r.Top + VerticalScrollingAmount, r.Width, s);
                Rectangle r2 = new Rectangle(r.Left + HorizontalScrollingAmount, r.Top + s + VerticalScrollingAmount, resizerSize, r.Height - (2 * s));
                Rectangle r3 = new Rectangle(r.Right - s + HorizontalScrollingAmount, r.Top + s + VerticalScrollingAmount, s, r.Height - (2 * s));
                Rectangle r4 = new Rectangle(r.Left + HorizontalScrollingAmount, r.Bottom - s + VerticalScrollingAmount, r.Width, s);

                Color c = UserInterfaceManager.Skin.Controls["Control.Outline"].Layers[0].States.Enabled.Color;

                UserInterfaceManager.Renderer.Begin();
                if ((ResizeEdge & Anchors.Top) == Anchors.Top || !partialOutline) UserInterfaceManager.Renderer.Draw(t, r1, c);
                if ((ResizeEdge & Anchors.Left) == Anchors.Left || !partialOutline) UserInterfaceManager.Renderer.Draw(t, r2, c);
                if ((ResizeEdge & Anchors.Right) == Anchors.Right || !partialOutline) UserInterfaceManager.Renderer.Draw(t, r3, c);
                if ((ResizeEdge & Anchors.Bottom) == Anchors.Bottom || !partialOutline) UserInterfaceManager.Renderer.Draw(t, r4, c);
                UserInterfaceManager.Renderer.End();
            }
            else if (DesignMode && Focused)
            {
                Rectangle r = ControlRectangleRelativeToParent;
                if (child)
                {
                    r = new Rectangle(r.Left + (parent.ControlLeftAbsoluteCoordinate - root.ControlLeftAbsoluteCoordinate), r.Top + (parent.ControlTopAbsoluteCoordinate - root.ControlTopAbsoluteCoordinate), r.Width, r.Height);
                }

                Texture2D t = UserInterfaceManager.Skin.Controls["Control.Outline"].Layers[0].Image.Texture.Resource;

                int s = resizerSize;
                Rectangle r1 = new Rectangle(r.Left + HorizontalScrollingAmount, r.Top + VerticalScrollingAmount, r.Width, s);
                Rectangle r2 = new Rectangle(r.Left + HorizontalScrollingAmount, r.Top + s + VerticalScrollingAmount, resizerSize, r.Height - (2 * s));
                Rectangle r3 = new Rectangle(r.Right - s + HorizontalScrollingAmount, r.Top + s + VerticalScrollingAmount, s, r.Height - (2 * s));
                Rectangle r4 = new Rectangle(r.Left + HorizontalScrollingAmount, r.Bottom - s + VerticalScrollingAmount, r.Width, s);

                Color c = UserInterfaceManager.Skin.Controls["Control.Outline"].Layers[0].States.Enabled.Color;

                UserInterfaceManager.Renderer.Begin();
                UserInterfaceManager.Renderer.Draw(t, r1, c);
                UserInterfaceManager.Renderer.Draw(t, r2, c);
                UserInterfaceManager.Renderer.Draw(t, r3, c);
                UserInterfaceManager.Renderer.Draw(t, r4, c);
                UserInterfaceManager.Renderer.End();
            } 
        } // DrawOutline

        /// <summary>
        /// Draw control.
        /// </summary>
        protected virtual void DrawControl(Rectangle rect)
        {
            if (backgroundColor != UndefinedColor && backgroundColor != Color.Transparent)
            {
                UserInterfaceManager.Renderer.Draw(UserInterfaceManager.Skin.Images["Control"].Texture.Resource, rect, backgroundColor);
            }
            // This is a little concession that I did.
            // If the control is a container then it is not renderer, except its children.
            if (Utilities.ControlTypeName(this) != "Container")
                UserInterfaceManager.Renderer.DrawLayer(this, skinControl.Layers[0], rect);
        } // DrawControl

        /// <summary>
        /// Get clipping rectangle from control c
        /// </summary>
        private Rectangle ClippingRectangle(Control c)
        {
            Rectangle rectangle = new Rectangle(c.ControlAndMarginsLeftAbsoluteCoordinate - root.ControlLeftAbsoluteCoordinate,
                                                c.ControlAndMarginsTopAbsoluteCoordinate - root.ControlTopAbsoluteCoordinate,
                                                c.ControlAndMarginsWidth, c.ControlAndMarginsHeight);

            int x1 = rectangle.Left;
            int x2 = rectangle.Right;
            int y1 = rectangle.Top;
            int y2 = rectangle.Bottom;

            Control control = c.Parent;
            while (control != null)
            {
                int cx1 = control.ControlAndMarginsLeftAbsoluteCoordinate - root.ControlLeftAbsoluteCoordinate;
                int cy1 = control.ControlAndMarginsTopAbsoluteCoordinate - root.ControlTopAbsoluteCoordinate;
                int cx2 = cx1 + control.ControlAndMarginsWidth;
                int cy2 = cy1 + control.ControlAndMarginsHeight;

                if (x1 < cx1) x1 = cx1;
                if (y1 < cy1) y1 = cy1;
                if (x2 > cx2) x2 = cx2;
                if (y2 > cy2) y2 = cy2;

                control = control.Parent;
            }

            int fx2 = x2 - x1;
            int fy2 = y2 - y1;

            if (x1 < 0) x1 = 0;
            if (y1 < 0) y1 = 0;
            if (fx2 < 0) fx2 = 0;
            if (fy2 < 0) fy2 = 0;
            if (x1 > root.Width) x1 = root.Width;
            if (y1 > root.Height) y1 = root.Height;
            if (fx2 > root.Width) fx2 = root.Width;
            if (fy2 > root.Height) fy2 = root.Height;

            return new Rectangle(x1, y1, fx2, fy2);
        } // ClippingRectangle

        /// <summary>
        /// Check if a control is detached
        /// </summary>
        private static bool CheckDetached(Control c)
        {
            Control parent = c.Parent;
            while (parent != null)
            {
                if (parent.Detached)
                {
                    return true;
                }
                parent = parent.Parent;
            }
            return c.Detached;
        } // CheckDetached

        #endregion

        #region Add, Remove, Containt, and search child control by name.

        /// <summary>
        /// Add a control as child of this control.
        /// </summary>
        public virtual void Add(Control control)
        {
            if (control != null)
            {
                if (!childrenControls.Contains(control))
                {
                    if (control.Parent != null)
                        control.Parent.Remove(control);
                    else
                        UserInterfaceManager.Remove(control);

                    control.parent = this;
                    control.Root = root;
                    control.Enabled = (Enabled ? control.Enabled : Enabled);
                    childrenControls.Add(control);

                    Resize += control.OnParentResize;

                    control.SetAnchorMargins();

                    if (!Suspended) 
                        OnParentChanged(new EventArgs());
                }
            }
        } // Add

        /// <summary>
        /// Remove a control as child of this control.
        /// </summary>
        public virtual void Remove(Control control)
        {
            if (control != null)
            {
                if (control.Focused && control.Root != null) 
                    control.Root.Focused = true;
                else if (control.Focused) 
                    control.Focused = false;

                childrenControls.Remove(control);

                control.parent = null;
                control.Root = control;
                
                Resize -= control.OnParentResize;

                if (!Suspended) 
                    OnParentChanged(new EventArgs());
            }
        } // Remove

        /// <summary>
        /// Search for a control.
        /// </summary>
        public virtual bool Contains(Control control, bool recursively = true)
        {
            if (ChildrenControls != null)
            {
                foreach (Control c in ChildrenControls)
                {
                    if (c == control)
                        return true;
                    if (recursively && c.Contains(control))
                        return true;
                }
            }
            return false;
        } // Contains

        /// <summary>
        /// Search for a children control by its name.
        /// </summary>
        /// <param name="name">Control's name</param>
        public virtual Control SearchChildControlByName(string name)
        {
            Control ret = null;
            foreach (Control c in ChildrenControls)
            {
                if (c.Name.ToLower() == name.ToLower())
                {
                    ret = c;
                    break;
                }
                ret = c.SearchChildControlByName(name);
                if (ret != null)
                    break;
            }
            return ret;
        } // SearchChildControlByName

        #endregion

        #region Invalidate

        /// <summary>
        /// Invalidate it and its parents.
        /// </summary>
        public virtual void Invalidate()
        {
            invalidated = true;
            if (parent != null)
                parent.Invalidate();
        } // Invalidate

        #endregion

        #region Bring front or back

        /// <summary>
        /// Bring control to front.
        /// </summary>
        public void BringToFront()
        {
            UserInterfaceManager.BringToFront(this);
        } // BringToFront

        /// <summary>
        /// Send control to back.
        /// </summary>
        public void SendToBack()
        {
            UserInterfaceManager.SendToBack(this);
        } // SendToBack

        #endregion

        #region Show, Hide, Refresh

        /// <summary>
        /// Show control
        /// </summary>
        public virtual void Show()
        {
            Visible = true;
        } // Show

        /// <summary>
        /// Hide control.
        /// </summary>
        public virtual void Hide()
        {
            Visible = false;
        } // Hide

        /// <summary>
        /// Refresh control
        /// </summary>
        public virtual void Refresh()
        {
            OnMove(new MoveEventArgs(left, top, left, top));
            OnResize(new ResizeEventArgs(width, height, width, height));
        } // Refresh

        #endregion
        
        #region Send Message

        /// <summary>
        /// Send message.
        /// </summary>
        internal virtual void SendMessage(Message message, EventArgs e)
        {
            switch (message)
            {
                case Message.Click:
                    {
                        ClickProcess(e as MouseEventArgs);
                        break;
                    }
                case Message.MouseDown:
                    {
                        MouseDownProcess(e as MouseEventArgs);
                        break;
                    }
                case Message.MouseUp:
                    {
                        MouseUpProcess(e as MouseEventArgs);
                        break;
                    }
                case Message.MousePress:
                    {
                        MousePressProcess(e as MouseEventArgs);
                        break;
                    }
                case Message.MouseMove:
                    {
                        MouseMoveProcess(e as MouseEventArgs);
                        break;
                    }
                case Message.MouseOver:
                    {
                        MouseOverProcess(e as MouseEventArgs);
                        break;
                    }
                case Message.MouseOut:
                    {
                        MouseOutProcess(e as MouseEventArgs);
                        break;
                    }
                case Message.KeyDown:
                    {
                        KeyDownProcess(e as KeyEventArgs);
                        break;
                    }
                case Message.KeyUp:
                    {
                        KeyUpProcess(e as KeyEventArgs);
                        break;
                    }
                case Message.KeyPress:
                    {
                        KeyPressProcess(e as KeyEventArgs);
                        break;
                    }
            }
        } // SendMessage

        #endregion

        #region Keyboard

        private void KeyPressProcess(KeyEventArgs e)
        {
            Invalidate();
            if (!Suspended) OnKeyPress(e);
        } // KeyPressProcess

        private void KeyDownProcess(KeyEventArgs e)
        {
            Invalidate();

            ToolTipOut();

            if (e.Key == Microsoft.Xna.Framework.Input.Keys.Space && !IsPressed)
            {
                pressed[(int)MouseButton.None] = true;
            }

            if (!Suspended) OnKeyDown(e);
        } // KeyDownProcess

        private void KeyUpProcess(KeyEventArgs e)
        {
            Invalidate();

            if (e.Key == Microsoft.Xna.Framework.Input.Keys.Space && pressed[(int)MouseButton.None])
            {
                pressed[(int)MouseButton.None] = false;
            }

            if (!Suspended) OnKeyUp(e);

            if (e.Key == Microsoft.Xna.Framework.Input.Keys.Apps && !e.Handled)
            {
                if (ContextMenu != null)
                {
                    ContextMenu.Show(this, ControlLeftAbsoluteCoordinate + 8, ControlTopAbsoluteCoordinate + 8);
                }
            }
        } // KeyUpProcess

        #endregion

        #region Mouse

        /// <summary>
        /// Mouse Down Process
        /// </summary>
        private void MouseDownProcess(MouseEventArgs e)
        {
            Invalidate();
            pressed[(int)e.Button] = true;

            if (e.Button == MouseButton.Left)
            {
                pressSpot = new Point(TransformPosition(e).Position.X, TransformPosition(e).Position.Y);

                if (CheckResizableArea(e.Position))
                {
                    pressDiff[0] = pressSpot.X;
                    pressDiff[1] = pressSpot.Y;
                    pressDiff[2] = Width - pressSpot.X;
                    pressDiff[3] = Height - pressSpot.Y;

                    IsResizing = true;
                    if (outlineResizing) 
                        OutlineRectangle = ControlRectangleRelativeToParent;
                    if (!Suspended) 
                        OnResizeBegin(e);
                }
                else if (CheckMovableArea(e.Position))
                {
                    IsMoving = true;
                    if (outlineMoving) 
                        OutlineRectangle = ControlRectangleRelativeToParent;
                    if (!Suspended) 
                        OnMoveBegin(e);
                }
            }

            ToolTipOut();

            if (!Suspended) 
                OnMouseDown(TransformPosition(e));
        } // MouseDownProcess

        /// <summary>
        /// Mouse Up Process
        /// </summary>
        private void MouseUpProcess(MouseEventArgs e)
        {
            Invalidate();
            if (pressed[(int)e.Button] || IsMoving || IsResizing)
            {
                pressed[(int)e.Button] = false;

                if (e.Button == MouseButton.Left)
                {
                    if (IsResizing)
                    {
                        IsResizing = false;
                        if (outlineResizing)
                        {
                            Left = OutlineRectangle.Left;
                            Top = OutlineRectangle.Top;
                            Width = OutlineRectangle.Width;
                            Height = OutlineRectangle.Height;
                            OutlineRectangle = Rectangle.Empty;
                        }
                        if (!Suspended) OnResizeEnd(e);
                    }
                    else if (IsMoving)
                    {
                        IsMoving = false;
                        if (outlineMoving)
                        {
                            Left = OutlineRectangle.Left;
                            Top = OutlineRectangle.Top;
                            OutlineRectangle = Rectangle.Empty;
                        }
                        if (!Suspended) OnMoveEnd(e);
                    }
                }
                if (!Suspended) OnMouseUp(TransformPosition(e));
            }
        } // MouseUpProcess

        /// <summary>
        /// Mouse Press Process
        /// </summary>
        private void MousePressProcess(MouseEventArgs e)
        {
            if (pressed[(int)e.Button] && !IsMoving && !IsResizing)
            {
                if (!Suspended) 
                    OnMousePress(TransformPosition(e));
            }
        } // MousePressProcess

        /// <summary>
        /// Mouse Over Process
        /// </summary>
        private void MouseOverProcess(MouseEventArgs e)
        {
            Invalidate();
            hovered = true;
            ToolTipOver();

            #if (WINDOWS)
                if (Cursor != null && UserInterfaceManager.Cursor != Cursor) 
                    UserInterfaceManager.Cursor = Cursor;
            #endif

            if (!Suspended) 
                OnMouseOver(e);
        } // MouseOverProcess

        /// <summary>
        /// Mouse Out Process
        /// </summary>
        private void MouseOutProcess(MouseEventArgs e)
        {
            Invalidate();
            hovered = false;
            ToolTipOut();

            #if (WINDOWS)
                UserInterfaceManager.Cursor = UserInterfaceManager.Skin.Cursors["Default"].Cursor;
            #endif

            if (!Suspended)
                OnMouseOut(e);
        } // MouseOutProcess

        /// <summary>
        /// Mouse Move Process
        /// </summary>
        private void MouseMoveProcess(MouseEventArgs e)
        {
            if (CheckPosition(e.Position) && !inside)
            {
                inside = true;
                Invalidate();
            }
            else if (!CheckPosition(e.Position) && inside)
            {
                inside = false;
                Invalidate();
            }

            PerformResize(e);

            if (!IsResizing && IsMoving)
            {
                int x = (parent != null) ? parent.ControlLeftAbsoluteCoordinate : 0;
                int y = (parent != null) ? parent.ControlTopAbsoluteCoordinate : 0;

                int l = e.Position.X - x - pressSpot.X - HorizontalScrollingAmount;
                int t = e.Position.Y - y - pressSpot.Y - VerticalScrollingAmount;

                if (!Suspended)
                {
                    MoveEventArgs v = new MoveEventArgs(l, t, Left, Top);
                    OnValidateMove(v);

                    l = v.Left;
                    t = v.Top;
                }

                if (outlineMoving)
                {
                    OutlineRectangle = new Rectangle(l, t, OutlineRectangle.Width, OutlineRectangle.Height);
                    if (parent != null) parent.Invalidate();
                }
                else
                {
                    Left = l;
                    Top = t;
                }
            }

            if (!Suspended)
            {
                OnMouseMove(TransformPosition(e));
            }
        } // MouseMoveProcess

        /// <summary>
        /// Click Process
        /// </summary>
        private void ClickProcess(EventArgs e)
        {
            long timer = (long)TimeSpan.FromTicks(DateTime.Now.Ticks).TotalMilliseconds;

            MouseEventArgs ex = (e is MouseEventArgs) ? (MouseEventArgs)e : new MouseEventArgs();

            if ((doubleClickTimer == 0 || (timer - doubleClickTimer > UserInterfaceManager.DoubleClickTime)) || !doubleClicks)
            {
                TimeSpan ts = new TimeSpan(DateTime.Now.Ticks);
                doubleClickTimer = (long)ts.TotalMilliseconds;
                doubleClickButton = ex.Button;

                if (!Suspended) OnClick(e);
            }
            else if (timer - doubleClickTimer <= UserInterfaceManager.DoubleClickTime && (ex.Button == doubleClickButton && ex.Button != MouseButton.None))
            {
                doubleClickTimer = 0;
                if (!Suspended) OnDoubleClick(e);
            }
            else
            {
                doubleClickButton = MouseButton.None;
            }

            if (ex.Button == MouseButton.Right && ContextMenu != null && !e.Handled)
            {
                ContextMenu.Show(this, ex.Position.X, ex.Position.Y);
            }
        } // ClickProcess

        /// <summary>
        /// Check Position
        /// </summary>
        private bool CheckPosition(Point pos)
        {
            if ((pos.X >= ControlLeftAbsoluteCoordinate) && (pos.X < ControlLeftAbsoluteCoordinate + Width))
            {
                if ((pos.Y >= ControlTopAbsoluteCoordinate) && (pos.Y < ControlTopAbsoluteCoordinate + Height))
                {
                    return true;
                }
            }
            return false;
        } // CheckPosition

        /// <summary>
        /// Check Movable Area
        /// </summary>
        private bool CheckMovableArea(Point pos)
        {
            if (movable)
            {
                Rectangle rect = movableArea;

                if (rect == Rectangle.Empty)
                {
                    rect = new Rectangle(0, 0, width, height);
                }

                pos.X -= ControlLeftAbsoluteCoordinate;
                pos.Y -= ControlTopAbsoluteCoordinate;

                if ((pos.X >= rect.X) && (pos.X < rect.X + rect.Width))
                {
                    if ((pos.Y >= rect.Y) && (pos.Y < rect.Y + rect.Height))
                    {
                        return true;
                    }
                }
            }
            return false;
        } // CheckMovableArea

        /// <summary>
        /// Check Resizable Area
        /// </summary>
        private bool CheckResizableArea(Point pos)
        {
            if (resizable)
            {
                pos.X -= ControlLeftAbsoluteCoordinate;
                pos.Y -= ControlTopAbsoluteCoordinate;

                if ((pos.X >= 0 && pos.X < resizerSize && pos.Y >= 0 && pos.Y < Height) ||
                    (pos.X >= Width - resizerSize && pos.X < Width && pos.Y >= 0 && pos.Y < Height) ||
                    (pos.Y >= 0 && pos.Y < resizerSize && pos.X >= 0 && pos.X < Width) ||
                    (pos.Y >= Height - resizerSize && pos.Y < Height && pos.X >= 0 && pos.X < Width))
                {
                    return true;
                }
            }
            return false;
        } // CheckResizableArea

        /// <summary>
        /// Transform Position
        /// </summary>
        private MouseEventArgs TransformPosition(MouseEventArgs e)
        {
            MouseEventArgs ee = new MouseEventArgs(e.State, e.Button, e.Position) { Difference = e.Difference };

            ee.Position.X = ee.State.X - ControlLeftAbsoluteCoordinate;
            ee.Position.Y = ee.State.Y - ControlTopAbsoluteCoordinate;
            return ee;
        } // TransformPosition

        #endregion

        #region Anchors

        private void SetAnchorMargins()
        {
            if (Parent != null)
            {
                anchorMargins.Left = Left;
                anchorMargins.Top = Top;
                anchorMargins.Right  = Parent.VirtualWidth  - Width - Left;
                anchorMargins.Bottom = Parent.VirtualHeight - Height - Top;
            }
            else
            {
                anchorMargins = new Margins();
            }
        } // SetAnchorMargins

        /// <summary>
        /// Process anchor on parent resize.
        /// </summary>
        private void ProcessAnchor(ResizeEventArgs e)
        {
            int parentVirtualWidth, parentVirtualHeight;
            // If the user Interface is the father...
            if (Parent == null)
            {
                parentVirtualWidth = UserInterfaceManager.Screen.Width;
                parentVirtualHeight = UserInterfaceManager.Screen.Height;
            }
            else // If it is a control...
            {
                parentVirtualWidth = Parent.VirtualWidth;
                parentVirtualHeight = Parent.VirtualHeight;
            }

            // Right (but not left)
            if (((Anchor & Anchors.Right) == Anchors.Right) && ((Anchor & Anchors.Left) != Anchors.Left))
            {
                Left = parentVirtualWidth - Width - anchorMargins.Right;
            }
            // Left and Right
            else if (((Anchor & Anchors.Right) == Anchors.Right) && ((Anchor & Anchors.Left) == Anchors.Left))
            {
                Width = parentVirtualWidth - Left - anchorMargins.Right;
            }
            // No left nor right
            else if (((Anchor & Anchors.Right) != Anchors.Right) && ((Anchor & Anchors.Left) != Anchors.Left))
            {
                int diff = (e.Width - e.OldWidth);
                if (e.Width % 2 != 0 && diff != 0)
                {
                    diff += (diff / Math.Abs(diff));
                }
                Left += (diff / 2);
            }
            // Bottom (but not top)
            if (((Anchor & Anchors.Bottom) == Anchors.Bottom) && ((Anchor & Anchors.Top) != Anchors.Top))
            {
                Top = parentVirtualHeight - Height - anchorMargins.Bottom;
            }
            // Bottom and top
            else if (((Anchor & Anchors.Bottom) == Anchors.Bottom) && ((Anchor & Anchors.Top) == Anchors.Top))
            {
                Height = parentVirtualHeight - Top - anchorMargins.Bottom;
            }
            // No bottom nor top
            else if (((Anchor & Anchors.Bottom) != Anchors.Bottom) && ((Anchor & Anchors.Top) != Anchors.Top))
            {
                int diff = (e.Height - e.OldHeight);
                if (e.Height % 2 != 0 && diff != 0)
                {
                    diff += (diff / Math.Abs(diff));
                }
                Top += (diff / 2);
            }
        } // ProcessAnchor

        #endregion

        #region Resize

        /// <summary>
        /// Perform Resize.
        /// </summary>
        private void PerformResize(MouseEventArgs e)
        {
            if (resizable && !IsMoving)
            {
                if (!IsResizing)
                {
                    ResizePosition(e);
                    #if (WINDOWS)
                        UserInterfaceManager.Cursor = Cursor = ResizeCursor();
                    #endif
                }

                if (IsResizing)
                {
                    invalidated = true;
                    
                    #region Where is the resizing?

                    bool top = false;
                    bool bottom = false;
                    bool left = false;
                    bool right = false;

                    if ((resizeArea == Alignment.TopCenter || resizeArea == Alignment.TopLeft || resizeArea == Alignment.TopRight) && (resizeEdge & Anchors.Top) == Anchors.Top) 
                        top = true;
                    else if ((resizeArea == Alignment.BottomCenter || resizeArea == Alignment.BottomLeft || resizeArea == Alignment.BottomRight) && (resizeEdge & Anchors.Bottom) == Anchors.Bottom) 
                        bottom = true;

                    if ((resizeArea == Alignment.MiddleLeft || resizeArea == Alignment.BottomLeft || resizeArea == Alignment.TopLeft) && (resizeEdge & Anchors.Left) == Anchors.Left) 
                        left = true;
                    else if ((resizeArea == Alignment.MiddleRight || resizeArea == Alignment.BottomRight || resizeArea == Alignment.TopRight) && (resizeEdge & Anchors.Right) == Anchors.Right) 
                        right = true;

                    #endregion

                    int newWidth = Width;
                    int newHeight = Height;
                    int newLeft = Left;
                    int newTop = Top;

                    if (outlineResizing && !OutlineRectangle.IsEmpty)
                    {
                        newLeft = OutlineRectangle.Left;
                        newTop = OutlineRectangle.Top;
                        newWidth = OutlineRectangle.Width;
                        newHeight = OutlineRectangle.Height;
                    }

                    int px = e.Position.X - (parent != null ? parent.ControlLeftAbsoluteCoordinate : 0);
                    int py = e.Position.Y - (parent != null ? parent.ControlTopAbsoluteCoordinate : 0);

                    if (left)
                    {
                        newWidth = newWidth + (newLeft - px) + HorizontalScrollingAmount + pressDiff[0];
                        newLeft = px - HorizontalScrollingAmount - pressDiff[0] - CheckWidth(ref newWidth);
                    }
                    else if (right)
                    {
                        newWidth = px - newLeft - HorizontalScrollingAmount + pressDiff[2];
                        CheckWidth(ref newWidth);
                    }

                    if (top)
                    {
                        newHeight = newHeight + (newTop - py) + VerticalScrollingAmount + pressDiff[1];
                        newTop = py - VerticalScrollingAmount - pressDiff[1] - CheckHeight(ref newHeight);
                    }
                    else if (bottom)
                    {
                        newHeight = py - newTop - VerticalScrollingAmount + pressDiff[3];
                        CheckHeight(ref newHeight);
                    }

                    if (!Suspended)
                    {
                        ResizeEventArgs v = new ResizeEventArgs(newWidth, newHeight, Width, Height);
                        OnValidateResize(v);

                        if (top)
                        {
                            // Compensate for a possible height change from Validate event
                            newTop += (newHeight - v.Height);
                        }
                        if (left)
                        {
                            // Compensate for a possible width change from Validate event
                            newLeft += (newWidth - v.Width);
                        }
                        newWidth = v.Width;
                        newHeight = v.Height;
                    }

                    if (outlineResizing)
                    {
                        OutlineRectangle = new Rectangle(newLeft, newTop, newWidth, newHeight);
                        if (parent != null) parent.Invalidate();
                    }
                    else
                    {
                        Width = newWidth;
                        Height = newHeight;
                        Top = newTop;
                        Left = newLeft;
                    }
                }
            }
        } // PerformResize

        /// <summary>
        /// Check if the width is beyond its limits. And if that is the case modify the width to the maximum width or the minimum width.
        /// </summary>
        private int CheckWidth(ref int w)
        {
            int diff = 0;

            if (w > MaximumWidth)
            {
                diff = MaximumWidth - w;
                w = MaximumWidth;
            }
            if (w < MinimumWidth)
            {
                diff = MinimumWidth - w;
                w = MinimumWidth;
            }

            return diff;
        } // CheckWidth

        /// <summary>
        /// Check if the height is beyond its limits. And if that is the case modify the width to the maximum height or the minimum height.
        /// </summary>
        private int CheckHeight(ref int h)
        {
            int diff = 0;

            if (h > MaximumHeight)
            {
                diff = MaximumHeight - h;
                h = MaximumHeight;
            }
            if (h < MinimumHeight)
            {
                diff = MinimumHeight - h;
                h = MinimumHeight;
            }

            return diff;
        } // CheckHeight

        #if (WINDOWS)
            /// <summary>
            /// Get Resize Cursor
            /// </summary>
            /// <returns></returns>
            private Cursor ResizeCursor()
            {
                switch (resizeArea)
                {
                    case Alignment.TopCenter:
                        {
                            return ((resizeEdge & Anchors.Top) == Anchors.Top) ? UserInterfaceManager.Skin.Cursors["Vertical"].Cursor : Cursor;
                        }
                    case Alignment.BottomCenter:
                        {
                            return ((resizeEdge & Anchors.Bottom) == Anchors.Bottom) ? UserInterfaceManager.Skin.Cursors["Vertical"].Cursor : Cursor;
                        }
                    case Alignment.MiddleLeft:
                        {
                            return ((resizeEdge & Anchors.Left) == Anchors.Left) ? UserInterfaceManager.Skin.Cursors["Horizontal"].Cursor : Cursor;
                        }
                    case Alignment.MiddleRight:
                        {
                            return ((resizeEdge & Anchors.Right) == Anchors.Right) ? UserInterfaceManager.Skin.Cursors["Horizontal"].Cursor : Cursor;
                        }
                    case Alignment.TopLeft:
                        {
                            return ((resizeEdge & Anchors.Left) == Anchors.Left && (resizeEdge & Anchors.Top) == Anchors.Top) ? UserInterfaceManager.Skin.Cursors["DiagonalLeft"].Cursor : Cursor;
                        }
                    case Alignment.BottomRight:
                        {
                            return ((resizeEdge & Anchors.Bottom) == Anchors.Bottom && (resizeEdge & Anchors.Right) == Anchors.Right) ? UserInterfaceManager.Skin.Cursors["DiagonalLeft"].Cursor : Cursor;
                        }
                    case Alignment.TopRight:
                        {
                            return ((resizeEdge & Anchors.Top) == Anchors.Top && (resizeEdge & Anchors.Right) == Anchors.Right) ? UserInterfaceManager.Skin.Cursors["DiagonalRight"].Cursor : Cursor;
                        }
                    case Alignment.BottomLeft:
                        {
                            return ((resizeEdge & Anchors.Bottom) == Anchors.Bottom && (resizeEdge & Anchors.Left) == Anchors.Left) ? UserInterfaceManager.Skin.Cursors["DiagonalRight"].Cursor : Cursor;
                        }
                }
                return UserInterfaceManager.Skin.Cursors["Default"].Cursor;
            } // ResizeCursor
        #endif

        /// <summary>
        /// Resize Position.
        /// </summary>
        private void ResizePosition(MouseEventArgs e)
        {
            int x = e.Position.X - ControlLeftAbsoluteCoordinate;
            int y = e.Position.Y - ControlTopAbsoluteCoordinate;
            bool l = false, t = false, r = false, b = false;

            resizeArea = Alignment.None;

            if (CheckResizableArea(e.Position))
            {
                if (x < resizerSize) l = true;
                if (x >= Width - resizerSize) r = true;
                if (y < resizerSize) t = true;
                if (y >= Height - resizerSize) b = true;

                if (l && t) resizeArea = Alignment.TopLeft;
                else if (l && b) resizeArea = Alignment.BottomLeft;
                else if (r && t) resizeArea = Alignment.TopRight;
                else if (r && b) resizeArea = Alignment.BottomRight;
                else if (l) resizeArea = Alignment.MiddleLeft;
                else if (t) resizeArea = Alignment.TopCenter;
                else if (r) resizeArea = Alignment.MiddleRight;
                else if (b) resizeArea = Alignment.BottomCenter;
            }
            else
            {
                resizeArea = Alignment.None;
            }
        } // ResizePosition

        #endregion

        #region Handlers

        protected internal void OnDeviceReset(object sender, EventArgs e)
        {
            if (!e.Handled)
                Invalidate();
        } // OnDeviceSettingsChanged

        protected virtual void OnMouseUp(MouseEventArgs e)
        {
            if (MouseUp != null) 
                MouseUp.Invoke(this, e);
        } // OnMouseUp

        protected virtual void OnMouseDown(MouseEventArgs e)
        {
            if (MouseDown != null)
                MouseDown.Invoke(this, e);
        } // OnMouseDown

        protected virtual void OnMouseMove(MouseEventArgs e)
        {
            if (MouseMove != null) 
                MouseMove.Invoke(this, e);
        } // OnMouseMove

        protected virtual void OnMouseOver(MouseEventArgs e)
        {
            if (MouseOver != null)
                MouseOver.Invoke(this, e);
        } // OnMouseOver

        protected virtual void OnMouseOut(MouseEventArgs e)
        {
            if (MouseOut != null) 
                MouseOut.Invoke(this, e);
        } // OnMouseOut

        protected virtual void OnClick(EventArgs e)
        {
            if (Click != null) 
                Click.Invoke(this, e);
        } // OnClick

        protected virtual void OnDoubleClick(EventArgs e)
        {
            if (DoubleClick != null) 
                DoubleClick.Invoke(this, e);
        } // OnDoubleClick

        protected virtual void OnMove(MoveEventArgs e)
        {
            if (parent != null) 
                parent.Invalidate();
            if (Move != null)
                Move.Invoke(this, e);
        } // OnMove

        protected virtual void OnResize(ResizeEventArgs e)
        {
            Invalidate();
            if (Resize != null) 
                Resize.Invoke(this, e);
        } // OnResize

        protected virtual void OnValidateResize(ResizeEventArgs e)
        {
            if (ValidateResize != null)
                ValidateResize.Invoke(this, e);
        } // OnValidateResize

        protected virtual void OnValidateMove(MoveEventArgs e)
        {
            if (ValidateMove != null) 
                ValidateMove.Invoke(this, e);
        } // OnValidateMove

        protected virtual void OnMoveBegin(EventArgs e)
        {
            if (MoveBegin != null) 
                MoveBegin.Invoke(this, e);
        } // OnMoveBegin

        protected virtual void OnMoveEnd(EventArgs e)
        {
            if (MoveEnd != null) 
                MoveEnd.Invoke(this, e);
        } // OnMoveEnd

        protected virtual void OnResizeBegin(EventArgs e)
        {
            if (ResizeBegin != null) 
                ResizeBegin.Invoke(this, e);
        } // OnResizeBegin

        protected virtual void OnResizeEnd(EventArgs e)
        {
            if (ResizeEnd != null) 
                ResizeEnd.Invoke(this, e);
        } // OnResizeEnd

        internal virtual void OnParentResize(object sender, ResizeEventArgs e)
        {
            ProcessAnchor(e);
        } // OnParentResize

        protected virtual void OnKeyUp(KeyEventArgs e)
        {
            if (KeyUp != null) 
                KeyUp.Invoke(this, e);
        } // OnKeyUp

        protected virtual void OnKeyDown(KeyEventArgs e)
        {
            if (KeyDown != null) 
                KeyDown.Invoke(this, e);
        } // OnKeyDown

        protected virtual void OnKeyPress(KeyEventArgs e)
        {
            if (KeyPress != null) 
                KeyPress.Invoke(this, e);
        } // OnKeyPress

        protected internal void OnDraw(DrawEventArgs e)
        {
            if (Draw != null) 
                Draw.Invoke(this, e);
        } // OnDraw
        
        protected virtual void OnColorChanged(EventArgs e)
        {
            if (ColorChanged != null) 
                ColorChanged.Invoke(this, e);
        } // OnColorChanged

        protected virtual void OnTextColorChanged(EventArgs e)
        {
            if (TextColorChanged != null) 
                TextColorChanged.Invoke(this, e);
        } // OnTextColorChanged

        protected virtual void OnBackColorChanged(EventArgs e)
        {
            if (BackColorChanged != null) 
                BackColorChanged.Invoke(this, e);
        } // OnBackColorChanged

        protected virtual void OnTextChanged(EventArgs e)
        {
            if (TextChanged != null) 
                TextChanged.Invoke(this, e);
        } // OnTextChanged

        protected virtual void OnAnchorChanged(EventArgs e)
        {
            if (AnchorChanged != null) 
                AnchorChanged.Invoke(this, e);
        } // OnAnchorChanged

        protected internal virtual void OnSkinChanged(EventArgs e)
        {
            if (SkinChanged != null) 
                SkinChanged.Invoke(this, e);
        } // OnSkinChanged

        protected internal virtual void OnSkinChanging(EventArgs e)
        {
            if (SkinChanging != null) 
                SkinChanging.Invoke(this, e);
        } // OnSkinChanged

        protected virtual void OnParentChanged(EventArgs e)
        {
            if (ParentChanged != null) 
                ParentChanged.Invoke(this, e);
        } // OnParentChanged

        protected virtual void OnRootChanged(EventArgs e)
        {
            if (RootChanged != null)
                RootChanged.Invoke(this, e);
        } // OnRootChanged

        protected virtual void OnVisibleChanged(EventArgs e)
        {
            if (VisibleChanged != null) 
                VisibleChanged.Invoke(this, e);
        } // OnVisibleChanged

        protected virtual void OnEnabledChanged(EventArgs e)
        {
            if (EnabledChanged != null) 
                EnabledChanged.Invoke(this, e);
        } // OnEnabledChanged

        protected virtual void OnAlphaChanged(EventArgs e)
        {
            if (AlphaChanged != null) 
                AlphaChanged.Invoke(this, e);
        } // OnAlphaChanged

        protected virtual void OnMousePress(MouseEventArgs e)
        {
            if (MousePress != null) 
                MousePress.Invoke(this, e);
        } // OnMousePress

        protected virtual void OnFocusLost()
        {
            if (FocusLost != null)
                FocusLost.Invoke(this, new EventArgs());
        } // OnFocusLost

        protected virtual void OnFocusGained()
        {
            if (FocusGained != null)
                FocusGained.Invoke(this, new EventArgs());
        } // OnFocusGained

        #endregion

        #region Initialize New Controls (static)

        /// <summary>
        /// There is a big problem when there is a call to virtual method in a constructor. 
        /// In these cases the virtual call needs to be avoided, but a call to a virtual member after a new is ugly and error prone, for that reason this method exist.
        /// However the calls need to be in the opposite order, because a control creation inside a control creation can raise an exception if
        /// the virtual method of the latter is called before the former.
        /// </summary>
        internal static void InitializeNewControls()
        {
            while (newControls.Count > 0)
            {
                newControls.Peek().Init();
                newControls.Dequeue();
            }
        } // InitializeNewControls

        #endregion

    } // Control
} // XNAFinalEngine.UserInterface