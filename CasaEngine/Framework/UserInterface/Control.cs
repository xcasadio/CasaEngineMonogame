
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using CasaEngine.Core;
using CasaEngine.Core.Helpers;
using CasaEngine.Framework.Assets.Textures;
using CasaEngine.Framework.UserInterface.Controls.Auxiliary;
using CasaEngine.Framework.UserInterface.Controls.Menu;
using Microsoft.Xna.Framework.Graphics;
using Cursor = CasaEngine.Framework.UserInterface.Cursors.Cursor;
using ToolTip = CasaEngine.Framework.UserInterface.Controls.Auxiliary.ToolTip;

namespace CasaEngine.Framework.UserInterface;

public class ControlsList : EventedList<Control>
{
    public ControlsList() { }

    public ControlsList(int capacity) : base(capacity) { }

    public ControlsList(IEnumerable<Control> collection) : base(collection) { }

} // ControlsList

public class Control : Disposable
{
    public static readonly Color UndefinedColor = new(255, 255, 255, 0);

    // List of all controls.
    private static readonly ControlsList controlList = new();

    // List of all child controls.
    private readonly ControlsList _childrenControls = new();

    // Specifies how many pixels is used for edges (and corners) allowing resizing of the control.
    private int _resizerSize = 4;

    // Rectangular area that reacts on moving the control with the mouse.
    private Rectangle _movableArea = Rectangle.Empty;

    // Parent control.
    private Control _parent;

    // The root control.
    private Control _root;

    // Indicates whether this control can receive focus. 
    private bool _canFocus = true;

    // Indicates whether this control can be moved by the mouse.
    private bool _movable;

    // Indicate whether this control can be resized by the mouse.
    private bool _resizable;

    // Indicates whether this control should process mouse double-clicks.
    private bool _doubleClicks = true;

    //  Indicates whether this control should use ouline resizing.
    private bool _outlineResizing;

    // Indicates whether this control should use outline moving.
    private bool _outlineMoving;

    // Indicates the distance from another control. Usable with StackPanel control.
    private Margins _margins = new(4, 4, 4, 4);

    // Indicates whether the control outline is displayed only for certain edges. 
    private bool _partialOutline = true;

    // Indicates whether the control is allowed to be brought in the front.
    private bool _stayOnBack;

    // Indicates that the control should stay on top of other controls.
    private bool _stayOnTop;

    // Control's tool tip.
    internal ToolTip toolTip;

    // The area where is the control supposed to be drawn.
    private Rectangle _drawingRect = Rectangle.Empty;

    // The skin parameters used for rendering the control.
    private SkinControlInformation _skinControl;

    // Indicates whether the control can respond to user interaction.
    private bool _enabled = true;

    // Indicates whether the control is rendered.
    private bool _visible = true;

    // The color for the control.
    private Color _color = UndefinedColor;

    // Text color for the control.
    private Color _textColor = UndefinedColor;

    // The background color for the control.
    private Color _backgroundColor = Color.Transparent;

    // The alpha value for this control.
    private byte _alpha = 255;

    // The edges of the container to which a control is bound and determines how a control is resized with its parent.
    private Anchors _anchor = Anchors.Left | Anchors.Top;

    // The width of the control.
    private int _width = 64;

    // The height of the control.
    private int _height = 64;

    // The distance, in pixels, between the left edge of the control and the left edge of its parent.
    private int _left;

    // The distance, in pixels, between the top edge of the control and the top edge of its parent.
    private int _top;

    // The minimum width in pixels the control can be sized to.
    private int _minimumWidth;

    // The maximum width in pixels the control can be sized to.
    private int _maximumWidth = 4096;

    // The minimum height in pixels the control can be sized to.
    private int _minimumHeight;

    // The maximum height in pixels the control can be sized to.
    private int _maximumHeight = 4096;

    // Stack that stores new controls.
    private static readonly Queue<Control> NewControls = new();

    private Anchors _resizeEdge = Anchors.All;
    private string _text = "Control";
    private long _tooltipTimer;
    private long _doubleClickTimer;
    private MouseButton _doubleClickButton = MouseButton.None;
    private bool _invalidated = true;
    private RenderTarget _renderTarget;
    private Point _pressSpot = Point.Zero;
    private readonly int[] _pressDiff = new int[4];
    private Alignment _resizeArea = Alignment.None;
    private bool _hovered;
    private bool _inside;
    private readonly bool[] _pressed = new bool[32];
    private Margins _anchorMargins;
    private Rectangle _outlineRectangle = Rectangle.Empty;

    public static ControlsList ControlList => controlList;

    internal virtual int VirtualHeight
    {
        get
        {
            if (Parent is Container && (Parent as Container).AutoScroll)
            {
                // So it is a client area...
                var maxHeight = 0;
                foreach (var childControl in ChildrenControls)
                {
                    if ((childControl.Anchor & Anchors.Bottom) != Anchors.Bottom && childControl.Visible)
                    {
                        if (childControl.Top + childControl.Height > maxHeight)
                        {
                            maxHeight = childControl.Top + childControl.Height;
                        }
                    }
                }
                if (maxHeight < Height)
                {
                    maxHeight = Height;
                }

                return maxHeight;
            }
            return Height;
        }
    } // VirtualHeight

    internal virtual int VirtualWidth
    {
        get
        {
            if (Parent is Container && (Parent as Container).AutoScroll)
            {
                // So it is a client area...
                var maxWidth = 0;

                foreach (var c in ChildrenControls)
                {
                    if ((c.Anchor & Anchors.Right) != Anchors.Right && c.Visible)
                    {
                        if (c.Left + c.Width > maxWidth)
                        {
                            maxWidth = c.Left + c.Width;
                        }
                    }
                }
                if (maxWidth < Width)
                {
                    maxWidth = Width;
                }

                return maxWidth;
            }
            return Width;
        }
    } // VirtualWidth

    public virtual int Left
    {
        get => _left;
        set
        {
            if (_left != value)
            {
                var old = _left;
                _left = value;

                SetAnchorMargins();

                if (!Suspended)
                {
                    OnMove(new MoveEventArgs(_left, _top, old, _top));
                }
            }
        }
    } // Left

    public virtual int Top
    {
        get => _top;
        set
        {
            if (_top != value)
            {
                var old = _top;
                _top = value;

                SetAnchorMargins();

                if (!Suspended)
                {
                    OnMove(new MoveEventArgs(_left, _top, _left, old));
                }
            }
        }
    } // Top

    public virtual int Width
    {
        get => _width;
        set
        {
            if (_width != value)
            {
                var old = _width;
                _width = value;

                if (_skinControl != null)
                {
                    if (_width + _skinControl.OriginMargins.Horizontal > MaximumWidth)
                    {
                        _width = MaximumWidth - _skinControl.OriginMargins.Horizontal;
                    }
                }
                else
                {
                    if (_width > MaximumWidth)
                    {
                        _width = MaximumWidth;
                    }
                }
                if (_width < MinimumWidth)
                {
                    _width = MinimumWidth;
                }

                if (_width > MinimumWidth)
                {
                    SetAnchorMargins();
                }

                if (!Suspended)
                {
                    OnResize(new ResizeEventArgs(_width, _height, old, _height));
                }
            }
        }
    } // Width

    public virtual int Height
    {
        get => _height;
        set
        {
            if (_height != value)
            {
                var old = _height;

                _height = value;

                if (_skinControl != null)
                {
                    if (_height + _skinControl.OriginMargins.Vertical > MaximumHeight)
                    {
                        _height = MaximumHeight - _skinControl.OriginMargins.Vertical;
                    }
                }
                else
                {
                    if (_height > MaximumHeight)
                    {
                        _height = MaximumHeight;
                    }
                }
                if (_height < MinimumHeight)
                {
                    _height = MinimumHeight;
                }

                if (_height > MinimumHeight)
                {
                    SetAnchorMargins();
                }

                if (!Suspended)
                {
                    OnResize(new ResizeEventArgs(_width, _height, _width, old));
                }
            }

        }
    } // Height

