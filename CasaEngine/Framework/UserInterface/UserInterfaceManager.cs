
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

#if (!XBOX)
#endif
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Textures;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.UserInterface.Controls.Windows;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Cursor = CasaEngine.Framework.UserInterface.Cursors.Cursor;

namespace CasaEngine.Framework.UserInterface;

public class UserInterfaceManager
{

    /*private sealed class ScripUserInterface : Script
    {

        public override void Update()
        {
            UserInterfaceManager.Update();
        }

        public override void PreRenderUpdate()
        {
            UserInterfaceManager.PreRenderControls();
        }

        public override void PostRenderUpdate()
        {
            UserInterfaceManager.RenderUserInterfaceToScreen();
        }

    } // ScripUserInterface
    */

    private struct ControlStates
    {
        public Control[] Buttons;
        public int Click;
        public Control Over;
    } // ControlStates

#if (!XBOX)
    private Cursor _cursor;
    //private Form _window;
#endif

    // Main render target, when the UI will be render.
    private RenderTarget _renderTarget;

    private Control _focusedControl;
    private ModalContainer _modalWindow;
    private ControlStates _states;

    // To avoid more than one initialization.
    private bool _initialized;

    // Used to call the update and render method in the correct order without explicit calls.
    //private GameObject userInterfaceGameObject;

    // Used to generate the resize event.
    private int _oldScreenWidth, _oldScreenHeight;

    public GraphicsDevice GraphicsDevice { get; private set; }
    public AssetContentManager AssetContentManager { get; private set; }
    public CasaEngineGame Game { get; private set; }

#if (!XBOX)

    public Cursor Cursor
    {
        get => _cursor;
        set
        {
            _cursor = value;

            //if (_window.InvokeRequired)
            //{
            //    _window.Invoke(new Action(() => _window.Cursor = value.Resource));
            //}
            //else
            //{
            //    _window.Cursor = value.Resource;
            //}
        }
    } // Cursor

#endif

    internal Skin Skin { get; private set; }

    internal Renderer Renderer { get; private set; }

    internal Screen Screen { get; private set; }

    public bool Visible { get; set; }

    public bool InputEnabled { get; set; }

    public Input InputSystem { get; set; }

    public ControlsList RootControls { get; private set; }

    internal ControlsList OrderList { get; private set; }

    public int ToolTipDelay { get; set; }

    public int MenuDelay { get; set; }

    public int DoubleClickTime { get; set; }

    public int TextureResizeIncrement { get; set; }

    public bool ToolTipsEnabled { get; set; }

    public bool AutoUnfocus { get; set; }

