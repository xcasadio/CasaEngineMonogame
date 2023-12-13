using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Reflection;
using Microsoft.Xna.Framework.Input;
using System.IO;
using System.Text;
using System.Media;
using TomShane.Neoforce.Controls.Logs;
using TomShane.Neoforce.Controls.Skins;

[assembly: CLSCompliant(false)]

namespace TomShane.Neoforce.Controls;

public class Manager : DrawableGameComponent
{
    private struct ControlStates
    {
        public Control[] Buttons;
        public int Click;
        public Control Over;
    }

    internal readonly Version SkinVersion = new(0, 7);
    internal Version LayoutVersion = new(0, 7);
    internal const string _SkinDirectory = ".\\Content\\Skins\\";
    internal const string _LayoutDirectory = ".\\Content\\Layout\\";
    internal const string DefaultSkin = "Default";
    internal const string SkinExtension = ".skin";
    internal const int _MenuDelay = 500;
    internal const int _ToolTipDelay = 500;
    internal const int _DoubleClickTime = 500;
    internal const int _TextureResizeIncrement = 32;
    internal const RenderTargetUsage RenderTargetUsage = Microsoft.Xna.Framework.Graphics.RenderTargetUsage.DiscardContents;

    private bool _renderTargetValid;
    private int _targetFrames = 60;
    private long _drawTime;
    private long _updateTime;
    private ArchiveManager _content;
    private InputSystem _input;
    private bool _inputEnabled = true;
    private List<Component> _components;
    private ControlsList _controls;
    private Skin _skin;
    private string _skinName = DefaultSkin;
    private readonly IArchiveManager _archiveManager;
    private string _layoutDirectory = _LayoutDirectory;
    private string _skinDirectory = _SkinDirectory;
    private Control _focusedControl;
    private ModalContainer _modalWindow;
    private ControlStates _states;
    private KeyboardLayout _keyboardLayout;
    private List<KeyboardLayout> _keyboardLayouts = new();
    private bool _disposing;
    private bool _autoUnfocus = true;
    private bool _autoCreateRenderTarget = true;
    private IMouseStateProvider _mouseStateProvider;

    public bool DeviceReset { get; private set; }

    public ILogger Logger { get; }

    /// <summary>
    /// Gets a value indicating whether Manager is in the process of disposing.
    /// </summary>
    public virtual bool Disposing => _disposing;

    /// <summary>
    /// Gets or sets an application cursor.
    /// </summary>
    public Cursor Cursor { get; set; }

    /// <summary>
    /// Should a software cursor be drawn? Very handy on a PC build.
    /// </summary>
    public bool ShowSoftwareCursor { get; set; } = true;

    public IGraphicsDeviceService Graphics { get; }

    public Renderer Renderer { get; set; }

    /// <summary>
    /// Returns <see cref="ArchiveManager"/> used for loading assets.
    /// </summary>
    public virtual ArchiveManager Content => _content;

    /// <summary>
    /// Returns <see cref="InputSystem"/> instance responsible for managing user input.
    /// </summary>
    public virtual InputSystem Input => _input;

    /// <summary>
    /// Returns list of components added to the manager.
    /// </summary>
    public virtual IEnumerable<Component> Components => _components;

    /// <summary>
    /// Returns list of controls added to the manager.
    /// </summary>
    public virtual IEnumerable<Control> Controls => _controls;

    /// <summary>
    /// Gets or sets the depth value used for rendering sprites.
    /// </summary>
    public virtual float GlobalDepth { get; set; }

    /// <summary>
    /// Gets or sets the time that passes before the <see cref="ToolTip"/> appears.
    /// </summary>
    public virtual int ToolTipDelay { get; set; } = _ToolTipDelay;

    /// <summary>
    /// Gets or sets the time that passes before a submenu appears when hovered over menu item.
    /// </summary>
    public virtual int MenuDelay { get; set; } = _MenuDelay;

    /// <summary>
    /// Gets or sets the maximum number of milliseconds that can elapse between a first click and a second click to consider the mouse action a double-click.
    /// </summary>
    public virtual int DoubleClickTime { get; set; } = _DoubleClickTime;

    /// <summary>
    /// Gets or sets texture size increment in pixel while performing controls resizing.
    /// </summary>
    public virtual int TextureResizeIncrement { get; set; } = _TextureResizeIncrement;

    /// <summary>
    /// Enables or disables showing of tooltips globally.
    /// </summary>
    public virtual bool ToolTipsEnabled { get; set; } = true;

    /// <summary>
    /// Enables or disables input processing.                   
    /// </summary>
    public virtual bool InputEnabled
    {
        get => _inputEnabled;
        set => _inputEnabled = value;
    }

    /// <summary>
    /// Gets or sets the default render target to set after drawing.                           
    /// </summary>  
    public RenderTarget2D? DefaultRenderTarget { get; set; }

    public RenderTarget2D RenderTarget { get; private set; }

    /// <summary>
    /// Gets or sets update interval for drawing, logic and input.                           
    /// </summary>    
    public virtual int TargetFrames
    {
        get => _targetFrames;
        set => _targetFrames = value;
    }

    /// <summary>
    /// Gets or sets collection of active keyboard layouts.     
    /// </summary>
    public virtual List<KeyboardLayout> KeyboardLayouts
    {
        get => _keyboardLayouts;
        set => _keyboardLayouts = value;
    }