    public virtual int MinimumWidth
    {
        get => _minimumWidth;
        set
        {
            _minimumWidth = value;
            if (_minimumWidth < 0)
            {
                _minimumWidth = 0;
            }

            if (_minimumWidth > _maximumWidth)
            {
                _minimumWidth = _maximumWidth;
            }

            if (_width < MinimumWidth)
            {
                Width = MinimumWidth;
            }
        }
    } // MinimumWidth

    public virtual int MinimumHeight
    {
        get => _minimumHeight;
        set
        {
            _minimumHeight = value;
            if (_minimumHeight < 0)
            {
                _minimumHeight = 0;
            }

            if (_minimumHeight > _maximumHeight)
            {
                _minimumHeight = _maximumHeight;
            }

            if (_height < MinimumHeight)
            {
                Height = MinimumHeight;
            }
        }
    } // MinimumHeight

    public virtual int MaximumWidth
    {
        get
        {
            var max = _maximumWidth;
            if (max > UserInterfaceManager.Screen.Width)
            {
                max = UserInterfaceManager.Screen.Width;
            }

            return max;
        }
        set
        {
            _maximumWidth = value;
            if (_maximumWidth < _minimumWidth)
            {
                _maximumWidth = _minimumWidth;
            }

            if (_width > MaximumWidth)
            {
                Width = MaximumWidth;
            }
        }
    } // MaximumWidth

    public virtual int MaximumHeight
    {
        get
        {
            var max = _maximumHeight;
            if (max > UserInterfaceManager.Screen.Height)
            {
                max = UserInterfaceManager.Screen.Height;
            }

            return max;
        }
        set
        {
            _maximumHeight = value;
            if (_maximumHeight < _minimumHeight)
            {
                _maximumHeight = _minimumHeight;
            }

            if (_height > MaximumHeight)
            {
                Height = MaximumHeight;
            }
        }
    } // MaximumHeight

    internal virtual int HorizontalScrollingAmount { get; set; }

    internal virtual int VerticalScrollingAmount { get; set; }

    internal virtual int ControlLeftAbsoluteCoordinate
    {
        get
        {
            if (_parent == null)
            {
                return _left + HorizontalScrollingAmount;
            }

            if (_parent.SkinInformation == null)
            {
                return _parent.ControlLeftAbsoluteCoordinate + _left + HorizontalScrollingAmount;
            }

            return _parent.ControlLeftAbsoluteCoordinate + _left - _parent.SkinInformation.OriginMargins.Left + HorizontalScrollingAmount;
        }
    } // ControlLeftAbsoluteCoordinate

    internal virtual int ControlTopAbsoluteCoordinate
    {
        get
        {
            if (_parent == null)
            {
                return _top + VerticalScrollingAmount;
            }

            if (_parent.SkinInformation == null)
            {
                return _parent.ControlTopAbsoluteCoordinate + _top + VerticalScrollingAmount;
            }

            return _parent.ControlTopAbsoluteCoordinate + _top - _parent.SkinInformation.OriginMargins.Top + VerticalScrollingAmount;
        }
    } // ControlTopAbsoluteCoordinate

    protected virtual int ControlAndMarginsLeftAbsoluteCoordinate
    {
        get
        {
            if (_skinControl == null)
            {
                return ControlLeftAbsoluteCoordinate;
            }

            return ControlLeftAbsoluteCoordinate - _skinControl.OriginMargins.Left;
        }
    } // ControlAndMarginsLeftAbsoluteCoordinate

    protected virtual int ControlAndMarginsTopAbsoluteCoordinate
    {
        get
        {
            if (_skinControl == null)
            {
                return ControlTopAbsoluteCoordinate;
            }

            return ControlTopAbsoluteCoordinate - _skinControl.OriginMargins.Top;
        }
    } // ControlAndMarginsTopAbsoluteCoordinate

    internal virtual int ControlAndMarginsWidth
    {
        get
        {
            if (_skinControl == null)
            {
                return _width;
            }

            return _width + _skinControl.OriginMargins.Left + _skinControl.OriginMargins.Right;
        }
    } // ControlAndMarginsWidth

    internal virtual int ControlAndMarginsHeight
    {
        get
        {
            if (_skinControl == null)
            {
                return _height;
            }

            return _height + _skinControl.OriginMargins.Top + _skinControl.OriginMargins.Bottom;
        }
    } // ControlAndMarginsHeight

    public virtual Margins ClientMargins { get; set; }

    public virtual int ClientLeft => ClientMargins.Left;

    public virtual int ClientTop => ClientMargins.Top;

    public virtual int ClientWidth
    {
        get => ControlAndMarginsWidth - ClientMargins.Left - ClientMargins.Right;
        set => Width = value + ClientMargins.Horizontal - _skinControl.OriginMargins.Horizontal;
    } // ClientWidth

    public virtual int ClientHeight
    {
        get => ControlAndMarginsHeight - ClientMargins.Top - ClientMargins.Bottom;
        set => Height = value + ClientMargins.Vertical - _skinControl.OriginMargins.Vertical;
    } // ClientHeight

    internal virtual Rectangle ControlRectangle => new(ControlLeftAbsoluteCoordinate, ControlTopAbsoluteCoordinate, ControlAndMarginsWidth, ControlAndMarginsHeight); // ControlRectangle

    protected virtual Rectangle ControlAndMarginsRectangle => new(ControlAndMarginsLeftAbsoluteCoordinate, ControlAndMarginsTopAbsoluteCoordinate, ControlAndMarginsWidth, ControlAndMarginsHeight); // ControlAndMarginsRectangle

    protected virtual Rectangle ClientRectangle => new(ClientLeft, ClientTop, ClientWidth, ClientHeight); // ClientRectangle

    internal virtual Rectangle ControlRectangleRelativeToParent => new(Left, Top, Width, Height); // ControlRectangleRelativeToParent

    private Rectangle OutlineRectangle
    {
        get => _outlineRectangle;
        set
        {
            _outlineRectangle = value;
            if (value != Rectangle.Empty)
            {
                if (_outlineRectangle.Width > MaximumWidth)
                {
                    _outlineRectangle.Width = MaximumWidth;
                }

                if (_outlineRectangle.Height > MaximumHeight)
                {
                    _outlineRectangle.Height = MaximumHeight;
                }

                if (_outlineRectangle.Width < MinimumWidth)
                {
                    _outlineRectangle.Width = MinimumWidth;
                }

                if (_outlineRectangle.Height < MinimumHeight)
                {
                    _outlineRectangle.Height = MinimumHeight;
                }
            }
        }
    } // OutlineRectangle

    internal virtual Margins DefaultDistanceFromAnotherControl
    {
        get => _margins;
        set => _margins = value;
    }

    internal void SetPosition(int left, int top)
    {
        _left = left;
        _top = top;
    } // SetPosition

    public UserInterfaceManager UserInterfaceManager { get; private set; }

#if (WINDOWS)
    public Cursor Cursor { get; set; }
#endif

    public virtual ControlsList ChildrenControls => _childrenControls;

    public virtual Rectangle MovableArea
    {
        get => _movableArea;
        set => _movableArea = value;
    }

    public virtual bool IsChild => _parent != null;

    public virtual bool IsParent => _childrenControls != null && _childrenControls.Count > 0;

    public virtual bool IsRoot => _root == this;

    public virtual bool CanFocus
    {
        get => _canFocus;
        set => _canFocus = value;
    }

    public virtual bool Detached { get; set; }

    public virtual bool Passive { get; set; }

    public virtual bool Movable
    {
        get => _movable;
        set => _movable = value;
    }

    public virtual bool Resizable
    {
        get => _resizable;
        set => _resizable = value;
    }

    public virtual int ResizerSize
    {
        get => _resizerSize;
        set => _resizerSize = value;
    }

    public virtual ContextMenu ContextMenu { get; set; }

    public virtual bool DoubleClicks
    {
        get => _doubleClicks;
        set => _doubleClicks = value;
    }

    public virtual bool OutlineResizing
    {
        get => _outlineResizing;
        set => _outlineResizing = value;
    }

    public virtual bool OutlineMoving
    {
        get => _outlineMoving;
        set => _outlineMoving = value;
    }

    public virtual bool DesignMode { get; set; }

    public virtual bool PartialOutline
    {
        get => _partialOutline;
        set => _partialOutline = value;
    } // PartialOutline

