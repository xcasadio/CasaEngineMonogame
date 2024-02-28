using CasaEngine.Core.Log;
using CasaEngine.Core.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using TomShane.Neoforce.Controls.Graphics;
using TomShane.Neoforce.Controls.Input;
using TomShane.Neoforce.Controls.Serialization;
using TomShane.Neoforce.Controls.Skins;

namespace TomShane.Neoforce.Controls;

public class Control : Component
{
    public static readonly Color UndefinedColor = new(255, 255, 255, 0);

    internal static readonly ControlsList Stack = new();

    private Color _color = UndefinedColor;
    private Color _textColor = UndefinedColor;
    private Color _backColor = Color.Transparent;
    private byte _alpha = 255;
    private Anchors _anchor = Anchors.Left | Anchors.Top;
    private string _text = "Control";
    private bool _visible = true;
    private bool _enabled = true;
    private SkinControl? _skin;
    private Control? _parent;
    private Control? _root;
    private int _left;
    private int _top;
    private int _width = 64;
    private int _height = 64;
    private long _tooltipTimer;
    private long _doubleClickTimer;
    private MouseButton _doubleClickButton = MouseButton.None;
    private Type _toolTipType = typeof(ToolTip);
    private ToolTip _toolTip;

    private readonly ControlsList _controls = new();
    private bool _invalidated = true;
    private int _minimumWidth;
    private int _maximumWidth = 4096;
    private int _minimumHeight;
    private int _maximumHeight = 4096;
    private int _topModifier;
    private int _leftModifier;
    private int _virtualHeight = 64;
    private int _virtualWidth = 64;
    private bool _stayOnBack;
    private bool _stayOnTop;

    private RenderTarget2D _target;
    private Point _pressSpot = Point.Zero;
    private readonly int[] _pressDiff = new int[4];
    private Alignment _resizeArea = Alignment.None;
    private bool _hovered;
    private bool _inside;
    private readonly bool[] _pressed = new bool[32];
    private bool _isResizing;
    private Margins _anchorMargins;
    private Rectangle _outlineRect = Rectangle.Empty;
    /// <summary>
    /// Tracks the position of the mouse scroll wheel
    /// </summary>
    private int _scrollWheel = 0;

    /// <summary>
    /// Gets or sets the cursor displaying over the control.
    /// </summary>
    public Cursor Cursor { get; set; }

    /// <summary>
    /// Gets a list of all child controls.
    /// </summary>
    public IEnumerable<Control> Controls => _controls;

    /// <summary>
    /// Gets or sets a rectangular area that reacts on moving the control with the mouse.
    /// </summary>
    public Rectangle MovableArea { get; set; } = Rectangle.Empty;

    /// <summary>
    /// Gets a value indicating whether this control is a child control.
    /// </summary>
    public bool IsChild => _parent != null;

    /// <summary>
    /// Gets a value indicating whether this control is a parent control.
    /// </summary>
    public bool IsParent => _controls != null && _controls.Count > 0;

    /// <summary>
    /// Gets a value indicating whether this control is a root control.
    /// </summary>
    public bool IsRoot => _root == this;

    /// <summary>
    /// Gets or sets a value indicating whether this control can receive focus. 
    /// </summary>
    public bool CanFocus { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether this control is rendered off the parents texture.
    /// </summary>
    public bool Detached { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this controls can receive user input events.
    /// </summary>
    public bool Passive { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this control can be moved by the mouse.
    /// </summary>
    public bool Movable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this control can be resized by the mouse.
    /// </summary>
    public bool Resizable { get; set; }

    /// <summary>
    /// Gets or sets the size of the rectangular borders around the control used for resizing by the mouse.
    /// </summary>
    public int ResizerSize { get; set; } = 4;

    /// <summary>
    /// Gets or sets the ContextMenu associated with this control.
    /// </summary>
    public ContextMenu ContextMenu { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this control should process mouse double-clicks.
    /// </summary>
    public bool DoubleClicks { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether this control should use ouline resizing.
    /// </summary>
    public bool OutlineResizing { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this control should use outline moving.
    /// </summary>
    public bool OutlineMoving { get; set; }

    /// <summary>
    /// Gets or sets the object that contains data about the control.
    /// </summary>
    public object Tag { get; set; }

    /// <summary>
    /// Gets or sets the value indicating the distance from another control. Usable with StackPanel control.
    /// </summary>
    public Margins Margins { get; set; } = new(4, 4, 4, 4);

    /// <summary>
    /// Gets or sets the value indicating wheter control is in design mode.
    /// </summary>
    public bool DesignMode { get; set; }

    /// <summary>
    /// Gets or sets gamepad actions for the control.
    /// </summary>
    public GamePadActions GamePadActions { get; set; } = new();

    /// <summary>
    /// Gets or sets the value indicating whether the control outline is displayed only for certain edges. 
    /// </summary>   
    public bool PartialOutline { get; set; } = true;

    /// <summary>
    /// Gets or sets the value indicating whether the control is allowed to be brought in the front.
    /// </summary>
    public bool StayOnBack
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
    }

    /// <summary>
    /// Gets or sets the value indicating that the control should stay on top of other controls.
    /// </summary>
    public bool StayOnTop
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
    }

    /// <summary>
    /// Gets or sets a name of the control.
    /// </summary>
    public string Name { get; set; } = "Control";

    /// <summary>
    /// Gets or sets a value indicating whether this control has input focus.
    /// </summary>
    public bool Focused
    {
        get => Manager.FocusedControl == this;
        set
        {
            Invalidate();
            if (value)
            {
                var f = Focused;
                Manager.FocusedControl = this;
                if (!Suspended && value && !f)
                {
                    OnFocusGained(new EventArgs());
                }

                if (Focused && Root != null && Root is Container)
                {
                    (Root as Container).ScrollTo(this);
                }
            }
            else
            {
                var f = Focused;
                if (Manager.FocusedControl == this)
                {
                    Manager.FocusedControl = null;
                }

                if (!Suspended && !value && f)
                {
                    OnFocusLost(new EventArgs());
                }
            }
        }
    }

    /// <summary>
    /// Gets a value indicating current state of the control.
    /// </summary>
    public virtual ControlState ControlState
    {
        get
        {
            /*if (DesignMode)
            {
                return ControlState.Enabled;
            }*/

            if (Suspended && !DesignMode)
            {
                return ControlState.Disabled;
            }

            if (!_enabled && !DesignMode)
            {
                return ControlState.Disabled;
            }

            if ((IsPressed && _inside) || (Focused && IsPressed))
            {
                return ControlState.Pressed;
            }

            if (_hovered && !IsPressed)
            {
                return ControlState.Hovered;
            }

            if ((Focused && !_inside) || (_hovered && IsPressed && !_inside) || (Focused && !_hovered && _inside))
            {
                return ControlState.Focused;
            }


            return ControlState.Enabled;
        }
    }

    public Type ToolTipType
    {
        get => _toolTipType;
        set
        {
            _toolTipType = value;
            if (_toolTip != null)
            {
                _toolTip.Dispose();
                _toolTip = null;
            }
        }
    }

    public ToolTip ToolTip
    {
        get
        {
            if (_toolTip == null)
            {
                var t = new Type[] { };
                var p = new object[] { };

                _toolTip = (ToolTip)_toolTipType.GetConstructor(t).Invoke(p);
                _toolTip.Initialize(Manager);
                _toolTip.Visible = false;
            }
            return _toolTip;
        }
        set => _toolTip = value;
    }

    protected internal bool IsPressed
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
    }

    internal int TopModifier
    {
        get => _topModifier;
        set => _topModifier = value;
    }

    internal int LeftModifier
    {
        get => _leftModifier;
        set => _leftModifier = value;
    }

    internal int VirtualHeight => GetVirtualHeight();

    //set { virtualHeight = value; }
    internal int VirtualWidth => GetVirtualWidth();

    //set { virtualWidth = value; }
    /// <summary>
    /// Gets an area where is the control supposed to be drawn.
    /// </summary>
    public Rectangle DrawingRect { get; private set; } = Rectangle.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this control should receive any events.
    /// </summary>
    public bool Suspended { get; set; }

    protected internal bool Hovered => _hovered;

    protected internal bool Inside => _inside;

    protected internal bool[] Pressed => _pressed;

    /// <summary>
    /// Gets or sets a value indicating whether this controls is currently being moved.
    /// </summary>
    protected bool IsMoving { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this controls is currently being resized.
    /// </summary>
    protected bool IsResizing
    {
        get => _isResizing;
        set => _isResizing = value;
    }

    /// <summary>
    /// Gets or sets the edges of the container to which a control is bound and determines how a control is resized with its parent.
    /// </summary>
    public Anchors Anchor
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
    }

    /// <summary>
    /// Gets or sets the edges of the contol which are allowed for resizing.
    /// </summary>
    public Anchors ResizeEdge { get; set; } = Anchors.All;

    /// <summary>
    /// Gets or sets the skin used for rendering the control.
    /// </summary>
    public SkinControl? Skin
    {
        get => _skin;
        set
        {
            _skin = value;
            ClientMargins = _skin?.ClientMargins ?? ClientMargins;
        }
    }

    /// <summary>
    /// Gets or sets the text associated with this control.
    /// </summary>
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
    }

    /// <summary>
    /// Gets or sets the alpha value for this control.
    /// </summary>
    public byte Alpha
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
    }

    /// <summary>
    /// Gets or sets the background color for the control.
    /// </summary>
    public Color BackColor
    {
        get => _backColor;
        set
        {
            _backColor = value;
            Invalidate();
            if (!Suspended)
            {
                OnBackColorChanged(new EventArgs());
            }
        }
    }

    /// <summary>
    /// Gets or sets the color for the control.
    /// </summary>
    public Color Color
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
    }