    /// <summary>
    /// Gets or sets a value indicating if Guide component can be used
    /// </summary>
    public bool UseGuide { get; set; }

    /// <summary>
    /// Gets or sets a value indicating if a control should unfocus if you click outside on the screen.
    /// </summary>

    public virtual bool AutoUnfocus
    {
        get => _autoUnfocus;
        set => _autoUnfocus = value;
    }

    /// <summary>
    /// Gets or sets a value indicating wheter Manager should create render target automatically.
    /// </summary>    

    public virtual bool AutoCreateRenderTarget
    {
        get => _autoCreateRenderTarget;
        set => _autoCreateRenderTarget = value;
    }

    /// <summary>
    /// Gets or sets current keyboard layout for text input.    
    /// </summary>
    public virtual KeyboardLayout KeyboardLayout
    {
        get
        {
            if (_keyboardLayout == null)
            {
                _keyboardLayout = new KeyboardLayout();
            }
            return _keyboardLayout;
        }
        set => _keyboardLayout = value;
    }

    /// <summary>
    /// Gets or sets the initial directory for looking for the skins in.
    /// </summary>
    public virtual string SkinDirectory
    {
        get
        {
            if (!_skinDirectory.EndsWith("\\"))
            {
                _skinDirectory += "\\";
            }
            return _skinDirectory;
        }
        set
        {
            _skinDirectory = value;
            if (!_skinDirectory.EndsWith("\\"))
            {
                _skinDirectory += "\\";
            }
        }
    }

    /// <summary>
    /// Gets or sets the initial directory for looking for the layout files in.
    /// </summary>
    public virtual string LayoutDirectory
    {
        get
        {
            if (!_layoutDirectory.EndsWith("\\"))
            {
                _layoutDirectory += "\\";
            }
            return _layoutDirectory;
        }
        set
        {
            _layoutDirectory = value;
            if (!_layoutDirectory.EndsWith("\\"))
            {
                _layoutDirectory += "\\";
            }
        }
    }

    /// <summary>
    /// Gets width of the selected render target in pixels.
    /// </summary>
    public virtual int TargetWidth
    {
        get
        {
            if (RenderTarget != null)
            {
                return RenderTarget.Width;
            }

            return ScreenWidth;
        }
    }

    /// <summary>
    /// Gets height of the selected render target in pixels.
    /// </summary>
    public virtual int TargetHeight
    {
        get
        {
            if (RenderTarget != null)
            {
                return RenderTarget.Height;
            }

            return ScreenHeight;
        }
    }

    /// <summary>
    /// Gets current width of the screen in pixels.
    /// </summary>
    public virtual int ScreenWidth
    {
        get
        {
            if (GraphicsDevice != null)
            {
                return GraphicsDevice.PresentationParameters.BackBufferWidth;
            }

            return 0;
        }

    }

    /// <summary>
    /// Gets current height of the screen in pixels.
    /// </summary>
    public virtual int ScreenHeight
    {
        get
        {
            if (GraphicsDevice != null)
            {
                return GraphicsDevice.PresentationParameters.BackBufferHeight;
            }

            return 0;
        }
    }

    /// <summary>
    /// Gets or sets new skin used by all controls.
    /// </summary>
    public virtual Skin Skin
    {
        get => _skin;
        set => SetSkin(value);
    }

    /// <summary>
    /// Returns currently active modal window.
    /// </summary>
    public virtual ModalContainer ModalWindow
    {
        get => _modalWindow;
        internal set
        {
            _modalWindow = value;

            if (value != null)
            {
                value.ModalResult = ModalResult.None;

                value.Visible = true;
                value.Focused = true;
            }
        }
    }

    /// <summary>
    /// Returns currently focused control.
    /// </summary>
    public virtual Control FocusedControl
    {
        get => _focusedControl;
        internal set
        {
            if (value != null && value.Visible && value.Enabled)
            {
                if (value != null && value.CanFocus)
                {
                    if (_focusedControl == null || (_focusedControl != null && value.Root != _focusedControl.Root) || !value.IsRoot)
                    {
                        if (_focusedControl != null && _focusedControl != value)
                        {
                            _focusedControl.Focused = false;
                        }
                        _focusedControl = value;
                    }
                }
                else if (value != null && !value.CanFocus)
                {
                    if (_focusedControl != null && value.Root != _focusedControl.Root)
                    {
                        if (_focusedControl != value.Root)
                        {
                            _focusedControl.Focused = false;
                        }
                        _focusedControl = value.Root;
                    }
                    else if (_focusedControl == null)
                    {
                        _focusedControl = value.Root;
                    }
                }
                BringToFront(value.Root);
            }
            else if (value == null)
            {
                _focusedControl = value;
            }
        }
    }

    internal virtual ControlsList OrderList { get; }

    /// <summary>
    /// Occurs when the GraphicsDevice settings are changed.
    /// </summary>
    public event DeviceEventHandler DeviceSettingsChanged;

    /// <summary>
    /// Occurs when the skin is about to change.
    /// </summary>
    public event SkinEventHandler SkinChanging;

    /// <summary>
    /// Occurs when the skin changes.
    /// </summary>
    public event SkinEventHandler SkinChanged;

    /// <summary>
    /// Occurs when game window is about to close.
    /// </summary>
    public event WindowClosingEventHandler WindowClosing;