    public virtual bool StayOnBack
    {
        get => _stayOnBack;
        set
        {
            if (value && _stayOnTop)
            {
                _stayOnTop = false;
            }

            _stayOnBack = value;
        }
    } // StayOnBack

    public virtual bool StayOnTop
    {
        get => _stayOnTop;
        set
        {
            if (value && _stayOnBack)
            {
                _stayOnBack = false;
            }

            _stayOnTop = value;
        }
    } // StayOnTop

    public string Name { get; set; }

    public virtual bool Focused
    {
        get => UserInterfaceManager.FocusedControl == this;
        set
        {
            Invalidate();
            var previousValue = Focused;
            if (value)
            {
                UserInterfaceManager.FocusedControl = this;
                if (!Suspended && !previousValue)
                {
                    OnFocusGained();
                }

                if (Focused && Root != null && Root is Container)
                {
                    (Root as Container).ScrollTo(this);
                }
            }
            else
            {
                if (UserInterfaceManager.FocusedControl == this)
                {
                    UserInterfaceManager.FocusedControl = null;
                }

                if (!Suspended && previousValue)
                {
                    OnFocusLost();
                }
            }
        }
    } // Focused

    public virtual ControlState ControlState
    {
        get
        {
            if (DesignMode)
            {
                return ControlState.Enabled;
            }

            if (Suspended)
            {
                return ControlState.Disabled;
            }

            if (!_enabled)
            {
                return ControlState.Disabled;
            }

            if (IsPressed && _inside || Focused && IsPressed)
            {
                return ControlState.Pressed;
            }

            if (_hovered && !IsPressed)
            {
                return ControlState.Hovered;
            }

            if (Focused && !_inside || _hovered && IsPressed && !_inside || Focused && !_hovered && _inside)
            {
                return ControlState.Focused;
            }

            return ControlState.Enabled;
        }
    } // ControlState

    public virtual ToolTip ToolTip
    {
        get => toolTip ?? (toolTip = new ToolTip(UserInterfaceManager) { Visible = false });
        set => toolTip = value;
    } // ToolTip

    protected internal virtual bool IsPressed
    {
        get
        {
            for (var i = 0; i < _pressed.Length - 1; i++)
            {
                if (_pressed[i])
                {
                    return true;
                }
            }
            return false;
        }
    } // IsPressed

    public Rectangle DrawingRectangle
    {
        get => _drawingRect;
        private set => _drawingRect = value;
    } // DrawingRect

    public virtual bool Suspended { get; set; }

    protected internal virtual bool Hovered => _hovered;

    protected internal virtual bool Inside => _inside;

    protected internal virtual bool[] Pressed => _pressed;

    protected virtual bool IsMoving { get; set; }

    protected virtual bool IsResizing { get; set; }

    public virtual Anchors Anchor
    {
        get => _anchor;
        set
        {
            _anchor = value;
            SetAnchorMargins();
            if (!Suspended)
            {
                OnAnchorChanged(new EventArgs());
            }
        }
    } // Anchor

    public virtual Anchors ResizeEdge
    {
        get => _resizeEdge;
        set => _resizeEdge = value;
    } // ResizeEdge

    internal virtual SkinControlInformation SkinInformation
    {
        get => _skinControl;
        set
        {
            _skinControl = value;
            ClientMargins = _skinControl.ClientMargins;
        }
    } // SkinControlInformation

    public virtual string Text
    {
        get => _text;
        set
        {
            _text = value;
            Invalidate();
            if (!Suspended)
            {
                OnTextChanged(new EventArgs());
            }
        }
    } // Text

    public virtual byte Alpha
    {
        get => _alpha;
        set
        {
            _alpha = value;
            if (!Suspended)
            {
                OnAlphaChanged(new EventArgs());
            }
        }
    } // Alpha

