using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TomShane.Neoforce.Controls
{
    /// <summary>
    /// Defines the gamepad actions mapping.
    /// </summary>
    public class GamePadActions
    {
        public readonly GamePadButton Click = GamePadButton.A;
        public readonly GamePadButton Press = GamePadButton.Y;
        public readonly GamePadButton Left = GamePadButton.LeftStickLeft;
        public readonly GamePadButton Right = GamePadButton.LeftStickRight;
        public readonly GamePadButton Up = GamePadButton.LeftStickUp;
        public readonly GamePadButton Down = GamePadButton.LeftStickDown;
        public readonly GamePadButton NextControl = GamePadButton.RightShoulder;
        public readonly GamePadButton PrevControl = GamePadButton.LeftShoulder;
        public readonly GamePadButton ContextMenu = GamePadButton.X;
    }

    /// <summary>
    /// Defines type used as a controls collection.
    /// </summary>
    public class ControlsList : EventedList<Control>
    {
        public ControlsList() : base() { }
        public ControlsList(int capacity) : base(capacity) { }
        public ControlsList(IEnumerable<Control> collection) : base(collection) { }
    }

    /// <summary>
    /// Defines the base class for all controls.
    /// </summary>    
    public class Control : Component
    {
        public static readonly Color UndefinedColor = new(255, 255, 255, 0);

        internal static readonly ControlsList Stack = new();

        private Color _color = UndefinedColor;
        private Color _textColor = UndefinedColor;
        private Color _backColor = Color.Transparent;
        private byte _alpha = 255;
        private Anchors _anchor = Anchors.Left | Anchors.Top;
        private Anchors _resizeEdge = Anchors.All;
        private string _text = "Control";
        private bool _visible = true;
        private bool _enabled = true;
        private SkinControl _skin;
        private Control _parent;
        private Control _root;
        private int _left;
        private int _top;
        private int _width = 64;
        private int _height = 64;
        private ContextMenu _contextMenu;
        private long _tooltipTimer;
        private long _doubleClickTimer;
        private MouseButton _doubleClickButton = MouseButton.None;
        private Type _toolTipType = typeof(ToolTip);
        private ToolTip _toolTip;
        private bool _doubleClicks = true;
        private bool _outlineResizing;
        private bool _outlineMoving;
        private bool _partialOutline = true;

        private ControlsList _controls = new();
        private Rectangle _movableArea = Rectangle.Empty;
        private bool _movable;
        private bool _resizable;
        private bool _invalidated = true;
        private int _resizerSize = 4;
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
        private int[] _pressDiff = new int[4];
        private Alignment _resizeArea = Alignment.None;
        private bool _hovered;
        private bool _inside;
        private bool[] _pressed = new bool[32];
        private bool _isMoving;
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
        public virtual IEnumerable<Control> Controls => _controls;

        /// <summary>
        /// Gets or sets a rectangular area that reacts on moving the control with the mouse.
        /// </summary>
        public virtual Rectangle MovableArea
        {
            get => _movableArea;
            set => _movableArea = value;
        }

        /// <summary>
        /// Gets a value indicating whether this control is a child control.
        /// </summary>
        public virtual bool IsChild => _parent != null;

        /// <summary>
        /// Gets a value indicating whether this control is a parent control.
        /// </summary>
        public virtual bool IsParent => _controls != null && _controls.Count > 0;

        /// <summary>
        /// Gets a value indicating whether this control is a root control.
        /// </summary>
        public virtual bool IsRoot => _root == this;

        /// <summary>
        /// Gets or sets a value indicating whether this control can receive focus. 
        /// </summary>
        public virtual bool CanFocus { get; set; } = true;

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
        public virtual bool Movable
        {
            get => _movable;
            set => _movable = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this control can be resized by the mouse.
        /// </summary>
        public virtual bool Resizable
        {
            get => _resizable;
            set => _resizable = value;
        }

        /// <summary>
        /// Gets or sets the size of the rectangular borders around the control used for resizing by the mouse.
        /// </summary>
        public virtual int ResizerSize
        {
            get => _resizerSize;
            set => _resizerSize = value;
        }

        /// <summary>
        /// Gets or sets the ContextMenu associated with this control.
        /// </summary>
        public virtual ContextMenu ContextMenu
        {
            get => _contextMenu;
            set => _contextMenu = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this control should process mouse double-clicks.
        /// </summary>
        public virtual bool DoubleClicks
        {
            get => _doubleClicks;
            set => _doubleClicks = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this control should use ouline resizing.
        /// </summary>
        public virtual bool OutlineResizing
        {
            get => _outlineResizing;
            set => _outlineResizing = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this control should use outline moving.
        /// </summary>
        public virtual bool OutlineMoving
        {
            get => _outlineMoving;
            set => _outlineMoving = value;
        }

        /// <summary>
        /// Gets or sets the object that contains data about the control.
        /// </summary>
        public virtual object Tag { get; set; }

        /// <summary>
        /// Gets or sets the value indicating the distance from another control. Usable with StackPanel control.
        /// </summary>
        public virtual Margins Margins { get; set; } = new(4, 4, 4, 4);

        /// <summary>
        /// Gets or sets the value indicating wheter control is in design mode.
        /// </summary>
        public virtual bool DesignMode { get; set; }

        /// <summary>
        /// Gets or sets gamepad actions for the control.
        /// </summary>
        public virtual GamePadActions GamePadActions { get; set; } = new();

        /// <summary>
        /// Gets or sets the value indicating whether the control outline is displayed only for certain edges. 
        /// </summary>   
        public virtual bool PartialOutline
        {
            get => _partialOutline;
            set => _partialOutline = value;
        }

        /// <summary>
        /// Gets or sets the value indicating whether the control is allowed to be brought in the front.
        /// </summary>
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
        }

        /// <summary>
        /// Gets or sets the value indicating that the control should stay on top of other controls.
        /// </summary>
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
        }

        /// <summary>
        /// Gets or sets a name of the control.
        /// </summary>
        public virtual string Name { get; set; } = "Control";

        /// <summary>
        /// Gets or sets a value indicating whether this control has input focus.
        /// </summary>
        public virtual bool Focused
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

        public virtual Type ToolTipType
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

        public virtual ToolTip ToolTip
        {
            get
            {
                if (_toolTip == null)
                {
                    var t = new Type[1] { typeof(Manager) };
                    var p = new object[1] { Manager };

                    _toolTip = (ToolTip)_toolTipType.GetConstructor(t).Invoke(p);
                    _toolTip.Init();
                    _toolTip.Visible = false;
                }
                return _toolTip;
            }
            set => _toolTip = value;
        }

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
        }

        internal virtual int TopModifier
        {
            get => _topModifier;
            set => _topModifier = value;
        }

        internal virtual int LeftModifier
        {
            get => _leftModifier;
            set => _leftModifier = value;
        }

        internal virtual int VirtualHeight => GetVirtualHeight();

        //set { virtualHeight = value; }
        internal virtual int VirtualWidth => GetVirtualWidth();

        //set { virtualWidth = value; }
        /// <summary>
        /// Gets an area where is the control supposed to be drawn.
        /// </summary>
        public Rectangle DrawingRect { get; private set; } = Rectangle.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether this control should receive any events.
        /// </summary>
        public virtual bool Suspended { get; set; }

        protected internal virtual bool Hovered => _hovered;

        protected internal virtual bool Inside => _inside;

        protected internal virtual bool[] Pressed => _pressed;

        /// <summary>
        /// Gets or sets a value indicating whether this controls is currently being moved.
        /// </summary>
        protected virtual bool IsMoving
        {
            get => _isMoving;
            set => _isMoving = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this controls is currently being resized.
        /// </summary>
        protected virtual bool IsResizing
        {
            get => _isResizing;
            set => _isResizing = value;
        }

        /// <summary>
        /// Gets or sets the edges of the container to which a control is bound and determines how a control is resized with its parent.
        /// </summary>
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
        }

        /// <summary>
        /// Gets or sets the edges of the contol which are allowed for resizing.
        /// </summary>
        public virtual Anchors ResizeEdge
        {
            get => _resizeEdge;
            set => _resizeEdge = value;
        }

        /// <summary>
        /// Gets or sets the skin used for rendering the control.
        /// </summary>
        public virtual SkinControl Skin
        {
            get => _skin;
            set
            {
                _skin = value;
                ClientMargins = _skin.ClientMargins;
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
        }

        /// <summary>
        /// Gets or sets the background color for the control.
        /// </summary>
        public virtual Color BackColor
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
        }

        /// <summary>
        /// Gets or sets the text color for the control.
        /// </summary>
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
        }

        /// <summary>
        /// Gets or sets a value indicating whether the control can respond to user interaction.
        /// </summary>
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
        public virtual Control Parent
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
        public virtual Control Root
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

        /// <summary>
        /// Gets or sets the distance, in pixels, between the left edge of the control and the left edge of its parent.
        /// </summary>
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
        }

        /// <summary>
        /// Gets or sets the distance, in pixels, between the top edge of the control and the top edge of its parent.
        /// </summary>
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
        }

        /// <summary>
        /// Gets or sets the width of the control.
        /// </summary>
        public virtual int Width
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
        public virtual int Height
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
        public virtual int MaximumWidth
        {
            get
            {
                var max = _maximumWidth;
                if (max > Manager.TargetWidth)
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
        public virtual int MaximumHeight
        {
            get
            {
                var max = _maximumHeight;
                if (max > Manager.TargetHeight)
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

        public virtual int AbsoluteLeft
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

        public virtual int AbsoluteTop
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

        public virtual int OriginLeft
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

        public virtual int OriginTop
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

        public virtual int OriginWidth
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

        public virtual int OriginHeight
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

        public virtual int ClientLeft =>
            //if (skin == null) return Left;
            ClientMargins.Left;

        public virtual int ClientTop =>
            //if (skin == null) return Top;
            ClientMargins.Top;

        public virtual int ClientWidth
        {
            get =>
                //if (skin == null) return Width;
                OriginWidth - ClientMargins.Left - ClientMargins.Right;
            set => Width = value + ClientMargins.Horizontal - _skin.OriginMargins.Horizontal;
        }

        public virtual int ClientHeight
        {
            get =>
                //if (skin == null) return Height;
                OriginHeight - ClientMargins.Top - ClientMargins.Bottom;
            set => Height = value + ClientMargins.Vertical - _skin.OriginMargins.Vertical;
        }

        public virtual Rectangle AbsoluteRect => new Rectangle(AbsoluteLeft, AbsoluteTop, OriginWidth, OriginHeight);

        public virtual Rectangle OriginRect => new Rectangle(OriginLeft, OriginTop, OriginWidth, OriginHeight);

        public virtual Rectangle ClientRect => new Rectangle(ClientLeft, ClientTop, ClientWidth, ClientHeight);

        public virtual Rectangle ControlRect
        {
            get => new Rectangle(Left, Top, Width, Height);
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

        public Control(Manager manager)
            : base(manager)
        {
            if (Manager == null)
            {
                throw new Exception("Control cannot be created. Manager instance is needed.");
            }

            if (Manager.Skin == null)
            {
                throw new Exception("Control cannot be created. No skin loaded.");
            }

            _text = Utilities.DeriveControlName(this);
            _root = this;

            InitSkin();

            CheckLayer(_skin, "Control");

            if (Skin != null)
            {
                SetDefaultSize(_width, _height);
                SetMinimumSize(MinimumWidth, MinimumHeight);
                ResizerSize = _skin.ResizerSize;
            }

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
                if (_contextMenu != null)
                {
                    _contextMenu.Dispose();
                    _contextMenu = null;
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

        internal virtual void PrepareTexture(Renderer renderer, GameTime gameTime)
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

                        Manager.GraphicsDevice.SetRenderTarget(null);
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

        private void DrawChildControls(Renderer renderer, GameTime gameTime, bool firstDetachedLevel)
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

        private void DrawControls(Renderer renderer, Rectangle rect, GameTime gameTime, bool firstDetach)
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

        private void DrawDetached(Control control, Renderer renderer, GameTime gameTime)
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

        internal virtual void Render(Renderer renderer, GameTime gameTime)
        {
            if (_visible && _target != null)
            {
                var draw = true;

                if (draw)
                {
                    renderer.Begin(BlendingMode.Default);
                    renderer.Draw(_target, OriginLeft, OriginTop, new Rectangle(0, 0, OriginWidth, OriginHeight), Color.FromNonPremultiplied(255, 255, 255, Alpha));
                    renderer.End();

                    DrawDetached(this, renderer, gameTime);

                    DrawOutline(renderer, false);
                }
            }
        }

        private void DrawOutline(Renderer renderer, bool child)
        {
            if (!OutlineRect.IsEmpty)
            {
                var r = OutlineRect;
                if (child)
                {
                    r = new Rectangle(OutlineRect.Left + (_parent.AbsoluteLeft - _root.AbsoluteLeft), OutlineRect.Top + (_parent.AbsoluteTop - _root.AbsoluteTop), OutlineRect.Width, OutlineRect.Height);
                }

                var t = Manager.Skin.Controls["Control.Outline"].Layers[0].Image.Resource;

                var s = _resizerSize;
                var r1 = new Rectangle(r.Left + _leftModifier, r.Top + _topModifier, r.Width, s);
                var r2 = new Rectangle(r.Left + _leftModifier, r.Top + s + _topModifier, _resizerSize, r.Height - 2 * s);
                var r3 = new Rectangle(r.Right - s + _leftModifier, r.Top + s + _topModifier, s, r.Height - 2 * s);
                var r4 = new Rectangle(r.Left + _leftModifier, r.Bottom - s + _topModifier, r.Width, s);

                var c = Manager.Skin.Controls["Control.Outline"].Layers[0].States.Enabled.Color;

                renderer.Begin(BlendingMode.Default);
                if ((ResizeEdge & Anchors.Top) == Anchors.Top || !_partialOutline)
                {
                    renderer.Draw(t, r1, c);
                }

                if ((ResizeEdge & Anchors.Left) == Anchors.Left || !_partialOutline)
                {
                    renderer.Draw(t, r2, c);
                }

                if ((ResizeEdge & Anchors.Right) == Anchors.Right || !_partialOutline)
                {
                    renderer.Draw(t, r3, c);
                }

                if ((ResizeEdge & Anchors.Bottom) == Anchors.Bottom || !_partialOutline)
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

                var s = _resizerSize;
                var r1 = new Rectangle(r.Left + _leftModifier, r.Top + _topModifier, r.Width, s);
                var r2 = new Rectangle(r.Left + _leftModifier, r.Top + s + _topModifier, _resizerSize, r.Height - 2 * s);
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

        public virtual void SetPosition(int left, int top)
        {
            _left = left;
            _top = top;
        }

        public virtual void SetSize(int width, int height)
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

        public override void Init()
        {
            base.Init();

            OnMove(new MoveEventArgs());
            OnResize(new ResizeEventArgs());
        }

        protected internal virtual void InitSkin()
        {
            if (Manager != null && Manager.Skin != null && Manager.Skin.Controls != null)
            {
                var s = Manager.Skin.Controls[Utilities.DeriveControlName(this)];
                if (s != null)
                {
                    Skin = new SkinControl(s);
                }
                else
                {
                    Skin = new SkinControl(Manager.Skin.Controls["Control"]);
                }
            }
            else
            {
                throw new Exception("Control skin cannot be initialized. No skin loaded.");
            }
        }

        protected virtual void SetDefaultSize(int width, int height)
        {
            if (_skin.DefaultSize.Width > 0)
            {
                Width = _skin.DefaultSize.Width;
            }
            else
            {
                Width = width;
            }

            if (_skin.DefaultSize.Height > 0)
            {
                Height = _skin.DefaultSize.Height;
            }
            else
            {
                Height = height;
            }
        }

        protected virtual void SetMinimumSize(int minimumWidth, int minimumHeight)
        {
            if (_skin.MinimumSize.Width > 0)
            {
                MinimumWidth = _skin.MinimumSize.Width;
            }
            else
            {
                MinimumWidth = minimumWidth;
            }

            if (_skin.MinimumSize.Height > 0)
            {
                MinimumHeight = _skin.MinimumSize.Height;
            }
            else
            {
                MinimumHeight = minimumHeight;
            }
        }

        protected internal void OnDeviceSettingsChanged(DeviceEventArgs e)
        {
            if (!e.Handled)
            {
                Invalidate();
            }
        }

        protected virtual void DrawControl(Renderer renderer, Rectangle rect, GameTime gameTime)
        {
            if (_backColor != UndefinedColor && _backColor != Color.Transparent)
            {
                renderer.Draw(Manager.Skin.Images["Control"].Resource, rect, _backColor);
            }
            renderer.DrawLayer(this, _skin.Layers[0], rect);
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

        protected internal virtual void CheckLayer(SkinControl skin, string layer)
        {
            if (!(skin != null && skin.Layers != null && skin.Layers.Count > 0 && skin.Layers[layer] != null))
            {
                throw new Exception("Unable to read skin layer \"" + layer + "\" for control \"" + Utilities.DeriveControlName(this) + "\".");
            }
        }

        protected internal virtual void CheckLayer(SkinControl skin, int layer)
        {
            if (!(skin != null && skin.Layers != null && skin.Layers.Count > 0 && skin.Layers[layer] != null))
            {
                throw new Exception("Unable to read skin layer with index \"" + layer.ToString() + "\" for control \"" + Utilities.DeriveControlName(this) + "\".");
            }
        }

        public virtual Control GetControl(string name)
        {
            Control ret = null;
            foreach (var c in Controls)
            {
                if (c.Name.ToLower() == name.ToLower())
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

                    control.Manager = Manager;
                    control._parent = this;
                    control.Root = _root;
                    control.Enabled = Enabled ? control.Enabled : Enabled;
                    _controls.Add(control);

                    _virtualHeight = GetVirtualHeight();
                    _virtualWidth = GetVirtualWidth();

                    Manager.DeviceSettingsChanged += new DeviceEventHandler(control.OnDeviceSettingsChanged);
                    Manager.SkinChanging += new SkinEventHandler(control.OnSkinChanging);
                    Manager.SkinChanged += new SkinEventHandler(control.OnSkinChanged);
                    Resize += new ResizeEventHandler(control.OnParentResize);

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

        public virtual bool Contains(Control control, bool recursively)
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

        public virtual void BringToFront()
        {
            Manager?.BringToFront(this);
        }

        public virtual void SendToBack()
        {
            Manager?.SendToBack(this);
        }

        public virtual void Show()
        {
            Visible = true;
        }

        public virtual void Hide()
        {
            Visible = false;
        }

        public virtual void Refresh()
        {
            OnMove(new MoveEventArgs(_left, _top, _left, _top));
            OnResize(new ResizeEventArgs(_width, _height, _width, _height));
        }

        public virtual void SendMessage(Message message, EventArgs e)
        {
            MessageProcess(message, e);
        }

        protected virtual void MessageProcess(Message message, EventArgs e)
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
                _contextMenu?.Show(this, AbsoluteLeft + 8, AbsoluteTop + 8);
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
                _contextMenu?.Show(this, AbsoluteLeft + 8, AbsoluteTop + 8);
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
                    if (_outlineResizing)
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
                    if (_outlineMoving)
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
            if (_pressed[(int)e.Button] || _isMoving || _isResizing)
            {
                _pressed[(int)e.Button] = false;

                if (e.Button == MouseButton.Left)
                {
                    if (IsResizing)
                    {
                        IsResizing = false;
                        if (_outlineResizing)
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
                        if (_outlineMoving)
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
            Manager.Cursor = Manager.Skin.Cursors["Default"].Resource;

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
                var x = _parent != null ? _parent.AbsoluteLeft : 0;
                var y = _parent != null ? _parent.AbsoluteTop : 0;

                var l = e.Position.X - x - _pressSpot.X - _leftModifier;
                var t = e.Position.Y - y - _pressSpot.Y - _topModifier;

                if (!Suspended)
                {
                    var v = new MoveEventArgs(l, t, Left, Top);
                    OnValidateMove(v);

                    l = v.Left;
                    t = v.Top;
                }

                if (_outlineMoving)
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
                !_doubleClicks)
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

            if (ex.Button == MouseButton.Right && _contextMenu != null && !e.Handled)
            {
                _contextMenu.Show(this, ex.Position.X, ex.Position.Y);
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
            if (_movable)
            {
                var rect = _movableArea;

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
            if (_resizable)
            {
                pos.X -= AbsoluteLeft;
                pos.Y -= AbsoluteTop;

                if ((pos.X >= 0 && pos.X < _resizerSize && pos.Y >= 0 && pos.Y < Height) ||
                    (pos.X >= Width - _resizerSize && pos.X < Width && pos.Y >= 0 && pos.Y < Height) ||
                    (pos.Y >= 0 && pos.Y < _resizerSize && pos.X >= 0 && pos.X < Width) ||
                    (pos.Y >= Height - _resizerSize && pos.Y < Height && pos.X >= 0 && pos.X < Width))
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
            if (_resizable && !IsMoving)
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
                        _resizeArea == Alignment.TopRight) && (_resizeEdge & Anchors.Top) == Anchors.Top)
                    {
                        top = true;
                    }

                    else if ((_resizeArea == Alignment.BottomCenter ||
                              _resizeArea == Alignment.BottomLeft ||
                              _resizeArea == Alignment.BottomRight) && (_resizeEdge & Anchors.Bottom) == Anchors.Bottom)
                    {
                        bottom = true;
                    }

                    if ((_resizeArea == Alignment.MiddleLeft ||
                         _resizeArea == Alignment.BottomLeft ||
                         _resizeArea == Alignment.TopLeft) && (_resizeEdge & Anchors.Left) == Anchors.Left)
                    {
                        left = true;
                    }

                    else if ((_resizeArea == Alignment.MiddleRight ||
                              _resizeArea == Alignment.BottomRight ||
                              _resizeArea == Alignment.TopRight) && (_resizeEdge & Anchors.Right) == Anchors.Right)
                    {
                        right = true;
                    }

                    var w = Width;
                    var h = Height;
                    var l = Left;
                    var t = Top;

                    if (_outlineResizing && !OutlineRect.IsEmpty)
                    {
                        l = OutlineRect.Left;
                        t = OutlineRect.Top;
                        w = OutlineRect.Width;
                        h = OutlineRect.Height;
                    }

                    var px = e.Position.X - (_parent != null ? _parent.AbsoluteLeft : 0);
                    var py = e.Position.Y - (_parent != null ? _parent.AbsoluteTop : 0);

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

                    if (_outlineResizing)
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
                        return (_resizeEdge & Anchors.Top) == Anchors.Top ? Manager.Skin.Cursors["Vertical"].Resource : Cursor;
                    }
                case Alignment.BottomCenter:
                    {
                        return (_resizeEdge & Anchors.Bottom) == Anchors.Bottom ? Manager.Skin.Cursors["Vertical"].Resource : Cursor;
                    }
                case Alignment.MiddleLeft:
                    {
                        return (_resizeEdge & Anchors.Left) == Anchors.Left ? Manager.Skin.Cursors["Horizontal"].Resource : Cursor;
                    }
                case Alignment.MiddleRight:
                    {
                        return (_resizeEdge & Anchors.Right) == Anchors.Right ? Manager.Skin.Cursors["Horizontal"].Resource : Cursor;
                    }
                case Alignment.TopLeft:
                    {
                        return (_resizeEdge & Anchors.Left) == Anchors.Left && (_resizeEdge & Anchors.Top) == Anchors.Top ? Manager.Skin.Cursors["DiagonalLeft"].Resource : Cursor;
                    }
                case Alignment.BottomRight:
                    {
                        return (_resizeEdge & Anchors.Bottom) == Anchors.Bottom && (_resizeEdge & Anchors.Right) == Anchors.Right ? Manager.Skin.Cursors["DiagonalLeft"].Resource : Cursor;
                    }
                case Alignment.TopRight:
                    {
                        return (_resizeEdge & Anchors.Top) == Anchors.Top && (_resizeEdge & Anchors.Right) == Anchors.Right ? Manager.Skin.Cursors["DiagonalRight"].Resource : Cursor;
                    }
                case Alignment.BottomLeft:
                    {
                        return (_resizeEdge & Anchors.Bottom) == Anchors.Bottom && (_resizeEdge & Anchors.Left) == Anchors.Left ? Manager.Skin.Cursors["DiagonalRight"].Resource : Cursor;
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

        protected virtual void OnMouseOver(MouseEventArgs e)
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

        protected virtual void OnMove(MoveEventArgs e)
        {
            _parent?.Invalidate();

            Move?.Invoke(this, e);
        }

        protected virtual void OnResize(ResizeEventArgs e)
        {
            Invalidate();
            Resize?.Invoke(this, e);
        }

        protected virtual void OnValidateResize(ResizeEventArgs e)
        {
            ValidateResize?.Invoke(this, e);
        }

        protected virtual void OnValidateMove(MoveEventArgs e)
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

        protected virtual void OnResizeBegin(EventArgs e)
        {
            ResizeBegin?.Invoke(this, e);
        }

        protected virtual void OnResizeEnd(EventArgs e)
        {
            ResizeEnd?.Invoke(this, e);
        }

        protected virtual void OnParentResize(object sender, ResizeEventArgs e)
        {
            ProcessAnchor(e);
        }

        protected virtual void OnKeyUp(KeyEventArgs e)
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

        protected virtual void OnGamePadUp(GamePadEventArgs e)
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

        protected virtual void OnColorChanged(EventArgs e)
        {
            ColorChanged?.Invoke(this, e);
        }

        protected virtual void OnTextColorChanged(EventArgs e)
        {
            TextColorChanged?.Invoke(this, e);
        }

        protected virtual void OnBackColorChanged(EventArgs e)
        {
            BackColorChanged?.Invoke(this, e);
        }

        protected virtual void OnTextChanged(EventArgs e)
        {
            TextChanged?.Invoke(this, e);
        }

        protected virtual void OnAnchorChanged(EventArgs e)
        {
            AnchorChanged?.Invoke(this, e);
        }

        protected internal virtual void OnSkinChanged(EventArgs e)
        {
            SkinChanged?.Invoke(this, e);
        }

        protected internal virtual void OnSkinChanging(EventArgs e)
        {
            SkinChanging?.Invoke(this, e);
        }

        protected virtual void OnParentChanged(EventArgs e)
        {
            ParentChanged?.Invoke(this, e);
        }

        protected virtual void OnRootChanged(EventArgs e)
        {
            RootChanged?.Invoke(this, e);
        }

        protected virtual void OnVisibleChanged(EventArgs e)
        {
            VisibleChanged?.Invoke(this, e);
        }

        protected virtual void OnEnabledChanged(EventArgs e)
        {
            EnabledChanged?.Invoke(this, e);
        }

        protected virtual void OnAlphaChanged(EventArgs e)
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
    }

}