    /// <summary>
    /// Initializes a new instance of the Manager class.
    /// </summary>
    /// <param name="game">
    ///     The Game class.
    /// </param>
    /// <param name="graphics">
    ///     The GraphicsDeviceManager class provided by the Game class.
    /// </param>
    /// <param name="skin">
    ///     The name of the skin being loaded at the start.
    /// </param>
    /// <param name="archiveManager"></param>
    /// <param name="logger"></param>
    public Manager(Game game, IGraphicsDeviceService graphics, string skin, IArchiveManager archiveManager, ILogger logger)
        : base(game)
    {
        _disposing = false;

        _content = new ArchiveManager(Game.Services);
        _input = new InputSystem(this, new InputOffset(0, 0, 1f, 1f));
        _components = new List<Component>();
        _controls = new ControlsList();
        OrderList = new ControlsList();

        Graphics = graphics;
        graphics.DeviceReset += GraphicsOnDeviceReset;
        graphics.DeviceResetting += GraphicsOnDeviceReset;
        graphics.DeviceCreated += GraphicsOnDeviceReset;

        _skinName = skin;
        _archiveManager = archiveManager;
        Logger = logger;

        _states.Buttons = new Control[32];
        _states.Click = -1;
        _states.Over = null;

        _input.MouseDown += MouseDownProcess;
        _input.MouseUp += MouseUpProcess;
        _input.MousePress += MousePressProcess;
        _input.MouseMove += MouseMoveProcess;
        _input.MouseScroll += MouseScrollProcess;

        _input.GamePadDown += GamePadDownProcess;
        _input.GamePadUp += GamePadUpProcess;
        _input.GamePadPress += GamePadPressProcess;

        _input.KeyDown += KeyDownProcess;
        _input.KeyUp += KeyUpProcess;
        _input.KeyPress += KeyPressProcess;

        _keyboardLayouts.Add(new KeyboardLayout());
        _keyboardLayouts.Add(new CzechKeyboardLayout());
        _keyboardLayouts.Add(new GermanKeyboardLayout());
    }

    /// <summary>
    /// Initializes a new instance of the Manager class.
    /// </summary>
    /// <param name="game">
    /// The Game class.
    /// </param>   
    /// <param name="skin">
    /// The name of the skin being loaded at the start.
    /// </param>
    public Manager(Game game, string skin, IArchiveManager archiveManager, ILogger logger)
        : this(game, game.Services.GetService<IGraphicsDeviceService>(), skin, archiveManager, logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the Manager class, loads the default skin and registers manager in the game class automatically.
    /// </summary>
    /// <param name="game">
    ///     The Game class.
    /// </param>
    /// <param name="graphics">
    ///     The GraphicsDeviceManager class provided by the Game class.
    /// </param>
    /// <param name="archiveManager"></param>
    public Manager(Game game, IGraphicsDeviceService graphics, IArchiveManager archiveManager, ILogger logger)
        : this(game, graphics, DefaultSkin, archiveManager, logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the Manager class, loads the default skin and registers manager in the game class automatically.
    /// </summary>
    /// <param name="game">
    /// The Game class.
    /// </param>
    public Manager(Game game, ILogger logger)
        : this(game,
            game.Services.GetService(typeof(IGraphicsDeviceManager)) as GraphicsDeviceManager,
            DefaultSkin,
            new ArchiveManager(game.Services),
            logger)
    {
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _disposing = true;

            // Recursively disposing all controls added to the manager and its child controls.
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

            // Disposing all components added to manager.
            if (_components != null)
            {
                var c = _components.Count;
                for (var i = 0; i < c; i++)
                {
                    if (_components.Count > 0)
                    {
                        _components[0].Dispose();
                    }
                }
            }

            if (_content != null)
            {
                _content.Unload();
                _content.Dispose();
                _content = null;
            }

            if (Renderer != null)
            {
                Renderer.Dispose();
                Renderer = null;
            }
            if (_input != null)
            {
                _input.Dispose();
                _input = null;
            }
        }
        if (GraphicsDevice != null)
        {
            GraphicsDevice.DeviceReset -= GraphicsDevice_DeviceReset;
        }

        base.Dispose(disposing);
    }

    public void SetCursor(Cursor cursor)
    {
        Cursor = cursor;
        if (Cursor.CursorTexture == null)
        {
            Cursor.CursorTexture = Texture2D.FromStream(GraphicsDevice, new FileStream(
                Cursor.CursorPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None));
        }
    }

    private void InitSkins()
    {
        // Initializing skins for every control created, even not visible or 
        // not added to the manager or another parent.
        foreach (var c in Control.Stack)
        {
            c.InitSkin();
        }
    }

    private void InitControls()
    {
        // Initializing all controls created, even not visible or 
        // not added to the manager or another parent.
        foreach (var c in Control.Stack)
        {
            c.Init();
        }
    }

    private void SortLevel(ControlsList cs)
    {
        if (cs != null)
        {
            foreach (var c in cs)
            {
                if (c.Visible)
                {
                    OrderList.Add(c);
                    SortLevel(c.Controls as ControlsList);
                }
            }
        }
    }

    /// <summary>
    /// Method used as an event handler for the GraphicsDeviceManager.PreparingDeviceSettings event.
    /// </summary>
    private void GraphicsOnDeviceReset(object sender, System.EventArgs e)
    {
        if (sender is IGraphicsDeviceService graphicsDeviceService)
        {
            var w = graphicsDeviceService.GraphicsDevice.PresentationParameters.BackBufferWidth;
            var h = graphicsDeviceService.GraphicsDevice.PresentationParameters.BackBufferHeight;

            OnScreenResize(w, h);

            DeviceSettingsChanged?.Invoke(new DeviceEventArgs(graphicsDeviceService.GraphicsDevice.PresentationParameters));
        }
    }

    public void OnScreenResized(int width, int height)
    {
        InvalidateRenderTarget();

        GraphicsDevice.PresentationParameters.BackBufferWidth = width;
        GraphicsDevice.PresentationParameters.BackBufferHeight = height;

        OnScreenResize(width, height);
    }

    public void OnScreenResize(int width, int height)
    {
        foreach (var c in Controls)
        {
            SetMaxSize(c, width, height);
        }
    }

    private void SetMaxSize(Control c, int w, int h)
    {
        if (c.Width > w)
        {
            w -= c.Skin != null ? c.Skin.OriginMargins.Horizontal : 0;
            c.Width = w;
        }
        if (c.Height > h)
        {
            h -= c.Skin != null ? c.Skin.OriginMargins.Vertical : 0;
            c.Height = h;
        }

        foreach (var cx in c.Controls)
        {
            SetMaxSize(cx, w, h);
        }
    }

    /// <summary>
    /// Initializes the controls manager.
    /// </summary>    

    public override void Initialize()
    {
        base.Initialize();

        Game.Window.ClientSizeChanged += (sender, e) =>
        {
            InvalidateRenderTarget();
        };

        if (_autoCreateRenderTarget)
        {
            RenderTarget?.Dispose();
            RenderTarget = CreateRenderTarget();
        }

        GraphicsDevice.DeviceReset += GraphicsDevice_DeviceReset;

        _input.Initialize();
        Renderer = new Renderer(this);
        SetSkin(_skinName, _archiveManager);
    }

    private void InvalidateRenderTarget()
    {
        _renderTargetValid = false;
    }

    public virtual RenderTarget2D CreateRenderTarget()
    {
        return CreateRenderTarget(ScreenWidth, ScreenHeight);
    }

    public virtual RenderTarget2D CreateRenderTarget(int width, int height)
    {
        Input.InputOffset = new InputOffset(0, 0, ScreenWidth / (float)width, ScreenHeight / (float)height);
        return new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.None, GraphicsDevice.PresentationParameters.MultiSampleCount, RenderTargetUsage);
    }