    /// <summary>
    /// Gets or sets the text color for the control.
    /// </summary>
    public Color TextColor
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
    }

    /// <summary>
    /// Gets or sets a value indicating whether the control can respond to user interaction.
    /// </summary>
    public bool Enabled
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

            foreach (var c in _controls)
            {
                c.Enabled = value;
            }

            if (!Suspended)
            {
                OnEnabledChanged(new EventArgs());
            }
        }
    }

    /// <summary>
    /// Gets or sets a value that indicates whether the control is rendered.
    /// </summary>
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
    }

    /// <summary>
    /// Gets or sets the parent for the control.
    /// </summary>
    public Control? Parent
    {
        get => _parent;
        set
        {
            if (_parent != value)
            {
                if (value != null)
                {
                    value.Add(this);
                }
                else
                {
                    Manager.Add(this);
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the root for the control.
    /// </summary>
    public Control Root
    {
        get => _root;
        private set
        {
            if (_root != value)
            {
                _root = value;

                foreach (var c in _controls)
                {
                    c.Root = _root;
                }

                if (!Suspended)
                {
                    OnRootChanged(new EventArgs());
                }
            }
        }
    }

    // position in the screen : between 0 and 1. Use to adapt in any resolution
    public float LeftScreenRatio { get; set; }

    // position in the screen : between 0 and 1. Use to adapt in any resolution
    public float TopScreenRatio { get; set; }

    // between 0 and 1. Use to adapt in any resolution
    public float WidthScreenRatio { get; set; }

    //between 0 and 1. Use to adapt in any resolution
    public float HeightScreenRatio { get; set; }

    /// <summary>
    /// Gets or sets the distance, in pixels, between the left edge of the control and the left edge of its parent.
    /// </summary>
    public int Left
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
    }

    /// <summary>
    /// Gets or sets the distance, in pixels, between the top edge of the control and the top edge of its parent.
    /// </summary>
    public int Top
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
    }

    /// <summary>
    /// Gets or sets the width of the control.
    /// </summary>
    public int Width
    {
        get => _width;
        set
        {
            if (value < 0 && Parent != null && value > int.MinValue)
            {
                var mult = value / -100f;
                value = (int)(_parent.Width * mult);
            }

            if (_width != value)
            {
                var old = _width;
                _width = value;

                if (_skin != null)
                {
                    if (_width + _skin.OriginMargins.Horizontal > MaximumWidth)
                    {
                        _width = MaximumWidth - _skin.OriginMargins.Horizontal;
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

                if (_width > 0)
                {
                    SetAnchorMargins();
                }

                if (!Suspended)
                {
                    OnResize(new ResizeEventArgs(_width, _height, old, _height));
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the height of the control.
    /// </summary>
    public int Height
    {
        get => _height;
        set
        {
            if (value < 0 && Parent != null && value > int.MinValue)
            {
                var mult = value / -100f;
                value = (int)(_parent.Height * mult);
            }

            if (_height != value)
            {
                var old = _height;

                _height = value;

                if (_skin != null)
                {
                    if (_height + _skin.OriginMargins.Vertical > MaximumHeight)
                    {
                        _height = MaximumHeight - _skin.OriginMargins.Vertical;
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

                if (_height > 0)
                {
                    SetAnchorMargins();
                }

                if (!Suspended)
                {
                    OnResize(new ResizeEventArgs(_width, _height, _width, old));
                }
            }

        }

    }

    /// <summary>
    /// Gets or sets the minimum width in pixels the control can be sized to.
    /// </summary>
    public int MinimumWidth
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
    }

    /// <summary>
    /// /// Gets or sets the minimum height in pixels the control can be sized to.
    /// </summary>
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
    }

    /// <summary>
    /// /// Gets or sets the maximum width in pixels the control can be sized to.
    /// </summary>
    public int MaximumWidth
    {
        get
        {
            var max = _maximumWidth;
            if (max > Manager?.TargetWidth)
            {
                max = Manager.TargetWidth;
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
    }

    /// <summary>
    /// Gets or sets the maximum height in pixels the control can be sized to.
    /// </summary>
    public int MaximumHeight
    {
        get
        {
            var max = _maximumHeight;
            if (max > Manager?.TargetHeight)
            {
                max = Manager.TargetHeight;
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
    }

    public int AbsoluteLeft
    {
        get
        {
            if (_parent == null)
            {
                return _left + LeftModifier;
            }

            if (_parent.Skin == null)
            {
                return _parent.AbsoluteLeft + _left + LeftModifier;
            }
            return _parent.AbsoluteLeft + _left - _parent.Skin.OriginMargins.Left + LeftModifier;
        }
    }

    public int AbsoluteTop
    {
        get
        {
            if (_parent == null)
            {
                return _top + TopModifier;
            }

            if (_parent.Skin == null)
            {
                return _parent.AbsoluteTop + _top + TopModifier;
            }
            return _parent.AbsoluteTop + _top - _parent.Skin.OriginMargins.Top + TopModifier;
        }
    }

    public int OriginLeft
    {
        get
        {
            if (_skin == null)
            {
                return AbsoluteLeft;
            }

            return AbsoluteLeft - _skin.OriginMargins.Left;
        }
    }

    public int OriginTop
    {
        get
        {
            if (_skin == null)
            {
                return AbsoluteTop;
            }

            return AbsoluteTop - _skin.OriginMargins.Top;
        }
    }

    public int OriginWidth
    {
        get
        {
            if (_skin == null)
            {
                return _width;
            }

            return _width + _skin.OriginMargins.Left + _skin.OriginMargins.Right;
        }
    }

    public int OriginHeight
    {
        get
        {
            if (_skin == null)
            {
                return _height;
            }

            return _height + _skin.OriginMargins.Top + _skin.OriginMargins.Bottom;
        }
    }

    public virtual Margins ClientMargins { get; set; }

    public int ClientLeft => ClientMargins.Left;

    public int ClientTop => ClientMargins.Top;

    public int ClientWidth
    {
        get => OriginWidth - ClientMargins.Left - ClientMargins.Right;
        set => Width = value + ClientMargins.Horizontal - _skin.OriginMargins.Horizontal;
    }

    public int ClientHeight
    {
        get => OriginHeight - ClientMargins.Top - ClientMargins.Bottom;
        set => Height = value + ClientMargins.Vertical - _skin.OriginMargins.Vertical;
    }

    public Rectangle AbsoluteRect => new(AbsoluteLeft, AbsoluteTop, OriginWidth, OriginHeight);

    public Rectangle OriginRect => new(OriginLeft, OriginTop, OriginWidth, OriginHeight);

    public Rectangle ClientRect => new(ClientLeft, ClientTop, ClientWidth, ClientHeight);

    public Rectangle ControlRect
    {
        get => new(Left, Top, Width, Height);
        set
        {
            Left = value.Left;
            Top = value.Top;
            Width = value.Width;
            Height = value.Height;
        }
    }

    private Rectangle OutlineRect
    {
        get => _outlineRect;
        set
        {
            _outlineRect = value;
            if (value != Rectangle.Empty)
            {
                if (_outlineRect.Width > MaximumWidth)
                {
                    _outlineRect.Width = MaximumWidth;
                }

                if (_outlineRect.Height > MaximumHeight)
                {
                    _outlineRect.Height = MaximumHeight;
                }

                if (_outlineRect.Width < MinimumWidth)
                {
                    _outlineRect.Width = MinimumWidth;
                }

                if (_outlineRect.Height < MinimumHeight)
                {
                    _outlineRect.Height = MinimumHeight;
                }
            }
        }
    }

    public event EventHandler Click;
    public event EventHandler DoubleClick;
    public event MouseEventHandler MouseDown;
    public event MouseEventHandler MousePress;
    public event MouseEventHandler MouseUp;
    public event MouseEventHandler MouseMove;
    public event MouseEventHandler MouseOver;
    public event MouseEventHandler MouseOut;
    /// <summary>
    /// Occurs when the mouse scroll wheel position changes
    /// </summary>
    public event MouseEventHandler MouseScroll;
    public event KeyEventHandler KeyDown;
    public event KeyEventHandler KeyPress;
    public event KeyEventHandler KeyUp;
    public event GamePadEventHandler GamePadDown;
    public event GamePadEventHandler GamePadUp;
    public event GamePadEventHandler GamePadPress;
    public event MoveEventHandler Move;
    public event MoveEventHandler ValidateMove;
    public event ResizeEventHandler Resize;
    public event ResizeEventHandler ValidateResize;
    public event DrawEventHandler Draw;
    public event EventHandler MoveBegin;
    public event EventHandler MoveEnd;
    public event EventHandler ResizeBegin;
    public event EventHandler ResizeEnd;
    public event EventHandler ColorChanged;
    public event EventHandler TextColorChanged;
    public event EventHandler BackColorChanged;
    public event EventHandler TextChanged;
    public event EventHandler AnchorChanged;
    public event EventHandler SkinChanging;
    public event EventHandler SkinChanged;
    public event EventHandler ParentChanged;
    public event EventHandler RootChanged;
    public event EventHandler VisibleChanged;
    public event EventHandler EnabledChanged;
    public event EventHandler AlphaChanged;
    public event EventHandler FocusLost;
    public event EventHandler FocusGained;
    public event DrawEventHandler DrawTexture;

    public Control()
    {
        Stack.Add(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_parent != null)
            {
                _parent.Remove(this);
            }
            else
            {
                Manager?.Remove(this);
            }

            Manager.OrderList?.Remove(this);

            // Possibly we added the menu to another parent than this control, 
            // so we dispose it manually, beacause in logic it belongs to this control.        
            if (ContextMenu != null)
            {
                ContextMenu.Dispose();
                ContextMenu = null;
            }

            // Recursively disposing all controls. The collection might change from its children, 
            // so we check it on count greater than zero.
            if (_controls != null)
            {
                var c = _controls.Count;
                for (var i = 0; i < c; i++)
                {
                    if (_controls.Count > 0)
                    {
                        _controls[0].Dispose();
                    }
                }
            }

            // Disposes tooltip owned by Manager        
            if (_toolTip != null && !Manager.Disposing)
            {
                _toolTip.Dispose();
                _toolTip = null;
            }

            // Removing this control from the global stack.
            Stack.Remove(this);

            if (_target != null)
            {
                _target.Dispose();
                _target = null;
            }
        }
        base.Dispose(disposing);
    }

    private int GetVirtualHeight()
    {
        if (Parent is Container && (Parent as Container).AutoScroll)
        {
            var maxy = 0;

            foreach (var c in Controls)
            {
                if ((c.Anchor & Anchors.Bottom) != Anchors.Bottom && c.Visible)
                {
                    if (c.Top + c.Height > maxy)
                    {
                        maxy = c.Top + c.Height;
                    }
                }
            }
            if (maxy < Height)
            {
                maxy = Height;
            }

            return maxy;
        }

        return Height;
    }

    private int GetVirtualWidth()
    {
        if (Parent is Container && (Parent as Container).AutoScroll)
        {
            var maxx = 0;

            foreach (var c in Controls)
            {
                if ((c.Anchor & Anchors.Right) != Anchors.Right && c.Visible)
                {
                    if (c.Left + c.Width > maxx)
                    {
                        maxx = c.Left + c.Width;
                    }
                }
            }
            if (maxx < Width)
            {
                maxx = Width;
            }

            return maxx;
        }

        return Width;
    }

    private Rectangle GetClippingRect(Control c)
    {
        var r = Rectangle.Empty;

        r = new Rectangle(c.OriginLeft - _root.AbsoluteLeft,
            c.OriginTop - _root.AbsoluteTop,
            c.OriginWidth,
            c.OriginHeight);

        var x1 = r.Left;
        var x2 = r.Right;
        var y1 = r.Top;
        var y2 = r.Bottom;

        var ctrl = c.Parent;
        while (ctrl != null)
        {
            var cx1 = ctrl.OriginLeft - _root.AbsoluteLeft;
            var cy1 = ctrl.OriginTop - _root.AbsoluteTop;
            var cx2 = cx1 + ctrl.OriginWidth;
            var cy2 = cy1 + ctrl.OriginHeight;

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

            ctrl = ctrl.Parent;
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

        if (x1 > _root.Width) { x1 = _root.Width; }
        if (y1 > _root.Height) { y1 = _root.Height; }
        if (fx2 > _root.Width)
        {
            fx2 = _root.Width;
        }

        if (fy2 > _root.Height)
        {
            fy2 = _root.Height;
        }

        var ret = new Rectangle(x1, y1, fx2, fy2);

        return ret;
    }

    private RenderTarget2D CreateRenderTarget(int width, int height)
    {
        if (width > 0 && height > 0)
        {
            return new RenderTarget2D(Manager.GraphicsDevice,
                width,
                height,
                false,
                SurfaceFormat.Color,
                DepthFormat.None,
                Manager.GraphicsDevice.PresentationParameters.MultiSampleCount,
                Manager.RenderTargetUsage);

        }

        return null;
    }

    internal void PrepareTexture(IRenderer renderer, GameTime gameTime)
    {
        if (_visible)
        {
            if (_invalidated)
            {
                OnDrawTexture(new DrawEventArgs(renderer, new Rectangle(0, 0, OriginWidth, OriginHeight), gameTime));

                if (_target == null || _target.Width < OriginWidth || _target.Height < OriginHeight)
                {
                    if (_target != null)
                    {
                        _target.Dispose();
                        _target = null;
                    }

                    var w = OriginWidth + (Manager.TextureResizeIncrement - OriginWidth % Manager.TextureResizeIncrement);
                    var h = OriginHeight + (Manager.TextureResizeIncrement - OriginHeight % Manager.TextureResizeIncrement);

                    if (h > Manager.TargetHeight)
                    {
                        h = Manager.TargetHeight;
                    }

                    if (w > Manager.TargetWidth)
                    {
                        w = Manager.TargetWidth;
                    }

                    _target = CreateRenderTarget(w, h);
                }

                if (_target != null)
                {
                    Manager.GraphicsDevice.SetRenderTarget(_target);
                    _target.GraphicsDevice.Clear(_backColor);

                    var rect = new Rectangle(0, 0, OriginWidth, OriginHeight);
                    DrawControls(renderer, rect, gameTime, false);
                    /*
                    var fileStream = File.Create("d:\\image.png");
                    _target.SaveAsPng(fileStream, _target.Width, _target.Height);
                    fileStream.Dispose();
                    */
                    //Manager.GraphicsDevice.SetRenderTarget(Manager.DefaultRenderTarget);
                }
                _invalidated = false;
            }
        }
    }

    private bool CheckDetached(Control c)
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
    }

    private void DrawChildControls(IRenderer renderer, GameTime gameTime, bool firstDetachedLevel)
    {
        if (_controls != null)
        {
            foreach (var c in _controls)
            {
                // We skip detached controls for first level after root (they are rendered separately in Draw() method)
                if (((c.Root == c.Parent && !c.Detached) || c.Root != c.Parent) && AbsoluteRect.Intersects(c.AbsoluteRect) && c._visible)
                {
                    Manager.GraphicsDevice.ScissorRectangle = GetClippingRect(c);

                    var rect = new Rectangle(c.OriginLeft - _root.AbsoluteLeft, c.OriginTop - _root.AbsoluteTop, c.OriginWidth, c.OriginHeight);
                    if (c.Root != c.Parent && ((!c.Detached && CheckDetached(c)) || firstDetachedLevel))
                    {
                        rect = new Rectangle(c.OriginLeft, c.OriginTop, c.OriginWidth, c.OriginHeight);
                        Manager.GraphicsDevice.ScissorRectangle = rect;
                    }

                    renderer.Begin(BlendingMode.Default);
                    c.DrawingRect = rect;
                    c.DrawControl(renderer, rect, gameTime);

                    var args = new DrawEventArgs();
                    args.Rectangle = rect;
                    args.Renderer = renderer;
                    args.GameTime = gameTime;
                    c.OnDraw(args);
                    renderer.End();

                    c.DrawChildControls(renderer, gameTime, firstDetachedLevel);

                    c.DrawOutline(renderer, true);
                }
            }
        }
    }

    private void DrawControls(IRenderer renderer, Rectangle rect, GameTime gameTime, bool firstDetach)
    {
        renderer.Begin(BlendingMode.Default);

        DrawingRect = rect;
        DrawControl(renderer, rect, gameTime);

        var args = new DrawEventArgs();
        args.Rectangle = rect;
        args.Renderer = renderer;
        args.GameTime = gameTime;
        OnDraw(args);

        renderer.End();

        DrawChildControls(renderer, gameTime, firstDetach);
    }

    private void DrawDetached(Control control, IRenderer renderer, GameTime gameTime)
    {
        if (control.Controls != null)
        {
            foreach (var c in control.Controls)
            {
                if (c.Detached && c.Visible)
                {
                    c.DrawControls(renderer, new Rectangle(c.OriginLeft, c.OriginTop, c.OriginWidth, c.OriginHeight), gameTime, true);
                }
            }
        }
    }

    internal virtual void Render(IRenderer renderer, GameTime gameTime)
    {
        if (_visible && _target != null)
        {
            renderer.Begin(BlendingMode.Default);
            renderer.Draw(_target, OriginLeft, OriginTop, new Rectangle(0, 0, OriginWidth, OriginHeight), Color.FromNonPremultiplied(255, 255, 255, Alpha));
            renderer.End();

            DrawDetached(this, renderer, gameTime);

            DrawOutline(renderer, false);
        }
    }

    private void DrawOutline(IRenderer renderer, bool child)
    {
        if (!OutlineRect.IsEmpty)
        {
            var r = OutlineRect;
            if (child)
            {
                r = new Rectangle(OutlineRect.Left + (_parent.AbsoluteLeft - _root.AbsoluteLeft), OutlineRect.Top + (_parent.AbsoluteTop - _root.AbsoluteTop), OutlineRect.Width, OutlineRect.Height);
            }

            var t = Manager.Skin.Controls["Control.Outline"].Layers[0].Image.Resource;

            var s = ResizerSize;
            var r1 = new Rectangle(r.Left + _leftModifier, r.Top + _topModifier, r.Width, s);
            var r2 = new Rectangle(r.Left + _leftModifier, r.Top + s + _topModifier, ResizerSize, r.Height - 2 * s);
            var r3 = new Rectangle(r.Right - s + _leftModifier, r.Top + s + _topModifier, s, r.Height - 2 * s);
            var r4 = new Rectangle(r.Left + _leftModifier, r.Bottom - s + _topModifier, r.Width, s);

            var c = Manager.Skin.Controls["Control.Outline"].Layers[0].States.Enabled.Color;

            renderer.Begin(BlendingMode.Default);
            if ((ResizeEdge & Anchors.Top) == Anchors.Top || !PartialOutline)
            {
                renderer.Draw(t, r1, c);
            }

            if ((ResizeEdge & Anchors.Left) == Anchors.Left || !PartialOutline)
            {
                renderer.Draw(t, r2, c);
            }

            if ((ResizeEdge & Anchors.Right) == Anchors.Right || !PartialOutline)
            {
                renderer.Draw(t, r3, c);
            }

            if ((ResizeEdge & Anchors.Bottom) == Anchors.Bottom || !PartialOutline)
            {
                renderer.Draw(t, r4, c);
            }

            renderer.End();
        }
        else if (DesignMode && Focused)
        {
            var r = ControlRect;
            if (child)
            {
                r = new Rectangle(r.Left + (_parent.AbsoluteLeft - _root.AbsoluteLeft), r.Top + (_parent.AbsoluteTop - _root.AbsoluteTop), r.Width, r.Height);
            }

            var t = Manager.Skin.Controls["Control.Outline"].Layers[0].Image.Resource;

            var s = ResizerSize;
            var r1 = new Rectangle(r.Left + _leftModifier, r.Top + _topModifier, r.Width, s);
            var r2 = new Rectangle(r.Left + _leftModifier, r.Top + s + _topModifier, ResizerSize, r.Height - 2 * s);
            var r3 = new Rectangle(r.Right - s + _leftModifier, r.Top + s + _topModifier, s, r.Height - 2 * s);
            var r4 = new Rectangle(r.Left + _leftModifier, r.Bottom - s + _topModifier, r.Width, s);

            var c = Manager.Skin.Controls["Control.Outline"].Layers[0].States.Enabled.Color;

            renderer.Begin(BlendingMode.Default);
            renderer.Draw(t, r1, c);
            renderer.Draw(t, r2, c);
            renderer.Draw(t, r3, c);
            renderer.Draw(t, r4, c);
            renderer.End();
        }
    }

    public void SetPosition(int left, int top)
    {
        _left = left;
        _top = top;
    }

    public void SetSize(int width, int height)
    {
        _width = width;
        _height = height;
    }

    internal void SetAnchorMargins()
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
    }

    private void ProcessAnchor(ResizeEventArgs e)
    {
        if ((Anchor & Anchors.Right) == Anchors.Right && (Anchor & Anchors.Left) != Anchors.Left)
        {
            Left = Parent.VirtualWidth - Width - _anchorMargins.Right;
        }
        else if ((Anchor & Anchors.Right) == Anchors.Right && (Anchor & Anchors.Left) == Anchors.Left)
        {
            Width = Parent.VirtualWidth - Left - _anchorMargins.Right;
        }
        else if ((Anchor & Anchors.Right) != Anchors.Right && (Anchor & Anchors.Left) != Anchors.Left)
        {
            var diff = e.Width - e.OldWidth;
            if (e.Width % 2 != 0 && diff != 0)
            {
                diff += diff / Math.Abs(diff);
            }
            Left += diff / 2;
        }
        if ((Anchor & Anchors.Bottom) == Anchors.Bottom && (Anchor & Anchors.Top) != Anchors.Top)
        {
            Top = Parent.VirtualHeight - Height - _anchorMargins.Bottom;
        }
        else if ((Anchor & Anchors.Bottom) == Anchors.Bottom && (Anchor & Anchors.Top) == Anchors.Top)
        {
            Height = Parent.VirtualHeight - Top - _anchorMargins.Bottom;
        }
        else if ((Anchor & Anchors.Bottom) != Anchors.Bottom && (Anchor & Anchors.Top) != Anchors.Top)
        {
            var diff = e.Height - e.OldHeight;
            if (e.Height % 2 != 0 && diff != 0)
            {
                diff += diff / Math.Abs(diff);
            }
            Top += diff / 2;
        }
    }

    public override void Initialize(Manager manager)
    {
        base.Initialize(manager);

        if (Manager == null)
        {
            Logs.WriteError("Control cannot be created. Manager instance is needed.");
            return;
        }

        if (Manager.Skin == null)
        {
            Logs.WriteError("Control cannot be created. No skin loaded.");
            return;
        }

        if (string.IsNullOrEmpty(_text))
        {
            _text = Utilities.DeriveControlName(this);
        }
        _root = this;

        InitializeSkin();

        CheckLayer(_skin, "Control");

        if (Skin != null)
        {
            SetDefaultSize(_width, _height);
            SetMinimumSize(MinimumWidth, MinimumHeight);
            ResizerSize = _skin.ResizerSize;
        }

        ComputePositionAndSizeWithRatio(Manager.ScreenWidth, Manager.ScreenHeight);

        OnMove(new MoveEventArgs());
        OnResize(new ResizeEventArgs());
    }

    public void ComputePositionAndSizeWithRatio(int width, int height)
    {
        if (Manager == null)
        {
            return;
        }

        _left = (int)(LeftScreenRatio * (float)width);
        _top = (int)(TopScreenRatio * (float)height);
        _width = (int)(WidthScreenRatio * (float)width);
        _height = (int)(HeightScreenRatio * (float)height);
    }

    protected internal virtual void InitializeSkin()
    {
        if (Manager?.Skin?.Controls != null)
        {
            var skinControl = Manager.Skin.Controls[Utilities.DeriveControlName(this)] ?? Manager.Skin.Controls["Control"];
            Skin = new SkinControl(skinControl);
        }
        else
        {
            Logs.WriteError("Control skin cannot be initialized. No skin loaded.");
        }
    }

    protected void SetDefaultSize(int width, int height)
    {
        Width = _skin?.DefaultSize.Width > 0 ? _skin.DefaultSize.Width : width;
        Height = _skin?.DefaultSize.Height > 0 ? _skin.DefaultSize.Height : height;

#if EDITOR
        if (Manager != null)
        {
            WidthScreenRatio = (float)Width / (float)Manager.CasaEngineGame.ScreenSizeWidth;
            HeightScreenRatio = (float)Width / (float)Manager.CasaEngineGame.ScreenSizeHeight;
        }
#endif
    }

    protected void SetMinimumSize(int minimumWidth, int minimumHeight)
    {
        MinimumWidth = _skin.MinimumSize.Width > 0 ? _skin.MinimumSize.Width : minimumWidth;
        MinimumHeight = _skin.MinimumSize.Height > 0 ? _skin.MinimumSize.Height : minimumHeight;
    }

    protected internal void OnDeviceSettingsChanged(DeviceEventArgs e)
    {
        if (!e.Handled)
        {
            Invalidate();
        }
    }

    protected virtual void DrawControl(IRenderer renderer, Rectangle rect, GameTime gameTime)
    {
        if (_backColor != UndefinedColor && _backColor != Color.Transparent)
        {
            renderer.Draw(Manager.Skin.Images["Control"].Resource, rect, _backColor);
        }
        else if (_skin.Layers.Count > 0)
        {
            renderer.DrawLayer(this, _skin.Layers[0], rect);
        }
    }

    protected internal override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        ToolTipUpdate();

        if (_controls != null)
        {
            var list = new ControlsList();
            list.AddRange(_controls);
            foreach (var c in list)
            {
                c.Update(gameTime);
            }
        }
    }

    protected internal void CheckLayer(SkinControl skin, string layer)
    {
        if (!(skin?.Layers?.Count > 0 && skin.Layers[layer] != null))
        {
            Logs.WriteError("Unable to read skin layer \"" + layer + "\" for control \"" + Utilities.DeriveControlName(this) + "\".");
        }
    }

    protected internal void CheckLayer(SkinControl skin, int layer)
    {
        if (!(skin?.Layers?.Count > 0 && skin.Layers[layer] != null))
        {
            Logs.WriteError("Unable to read skin layer with index \"" + layer.ToString() + "\" for control \"" + Utilities.DeriveControlName(this) + "\".");
        }
    }

    public Control GetControl(string name)
    {
        Control ret = null;
        foreach (var c in Controls)
        {
            if (string.Equals(c.Name, name, StringComparison.InvariantCultureIgnoreCase))
            {
                ret = c;
                break;
            }

            ret = c.GetControl(name);
            if (ret != null)
            {
                break;
            }
        }
        return ret;
    }

    public virtual void Add(Control control)
    {
        if (control != null)
        {
            if (!_controls.Contains(control))
            {
                if (control.Parent != null)
                {
                    control.Parent.Remove(control);
                }
                else
                {
                    Manager.Remove(control);
                }

                control._parent = this;
                control.Root = _root;
                control.Enabled = Enabled ? control.Enabled : Enabled;
                _controls.Add(control);

                _virtualHeight = GetVirtualHeight();
                _virtualWidth = GetVirtualWidth();

                Manager.DeviceSettingsChanged += control.OnDeviceSettingsChanged;
                Manager.SkinChanging += control.OnSkinChanging;
                Manager.SkinChanged += control.OnSkinChanged;
                Resize += control.OnParentResize;

                control.SetAnchorMargins();

                if (!Suspended)
                {
                    OnParentChanged(new EventArgs());
                }
            }
        }
    }

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

            _controls.Remove(control);

            control._parent = null;
            control.Root = control;

            Resize -= control.OnParentResize;
            Manager.DeviceSettingsChanged -= control.OnDeviceSettingsChanged;
            Manager.SkinChanging -= control.OnSkinChanging;
            Manager.SkinChanged -= control.OnSkinChanged;

            if (!Suspended)
            {
                OnParentChanged(new EventArgs());
            }
        }
    }

    public bool Contains(Control control, bool recursively)
    {
        if (Controls != null)
        {
            foreach (var c in Controls)
            {
                if (c == control)
                {
                    return true;
                }

                if (recursively && c.Contains(control, true))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public virtual void Invalidate()
    {
        _invalidated = true;
        _parent?.Invalidate();
    }

    public void BringToFront()
    {
        Manager?.BringToFront(this);
    }

    public void SendToBack()
    {
        Manager?.SendToBack(this);
    }

    public virtual void Show()
    {
        Visible = true;
    }

    public void Hide()
    {
        Visible = false;
    }

    public void Refresh()
    {
        OnMove(new MoveEventArgs(_left, _top, _left, _top));
        OnResize(new ResizeEventArgs(_width, _height, _width, _height));
    }

    public void SendMessage(Message message, EventArgs e)
    {
        MessageProcess(message, e);
    }

    protected void MessageProcess(Message message, EventArgs e)
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
            case Message.MouseScroll:
                {
                    MouseScrollProcess(e as MouseEventArgs);
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
            case Message.GamePadDown:
                {
                    GamePadDownProcess(e as GamePadEventArgs);
                    break;
                }
            case Message.GamePadUp:
                {
                    GamePadUpProcess(e as GamePadEventArgs);
                    break;
                }
            case Message.GamePadPress:
                {
                    GamePadPressProcess(e as GamePadEventArgs);
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
    }

    private void GamePadPressProcess(GamePadEventArgs e)
    {
        Invalidate();
        if (!Suspended)
        {
            OnGamePadPress(e);
        }
    }

    private void GamePadUpProcess(GamePadEventArgs e)
    {
        Invalidate();

        if (e.Button == GamePadActions.Press && _pressed[(int)e.Button])
        {
            _pressed[(int)e.Button] = false;
        }

        if (!Suspended)
        {
            OnGamePadUp(e);
        }

        if (e.Button == GamePadActions.ContextMenu && !e.Handled)
        {
            ContextMenu?.Show(this, AbsoluteLeft + 8, AbsoluteTop + 8);
        }
    }

    private void GamePadDownProcess(GamePadEventArgs e)
    {
        Invalidate();

        ToolTipOut();

        if (e.Button == GamePadActions.Press && !IsPressed)
        {
            _pressed[(int)e.Button] = true;
        }

        if (!Suspended)
        {
            OnGamePadDown(e);
        }
    }

    private void KeyPressProcess(KeyEventArgs e)
    {
        Invalidate();
        if (!Suspended)
        {
            OnKeyPress(e);
        }
    }

    private void KeyDownProcess(KeyEventArgs e)
    {
        Invalidate();

        ToolTipOut();

        if (e.Key == Microsoft.Xna.Framework.Input.Keys.Space && !IsPressed)
        {
            _pressed[(int)MouseButton.None] = true;
        }

        if (!Suspended)
        {
            OnKeyDown(e);
        }
    }

    private void KeyUpProcess(KeyEventArgs e)
    {
        Invalidate();

        if (e.Key == Microsoft.Xna.Framework.Input.Keys.Space && _pressed[(int)MouseButton.None])
        {
            _pressed[(int)MouseButton.None] = false;
        }

        if (!Suspended)
        {
            OnKeyUp(e);
        }

        if (e.Key == Microsoft.Xna.Framework.Input.Keys.Apps && !e.Handled)
        {
            ContextMenu?.Show(this, AbsoluteLeft + 8, AbsoluteTop + 8);
        }
    }

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
                if (OutlineResizing)
                {
                    OutlineRect = ControlRect;
                }

                if (!Suspended)
                {
                    OnResizeBegin(e);
                }
            }
            else if (CheckMovableArea(e.Position))
            {
                IsMoving = true;
                if (OutlineMoving)
                {
                    OutlineRect = ControlRect;
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
    }

    private void MouseUpProcess(MouseEventArgs e)
    {
        Invalidate();
        if (_pressed[(int)e.Button] || IsMoving || _isResizing)
        {
            _pressed[(int)e.Button] = false;

            if (e.Button == MouseButton.Left)
            {
                if (IsResizing)
                {
                    IsResizing = false;
                    if (OutlineResizing)
                    {
                        Left = OutlineRect.Left;
                        Top = OutlineRect.Top;
                        Width = OutlineRect.Width;
                        Height = OutlineRect.Height;
                        OutlineRect = Rectangle.Empty;
                    }
                    if (!Suspended)
                    {
                        OnResizeEnd(e);
                    }
                }
                else if (IsMoving)
                {
                    IsMoving = false;
                    if (OutlineMoving)
                    {
                        Left = OutlineRect.Left;
                        Top = OutlineRect.Top;
                        OutlineRect = Rectangle.Empty;
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
    }

    void MousePressProcess(MouseEventArgs e)
    {
        if (_pressed[(int)e.Button] && !IsMoving && !IsResizing)
        {
            if (!Suspended)
            {
                OnMousePress(TransformPosition(e));
            }
        }
    }

    void MouseScrollProcess(MouseEventArgs e)
    {
        if (!IsMoving && !IsResizing && !Suspended)
        {
            OnMouseScroll(e);
        }
    }

    private void MouseOverProcess(MouseEventArgs e)
    {
        Invalidate();
        _hovered = true;
        ToolTipOver();
        if (Cursor != null && Manager.Cursor != Cursor)
        {
            Manager.Cursor = Cursor;
        }

        if (!Suspended)
        {
            OnMouseOver(e);
        }
    }

    private void MouseOutProcess(MouseEventArgs e)
    {
        Invalidate();
        _hovered = false;
        ToolTipOut();
        Manager.Cursor = Manager.Skin.Cursors["Default"]?.Resource;

        if (!Suspended)
        {
            OnMouseOut(e);
        }
    }

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
            var x = _parent?.AbsoluteLeft ?? 0;
            var y = _parent?.AbsoluteTop ?? 0;

            var l = e.Position.X - x - _pressSpot.X - _leftModifier;
            var t = e.Position.Y - y - _pressSpot.Y - _topModifier;

            if (!Suspended)
            {
                var v = new MoveEventArgs(l, t, Left, Top);
                OnValidateMove(v);

                l = v.Left;
                t = v.Top;
            }

            if (OutlineMoving)
            {
                OutlineRect = new Rectangle(l, t, OutlineRect.Width, OutlineRect.Height);
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
    }

    private void ClickProcess(EventArgs e)
    {
        var timer = (long)TimeSpan.FromTicks(DateTime.Now.Ticks).TotalMilliseconds;

        var ex = e is MouseEventArgs args ? args : new MouseEventArgs();

        if (_doubleClickTimer == 0 || timer - _doubleClickTimer > Manager.DoubleClickTime ||
            !DoubleClicks)
        {
            var ts = new TimeSpan(DateTime.Now.Ticks);
            _doubleClickTimer = (long)ts.TotalMilliseconds;
            _doubleClickButton = ex.Button;

            if (!Suspended)
            {
                OnClick(e);
            }
        }
        else if (timer - _doubleClickTimer <= Manager.DoubleClickTime && ex.Button == _doubleClickButton && ex.Button != MouseButton.None)
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
    }

    private void ToolTipUpdate()
    {
        if (Manager.ToolTipsEnabled && _toolTip != null && _tooltipTimer > 0 && TimeSpan.FromTicks(DateTime.Now.Ticks).TotalMilliseconds - _tooltipTimer >= Manager.ToolTipDelay)
        {
            _tooltipTimer = 0;
            _toolTip.Visible = true;
            Manager.Add(_toolTip);
        }
    }

    private void ToolTipOver()
    {
        if (Manager.ToolTipsEnabled && _toolTip != null && _tooltipTimer == 0)
        {
            var ts = new TimeSpan(DateTime.Now.Ticks);
            _tooltipTimer = (long)ts.TotalMilliseconds;
        }
    }

    private void ToolTipOut()
    {
        if (Manager.ToolTipsEnabled && _toolTip != null)
        {
            _tooltipTimer = 0;
            _toolTip.Visible = false;
            Manager.Remove(_toolTip);
        }
    }

    private bool CheckPosition(Point pos)
    {
        if (pos.X >= AbsoluteLeft && pos.X < AbsoluteLeft + Width)
        {
            if (pos.Y >= AbsoluteTop && pos.Y < AbsoluteTop + Height)
            {
                return true;
            }
        }
        return false;
    }

    private bool CheckMovableArea(Point pos)
    {
        if (Movable)
        {
            var rect = MovableArea;

            if (rect == Rectangle.Empty)
            {
                rect = new Rectangle(0, 0, _width, _height);
            }

            pos.X -= AbsoluteLeft;
            pos.Y -= AbsoluteTop;

            if (pos.X >= rect.X && pos.X < rect.X + rect.Width)
            {
                if (pos.Y >= rect.Y && pos.Y < rect.Y + rect.Height)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private bool CheckResizableArea(Point pos)
    {
        if (Resizable)
        {
            pos.X -= AbsoluteLeft;
            pos.Y -= AbsoluteTop;

            if ((pos.X >= 0 && pos.X < ResizerSize && pos.Y >= 0 && pos.Y < Height) ||
                (pos.X >= Width - ResizerSize && pos.X < Width && pos.Y >= 0 && pos.Y < Height) ||
                (pos.Y >= 0 && pos.Y < ResizerSize && pos.X >= 0 && pos.X < Width) ||
                (pos.Y >= Height - ResizerSize && pos.Y < Height && pos.X >= 0 && pos.X < Width))
            {
                return true;
            }
        }
        return false;
    }

    protected MouseEventArgs TransformPosition(MouseEventArgs e)
    {
        var ee = new MouseEventArgs(e.State, e.Button, e.Position);
        ee.Difference = e.Difference;

        ee.Position.X = ee.State.X - AbsoluteLeft;
        ee.Position.Y = ee.State.Y - AbsoluteTop;
        return ee;
    }

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
    }

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
    }

    private void PerformResize(MouseEventArgs e)
    {
        if (Resizable && !IsMoving)
        {
            if (!IsResizing)
            {
                GetResizePosition(e);
                Manager.Cursor = Cursor = GetResizeCursor();
            }

            if (IsResizing)
            {
                _invalidated = true;

                var top = false;
                var bottom = false;
                var left = false;
                var right = false;

                if ((_resizeArea == Alignment.TopCenter ||
                     _resizeArea == Alignment.TopLeft ||
                     _resizeArea == Alignment.TopRight) && (ResizeEdge & Anchors.Top) == Anchors.Top)
                {
                    top = true;
                }

                else if ((_resizeArea == Alignment.BottomCenter ||
                          _resizeArea == Alignment.BottomLeft ||
                          _resizeArea == Alignment.BottomRight) && (ResizeEdge & Anchors.Bottom) == Anchors.Bottom)
                {
                    bottom = true;
                }

                if ((_resizeArea == Alignment.MiddleLeft ||
                     _resizeArea == Alignment.BottomLeft ||
                     _resizeArea == Alignment.TopLeft) && (ResizeEdge & Anchors.Left) == Anchors.Left)
                {
                    left = true;
                }

                else if ((_resizeArea == Alignment.MiddleRight ||
                          _resizeArea == Alignment.BottomRight ||
                          _resizeArea == Alignment.TopRight) && (ResizeEdge & Anchors.Right) == Anchors.Right)
                {
                    right = true;
                }

                var w = Width;
                var h = Height;
                var l = Left;
                var t = Top;

                if (OutlineResizing && !OutlineRect.IsEmpty)
                {
                    l = OutlineRect.Left;
                    t = OutlineRect.Top;
                    w = OutlineRect.Width;
                    h = OutlineRect.Height;
                }

                var px = e.Position.X - (_parent?.AbsoluteLeft ?? 0);
                var py = e.Position.Y - (_parent?.AbsoluteTop ?? 0);

                if (left)
                {
                    w = w + (l - px) + _leftModifier + _pressDiff[0];
                    l = px - _leftModifier - _pressDiff[0] - CheckWidth(ref w);

                }
                else if (right)
                {
                    w = px - l - _leftModifier + _pressDiff[2];
                    CheckWidth(ref w);
                }

                if (top)
                {
                    h = h + (t - py) + _topModifier + _pressDiff[1];
                    t = py - _topModifier - _pressDiff[1] - CheckHeight(ref h);
                }
                else if (bottom)
                {
                    h = py - t - _topModifier + _pressDiff[3];
                    CheckHeight(ref h);
                }

                if (!Suspended)
                {
                    var v = new ResizeEventArgs(w, h, Width, Height);
                    OnValidateResize(v);

                    if (top)
                    {
                        // Compensate for a possible height change from Validate event
                        t += h - v.Height;
                    }
                    if (left)
                    {
                        // Compensate for a possible width change from Validate event
                        l += w - v.Width;
                    }
                    w = v.Width;
                    h = v.Height;
                }

                if (OutlineResizing)
                {
                    OutlineRect = new Rectangle(l, t, w, h);
                    _parent?.Invalidate();
                }
                else
                {
                    Width = w;
                    Height = h;
                    Top = t;
                    Left = l;
                }
            }
        }
    }

    private Cursor GetResizeCursor()
    {
        var cur = Cursor;
        switch (_resizeArea)
        {
            case Alignment.TopCenter:
                {
                    return (ResizeEdge & Anchors.Top) == Anchors.Top ? Manager.Skin.Cursors["Vertical"].Resource : Cursor;
                }
            case Alignment.BottomCenter:
                {
                    return (ResizeEdge & Anchors.Bottom) == Anchors.Bottom ? Manager.Skin.Cursors["Vertical"].Resource : Cursor;
                }
            case Alignment.MiddleLeft:
                {
                    return (ResizeEdge & Anchors.Left) == Anchors.Left ? Manager.Skin.Cursors["Horizontal"].Resource : Cursor;
                }
            case Alignment.MiddleRight:
                {
                    return (ResizeEdge & Anchors.Right) == Anchors.Right ? Manager.Skin.Cursors["Horizontal"].Resource : Cursor;
                }
            case Alignment.TopLeft:
                {
                    return (ResizeEdge & Anchors.Left) == Anchors.Left && (ResizeEdge & Anchors.Top) == Anchors.Top ? Manager.Skin.Cursors["DiagonalLeft"].Resource : Cursor;
                }
            case Alignment.BottomRight:
                {
                    return (ResizeEdge & Anchors.Bottom) == Anchors.Bottom && (ResizeEdge & Anchors.Right) == Anchors.Right ? Manager.Skin.Cursors["DiagonalLeft"].Resource : Cursor;
                }
            case Alignment.TopRight:
                {
                    return (ResizeEdge & Anchors.Top) == Anchors.Top && (ResizeEdge & Anchors.Right) == Anchors.Right ? Manager.Skin.Cursors["DiagonalRight"].Resource : Cursor;
                }
            case Alignment.BottomLeft:
                {
                    return (ResizeEdge & Anchors.Bottom) == Anchors.Bottom && (ResizeEdge & Anchors.Left) == Anchors.Left ? Manager.Skin.Cursors["DiagonalRight"].Resource : Cursor;
                }
        }
        return Manager.Skin.Cursors["Default"].Resource;
    }

    private void GetResizePosition(MouseEventArgs e)
    {
        var x = e.Position.X - AbsoluteLeft;
        var y = e.Position.Y - AbsoluteTop;
        bool l = false, t = false, r = false, b = false;

        _resizeArea = Alignment.None;

        if (CheckResizableArea(e.Position))
        {
            if (x < ResizerSize)
            {
                l = true;
            }

            if (x >= Width - ResizerSize)
            {
                r = true;
            }

            if (y < ResizerSize)
            {
                t = true;
            }

            if (y >= Height - ResizerSize)
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
    }


    protected virtual void OnMouseUp(MouseEventArgs e)
    {
        MouseUp?.Invoke(this, e);
    }

    protected virtual void OnMouseDown(MouseEventArgs e)
    {
        MouseDown?.Invoke(this, e);
    }

    protected virtual void OnMouseMove(MouseEventArgs e)
    {
        MouseMove?.Invoke(this, e);
    }

    protected void OnMouseOver(MouseEventArgs e)
    {
        MouseOver?.Invoke(this, e);
    }

    protected virtual void OnMouseOut(MouseEventArgs e)
    {
        MouseOut?.Invoke(this, e);
    }

    protected virtual void OnClick(EventArgs e)
    {
        Click?.Invoke(this, e);
    }

    protected virtual void OnDoubleClick(EventArgs e)
    {
        DoubleClick?.Invoke(this, e);
    }

    protected void OnMove(MoveEventArgs e)
    {
#if EDITOR
        if (Manager != null)
        {
            LeftScreenRatio = (float)Left / (float)Manager.CasaEngineGame.ScreenSizeWidth;
            TopScreenRatio = (float)Top / (float)Manager.CasaEngineGame.ScreenSizeHeight;
        }
#endif

        _parent?.Invalidate();

        Move?.Invoke(this, e);
    }

    protected virtual void OnResize(ResizeEventArgs e)
    {
#if EDITOR
        if (Manager != null)
        {
            WidthScreenRatio = (float)Width / (float)Manager.CasaEngineGame.ScreenSizeWidth;
            HeightScreenRatio = (float)Height / (float)Manager.CasaEngineGame.ScreenSizeHeight;
        }
#endif

        Invalidate();
        Resize?.Invoke(this, e);
    }

    protected void OnValidateResize(ResizeEventArgs e)
    {
        ValidateResize?.Invoke(this, e);
    }

    protected void OnValidateMove(MoveEventArgs e)
    {
        ValidateMove?.Invoke(this, e);
    }

    protected virtual void OnMoveBegin(EventArgs e)
    {
        MoveBegin?.Invoke(this, e);
    }

    protected virtual void OnMoveEnd(EventArgs e)
    {
        MoveEnd?.Invoke(this, e);
    }

    protected void OnResizeBegin(EventArgs e)
    {
        ResizeBegin?.Invoke(this, e);
    }

    protected void OnResizeEnd(EventArgs e)
    {
        ResizeEnd?.Invoke(this, e);
    }

    protected void OnParentResize(object sender, ResizeEventArgs e)
    {
        ProcessAnchor(e);
    }

    protected void OnKeyUp(KeyEventArgs e)
    {
        KeyUp?.Invoke(this, e);
    }

    protected virtual void OnKeyDown(KeyEventArgs e)
    {
        KeyDown?.Invoke(this, e);
    }

    protected virtual void OnKeyPress(KeyEventArgs e)
    {
        KeyPress?.Invoke(this, e);
    }

    protected void OnGamePadUp(GamePadEventArgs e)
    {
        GamePadUp?.Invoke(this, e);
    }

    protected virtual void OnGamePadDown(GamePadEventArgs e)
    {
        GamePadDown?.Invoke(this, e);
    }

    protected virtual void OnGamePadPress(GamePadEventArgs e)
    {
        GamePadPress?.Invoke(this, e);
    }

    protected internal void OnDraw(DrawEventArgs e)
    {
        Draw?.Invoke(this, e);
    }

    protected void OnDrawTexture(DrawEventArgs e)
    {
        DrawTexture?.Invoke(this, e);
    }

    protected void OnColorChanged(EventArgs e)
    {
        ColorChanged?.Invoke(this, e);
    }

    protected void OnTextColorChanged(EventArgs e)
    {
        TextColorChanged?.Invoke(this, e);
    }

    protected void OnBackColorChanged(EventArgs e)
    {
        BackColorChanged?.Invoke(this, e);
    }

    protected virtual void OnTextChanged(EventArgs e)
    {
        TextChanged?.Invoke(this, e);
    }

    protected void OnAnchorChanged(EventArgs e)
    {
        AnchorChanged?.Invoke(this, e);
    }

    protected internal virtual void OnSkinChanged(EventArgs e)
    {
        SkinChanged?.Invoke(this, e);
    }

    protected internal void OnSkinChanging(EventArgs e)
    {
        SkinChanging?.Invoke(this, e);
    }

    protected void OnParentChanged(EventArgs e)
    {
        ParentChanged?.Invoke(this, e);
    }

    protected void OnRootChanged(EventArgs e)
    {
        RootChanged?.Invoke(this, e);
    }

    protected void OnVisibleChanged(EventArgs e)
    {
        VisibleChanged?.Invoke(this, e);
    }

    protected void OnEnabledChanged(EventArgs e)
    {
        EnabledChanged?.Invoke(this, e);
    }

    protected void OnAlphaChanged(EventArgs e)
    {
        AlphaChanged?.Invoke(this, e);
    }

    protected virtual void OnFocusLost(EventArgs e)
    {
        FocusLost?.Invoke(this, e);
    }

    protected virtual void OnFocusGained(EventArgs e)
    {
        FocusGained?.Invoke(this, e);
    }

    protected virtual void OnMousePress(MouseEventArgs e)
    {
        MousePress?.Invoke(this, e);
    }

    protected virtual void OnMouseScroll(MouseEventArgs e)
    {
        MouseScroll?.Invoke(this, e);
    }

    public virtual void Load(JObject element)
    {
        Alpha = element["alpha"].GetByte();
        if (!string.IsNullOrEmpty(element["anchor"].GetString()))
        {
            Anchor = element["anchor"].GetAnchors();
        }
        BackColor = element["back_color"].GetColor();
        CanFocus = element["can_focus"].GetBoolean();
        ClientMargins = element["client_margins"].GetMargins();
        Color = element["color"].GetColor();
        //DesignMode = element["design_mode"].GetBoolean();
        Detached = element["detached"].GetBoolean();
        DoubleClicks = element["double_clicks"].GetBoolean();
        Enabled = element["enabled"].GetBoolean();
        Height = element["height"].GetInt32();
        HeightScreenRatio = element["height_screen_ratio"].GetSingle();
        Left = element["left"].GetInt32();
        LeftScreenRatio = element["left_screen_ratio"].GetSingle();
        Margins = element["margins"].GetMargins();
        MaximumHeight = element["maximum_height"].GetInt32();
        MaximumWidth = element["maximum_width"].GetInt32();
        MinimumHeight = element["minimum_height"].GetInt32();
        MinimumWidth = element["minimum_width"].GetInt32();
        Movable = element["movable"].GetBoolean();
        MovableArea = element["movable_area"].GetRectangle();
        Name = element["name"].GetString();
        OutlineMoving = element["outline_moving"].GetBoolean();
        OutlineResizing = element["outline_resizing"].GetBoolean();
        PartialOutline = element["partial_outline"].GetBoolean();
        Passive = element["passive"].GetBoolean();
        Resizable = element["resizable"].GetBoolean();
        ResizeEdge = element["resize_edge"].GetAnchors();
        ResizerSize = element["resizer_size"].GetInt32();
        StayOnBack = element["stay_on_back"].GetBoolean();
        StayOnTop = element["stay_on_top"].GetBoolean();
        Suspended = element["suspended"].GetBoolean();
        Text = element["text"].GetString();
        TextColor = element["text_color"].GetColor();
        Top = element["top"].GetInt32();
        TopScreenRatio = element["top_screen_ratio"].GetSingle();
        Visible = element["visible"].GetBoolean();
        Width = element["width"].GetInt32();
        WidthScreenRatio = element["width_screen_ratio"].GetSingle();
    }

#if EDITOR
    public virtual void Save(JObject node)
    {
        node.Add("type", GetType().Name);

        node.Add("alpha", Alpha);
        node.Add("anchor", Anchor);
        node.Add("back_color", BackColor);
        node.Add("can_focus", CanFocus);
        node.Add("client_margins", ClientMargins);
        node.Add("color", Color);
        //node.Add("context_menu", ContextMenu);
        //node.Add("controls", Controls);
        //node.Add("cursor", Cursor);
        //node.Add("design_mode", DesignMode);
        node.Add("detached", Detached);
        node.Add("double_clicks", DoubleClicks);
        node.Add("enabled", Enabled);
        node.Add("height", Height);
        node.Add("height_screen_ratio", HeightScreenRatio);
        node.Add("left", Left);
        node.Add("left_screen_ratio", LeftScreenRatio);
        node.Add("margins", Margins);
        node.Add("maximum_height", MaximumHeight);
        node.Add("maximum_width", MaximumWidth);
        node.Add("minimum_height", MinimumHeight);
        node.Add("minimum_width", MinimumWidth);
        node.Add("movable", Movable);
        node.Add("movable_area", MovableArea);
        node.Add("name", Name);
        node.Add("outline_moving", OutlineMoving);
        node.Add("outline_resizing", OutlineResizing);
        //node.Add("parent", Parent);
        node.Add("partial_outline", PartialOutline);
        node.Add("passive", Passive);
        node.Add("resizable", Resizable);
        node.Add("resize_edge", ResizeEdge);
        node.Add("resizer_size", ResizerSize);
        node.Add("stay_on_back", StayOnBack);
        node.Add("stay_on_top", StayOnTop);
        node.Add("suspended", Suspended);
        //node.Add("tag", Tag);
        node.Add("text", Text);
        node.Add("text_color", TextColor);
        //node.Add("tooltip", ToolTip);
        //node.Add("tooltip_type", ToolTipType);
        node.Add("top", Top);
        node.Add("top_screen_ratio", TopScreenRatio);
        node.Add("visible", Visible);
        node.Add("width", Width);
        node.Add("width_screen_ratio", WidthScreenRatio);
    }
#endif
}