    public ModalContainer ModalWindow
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
    } // ModalWindow

    public Control FocusedControl
    {
        get
        {
            if (Visible)
            {
                return _focusedControl;
            }

            return null;
        }
        internal set
        {
            if (value != null && value.Visible && value.Enabled)
            {
                if (value.CanFocus)
                {
                    if (_focusedControl == null || _focusedControl != null && value.Root != _focusedControl.Root || !value.IsRoot)
                    {
                        if (_focusedControl != null && _focusedControl != value)
                        {
                            _focusedControl.Focused = false;
                        }
                        _focusedControl = value;
                    }
                }
                else
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
            else
            {
                _focusedControl = null;
            }
        }
    } // FocusedControl

    //internal AssetContentManager UserInterfaceContentManager { get; private set; }

    internal event EventHandler? DeviceReset;

    public event SkinEventHandler? SkinChanging;

    public event SkinEventHandler? SkinChanged;

    public event WindowClosingEventHandler? WindowClosing;

    public event ResizeEventHandler? WindowResize;

    public void Initialize(CasaEngineGame game, IntPtr? formHandle, Rectangle gameWindowClientBounds)
    {
        if (_initialized)
        {
            return;
        }

        try
        {
            Game = game;

            Skin = new Skin();
            Renderer = new Renderer();
            Screen = new Screen(game.GraphicsDevice);

            GraphicsDevice = game.GraphicsDevice;
            AssetContentManager = game.GameManager.AssetContentManager;
            Visible = true;
            InputEnabled = true;
            _initialized = true;
            // Set some public parameters.
            TextureResizeIncrement = 32;
            ToolTipDelay = 500;
            AutoUnfocus = true;
            ToolTipsEnabled = true;

#if (WINDOWS)
            MenuDelay = SystemInformation.MenuShowDelay;
            DoubleClickTime = SystemInformation.DoubleClickTime;
            //_window = (Form)System.Windows.Forms.Control.FromHandle(formHandle);
            //_window.FormClosing += FormClosing;
#endif

            RootControls = new ControlsList();
            OrderList = new ControlsList();

            Game.GraphicsDevice.DeviceReset += OnDeviceReset;

            _states.Buttons = new Control[32];
            _states.Click = -1;
            _states.Over = null;

            // Input events
            InputSystem = new Input(gameWindowClientBounds);
            InputSystem.MouseDown += MouseDownProcess;
            InputSystem.MouseUp += MouseUpProcess;
            InputSystem.MousePress += MousePressProcess;
            InputSystem.MouseMove += MouseMoveProcess;
            InputSystem.KeyDown += KeyDownProcess;
            InputSystem.KeyUp += KeyUpProcess;
            InputSystem.KeyPress += KeyPressProcess;

            // Final render target.
            /*AssetContentManager userContentManager = AssetContentManager.CurrentContentManager;
            UserInterfaceContentManager = new AssetContentManager { Name = "User Interface Content Manager", Hidden = true };
            AssetContentManager.CurrentContentManager = UserInterfaceContentManager;*/
            var size = new Core.Helpers.Size(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, Screen);
            _renderTarget = new RenderTarget(Game.GameManager.AssetContentManager, Game.GraphicsDevice, size.FullScreen,
                SurfaceFormat.Color, false)
            {
                Name = "User Interface Render Target",
            };
            //AssetContentManager.CurrentContentManager = userContentManager;

            // Init User Interface UserInterfaceManager.Renderer.
            Renderer.Initialize(Game.GraphicsDevice);

            // Set Default UserInterfaceManager.Skin.
            SetSkin("Default");

            // Window resize.
            _oldScreenWidth = Screen.Width;
            _oldScreenHeight = Screen.Height;
            Screen.ScreenSizeChanged += OnScreenSizeChanged;

            //warning in XNAFinalEngine this it call in Game.LoadContent() !!
            //To test
            /*GameInfo.Instance.Game.GraphicsDevice.Disposing += delegate
            {
                UserInterfaceManager.Renderer.Initialize();
                // Invalidate all controls.
                OnDeviceReset(null, new EventArgs());
                SetSkin(UserInterfaceManager.Skin.CurrentSkinName);
            };*/
        }
        catch (Exception e)
        {
            throw new InvalidOperationException("User Interface Manager: Error occurred during initialization. Was the engine started?", e);
        }
    }

    private void OnDeviceReset(object? sender, System.EventArgs e)
    {
        DeviceReset?.Invoke(sender, new EventArgs());
    }

    private void OnScreenSizeChanged(object? sender, System.EventArgs e)
    {
        WindowResize?.Invoke(null, new ResizeEventArgs(GraphicsDevice.PresentationParameters.BackBufferWidth,
            GraphicsDevice.PresentationParameters.BackBufferHeight, _oldScreenWidth, _oldScreenHeight));
        _oldScreenWidth = GraphicsDevice.PresentationParameters.BackBufferWidth;
        _oldScreenHeight = GraphicsDevice.PresentationParameters.BackBufferHeight;
    }

#if (WINDOWS)

    private void FormClosing(object? sender, FormClosingEventArgs e)
    {
        var ret = false;

        var ex = new WindowClosingEventArgs();
        if (WindowClosing != null)
        {
            WindowClosing.Invoke(null, ex);
            ret = ex.Cancel;
        }
        e.Cancel = ret;
    }

#endif

    public void DisposeControls()
    {
        try
        {
            foreach (var control in RootControls)
            {
                control.Dispose();
            }
            RootControls.Clear();
            OrderList.Clear();
            FocusedControl = null;
        }
        catch (Exception e)
        {
            throw new InvalidOperationException("User Interface Manager: Unable to dispose controls. Was the User Interface Manager started?", e);
        }
    }

    public void SetSkin(string skinName)
    {
        SkinChanging?.Invoke(new EventArgs());

        Skin.LoadSkin(Game, skinName);

#if (!XBOX)
        if (Skin.Cursors["Default"] != null)
        {
            Cursor = Skin.Cursors["Default"].Cursor;
        }
#endif

        // Initializing skins for every control created, even not visible or not added to the manager or another control.
        foreach (var control in Control.ControlList)
        {
            control.InitSkin();
        }

        SkinChanged?.Invoke(new EventArgs());

        //  Initializing all controls created, even not visible or not added to the manager or another control.
        foreach (var control in Control.ControlList)
        {
            control.Init();
        }
    } // SetSkin

    internal void BringToFront(Control control)
    {
        if (control != null && !control.StayOnBack)
        {
            // We search for the control's brothers.
            var brotherControls = control.Parent == null ? RootControls : control.Parent.ChildrenControls;
            if (brotherControls.Contains(control)) // The only case in which is false is when the control was not added to anything.
            {
                brotherControls.Remove(control);
                if (!control.StayOnTop)
                {
                    // We try to insert the control the higher that we can in the sorted list
                    var newControlPosition = brotherControls.Count;
                    for (var i = brotherControls.Count - 1; i >= 0; i--)
                    {
                        if (!brotherControls[i].StayOnTop) // If there is a control that has to be in top then we won't go any further.
                        {
                            break;
                        }

                        newControlPosition = i;
                    }
                    brotherControls.Insert(newControlPosition, control);
                }
                else
                {
                    brotherControls.Add(control);
                }
            }
        }
    } // BringToFront

    internal void SendToBack(Control control)
    {
        if (control != null && !control.StayOnTop)
        {
            var brotherControls = control.Parent == null ? RootControls : control.Parent.ChildrenControls;
            if (brotherControls.Contains(control))
            {
                brotherControls.Remove(control);
                if (!control.StayOnBack)
                {
                    var newControlPosition = 0;
                    for (var i = 0; i < brotherControls.Count; i++)
                    {
                        if (!brotherControls[i].StayOnBack)
                        {
                            break;
                        }

                        newControlPosition = i;
                    }
                    brotherControls.Insert(newControlPosition, control);
                }
                else
                {
                    brotherControls.Insert(0, control);
                }
            }
        }
    } // SendToBack

    public void Update(float elapsedTime)
    {
        if (!Visible || !InputEnabled)
        {
            return;
        }

        //try
        {
            // Init new controls.
            Control.InitializeNewControls();

            InputSystem.Update(elapsedTime);

            // In the control's update the Root Control list could be modified so we need to create an auxiliary list.
            var controlList = new ControlsList(RootControls);
            foreach (var control in controlList)
            {
                control.Update(elapsedTime);
            }
            OrderList.Clear();
            SortLevel(RootControls);
        }
        /*catch (Exception exception)
        {
            throw new InvalidOperationException("User Interface Manager: Update failed.", exception);
        }*/
    } // Update

    private void SortLevel(ControlsList controlList)
    {
        if (controlList != null)
        {
            for (var i = 0; i < controlList.Count; i++)
            {
                if (controlList[i].Visible)
                {
                    OrderList.Add(controlList[i]);
                    SortLevel(controlList[i].ChildrenControls);
                }
            }
        }
    } // SortLevel

    internal void Add(Control control)
    {
        if (control != null)
        {
            // If the control father is the manager...
            if (!RootControls.Contains(control))
            {
                control.Parent?.Remove(control);
                RootControls.Add(control);
                control.Parent = null;
                if (_focusedControl == null)
                {
                    control.Focused = true;
                }

                WindowResize += control.OnParentResize;
            }
        }
    } // Add

    internal void Remove(Control control)
    {
        if (control != null)
        {
            if (control.Focused)
            {
                control.Focused = false;
            }

            RootControls.Remove(control);
            // IsRemoved event
            WindowResize -= control.OnParentResize;
        }
    } // IsRemoved

    public void PreRenderControls()
    {
        if (!Visible)
        {
            return;
        }

        if (RootControls != null)
        {
            // Render each control in its own render target.
            foreach (var control in RootControls)
            {
                control.PreDrawControlOntoOwnTexture();
            }
            // Draw user interface texture.
            _renderTarget.EnableRenderTarget();
            //renderTarget.Clear(Color.Transparent);
            GraphicsDevice.Clear(ClearOptions.Target, Color.Transparent, 1.0f, 0);

            //GameInfo.Instance.Game.GraphicsDevice.Clear(Color.Transparent);
            foreach (var control in RootControls)
            {
                control.DrawControlOntoMainTexture();
            }
            _renderTarget.DisableRenderTarget();
        }
    } // DrawToTexture

    public void RenderUserInterfaceToScreen()
    {
        if (!Visible)
        {
            return;
        }

        if (RootControls != null)
        {
            Renderer.Begin();
            Renderer.Draw(_renderTarget.Resource, new Rectangle(0, 0, Screen.Width, Screen.Height), Color.White);
            Renderer.End();
        }
    } // DrawTextureToScreen

    private bool CheckParent(Control control, Point pos)
    {
        if (control.Parent != null && !CheckDetached(control))
        {
            var parent = control.Parent;
            var root = control.Root;

            var pr = new Rectangle(parent.ControlLeftAbsoluteCoordinate,
                parent.ControlTopAbsoluteCoordinate,
                parent.Width,
                parent.Height);

            var margins = root.SkinInformation.ClientMargins;
            var rr = new Rectangle(root.ControlLeftAbsoluteCoordinate + margins.Left,
                root.ControlTopAbsoluteCoordinate + margins.Top,
                root.ControlAndMarginsWidth - margins.Horizontal,
                root.ControlAndMarginsHeight - margins.Vertical);

            return rr.Contains(pos) && pr.Contains(pos);
        }

        return true;
    } // CheckParent

    private bool CheckState(Control control)
    {
        var modal = ModalWindow == null ? true : ModalWindow == control.Root;

        return control != null && !control.Passive && control.Visible && control.Enabled && modal;
    } // CheckState

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
    } // CheckOrder

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
    } // CheckDetached

    private bool CheckPosition(Control control, Point pos)
    {
        return control.ControlLeftAbsoluteCoordinate <= pos.X &&
               control.ControlTopAbsoluteCoordinate <= pos.Y &&
               control.ControlLeftAbsoluteCoordinate + control.Width >= pos.X &&
               control.ControlTopAbsoluteCoordinate + control.Height >= pos.Y &&
               CheckParent(control, pos);
    } // CheckPosition

    private bool CheckButtons(int index)
    {
        return _states.Buttons.Where((t, i) => i != index).All(t => t == null);
    } // CheckButtons

    private void TabNextControl(Control control)
    {
        var start = OrderList.IndexOf(control);
        var i = start;

        do
        {
            if (i < OrderList.Count - 1)
            {
                i++;
            }
            else
            {
                i = 0;
            }
        }
        while ((OrderList[i].Root != control.Root || !OrderList[i].CanFocus || OrderList[i].IsRoot || !OrderList[i].Enabled) && i != start);

        OrderList[i].Focused = true;
    } // TabNextControl

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
    } // TabPrevControl

    private void ProcessArrows(Control control, KeyEventArgs kbe)
    {
        //Control control = control;
        if (control.Parent != null && control.Parent.ChildrenControls != null)
        {
            var index = -1;

            if (kbe.Key == Keys.Left && !kbe.Handled)
            {
                var miny = int.MaxValue;
                var minx = int.MinValue;
                for (var i = 0; i < control.Parent.ChildrenControls.Count; i++)
                {
                    var cx = (control.Parent.ChildrenControls as ControlsList)[i];
                    if (cx == control || !cx.Visible || !cx.Enabled || cx.Passive || !cx.CanFocus)
                    {
                        continue;
                    }

                    var cay = control.Top + control.Height / 2;
                    var cby = cx.Top + cx.Height / 2;

                    if (Math.Abs(cay - cby) <= miny && cx.Left + cx.Width >= minx && cx.Left + cx.Width <= control.Left)
                    {
                        miny = Math.Abs(cay - cby);
                        minx = cx.Left + cx.Width;
                        index = i;
                    }
                }
            }
            else if (kbe.Key == Keys.Right && !kbe.Handled)
            {
                var miny = int.MaxValue;
                var minx = int.MaxValue;
                for (var i = 0; i < control.Parent.ChildrenControls.Count; i++)
                {
                    var cx = control.Parent.ChildrenControls[i];
                    if (cx == control || !cx.Visible || !cx.Enabled || cx.Passive || !cx.CanFocus)
                    {
                        continue;
                    }

                    var cay = control.Top + control.Height / 2;
                    var cby = cx.Top + cx.Height / 2;

                    if (Math.Abs(cay - cby) <= miny && cx.Left <= minx && cx.Left >= control.Left + control.Width)
                    {
                        miny = Math.Abs(cay - cby);
                        minx = cx.Left;
                        index = i;
                    }
                }
            }
            else if (kbe.Key == Keys.Up && !kbe.Handled)
            {
                var miny = int.MinValue;
                var minx = int.MaxValue;
                for (var i = 0; i < control.Parent.ChildrenControls.Count; i++)
                {
                    var cx = control.Parent.ChildrenControls[i];
                    if (cx == control || !cx.Visible || !cx.Enabled || cx.Passive || !cx.CanFocus)
                    {
                        continue;
                    }

                    var cax = control.Left + control.Width / 2;
                    var cbx = cx.Left + cx.Width / 2;

                    if (Math.Abs(cax - cbx) <= minx && cx.Top + cx.Height >= miny && cx.Top + cx.Height <= control.Top)
                    {
                        minx = Math.Abs(cax - cbx);
                        miny = cx.Top + cx.Height;
                        index = i;
                    }
                }
            }
            else if (kbe.Key == Keys.Down && !kbe.Handled)
            {
                var miny = int.MaxValue;
                var minx = int.MaxValue;
                for (var i = 0; i < control.Parent.ChildrenControls.Count; i++)
                {
                    var cx = control.Parent.ChildrenControls[i];
                    if (cx == control || !cx.Visible || !cx.Enabled || cx.Passive || !cx.CanFocus)
                    {
                        continue;
                    }

                    var cax = control.Left + control.Width / 2;
                    var cbx = cx.Left + cx.Width / 2;

                    if (Math.Abs(cax - cbx) <= minx && cx.Top <= miny && cx.Top >= control.Top + control.Height)
                    {
                        minx = Math.Abs(cax - cbx);
                        miny = cx.Top;
                        index = i;
                    }
                }
            }

            if (index != -1)
            {
                control.Parent.ChildrenControls[index].Focused = true;
                kbe.Handled = true;
            }
        }
    } // ProcessArrows

    private void MouseDownProcess(object sender, MouseEventArgs e)
    {
        var controlList = new ControlsList();
        controlList.AddRange(OrderList);

        if (AutoUnfocus && _focusedControl != null && _focusedControl.Root != _modalWindow)
        {
            var hit = RootControls.Any(cx => cx.ControlRectangle.Contains(e.Position));

            if (!hit)
            {
                if (Control.ControlList.Any(t => t.Visible && t.Detached && t.ControlRectangle.Contains(e.Position)))
                {
                    hit = true;
                }
            }
            if (!hit)
            {
                _focusedControl.Focused = false;
            }
        }

        for (var i = controlList.Count - 1; i >= 0; i--)
        {
            if (CheckState(controlList[i]) && CheckPosition(controlList[i], e.Position))
            {
                _states.Buttons[(int)e.Button] = controlList[i];
                controlList[i].SendMessage(Message.MouseDown, e);

                if (_states.Click == -1)
                {
                    _states.Click = (int)e.Button;

                    FocusedControl?.Invalidate();
                    controlList[i].Focused = true;
                }
                return;
            }
        }

        if (ModalWindow != null)
        {
            //SystemSounds.Beep.Play();
        }
        else // If we click the window background. This prevent a bug.
        {
            FocusedControl = null;
        }
    } // MouseDownProcess

    private void MouseUpProcess(object sender, MouseEventArgs e)
    {
        var control = _states.Buttons[(int)e.Button];
        if (control != null)
        {
            var res1 = CheckPosition(control, e.Position);
            var res2 = CheckOrder(control, e.Position);
            var res3 = _states.Click == (int)e.Button;
            var res4 = CheckButtons((int)e.Button);

            if (res1 && res2 && res3 && res4)
            {
                control.SendMessage(Message.Click, e);
            }
            _states.Click = -1;
            control.SendMessage(Message.MouseUp, e);
            _states.Buttons[(int)e.Button] = null;
            MouseMoveProcess(sender, e);
        }
    } // MouseUpProcess

    private void MousePressProcess(object sender, MouseEventArgs e)
    {
        var control = _states.Buttons[(int)e.Button];
        if (control != null)
        {
            if (CheckPosition(control, e.Position))
            {
                control.SendMessage(Message.MousePress, e);
            }
        }
    } // MousePressProcess

    private void MouseMoveProcess(object sender, MouseEventArgs e)
    {
        var controlList = new ControlsList();
        controlList.AddRange(OrderList);

        for (var i = controlList.Count - 1; i >= 0; i--)
        {
            var checkPosition = CheckPosition(controlList[i], e.Position);
            var checkState = CheckState(controlList[i]);

            if (checkState && (checkPosition && _states.Over == controlList[i] || _states.Buttons[(int)e.Button] == controlList[i]))
            {
                controlList[i].SendMessage(Message.MouseMove, e);
                break;
            }
        }

        for (var i = controlList.Count - 1; i >= 0; i--)
        {
            var checkPosition = CheckPosition(controlList[i], e.Position);
            var checkState = CheckState(controlList[i]) || controlList[i].toolTip != null && !string.IsNullOrEmpty(controlList[i].ToolTip.Text) && controlList[i].Visible;

            if (checkState && !checkPosition && _states.Over == controlList[i] && _states.Buttons[(int)e.Button] == null)
            {
                _states.Over = null;
                controlList[i].SendMessage(Message.MouseOut, e);
                break;
            }
        }

        for (var i = controlList.Count - 1; i >= 0; i--)
        {
            var checkPosition = CheckPosition(controlList[i], e.Position);
            var checkState = CheckState(controlList[i]) || controlList[i].toolTip != null && !string.IsNullOrEmpty(controlList[i].ToolTip.Text) && controlList[i].Visible;

            if (checkState && checkPosition && _states.Over != controlList[i] && _states.Buttons[(int)e.Button] == null)
            {
                _states.Over?.SendMessage(Message.MouseOut, e);
                _states.Over = controlList[i];
                controlList[i].SendMessage(Message.MouseOver, e);
                break;
            }
            if (_states.Over == controlList[i])
            {
                break;
            }
        }
    } // MouseMoveProcess

    private void KeyDownProcess(object sender, KeyEventArgs e)
    {
        var focusedControl = FocusedControl;

        if (focusedControl != null && CheckState(focusedControl))
        {
            if (_states.Click == -1)
            {
                _states.Click = (int)MouseButton.None;
            }
            _states.Buttons[(int)MouseButton.None] = focusedControl;
            focusedControl.SendMessage(Message.KeyDown, e);

            if (e.Key == Keys.Enter)
            {
                focusedControl.SendMessage(Message.Click, new MouseEventArgs(new MouseState(), MouseButton.None, Point.Zero));
            }
        }
    } // KeyDownProcess

    private void KeyUpProcess(object sender, KeyEventArgs e)
    {
        var control = _states.Buttons[(int)MouseButton.None];

        if (control != null)
        {
            if (e.Key == Keys.Space)
            {
                control.SendMessage(Message.Click, new MouseEventArgs(new MouseState(), MouseButton.None, Point.Zero));
            }
            _states.Click = -1;
            _states.Buttons[(int)MouseButton.None] = null;
            control.SendMessage(Message.KeyUp, e);
        }
    } // KeyUpProcess

    private void KeyPressProcess(object sender, KeyEventArgs e)
    {
        var control = _states.Buttons[(int)MouseButton.None];
        if (control != null)
        {
            control.SendMessage(Message.KeyPress, e);

            if ((e.Key == Keys.Right ||
                 e.Key == Keys.Left ||
                 e.Key == Keys.Up ||
                 e.Key == Keys.Down) && !e.Handled && CheckButtons((int)MouseButton.None))
            {
                ProcessArrows(control, e);
                KeyDownProcess(sender, e);
            }
            else if (e.Key == Keys.Tab && !e.Shift && !e.Handled && CheckButtons((int)MouseButton.None))
            {
                TabNextControl(control);
                KeyDownProcess(sender, e);
            }
            else if (e.Key == Keys.Tab && e.Shift && !e.Handled && CheckButtons((int)MouseButton.None))
            {
                TabPrevControl(control);
                KeyDownProcess(sender, e);
            }
        }
    } // KeyPressProcess

    public bool IsOverThisControl(Control control, Point pos)
    {
        if (!control.Visible)
        {
            return false;
        }

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
    } // IsOverThisControl

    public void Invalidate()
    {
        foreach (var rootControl in RootControls)
        {
            rootControl.Invalidate();
        }
    } // Invalidate

} // UserInterfaceManager
  // // XNAFinalEngine.UserInterface