    /// <summary>
    /// Sets and loads the new skin.
    /// </summary>
    /// <param name="name">
    ///     The name of the skin being loaded.
    /// </param>
    /// <param name="archiveManager"></param>
    public virtual void SetSkin(string name = null, IArchiveManager archiveManager = null)
    {
        var skin = new Skin(this, name ?? _skinName, archiveManager ?? _archiveManager);
        SetSkin(skin);
    }

    /// <summary>
    /// Sets the new skin.
    /// </summary>
    /// <param name="skin">
    /// The skin being set.
    /// </param>
    public virtual void SetSkin(Skin skin)
    {
        SkinChanging?.Invoke(new EventArgs());

        if (_skin != null)
        {
            Remove(_skin);
            _skin.Dispose();
            _skin = null;
            GC.Collect();
        }
        _skin = skin;
        _skin.Init();
        Add(_skin);
        _skinName = _skin.Name;

        if (_skin.Cursors["Default"] != null)
        {
            SetCursor(_skin.Cursors["Default"].Resource);
        }

        InitSkins();
        SkinChanged?.Invoke(new EventArgs());

        InitControls();
    }

    /// <summary>
    /// Brings the control to the front of the z-order.
    /// </summary>
    /// <param name="control">
    /// The control being brought to the front.
    /// </param>
    public virtual void BringToFront(Control control)
    {
        if (control != null && !control.StayOnBack)
        {
            var cs = control.Parent == null ? _controls as ControlsList : control.Parent.Controls as ControlsList;
            if (cs.Contains(control))
            {
                cs.Remove(control);
                if (!control.StayOnTop)
                {
                    var pos = cs.Count;
                    for (var i = cs.Count - 1; i >= 0; i--)
                    {
                        if (!cs[i].StayOnTop)
                        {
                            break;
                        }
                        pos = i;
                    }
                    cs.Insert(pos, control);
                }
                else
                {
                    cs.Add(control);
                }
            }
        }
    }

    /// <summary>
    /// Sends the control to the back of the z-order.
    /// </summary>
    /// <param name="control">
    /// The control being sent back.
    /// </param>
    public virtual void SendToBack(Control control)
    {
        if (control != null && !control.StayOnTop)
        {
            var cs = control.Parent == null ? _controls as ControlsList : control.Parent.Controls as ControlsList;
            if (cs.Contains(control))
            {
                cs.Remove(control);
                if (!control.StayOnBack)
                {
                    var pos = 0;
                    for (var i = 0; i < cs.Count; i++)
                    {
                        if (!cs[i].StayOnBack)
                        {
                            break;
                        }
                        pos = i;
                    }
                    cs.Insert(pos, control);
                }
                else
                {
                    cs.Insert(0, control);
                }
            }
        }
    }