    public virtual Color BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            _backgroundColor = value;
            Invalidate();
            if (!Suspended)
            {
                OnBackColorChanged(new EventArgs());
            }
        }
    } // BackgroundColor

    public virtual Color Color
    {
        get => _color;
        set
        {
            if (value != _color)
            {
                _color = value;
                Invalidate();
                if (!Suspended)
                {
                    OnColorChanged(new EventArgs());
                }
            }
        }
    } // Color

    public virtual Color TextColor
    {
        get => _textColor;
        set
        {
            if (value != _textColor)
            {
                _textColor = value;
                Invalidate();
                if (!Suspended)
                {
                    OnTextColorChanged(new EventArgs());
                }
            }
        }
    } // TextColor

    public virtual bool Enabled
    {
        get => _enabled;
        set
        {
            if (Root != null && Root != this && !Root.Enabled && value)
            {
                return;
            }

            _enabled = value;
            Invalidate();
            foreach (var c in _childrenControls)
            {
                c.Enabled = value;
            }
            if (!Suspended)
            {
                OnEnabledChanged(new EventArgs());
            }
        }
    } // Enabled

    public virtual bool Visible
    {
        get => _visible && (_parent == null || _parent.Visible);
        set
        {
            _visible = value;
            Invalidate();
            if (!Suspended)
            {
                OnVisibleChanged(new EventArgs());
            }
        }
    } // Visible

    public virtual Control Parent
    {
        get => _parent;
        set
        {
            // IsRemoved it from old parent
            if (_parent == null)
            {
                UserInterfaceManager.Remove(_parent);
            }
            else
            {
                _parent.Remove(this);
            }

            // Add this control to the new parent
            if (value != null)
            {
                value.Add(this);
            }
            else
            {
                UserInterfaceManager.Add(this);
            }
        }
    } // Parent

    public virtual Control Root
    {
        get => _root;
        private set
        {
            if (_root != value)
            {
                _root = value;
                foreach (var c in _childrenControls)
                {
                    c.Root = _root;
                }
                if (!Suspended)
                {
                    OnRootChanged(new EventArgs());
                }
            }
        }
    } // Root

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

    public Control(UserInterfaceManager userInterfaceManager)
    {
        UserInterfaceManager = userInterfaceManager;
        Name = "Control";
        Parent = null;
        _text = Utilities.ControlTypeName(this);
        _root = this;
        // Load skin information for this control.
        InitSkin();
        // Check skin layer existance.
        CheckLayer(_skinControl, "Control");

        SetDefaultSize(_width, _height);
        SetMinimumSize(MinimumWidth, MinimumHeight);
        ResizerSize = _skinControl.ResizerSize;

        // Add control to the list of all controls.
        controlList.Add(this);
        NewControls.Enqueue(this);

        // Events.
        UserInterfaceManager.DeviceReset += OnDeviceReset;
        UserInterfaceManager.SkinChanging += OnSkinChanging;
        UserInterfaceManager.SkinChanged += OnSkinChanged;
    } // Control

    protected void CheckLayer(SkinControlInformation skinControl, string layer)
    {
        if (skinControl == null || skinControl.Layers == null || skinControl.Layers.Count == 0 || skinControl.Layers[layer] == null)
        {
            throw new InvalidOperationException("User Interface: Unable to read skin layer \"" + layer + "\" for control \"" + Utilities.ControlTypeName(this) + "\".");
        }
    } // CheckLayer

    protected internal virtual void Init()
    {
        // Override if necessary.
    } // Init

    protected internal virtual void InitSkin()
    {
        if (UserInterfaceManager.Skin.Controls != null)
        {
            var skinControl = UserInterfaceManager.Skin.Controls[Utilities.ControlTypeName(this)];
            if (skinControl != null)
            {
                SkinInformation = skinControl;
            }
            else
            {
                SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["Control"]);
            }
        }
        else
        {
            throw new InvalidOperationException("User Interface: Control's skin cannot be initialized. No skin loaded.");
        }
    } // InitSkin

    protected void SetDefaultSize(int width, int height)
    {
        Width = _skinControl.DefaultSize.Width > 0 ? _skinControl.DefaultSize.Width : width;
        Height = _skinControl.DefaultSize.Height > 0 ? _skinControl.DefaultSize.Height : height;
    } // SetDefaultSize

    protected virtual void SetMinimumSize(int minimumWidth, int minimumHeight)
    {
        MinimumWidth = _skinControl.MinimumSize.Width > 0 ? _skinControl.MinimumSize.Width : minimumWidth;
        MinimumHeight = _skinControl.MinimumSize.Height > 0 ? _skinControl.MinimumSize.Height : minimumHeight;
    } // SetMinimumSize

    protected override void DisposeManagedResources()
    {
        // IsRemoved events.
        UserInterfaceManager.DeviceReset -= OnDeviceReset;
        UserInterfaceManager.SkinChanging -= OnSkinChanging;
        UserInterfaceManager.SkinChanged -= OnSkinChanged;

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

        if (_parent != null)
        {
            _parent.Remove(this);
        }
        else
        {
            UserInterfaceManager.Remove(this);
        }

        UserInterfaceManager.OrderList?.Remove(this);

        // Possibly we added the menu to another parent than this control, 
        // so we dispose it manually, because in logic it belongs to this control.        
        ContextMenu?.Dispose();

        // Recursively disposing all children controls.
        // The collection might change from its children, so we check it on count greater than zero.
        if (_childrenControls != null)
        {
            var childrenControlsCount = _childrenControls.Count;
            for (var i = childrenControlsCount - 1; i >= 0; i--)
            {
                _childrenControls[i].Dispose();
            }
        }

        // Disposes tooltip owned by Manager        
        toolTip?.Dispose();

        // Removing this control from the global stack.
        controlList.Remove(this);
        // IsRemoved object from queue to avoid a memory leak.
        if (NewControls.Contains(this))
        {
            while (true)
            {
                if (NewControls.Peek() == this)
                {
                    NewControls.Dequeue();
                    break;
                }
                NewControls.Enqueue(NewControls.Peek());
                NewControls.Dequeue();
            }
        }

        _renderTarget?.Dispose();
    } // DisposeManagedResources

    protected internal virtual void Update(float elapsedTime)
    {
        ToolTipUpdate();

        if (_childrenControls != null)
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
            var childrenControlsAuxList = new ControlsList(_childrenControls);
            foreach (var control in childrenControlsAuxList)
                control.Update(elapsedTime);
            //}
        }
    } // Update

    private void ToolTipUpdate()
    {
        if (UserInterfaceManager.ToolTipsEnabled && toolTip != null && _tooltipTimer > 0 &&
            TimeSpan.FromTicks(DateTime.Now.Ticks).TotalMilliseconds - _tooltipTimer >= UserInterfaceManager.ToolTipDelay)
        {
            _tooltipTimer = 0;
            toolTip.Visible = true;
            UserInterfaceManager.Add(toolTip);
        }
    } // ToolTipUpdate

    private void ToolTipOver()
    {
        if (UserInterfaceManager.ToolTipsEnabled && toolTip != null && _tooltipTimer == 0)
        {
            var ts = new TimeSpan(DateTime.Now.Ticks);
            _tooltipTimer = (long)ts.TotalMilliseconds;
        }
    } // ToolTipOver

    private void ToolTipOut()
    {
        if (UserInterfaceManager.ToolTipsEnabled && toolTip != null)
        {
            _tooltipTimer = 0;
            toolTip.Visible = false;
            UserInterfaceManager.Remove(toolTip);
        }
    } // ToolTipOut

    internal virtual void PreDrawControlOntoOwnTexture()
    {
        if (_visible && _invalidated)
        {
            if (_renderTarget == null || _renderTarget.Width < ControlAndMarginsWidth || _renderTarget.Height < ControlAndMarginsHeight)
            {
                _renderTarget?.Dispose();

                var w = ControlAndMarginsWidth + (UserInterfaceManager.TextureResizeIncrement - ControlAndMarginsWidth % UserInterfaceManager.TextureResizeIncrement);
                var h = ControlAndMarginsHeight + (UserInterfaceManager.TextureResizeIncrement - ControlAndMarginsHeight % UserInterfaceManager.TextureResizeIncrement);

                if (h > UserInterfaceManager.Screen.Height)
                {
                    h = UserInterfaceManager.Screen.Height;
                }

                if (w > UserInterfaceManager.Screen.Width)
                {
                    w = UserInterfaceManager.Screen.Width;
                }

                if (_width > 0 && _height > 0)
                {
                    //AssetContentManager userContentManager = AssetContentManager.CurrentContentManager;
                    //AssetContentManager.CurrentContentManager = UserInterfaceManager.UserInterfaceContentManager;
                    _renderTarget = new RenderTarget(UserInterfaceManager.AssetContentManager,
                        UserInterfaceManager.GraphicsDevice, new ScreenSize(w, h, UserInterfaceManager.Screen),
                        SurfaceFormat.Color, false)
                    {
                        Name = "User Interface Render Target"
                    };
                    //AssetContentManager.CurrentContentManager = userContentManager;
                }
                else
                {
                    _renderTarget = null;
                }
            }
            if (_renderTarget != null)
            {
                _renderTarget.EnableRenderTarget();
                _renderTarget.Clear(_backgroundColor);

                var rect = new Rectangle(0, 0, ControlAndMarginsWidth, ControlAndMarginsHeight);

                DrawControls(rect, false);

                _renderTarget.DisableRenderTarget();
                _invalidated = false;
            }
        }
    } // PreDrawControlOntoOwnTexture

    internal virtual void DrawControlOntoMainTexture()
    {
        // Some controls like the list box left the scissor rectangle in a not useful value. 
        // Therefore it is a good idea to reset it to fullscreen.
        UserInterfaceManager.GraphicsDevice.ScissorRectangle = new Rectangle(0, 0, UserInterfaceManager.Screen.Width, UserInterfaceManager.Screen.Height);
        if (_visible && _renderTarget != null)
        {
            UserInterfaceManager.Renderer.Begin();
            UserInterfaceManager.Renderer.Draw(_renderTarget.Resource,
                ControlAndMarginsLeftAbsoluteCoordinate, ControlAndMarginsTopAbsoluteCoordinate,
                new Rectangle(0, 0, ControlAndMarginsWidth, ControlAndMarginsHeight),
                Color.FromNonPremultiplied(255, 255, 255, Alpha));
            UserInterfaceManager.Renderer.End();
            DrawDetached(this);
            DrawOutline(false);
        }
    } // DrawControlOntoMainTexture

    private void DrawControls(Rectangle rectangle, bool firstDetach)
    {
        UserInterfaceManager.Renderer.Begin();

        DrawingRectangle = rectangle;

        var args = new DrawEventArgs { Rectangle = rectangle, };
        OnDraw(args);

        DrawControl(rectangle);

        UserInterfaceManager.Renderer.End();

        DrawChildControls(firstDetach);
    } // DrawControls

    private void DrawChildControls(bool firstDetachedLevel)
    {
        if (_childrenControls != null)
        {
            foreach (var childControl in _childrenControls)
            {
                // We skip detached controls for first level (after root) because they are rendered separately in the Draw method.
                if ((childControl.Root == childControl.Parent && !childControl.Detached || childControl.Root != childControl.Parent) && ControlRectangle.Intersects(childControl.ControlRectangle) && childControl._visible)
                {
                    UserInterfaceManager.GraphicsDevice.ScissorRectangle = ClippingRectangle(childControl);

                    // The position relative to its parent plus its width and height.
                    var rect = new Rectangle(childControl.ControlAndMarginsLeftAbsoluteCoordinate - _root.ControlLeftAbsoluteCoordinate, childControl.ControlAndMarginsTopAbsoluteCoordinate - _root.ControlTopAbsoluteCoordinate, childControl.ControlAndMarginsWidth, childControl.ControlAndMarginsHeight);
                    if (childControl.Root != childControl.Parent && (!childControl.Detached && CheckDetached(childControl) || firstDetachedLevel))
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
                            if (rect.X < 0)
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

                    var args = new DrawEventArgs { Rectangle = rect, };
                    childControl.OnDraw(args);

                    childControl.DrawControl(rect);

                    UserInterfaceManager.Renderer.End();

                    childControl.DrawChildControls(firstDetachedLevel);

                    childControl.DrawOutline(true);
                }
            }
        }
    } // DrawChildControls

    private static void DrawDetached(Control control)
    {
        if (control.ChildrenControls != null)
        {
            foreach (var c in control.ChildrenControls)
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

    private void DrawOutline(bool child)
    {
        if (!OutlineRectangle.IsEmpty)
        {
            var r = OutlineRectangle;
            if (child)
            {
                r = new Rectangle(OutlineRectangle.Left + (_parent.ControlLeftAbsoluteCoordinate - _root.ControlLeftAbsoluteCoordinate), OutlineRectangle.Top + (_parent.ControlTopAbsoluteCoordinate - _root.ControlTopAbsoluteCoordinate), OutlineRectangle.Width, OutlineRectangle.Height);
            }

            var t = UserInterfaceManager.Skin.Controls["Control.Outline"].Layers[0].Image.Texture.Resource;

            var s = _resizerSize;
            var r1 = new Rectangle(r.Left + HorizontalScrollingAmount, r.Top + VerticalScrollingAmount, r.Width, s);
            var r2 = new Rectangle(r.Left + HorizontalScrollingAmount, r.Top + s + VerticalScrollingAmount, _resizerSize, r.Height - 2 * s);
            var r3 = new Rectangle(r.Right - s + HorizontalScrollingAmount, r.Top + s + VerticalScrollingAmount, s, r.Height - 2 * s);
            var r4 = new Rectangle(r.Left + HorizontalScrollingAmount, r.Bottom - s + VerticalScrollingAmount, r.Width, s);

            var c = UserInterfaceManager.Skin.Controls["Control.Outline"].Layers[0].States.Enabled.Color;

            UserInterfaceManager.Renderer.Begin();
            if ((ResizeEdge & Anchors.Top) == Anchors.Top || !_partialOutline)
            {
                UserInterfaceManager.Renderer.Draw(t, r1, c);
            }

            if ((ResizeEdge & Anchors.Left) == Anchors.Left || !_partialOutline)
            {
                UserInterfaceManager.Renderer.Draw(t, r2, c);
            }

            if ((ResizeEdge & Anchors.Right) == Anchors.Right || !_partialOutline)
            {
                UserInterfaceManager.Renderer.Draw(t, r3, c);
            }

            if ((ResizeEdge & Anchors.Bottom) == Anchors.Bottom || !_partialOutline)
            {
                UserInterfaceManager.Renderer.Draw(t, r4, c);
            }

            UserInterfaceManager.Renderer.End();
        }
        else if (DesignMode && Focused)
        {
            var r = ControlRectangleRelativeToParent;
            if (child)
            {
                r = new Rectangle(r.Left + (_parent.ControlLeftAbsoluteCoordinate - _root.ControlLeftAbsoluteCoordinate), r.Top + (_parent.ControlTopAbsoluteCoordinate - _root.ControlTopAbsoluteCoordinate), r.Width, r.Height);
            }

            var t = UserInterfaceManager.Skin.Controls["Control.Outline"].Layers[0].Image.Texture.Resource;

            var s = _resizerSize;
            var r1 = new Rectangle(r.Left + HorizontalScrollingAmount, r.Top + VerticalScrollingAmount, r.Width, s);
            var r2 = new Rectangle(r.Left + HorizontalScrollingAmount, r.Top + s + VerticalScrollingAmount, _resizerSize, r.Height - 2 * s);
            var r3 = new Rectangle(r.Right - s + HorizontalScrollingAmount, r.Top + s + VerticalScrollingAmount, s, r.Height - 2 * s);
            var r4 = new Rectangle(r.Left + HorizontalScrollingAmount, r.Bottom - s + VerticalScrollingAmount, r.Width, s);

            var c = UserInterfaceManager.Skin.Controls["Control.Outline"].Layers[0].States.Enabled.Color;

            UserInterfaceManager.Renderer.Begin();
            UserInterfaceManager.Renderer.Draw(t, r1, c);
            UserInterfaceManager.Renderer.Draw(t, r2, c);
            UserInterfaceManager.Renderer.Draw(t, r3, c);
            UserInterfaceManager.Renderer.Draw(t, r4, c);
            UserInterfaceManager.Renderer.End();
        }
    } // DrawOutline

    protected virtual void DrawControl(Rectangle rect)
    {
        if (_backgroundColor != UndefinedColor && _backgroundColor != Color.Transparent)
        {
            UserInterfaceManager.Renderer.Draw(UserInterfaceManager.Skin.Images["Control"].Texture.Resource, rect, _backgroundColor);
        }
        // This is a little concession that I did.
        // If the control is a container then it is not renderer, except its children.
        if (Utilities.ControlTypeName(this) != "Container")
        {
            UserInterfaceManager.Renderer.DrawLayer(this, _skinControl.Layers[0], rect);
        }
    } // DrawControl

    private Rectangle ClippingRectangle(Control c)
    {
        var rectangle = new Rectangle(c.ControlAndMarginsLeftAbsoluteCoordinate - _root.ControlLeftAbsoluteCoordinate,
            c.ControlAndMarginsTopAbsoluteCoordinate - _root.ControlTopAbsoluteCoordinate,
            c.ControlAndMarginsWidth, c.ControlAndMarginsHeight);

        var x1 = rectangle.Left;
        var x2 = rectangle.Right;
        var y1 = rectangle.Top;
        var y2 = rectangle.Bottom;

        var control = c.Parent;
        while (control != null)
        {
            var cx1 = control.ControlAndMarginsLeftAbsoluteCoordinate - _root.ControlLeftAbsoluteCoordinate;
            var cy1 = control.ControlAndMarginsTopAbsoluteCoordinate - _root.ControlTopAbsoluteCoordinate;
            var cx2 = cx1 + control.ControlAndMarginsWidth;
            var cy2 = cy1 + control.ControlAndMarginsHeight;

            if (x1 < cx1)
            {
                x1 = cx1;
            }

            if (y1 < cy1)
            {
                y1 = cy1;
            }

            if (x2 > cx2)
            {
                x2 = cx2;
            }

            if (y2 > cy2)
            {
                y2 = cy2;
            }

            control = control.Parent;
        }

        var fx2 = x2 - x1;
        var fy2 = y2 - y1;

        if (x1 < 0)
        {
            x1 = 0;
        }

        if (y1 < 0)
        {
            y1 = 0;
        }

        if (fx2 < 0)
        {
            fx2 = 0;
        }

        if (fy2 < 0)
        {
            fy2 = 0;
        }

        if (x1 > _root.Width)
        {
            x1 = _root.Width;
        }

        if (y1 > _root.Height)
        {
            y1 = _root.Height;
        }

        if (fx2 > _root.Width)
        {
            fx2 = _root.Width;
        }

        if (fy2 > _root.Height)
        {
            fy2 = _root.Height;
        }

        return new Rectangle(x1, y1, fx2, fy2);
    } // ClippingRectangle

    private static bool CheckDetached(Control c)
    {
        var parent = c.Parent;
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

    public virtual void Add(Control control)
    {
        if (control != null)
        {
            if (!_childrenControls.Contains(control))
            {
                if (control.Parent != null)
                {
                    control.Parent.Remove(control);
                }
                else
                {
                    UserInterfaceManager.Remove(control);
                }

                control._parent = this;
                control.Root = _root;
                control.Enabled = Enabled ? control.Enabled : Enabled;
                _childrenControls.Add(control);

                Resize += control.OnParentResize;

                control.SetAnchorMargins();

                if (!Suspended)
                {
                    OnParentChanged(new EventArgs());
                }
            }
        }
    } // Add

    public virtual void Remove(Control control)
    {
        if (control != null)
        {
            if (control.Focused && control.Root != null)
            {
                control.Root.Focused = true;
            }
            else if (control.Focused)
            {
                control.Focused = false;
            }

            _childrenControls.Remove(control);

            control._parent = null;
            control.Root = control;

            Resize -= control.OnParentResize;

            if (!Suspended)
            {
                OnParentChanged(new EventArgs());
            }
        }
    } // IsRemoved

    public virtual bool Contains(Control control, bool recursively = true)
    {
        if (ChildrenControls != null)
        {
            foreach (var c in ChildrenControls)
            {
                if (c == control)
                {
                    return true;
                }

                if (recursively && c.Contains(control))
                {
                    return true;
                }
            }
        }
        return false;
    } // Contains

    public virtual Control SearchChildControlByName(string name)
    {
        Control ret = null;
        foreach (var c in ChildrenControls)
        {
            if (c.Name.ToLower() == name.ToLower())
            {
                ret = c;
                break;
            }
            ret = c.SearchChildControlByName(name);
            if (ret != null)
            {
                break;
            }
        }
        return ret;
    } // SearchChildControlByName

    public virtual void Invalidate()
    {
        _invalidated = true;
        _parent?.Invalidate();
    } // Invalidate

    public void BringToFront()
    {
        UserInterfaceManager.BringToFront(this);
    } // BringToFront

    public void SendToBack()
    {
        UserInterfaceManager.SendToBack(this);
    } // SendToBack

    public virtual void Show()
    {
        Visible = true;
    } // Show

    public virtual void Hide()
    {
        Visible = false;
    } // Hide

    public virtual void Refresh()
    {
        OnMove(new MoveEventArgs(_left, _top, _left, _top));
        OnResize(new ResizeEventArgs(_width, _height, _width, _height));
    } // Refresh

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

    private void KeyPressProcess(KeyEventArgs e)
    {
        Invalidate();
        if (!Suspended)
        {
            OnKeyPress(e);
        }
    } // KeyPressProcess

    private void KeyDownProcess(KeyEventArgs e)
    {
        Invalidate();

        ToolTipOut();

        if (e.Key == Keys.Space && !IsPressed)
        {
            _pressed[(int)MouseButton.None] = true;
        }

        if (!Suspended)
        {
            OnKeyDown(e);
        }
    } // KeyDownProcess

    private void KeyUpProcess(KeyEventArgs e)
    {
        Invalidate();

        if (e.Key == Keys.Space && _pressed[(int)MouseButton.None])
        {
            _pressed[(int)MouseButton.None] = false;
        }

        if (!Suspended)
        {
            OnKeyUp(e);
        }

        if (e.Key == Keys.Apps && !e.Handled)
        {
            ContextMenu?.Show(this, ControlLeftAbsoluteCoordinate + 8, ControlTopAbsoluteCoordinate + 8);
        }
    } // KeyUpProcess

    private void MouseDownProcess(MouseEventArgs e)
    {
        Invalidate();
        _pressed[(int)e.Button] = true;

        if (e.Button == MouseButton.Left)
        {
            _pressSpot = new Point(TransformPosition(e).Position.X, TransformPosition(e).Position.Y);

            if (CheckResizableArea(e.Position))
            {
                _pressDiff[0] = _pressSpot.X;
                _pressDiff[1] = _pressSpot.Y;
                _pressDiff[2] = Width - _pressSpot.X;
                _pressDiff[3] = Height - _pressSpot.Y;

                IsResizing = true;
                if (_outlineResizing)
                {
                    OutlineRectangle = ControlRectangleRelativeToParent;
                }

                if (!Suspended)
                {
                    OnResizeBegin(e);
                }
            }
            else if (CheckMovableArea(e.Position))
            {
                IsMoving = true;
                if (_outlineMoving)
                {
                    OutlineRectangle = ControlRectangleRelativeToParent;
                }

                if (!Suspended)
                {
                    OnMoveBegin(e);
                }
            }
        }

        ToolTipOut();

        if (!Suspended)
        {
            OnMouseDown(TransformPosition(e));
        }
    } // MouseDownProcess

    private void MouseUpProcess(MouseEventArgs e)
    {
        Invalidate();
        if (_pressed[(int)e.Button] || IsMoving || IsResizing)
        {
            _pressed[(int)e.Button] = false;

            if (e.Button == MouseButton.Left)
            {
                if (IsResizing)
                {
                    IsResizing = false;
                    if (_outlineResizing)
                    {
                        Left = OutlineRectangle.Left;
                        Top = OutlineRectangle.Top;
                        Width = OutlineRectangle.Width;
                        Height = OutlineRectangle.Height;
                        OutlineRectangle = Rectangle.Empty;
                    }
                    if (!Suspended)
                    {
                        OnResizeEnd(e);
                    }
                }
                else if (IsMoving)
                {
                    IsMoving = false;
                    if (_outlineMoving)
                    {
                        Left = OutlineRectangle.Left;
                        Top = OutlineRectangle.Top;
                        OutlineRectangle = Rectangle.Empty;
                    }
                    if (!Suspended)
                    {
                        OnMoveEnd(e);
                    }
                }
            }
            if (!Suspended)
            {
                OnMouseUp(TransformPosition(e));
            }
        }
    } // MouseUpProcess

    private void MousePressProcess(MouseEventArgs e)
    {
        if (_pressed[(int)e.Button] && !IsMoving && !IsResizing)
        {
            if (!Suspended)
            {
                OnMousePress(TransformPosition(e));
            }
        }
    } // MousePressProcess

    private void MouseOverProcess(MouseEventArgs e)
    {
        Invalidate();
        _hovered = true;
        ToolTipOver();

#if (WINDOWS)
        if (Cursor != null && UserInterfaceManager.Cursor != Cursor)
        {
            UserInterfaceManager.Cursor = Cursor;
        }
#endif

        if (!Suspended)
        {
            OnMouseOver(e);
        }
    } // MouseOverProcess

    private void MouseOutProcess(MouseEventArgs e)
    {
        Invalidate();
        _hovered = false;
        ToolTipOut();

#if (WINDOWS)
        UserInterfaceManager.Cursor = UserInterfaceManager.Skin.Cursors["Default"].Cursor;
#endif

        if (!Suspended)
        {
            OnMouseOut(e);
        }
    } // MouseOutProcess

    private void MouseMoveProcess(MouseEventArgs e)
    {
        if (CheckPosition(e.Position) && !_inside)
        {
            _inside = true;
            Invalidate();
        }
        else if (!CheckPosition(e.Position) && _inside)
        {
            _inside = false;
            Invalidate();
        }

        PerformResize(e);

        if (!IsResizing && IsMoving)
        {
            var x = _parent != null ? _parent.ControlLeftAbsoluteCoordinate : 0;
            var y = _parent != null ? _parent.ControlTopAbsoluteCoordinate : 0;

            var l = e.Position.X - x - _pressSpot.X - HorizontalScrollingAmount;
            var t = e.Position.Y - y - _pressSpot.Y - VerticalScrollingAmount;

            if (!Suspended)
            {
                var v = new MoveEventArgs(l, t, Left, Top);
                OnValidateMove(v);

                l = v.Left;
                t = v.Top;
            }

            if (_outlineMoving)
            {
                OutlineRectangle = new Rectangle(l, t, OutlineRectangle.Width, OutlineRectangle.Height);
                _parent?.Invalidate();
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

    private void ClickProcess(EventArgs e)
    {
        var timer = (long)TimeSpan.FromTicks(DateTime.Now.Ticks).TotalMilliseconds;

        var ex = e is MouseEventArgs ? (MouseEventArgs)e : new MouseEventArgs();

        if (_doubleClickTimer == 0 || timer - _doubleClickTimer > UserInterfaceManager.DoubleClickTime || !_doubleClicks)
        {
            var ts = new TimeSpan(DateTime.Now.Ticks);
            _doubleClickTimer = (long)ts.TotalMilliseconds;
            _doubleClickButton = ex.Button;

            if (!Suspended)
            {
                OnClick(e);
            }
        }
        else if (timer - _doubleClickTimer <= UserInterfaceManager.DoubleClickTime && ex.Button == _doubleClickButton && ex.Button != MouseButton.None)
        {
            _doubleClickTimer = 0;
            if (!Suspended)
            {
                OnDoubleClick(e);
            }
        }
        else
        {
            _doubleClickButton = MouseButton.None;
        }

        if (ex.Button == MouseButton.Right && ContextMenu != null && !e.Handled)
        {
            ContextMenu.Show(this, ex.Position.X, ex.Position.Y);
        }
    } // ClickProcess

    private bool CheckPosition(Point pos)
    {
        if (pos.X >= ControlLeftAbsoluteCoordinate && pos.X < ControlLeftAbsoluteCoordinate + Width)
        {
            if (pos.Y >= ControlTopAbsoluteCoordinate && pos.Y < ControlTopAbsoluteCoordinate + Height)
            {
                return true;
            }
        }
        return false;
    } // CheckPosition

    private bool CheckMovableArea(Point pos)
    {
        if (_movable)
        {
            var rect = _movableArea;

            if (rect == Rectangle.Empty)
            {
                rect = new Rectangle(0, 0, _width, _height);
            }

            pos.X -= ControlLeftAbsoluteCoordinate;
            pos.Y -= ControlTopAbsoluteCoordinate;

            if (pos.X >= rect.X && pos.X < rect.X + rect.Width)
            {
                if (pos.Y >= rect.Y && pos.Y < rect.Y + rect.Height)
                {
                    return true;
                }
            }
        }
        return false;
    } // CheckMovableArea

    private bool CheckResizableArea(Point pos)
    {
        if (_resizable)
        {
            pos.X -= ControlLeftAbsoluteCoordinate;
            pos.Y -= ControlTopAbsoluteCoordinate;

            if (pos.X >= 0 && pos.X < _resizerSize && pos.Y >= 0 && pos.Y < Height ||
                pos.X >= Width - _resizerSize && pos.X < Width && pos.Y >= 0 && pos.Y < Height ||
                pos.Y >= 0 && pos.Y < _resizerSize && pos.X >= 0 && pos.X < Width ||
                pos.Y >= Height - _resizerSize && pos.Y < Height && pos.X >= 0 && pos.X < Width)
            {
                return true;
            }
        }
        return false;
    } // CheckResizableArea

    private MouseEventArgs TransformPosition(MouseEventArgs e)
    {
        var ee = new MouseEventArgs(e.State, e.Button, e.Position) { Difference = e.Difference };

        ee.Position.X = ee.State.X - ControlLeftAbsoluteCoordinate;
        ee.Position.Y = ee.State.Y - ControlTopAbsoluteCoordinate;
        return ee;
    } // TransformPosition

    private void SetAnchorMargins()
    {
        if (Parent != null)
        {
            _anchorMargins.Left = Left;
            _anchorMargins.Top = Top;
            _anchorMargins.Right = Parent.VirtualWidth - Width - Left;
            _anchorMargins.Bottom = Parent.VirtualHeight - Height - Top;
        }
        else
        {
            _anchorMargins = new Margins();
        }
    } // SetAnchorMargins

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
        if ((Anchor & Anchors.Right) == Anchors.Right && (Anchor & Anchors.Left) != Anchors.Left)
        {
            Left = parentVirtualWidth - Width - _anchorMargins.Right;
        }
        // Left and Right
        else if ((Anchor & Anchors.Right) == Anchors.Right && (Anchor & Anchors.Left) == Anchors.Left)
        {
            Width = parentVirtualWidth - Left - _anchorMargins.Right;
        }
        // No left nor right
        else if ((Anchor & Anchors.Right) != Anchors.Right && (Anchor & Anchors.Left) != Anchors.Left)
        {
            var diff = e.Width - e.OldWidth;
            if (e.Width % 2 != 0 && diff != 0)
            {
                diff += diff / Math.Abs(diff);
            }
            Left += diff / 2;
        }
        // Bottom (but not top)
        if ((Anchor & Anchors.Bottom) == Anchors.Bottom && (Anchor & Anchors.Top) != Anchors.Top)
        {
            Top = parentVirtualHeight - Height - _anchorMargins.Bottom;
        }
        // Bottom and top
        else if ((Anchor & Anchors.Bottom) == Anchors.Bottom && (Anchor & Anchors.Top) == Anchors.Top)
        {
            Height = parentVirtualHeight - Top - _anchorMargins.Bottom;
        }
        // No bottom nor top
        else if ((Anchor & Anchors.Bottom) != Anchors.Bottom && (Anchor & Anchors.Top) != Anchors.Top)
        {
            var diff = e.Height - e.OldHeight;
            if (e.Height % 2 != 0 && diff != 0)
            {
                diff += diff / Math.Abs(diff);
            }
            Top += diff / 2;
        }
    } // ProcessAnchor

    private void PerformResize(MouseEventArgs e)
    {
        if (_resizable && !IsMoving)
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
                _invalidated = true;

                var top = false;
                var bottom = false;
                var left = false;
                var right = false;

                if ((_resizeArea == Alignment.TopCenter || _resizeArea == Alignment.TopLeft || _resizeArea == Alignment.TopRight) && (_resizeEdge & Anchors.Top) == Anchors.Top)
                {
                    top = true;
                }
                else if ((_resizeArea == Alignment.BottomCenter || _resizeArea == Alignment.BottomLeft || _resizeArea == Alignment.BottomRight) && (_resizeEdge & Anchors.Bottom) == Anchors.Bottom)
                {
                    bottom = true;
                }

                if ((_resizeArea == Alignment.MiddleLeft || _resizeArea == Alignment.BottomLeft || _resizeArea == Alignment.TopLeft) && (_resizeEdge & Anchors.Left) == Anchors.Left)
                {
                    left = true;
                }
                else if ((_resizeArea == Alignment.MiddleRight || _resizeArea == Alignment.BottomRight || _resizeArea == Alignment.TopRight) && (_resizeEdge & Anchors.Right) == Anchors.Right)
                {
                    right = true;
                }

                var newWidth = Width;
                var newHeight = Height;
                var newLeft = Left;
                var newTop = Top;

                if (_outlineResizing && !OutlineRectangle.IsEmpty)
                {
                    newLeft = OutlineRectangle.Left;
                    newTop = OutlineRectangle.Top;
                    newWidth = OutlineRectangle.Width;
                    newHeight = OutlineRectangle.Height;
                }

                var px = e.Position.X - (_parent != null ? _parent.ControlLeftAbsoluteCoordinate : 0);
                var py = e.Position.Y - (_parent != null ? _parent.ControlTopAbsoluteCoordinate : 0);

                if (left)
                {
                    newWidth = newWidth + (newLeft - px) + HorizontalScrollingAmount + _pressDiff[0];
                    newLeft = px - HorizontalScrollingAmount - _pressDiff[0] - CheckWidth(ref newWidth);
                }
                else if (right)
                {
                    newWidth = px - newLeft - HorizontalScrollingAmount + _pressDiff[2];
                    CheckWidth(ref newWidth);
                }

                if (top)
                {
                    newHeight = newHeight + (newTop - py) + VerticalScrollingAmount + _pressDiff[1];
                    newTop = py - VerticalScrollingAmount - _pressDiff[1] - CheckHeight(ref newHeight);
                }
                else if (bottom)
                {
                    newHeight = py - newTop - VerticalScrollingAmount + _pressDiff[3];
                    CheckHeight(ref newHeight);
                }

                if (!Suspended)
                {
                    var v = new ResizeEventArgs(newWidth, newHeight, Width, Height);
                    OnValidateResize(v);

                    if (top)
                    {
                        // Compensate for a possible height change from Validate event
                        newTop += newHeight - v.Height;
                    }
                    if (left)
                    {
                        // Compensate for a possible width change from Validate event
                        newLeft += newWidth - v.Width;
                    }
                    newWidth = v.Width;
                    newHeight = v.Height;
                }

                if (_outlineResizing)
                {
                    OutlineRectangle = new Rectangle(newLeft, newTop, newWidth, newHeight);
                    _parent?.Invalidate();
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

    private int CheckWidth(ref int w)
    {
        var diff = 0;

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

    private int CheckHeight(ref int h)
    {
        var diff = 0;

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
    private Cursor ResizeCursor()
    {
        switch (_resizeArea)
        {
            case Alignment.TopCenter:
                {
                    return (_resizeEdge & Anchors.Top) == Anchors.Top ? UserInterfaceManager.Skin.Cursors["Vertical"].Cursor : Cursor;
                }
            case Alignment.BottomCenter:
                {
                    return (_resizeEdge & Anchors.Bottom) == Anchors.Bottom ? UserInterfaceManager.Skin.Cursors["Vertical"].Cursor : Cursor;
                }
            case Alignment.MiddleLeft:
                {
                    return (_resizeEdge & Anchors.Left) == Anchors.Left ? UserInterfaceManager.Skin.Cursors["Horizontal"].Cursor : Cursor;
                }
            case Alignment.MiddleRight:
                {
                    return (_resizeEdge & Anchors.Right) == Anchors.Right ? UserInterfaceManager.Skin.Cursors["Horizontal"].Cursor : Cursor;
                }
            case Alignment.TopLeft:
                {
                    return (_resizeEdge & Anchors.Left) == Anchors.Left && (_resizeEdge & Anchors.Top) == Anchors.Top ? UserInterfaceManager.Skin.Cursors["DiagonalLeft"].Cursor : Cursor;
                }
            case Alignment.BottomRight:
                {
                    return (_resizeEdge & Anchors.Bottom) == Anchors.Bottom && (_resizeEdge & Anchors.Right) == Anchors.Right ? UserInterfaceManager.Skin.Cursors["DiagonalLeft"].Cursor : Cursor;
                }
            case Alignment.TopRight:
                {
                    return (_resizeEdge & Anchors.Top) == Anchors.Top && (_resizeEdge & Anchors.Right) == Anchors.Right ? UserInterfaceManager.Skin.Cursors["DiagonalRight"].Cursor : Cursor;
                }
            case Alignment.BottomLeft:
                {
                    return (_resizeEdge & Anchors.Bottom) == Anchors.Bottom && (_resizeEdge & Anchors.Left) == Anchors.Left ? UserInterfaceManager.Skin.Cursors["DiagonalRight"].Cursor : Cursor;
                }
        }
        return UserInterfaceManager.Skin.Cursors["Default"].Cursor;
    } // ResizeCursor
#endif

    private void ResizePosition(MouseEventArgs e)
    {
        var x = e.Position.X - ControlLeftAbsoluteCoordinate;
        var y = e.Position.Y - ControlTopAbsoluteCoordinate;
        bool l = false, t = false, r = false, b = false;

        _resizeArea = Alignment.None;

        if (CheckResizableArea(e.Position))
        {
            if (x < _resizerSize)
            {
                l = true;
            }

            if (x >= Width - _resizerSize)
            {
                r = true;
            }

            if (y < _resizerSize)
            {
                t = true;
            }

            if (y >= Height - _resizerSize)
            {
                b = true;
            }

            if (l && t)
            {
                _resizeArea = Alignment.TopLeft;
            }
            else if (l && b)
            {
                _resizeArea = Alignment.BottomLeft;
            }
            else if (r && t)
            {
                _resizeArea = Alignment.TopRight;
            }
            else if (r && b)
            {
                _resizeArea = Alignment.BottomRight;
            }
            else if (l)
            {
                _resizeArea = Alignment.MiddleLeft;
            }
            else if (t)
            {
                _resizeArea = Alignment.TopCenter;
            }
            else if (r)
            {
                _resizeArea = Alignment.MiddleRight;
            }
            else if (b)
            {
                _resizeArea = Alignment.BottomCenter;
            }
        }
        else
        {
            _resizeArea = Alignment.None;
        }
    } // ResizePosition

    protected internal void OnDeviceReset(object sender, EventArgs e)
    {
        if (!e.Handled)
        {
            Invalidate();
        }
    } // OnDeviceSettingsChanged

    protected virtual void OnMouseUp(MouseEventArgs e)
    {
        MouseUp?.Invoke(this, e);
    } // OnMouseUp

    protected virtual void OnMouseDown(MouseEventArgs e)
    {
        MouseDown?.Invoke(this, e);
    } // OnMouseDown

    protected virtual void OnMouseMove(MouseEventArgs e)
    {
        MouseMove?.Invoke(this, e);
    } // OnMouseMove

    protected virtual void OnMouseOver(MouseEventArgs e)
    {
        MouseOver?.Invoke(this, e);
    } // OnMouseOver

    protected virtual void OnMouseOut(MouseEventArgs e)
    {
        MouseOut?.Invoke(this, e);
    } // OnMouseOut

    protected virtual void OnClick(EventArgs e)
    {
        Click?.Invoke(this, e);
    } // OnClick

    protected virtual void OnDoubleClick(EventArgs e)
    {
        DoubleClick?.Invoke(this, e);
    } // OnDoubleClick

    protected virtual void OnMove(MoveEventArgs e)
    {
        _parent?.Invalidate();
        Move?.Invoke(this, e);
    } // OnMove

    protected virtual void OnResize(ResizeEventArgs e)
    {
        Invalidate();
        Resize?.Invoke(this, e);
    } // OnResize

    protected virtual void OnValidateResize(ResizeEventArgs e)
    {
        ValidateResize?.Invoke(this, e);
    } // OnValidateResize

    protected virtual void OnValidateMove(MoveEventArgs e)
    {
        ValidateMove?.Invoke(this, e);
    } // OnValidateMove

    protected virtual void OnMoveBegin(EventArgs e)
    {
        MoveBegin?.Invoke(this, e);
    } // OnMoveBegin

    protected virtual void OnMoveEnd(EventArgs e)
    {
        MoveEnd?.Invoke(this, e);
    } // OnMoveEnd

    protected virtual void OnResizeBegin(EventArgs e)
    {
        ResizeBegin?.Invoke(this, e);
    } // OnResizeBegin

    protected virtual void OnResizeEnd(EventArgs e)
    {
        ResizeEnd?.Invoke(this, e);
    } // OnResizeEnd

    internal virtual void OnParentResize(object sender, ResizeEventArgs e)
    {
        ProcessAnchor(e);
    } // OnParentResize

    protected virtual void OnKeyUp(KeyEventArgs e)
    {
        KeyUp?.Invoke(this, e);
    } // OnKeyUp

    protected virtual void OnKeyDown(KeyEventArgs e)
    {
        KeyDown?.Invoke(this, e);
    } // OnKeyDown

    protected virtual void OnKeyPress(KeyEventArgs e)
    {
        KeyPress?.Invoke(this, e);
    } // OnKeyPress

    protected internal void OnDraw(DrawEventArgs e)
    {
        Draw?.Invoke(this, e);
    } // OnDraw

    protected virtual void OnColorChanged(EventArgs e)
    {
        ColorChanged?.Invoke(this, e);
    } // OnColorChanged

    protected virtual void OnTextColorChanged(EventArgs e)
    {
        TextColorChanged?.Invoke(this, e);
    } // OnTextColorChanged

    protected virtual void OnBackColorChanged(EventArgs e)
    {
        BackColorChanged?.Invoke(this, e);
    } // OnBackColorChanged

    protected virtual void OnTextChanged(EventArgs e)
    {
        TextChanged?.Invoke(this, e);
    } // OnTextChanged

    protected virtual void OnAnchorChanged(EventArgs e)
    {
        AnchorChanged?.Invoke(this, e);
    } // OnAnchorChanged

    protected internal virtual void OnSkinChanged(EventArgs e)
    {
        SkinChanged?.Invoke(this, e);
    } // OnSkinChanged

    protected internal virtual void OnSkinChanging(EventArgs e)
    {
        SkinChanging?.Invoke(this, e);
    } // OnSkinChanged

    protected virtual void OnParentChanged(EventArgs e)
    {
        ParentChanged?.Invoke(this, e);
    } // OnParentChanged

    protected virtual void OnRootChanged(EventArgs e)
    {
        RootChanged?.Invoke(this, e);
    } // OnRootChanged

    protected virtual void OnVisibleChanged(EventArgs e)
    {
        VisibleChanged?.Invoke(this, e);
    } // OnVisibleChanged

    protected virtual void OnEnabledChanged(EventArgs e)
    {
        EnabledChanged?.Invoke(this, e);
    } // OnEnabledChanged

    protected virtual void OnAlphaChanged(EventArgs e)
    {
        AlphaChanged?.Invoke(this, e);
    } // OnAlphaChanged

    protected virtual void OnMousePress(MouseEventArgs e)
    {
        MousePress?.Invoke(this, e);
    } // OnMousePress

    protected virtual void OnFocusLost()
    {
        FocusLost?.Invoke(this, new EventArgs());
    } // OnFocusLost

    protected virtual void OnFocusGained()
    {
        FocusGained?.Invoke(this, new EventArgs());
    } // OnFocusGained

    internal static void InitializeNewControls()
    {
        while (NewControls.Count > 0)
        {
            NewControls.Peek().Init();
            NewControls.Dequeue();
        }
    } // InitializeNewControls

} // Control
  // XNAFinalEngine.UserInterface