    /// <summary>
    /// Called when the manager needs to be updated.
    /// </summary>
    /// <param name="gameTime">
    /// Time elapsed since the last call to Update.
    /// </param>
    public override void Update(GameTime gameTime)
    {
        _updateTime += gameTime.ElapsedGameTime.Ticks;
        var ms = TimeSpan.FromTicks(_updateTime).TotalMilliseconds;

        if (_targetFrames == 0 || ms == 0 || ms >= 1000f / _targetFrames)
        {
            var span = TimeSpan.FromTicks(_updateTime);
            gameTime = new GameTime(gameTime.TotalGameTime, span);
            _updateTime = 0;

            if (_inputEnabled)
            {
                _input.Update(gameTime);
            }

            if (_components != null)
            {
                foreach (var c in _components)
                {
                    c.Update(gameTime);
                }
            }

            var list = new ControlsList(_controls);

            foreach (var c in list)
            {
                c.Update(gameTime);
            }

            OrderList.Clear();
            SortLevel(_controls);
        }
    }

    /// <summary>
    /// Adds a component or a control to the manager.
    /// </summary>
    /// <param name="component">
    /// The component or control being added.
    /// </param>
    public virtual void Add(Component component)
    {
        if (component != null)
        {
            if (component is Control item && !_controls.Contains(item))
            {
                item.Parent?.Remove(item);

                _controls.Add(item);
                item.Manager = this;
                item.Parent = null;
                if (_focusedControl == null)
                {
                    item.Focused = true;
                }

                DeviceSettingsChanged += item.OnDeviceSettingsChanged;
                SkinChanging += item.OnSkinChanging;
                SkinChanged += item.OnSkinChanged;
            }
            else if (!(component is Control) && !_components.Contains(component))
            {
                _components.Add(component);
                component.Manager = this;
            }
        }
    }

    /// <summary>
    /// Removes a component or a control from the manager.
    /// </summary>
    /// <param name="component">
    /// The component or control being removed.
    /// </param>
    public virtual void Remove(Component component)
    {
        if (component != null)
        {
            if (component is Control control)
            {
                SkinChanging -= control.OnSkinChanging;
                SkinChanged -= control.OnSkinChanged;
                DeviceSettingsChanged -= control.OnDeviceSettingsChanged;

                if (control.Focused)
                {
                    control.Focused = false;
                }

                _controls.Remove(control);
            }
            else
            {
                _components.Remove(component);
            }
        }
    }

    public virtual void Prepare(GameTime gameTime)
    {

    }

    /// <summary>
    /// Renders all controls added to the manager.
    /// </summary>
    /// <param name="gameTime">
    /// Time passed since the last call to Draw.
    /// </param>
    public virtual void BeginDraw(GameTime gameTime)
    {
        if (!_renderTargetValid && AutoCreateRenderTarget)
        {
            if (RenderTarget != null)
            {
                RenderTarget.Dispose();
            }

            RenderTarget = CreateRenderTarget();
            Renderer = new Renderer(this);
            _renderTargetValid = true;
        }
    }

    public override void Draw(GameTime gameTime)
    {
        BeginDraw(gameTime);
        DrawInternal(gameTime);
    }

    private void DrawInternal(GameTime gameTime)
    {
        if (RenderTarget != null)
        {
            /*_drawTime += gameTime.ElapsedGameTime.Ticks;
            var span = TimeSpan.FromTicks(_drawTime);
            gameTime = new GameTime(gameTime.TotalGameTime, span);
            _drawTime = 0;*/

            if (_controls != null)
            {
                var list = new ControlsList();
                list.AddRange(_controls);

                foreach (var c in list)
                {
                    c.PrepareTexture(Renderer, gameTime);
                }

                GraphicsDevice.SetRenderTarget(RenderTarget);
                GraphicsDevice.Clear(Color.CornflowerBlue);

                if (Renderer != null)
                {
                    foreach (var c in list)
                    {
                        c.Render(Renderer, gameTime);
                    }
                }
            }

            if (ShowSoftwareCursor && Cursor != null)
            {
                Renderer.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
                var mstate = _mouseStateProvider?.GetState() ?? Mouse.GetState();
                var rect = new Rectangle(mstate.X, mstate.Y, Cursor.Width, Cursor.Height);
                Renderer.SpriteBatch.Draw(Cursor.CursorTexture, rect, null, Color.White, 0f, Cursor.HotSpot, SpriteEffects.None, 0f);
                Renderer.SpriteBatch.End();
            }
            /*
            var fileStream = File.Create("d:\\screenshot_renderer.jpeg");
            RenderTarget.SaveAsJpeg(fileStream, RenderTarget.Width, RenderTarget.Height);
            fileStream.Dispose();*/

            GraphicsDevice.SetRenderTarget(DefaultRenderTarget);
        }
        else
        {
            Logger.WriteError("Manager.RenderTarget has to be specified. Assign a render target or set Manager.AutoCreateRenderTarget property to true.");
        }
    }

    /// <summary>
    /// Draws texture resolved from RenderTarget used for rendering.
    /// </summary>

    public virtual void EndDraw()
    {
        EndDraw(new Rectangle(0, 0, ScreenWidth, ScreenHeight));
    }

    /// <summary>
    /// Draws texture resolved from RenderTarget to specified rectangle.
    /// </summary>

    public virtual void EndDraw(Rectangle rect)
    {
        if (RenderTarget != null && !DeviceReset)
        {
            Renderer.Begin(BlendingMode.Default);
            Renderer.Draw(RenderTarget, rect, Color.White);
            Renderer.End();
        }
        else if (DeviceReset)
        {
            DeviceReset = false;
        }
    }

    public virtual Control GetControl(string name)
    {
        foreach (var c in Controls)
        {
            if (string.Equals(c.Name, name, StringComparison.InvariantCultureIgnoreCase))
            {
                return c;
            }
        }
        return null;
    }

    private void GraphicsDevice_DeviceReset(object sender, System.EventArgs e)
    {
        DeviceReset = true;
        InvalidateRenderTarget();
    }

    private bool CheckParent(Control control, Point pos)
    {
        if (control.Parent != null && !CheckDetached(control))
        {
            var parent = control.Parent;
            var root = control.Root;

            var pr = new Rectangle(parent.AbsoluteLeft,
                parent.AbsoluteTop,
                parent.Width,
                parent.Height);

            var margins = root.Skin.ClientMargins;
            var rr = new Rectangle(root.AbsoluteLeft + margins.Left,
                root.AbsoluteTop + margins.Top,
                root.OriginWidth - margins.Horizontal,
                root.OriginHeight - margins.Vertical);

            return rr.Contains(pos) && pr.Contains(pos);
        }

        return true;
    }

    private bool CheckState(Control control)
    {
        var modal = ModalWindow == null ? true : ModalWindow == control.Root;

        return control != null && !control.Passive && control.Visible && control.Enabled && modal;
    }

    private bool CheckOrder(Control control, Point pos)
    {
        if (!CheckPosition(control, pos))
        {
            return false;
        }

        for (var i = OrderList.Count - 1; i > OrderList.IndexOf(control); i--)
        {
            var c = OrderList[i];

            if (!c.Passive && CheckPosition(c, pos) && CheckParent(c, pos))
            {
                return false;
            }
        }

        return true;
    }

    private bool CheckDetached(Control control)
    {
        var ret = control.Detached;
        if (control.Parent != null)
        {
            if (CheckDetached(control.Parent))
            {
                ret = true;
            }
        }
        return ret;
    }

    private bool CheckPosition(Control control, Point pos)
    {
        return control.AbsoluteLeft <= pos.X &&
               control.AbsoluteTop <= pos.Y &&
               control.AbsoluteLeft + control.Width >= pos.X &&
               control.AbsoluteTop + control.Height >= pos.Y &&
               CheckParent(control, pos);
    }

    private bool CheckButtons(int index)
    {
        for (var i = 0; i < _states.Buttons.Length; i++)
        {
            if (i == index)
            {
                continue;
            }

            if (_states.Buttons[i] != null)
            {
                return false;
            }
        }

        return true;
    }

    private void TabNextControl(Control control)
    {
        var start = OrderList.IndexOf(control);
        var i = start;

        do
        {
            if (i < OrderList.Count - 1)
            {
                i += 1;
            }
            else
            {
                i = 0;
            }
        }
        while ((OrderList[i].Root != control.Root || !OrderList[i].CanFocus || OrderList[i].IsRoot || !OrderList[i].Enabled) && i != start);

        OrderList[i].Focused = true;
    }

    private void TabPrevControl(Control control)
    {
        var start = OrderList.IndexOf(control);
        var i = start;

        do
        {
            if (i > 0)
            {
                i -= 1;
            }
            else
            {
                i = OrderList.Count - 1;
            }
        }
        while ((OrderList[i].Root != control.Root || !OrderList[i].CanFocus || OrderList[i].IsRoot || !OrderList[i].Enabled) && i != start);
        OrderList[i].Focused = true;
    }

    private void ProcessArrows(Control control, KeyEventArgs kbe, GamePadEventArgs gpe)
    {
        var c = control;
        if (c.Parent != null && c.Parent.Controls != null)
        {
            var index = -1;

            if ((kbe.Key == Keys.Left && !kbe.Handled) ||
                (gpe.Button == c.GamePadActions.Left && !gpe.Handled))
            {
                var miny = int.MaxValue;
                var minx = int.MinValue;
                for (var i = 0; i < (c.Parent.Controls as ControlsList).Count; i++)
                {
                    var cx = (c.Parent.Controls as ControlsList)[i];
                    if (cx == c || !cx.Visible || !cx.Enabled || cx.Passive || !cx.CanFocus)
                    {
                        continue;
                    }

                    var cay = c.Top + c.Height / 2;
                    var cby = cx.Top + cx.Height / 2;

                    if (Math.Abs(cay - cby) <= miny && cx.Left + cx.Width >= minx && cx.Left + cx.Width <= c.Left)
                    {
                        miny = Math.Abs(cay - cby);
                        minx = cx.Left + cx.Width;
                        index = i;
                    }
                }
            }
            else if ((kbe.Key == Keys.Right && !kbe.Handled) ||
                     (gpe.Button == c.GamePadActions.Right && !gpe.Handled))
            {
                var miny = int.MaxValue;
                var minx = int.MaxValue;
                for (var i = 0; i < (c.Parent.Controls as ControlsList).Count; i++)
                {
                    var cx = (c.Parent.Controls as ControlsList)[i];
                    if (cx == c || !cx.Visible || !cx.Enabled || cx.Passive || !cx.CanFocus)
                    {
                        continue;
                    }

                    var cay = c.Top + c.Height / 2;
                    var cby = cx.Top + cx.Height / 2;

                    if (Math.Abs(cay - cby) <= miny && cx.Left <= minx && cx.Left >= c.Left + c.Width)
                    {
                        miny = Math.Abs(cay - cby);
                        minx = cx.Left;
                        index = i;
                    }
                }
            }
            else if ((kbe.Key == Keys.Up && !kbe.Handled) ||
                     (gpe.Button == c.GamePadActions.Up && !gpe.Handled))
            {
                var miny = int.MinValue;
                var minx = int.MaxValue;
                for (var i = 0; i < (c.Parent.Controls as ControlsList).Count; i++)
                {
                    var cx = (c.Parent.Controls as ControlsList)[i];
                    if (cx == c || !cx.Visible || !cx.Enabled || cx.Passive || !cx.CanFocus)
                    {
                        continue;
                    }

                    var cax = c.Left + c.Width / 2;
                    var cbx = cx.Left + cx.Width / 2;

                    if (Math.Abs(cax - cbx) <= minx && cx.Top + cx.Height >= miny && cx.Top + cx.Height <= c.Top)
                    {
                        minx = Math.Abs(cax - cbx);
                        miny = cx.Top + cx.Height;
                        index = i;
                    }
                }
            }
            else if ((kbe.Key == Keys.Down && !kbe.Handled) ||
                     (gpe.Button == c.GamePadActions.Down && !gpe.Handled))
            {
                var miny = int.MaxValue;
                var minx = int.MaxValue;
                for (var i = 0; i < (c.Parent.Controls as ControlsList).Count; i++)
                {
                    var cx = (c.Parent.Controls as ControlsList)[i];
                    if (cx == c || !cx.Visible || !cx.Enabled || cx.Passive || !cx.CanFocus)
                    {
                        continue;
                    }

                    var cax = c.Left + c.Width / 2;
                    var cbx = cx.Left + cx.Width / 2;

                    if (Math.Abs(cax - cbx) <= minx && cx.Top <= miny && cx.Top >= c.Top + c.Height)
                    {
                        minx = Math.Abs(cax - cbx);
                        miny = cx.Top;
                        index = i;
                    }
                }
            }

            if (index != -1)
            {
                (c.Parent.Controls as ControlsList)[index].Focused = true;
                kbe.Handled = true;
                gpe.Handled = true;
            }
        }
    }

    private void MouseDownProcess(object sender, MouseEventArgs e)
    {
        var c = new ControlsList();
        c.AddRange(OrderList);

        if (_autoUnfocus && _focusedControl != null && _focusedControl.Root != _modalWindow)
        {
            var hit = false;

            foreach (var cx in Controls)
            {
                if (cx.AbsoluteRect.Contains(e.Position))
                {
                    hit = true;
                    break;
                }
            }
            if (!hit)
            {
                for (var i = 0; i < Control.Stack.Count; i++)
                {
                    if (Control.Stack[i].Visible && Control.Stack[i].Detached && Control.Stack[i].AbsoluteRect.Contains(e.Position))
                    {
                        hit = true;
                        break;
                    }
                }
            }
            if (!hit)
            {
                _focusedControl.Focused = false;
            }
        }

        for (var i = c.Count - 1; i >= 0; i--)
        {
            if (CheckState(c[i]) && CheckPosition(c[i], e.Position))
            {
                _states.Buttons[(int)e.Button] = c[i];
                c[i].SendMessage(Message.MouseDown, e);

                if (_states.Click == -1)
                {
                    _states.Click = (int)e.Button;

                    FocusedControl?.Invalidate();
                    c[i].Focused = true;
                }
                return;
            }
        }

        if (ModalWindow != null)
        {
            SystemSounds.Beep.Play();
        }
    }

    private void MouseUpProcess(object sender, MouseEventArgs e)
    {
        var c = _states.Buttons[(int)e.Button];
        if (c != null)
        {
            if (CheckPosition(c, e.Position) && CheckOrder(c, e.Position) && _states.Click == (int)e.Button && CheckButtons((int)e.Button))
            {
                c.SendMessage(Message.Click, e);
            }
            _states.Click = -1;
            c.SendMessage(Message.MouseUp, e);
            _states.Buttons[(int)e.Button] = null;
            MouseMoveProcess(sender, e);
        }
    }

    private void MousePressProcess(object sender, MouseEventArgs e)
    {
        var c = _states.Buttons[(int)e.Button];
        if (c != null)
        {
            if (CheckPosition(c, e.Position))
            {
                c.SendMessage(Message.MousePress, e);
            }
        }
    }

    private void MouseMoveProcess(object sender, MouseEventArgs e)
    {
        var c = new ControlsList();
        c.AddRange(OrderList);

        for (var i = c.Count - 1; i >= 0; i--)
        {
            var chpos = CheckPosition(c[i], e.Position);
            var chsta = CheckState(c[i]);

            if (chsta && ((chpos && _states.Over == c[i]) || _states.Buttons[(int)e.Button] == c[i]))
            {
                c[i].SendMessage(Message.MouseMove, e);
                break;
            }
        }

        for (var i = c.Count - 1; i >= 0; i--)
        {
            var chpos = CheckPosition(c[i], e.Position);
            var chsta = CheckState(c[i]) || (c[i].ToolTip.Text != "" && c[i].ToolTip.Text != null && c[i].Visible);

            if (chsta && !chpos && _states.Over == c[i] && _states.Buttons[(int)e.Button] == null)
            {
                _states.Over = null;
                c[i].SendMessage(Message.MouseOut, e);
                break;
            }
        }

        for (var i = c.Count - 1; i >= 0; i--)
        {
            var chpos = CheckPosition(c[i], e.Position);
            var chsta = CheckState(c[i]) || (c[i].ToolTip.Text != "" && c[i].ToolTip.Text != null && c[i].Visible);

            if (chsta && chpos && _states.Over != c[i] && _states.Buttons[(int)e.Button] == null)
            {
                _states.Over?.SendMessage(Message.MouseOut, e);
                _states.Over = c[i];
                c[i].SendMessage(Message.MouseOver, e);
                break;
            }

            if (_states.Over == c[i])
            {
                break;
            }
        }
    }

    /// <summary>
    /// Processes mouse scroll events for the manager.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void MouseScrollProcess(object sender, MouseEventArgs e)
    {
        var c = new ControlsList();
        c.AddRange(OrderList);

        for (var i = c.Count - 1; i >= 0; i--)
        {
            var chpos = CheckPosition(c[i], e.Position);
            var chsta = CheckState(c[i]);

            if (chsta && chpos && _states.Over == c[i])
            {
                c[i].SendMessage(Message.MouseScroll, e);
                break;
            }
        }
    }

    void GamePadDownProcess(object sender, GamePadEventArgs e)
    {
        var c = FocusedControl;

        if (c != null && CheckState(c))
        {
            if (_states.Click == -1)
            {
                _states.Click = (int)e.Button;
            }
            _states.Buttons[(int)e.Button] = c;
            c.SendMessage(Message.GamePadDown, e);

            if (e.Button == c.GamePadActions.Click)
            {
                c.SendMessage(Message.Click, new MouseEventArgs(new MouseState(), MouseButton.None, Point.Zero));
            }
        }
    }

    void GamePadUpProcess(object sender, GamePadEventArgs e)
    {
        var c = _states.Buttons[(int)e.Button];

        if (c != null)
        {
            if (e.Button == c.GamePadActions.Press)
            {
                c.SendMessage(Message.Click, new MouseEventArgs(new MouseState(), MouseButton.None, Point.Zero));
            }
            _states.Click = -1;
            _states.Buttons[(int)e.Button] = null;
            c.SendMessage(Message.GamePadUp, e);
        }
    }

    void GamePadPressProcess(object sender, GamePadEventArgs e)
    {
        var c = _states.Buttons[(int)e.Button];
        if (c != null)
        {
            c.SendMessage(Message.GamePadPress, e);

            if ((e.Button == c.GamePadActions.Right ||
                 e.Button == c.GamePadActions.Left ||
                 e.Button == c.GamePadActions.Up ||
                 e.Button == c.GamePadActions.Down) && !e.Handled && CheckButtons((int)e.Button))
            {
                ProcessArrows(c, new KeyEventArgs(), e);
                GamePadDownProcess(sender, e);
            }
            else if (e.Button == c.GamePadActions.NextControl && !e.Handled && CheckButtons((int)e.Button))
            {
                TabNextControl(c);
                GamePadDownProcess(sender, e);
            }
            else if (e.Button == c.GamePadActions.PrevControl && !e.Handled && CheckButtons((int)e.Button))
            {
                TabPrevControl(c);
                GamePadDownProcess(sender, e);
            }
        }
    }

    void KeyDownProcess(object sender, KeyEventArgs e)
    {
        var c = FocusedControl;

        if (c != null && CheckState(c))
        {
            if (_states.Click == -1)
            {
                _states.Click = (int)MouseButton.None;
            }
            _states.Buttons[(int)MouseButton.None] = c;
            c.SendMessage(Message.KeyDown, e);

            if (e.Key == Keys.Enter)
            {
                c.SendMessage(Message.Click, new MouseEventArgs(new MouseState(), MouseButton.None, Point.Zero));
            }
        }
    }

    void KeyUpProcess(object sender, KeyEventArgs e)
    {
        var c = _states.Buttons[(int)MouseButton.None];

        if (c != null)
        {
            if (e.Key == Keys.Space)
            {
                c.SendMessage(Message.Click, new MouseEventArgs(new MouseState(), MouseButton.None, Point.Zero));
            }
            _states.Click = -1;
            _states.Buttons[(int)MouseButton.None] = null;
            c.SendMessage(Message.KeyUp, e);
        }
    }

    void KeyPressProcess(object sender, KeyEventArgs e)
    {
        var c = _states.Buttons[(int)MouseButton.None];
        if (c != null)
        {
            c.SendMessage(Message.KeyPress, e);

            if ((e.Key == Keys.Right ||
                 e.Key == Keys.Left ||
                 e.Key == Keys.Up ||
                 e.Key == Keys.Down) && !e.Handled && CheckButtons((int)MouseButton.None))
            {
                ProcessArrows(c, e, new GamePadEventArgs(PlayerIndex.One));
                KeyDownProcess(sender, e);
            }
            else if (e.Key == Keys.Tab && !e.Shift && !e.Handled && CheckButtons((int)MouseButton.None))
            {
                TabNextControl(c);
                KeyDownProcess(sender, e);
            }
            else if (e.Key == Keys.Tab && e.Shift && !e.Handled && CheckButtons((int)MouseButton.None))
            {
                TabPrevControl(c);
                KeyDownProcess(sender, e);
            }
        }
    }

    public void SetProviders(IKeyboardStateProvider keyboardStateProvider, IMouseStateProvider mouseStateProvider)
    {
        _mouseStateProvider = mouseStateProvider;
        _input.SetProviders(keyboardStateProvider, mouseStateProvider);
    }
}