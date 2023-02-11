
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/


#if (!XBOX)
#endif
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using CasaEngine.Asset;

namespace XNAFinalEngine.UserInterface
{

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
        private CasaEngine.Asset.Cursors.Cursor _cursor;

        private Form _window;
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

#if (!XBOX)

        public CasaEngine.Asset.Cursors.Cursor Cursor
        {
            get => _cursor;
            set
            {
                _cursor = value;

                if (_window.InvokeRequired == true)
                {
                    _window.Invoke(new Action(() => _window.Cursor = value.Resource));
                }
                else
                {
                    _window.Cursor = value.Resource;
                }
            }
        } // Cursor

#endif

        internal Skin Skin { get; private set; }

        internal Renderer Renderer { get; private set; }

        internal CasaEngine.CoreSystems.Screen Screen { get; private set; }


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
                    return _focusedControl;
                return null;
            }
            internal set
            {
                if (value != null && value.Visible && value.Enabled)
                {
                    if (value.CanFocus)
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
                    _focusedControl = null;
            }
        } // FocusedControl

        //internal AssetContentManager UserInterfaceContentManager { get; private set; }



        internal event EventHandler DeviceReset;

        public event SkinEventHandler SkinChanging;

        public event SkinEventHandler SkinChanged;

        public event WindowClosingEventHandler WindowClosing;

        public event ResizeEventHandler WindowResize;



        public void Initialize(GraphicsDevice graphicsDevice, IntPtr formHandle, Rectangle gameWindowClientBounds)
        {
            if (_initialized)
                return;
            try
            {
                Skin = new Skin();
                Renderer = new Renderer();
                Screen = new CasaEngine.CoreSystems.Screen(graphicsDevice);

                GraphicsDevice = graphicsDevice;
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
                _window = (Form)System.Windows.Forms.Control.FromHandle(formHandle);
                _window.FormClosing += FormClosing;
#endif

                RootControls = new ControlsList();
                OrderList = new ControlsList();

                graphicsDevice.DeviceReset += OnDeviceReset;

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
                XNAFinalEngine.Helpers.Size size = new XNAFinalEngine.Helpers.Size(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, Screen);
                _renderTarget = new RenderTarget(graphicsDevice, size.FullScreen, SurfaceFormat.Color, false, RenderTarget.AntialiasingType.NoAntialiasing)
                {
                    Name = "User Interface Render Target",
                };
                //AssetContentManager.CurrentContentManager = userContentManager;

                // Init User Interface UserInterfaceManager.Renderer.
                Renderer.Initialize(graphicsDevice);

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
        } // Initialize



        private void OnDeviceReset(object sender, System.EventArgs e)
        {
            if (DeviceReset != null)
                DeviceReset.Invoke(sender, new EventArgs());
        } // OnPrepareGraphicsDevice



        private void OnScreenSizeChanged(object sender, System.EventArgs e)
        {
            if (WindowResize != null)
                WindowResize.Invoke(null, new ResizeEventArgs(GraphicsDevice.PresentationParameters.BackBufferWidth,
                                                              GraphicsDevice.PresentationParameters.BackBufferHeight,
                                                              _oldScreenWidth, _oldScreenHeight));
            _oldScreenWidth = GraphicsDevice.PresentationParameters.BackBufferWidth;
            _oldScreenHeight = GraphicsDevice.PresentationParameters.BackBufferHeight;
        } // OnScreenSizeChanged



#if (WINDOWS)

        private void FormClosing(object sender, FormClosingEventArgs e)
        {
            bool ret = false;

            WindowClosingEventArgs ex = new WindowClosingEventArgs();
            if (WindowClosing != null)
            {
                WindowClosing.Invoke(null, ex);
                ret = ex.Cancel;
            }
            e.Cancel = ret;
        } // FormClosing

#endif



        public void DisposeControls()
        {
            try
            {
                for (int i = 0; i < RootControls.Count; i++)
                {
                    RootControls[i].Dispose();
                }
                RootControls.Clear();
                OrderList.Clear();
                FocusedControl = null;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("User Interface Manager: Unable to dispose controls. Was the User Interface Manager started?", e);
            }
        } // DisposeControls



        public void SetSkin(string skinName)
        {
            if (SkinChanging != null)
                SkinChanging.Invoke(new EventArgs());

            Skin.LoadSkin(GraphicsDevice, skinName);

#if (!XBOX)
            if (Skin.Cursors["Default"] != null)
            {
                Cursor = Skin.Cursors["Default"].Cursor;
            }
#endif

            // Initializing skins for every control created, even not visible or not added to the manager or another control.
            foreach (Control control in Control.ControlList)
            {
                control.InitSkin();
            }

            if (SkinChanged != null)
                SkinChanged.Invoke(new EventArgs());

            //  Initializing all controls created, even not visible or not added to the manager or another control.
            foreach (Control control in Control.ControlList)
            {
                control.Init();
            }
        } // SetSkin



        internal void BringToFront(Control control)
        {
            if (control != null && !control.StayOnBack)
            {
                // We search for the control's brothers.
                ControlsList brotherControls = (control.Parent == null) ? RootControls : control.Parent.ChildrenControls;
                if (brotherControls.Contains(control)) // The only case in which is false is when the control was not added to anything.
                {
                    brotherControls.Remove(control);
                    if (!control.StayOnTop)
                    {
                        // We try to insert the control the higher that we can in the sorted list
                        int newControlPosition = brotherControls.Count;
                        for (int i = brotherControls.Count - 1; i >= 0; i--)
                        {
                            if (!brotherControls[i].StayOnTop) // If there is a control that has to be in top then we won't go any further.
                                break;
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
                ControlsList brotherControls = (control.Parent == null) ? RootControls : control.Parent.ChildrenControls;
                if (brotherControls.Contains(control))
                {
                    brotherControls.Remove(control);
                    if (!control.StayOnBack)
                    {
                        int newControlPosition = 0;
                        for (int i = 0; i < brotherControls.Count; i++)
                        {
                            if (!brotherControls[i].StayOnBack)
                                break;
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
                return;
            //try
            {
                // Init new controls.
                Control.InitializeNewControls();

                InputSystem.Update(elapsedTime);


                // In the control's update the Root Control list could be modified so we need to create an auxiliary list.
                ControlsList controlList = new ControlsList(RootControls);
                foreach (Control control in controlList)
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
                for (int i = 0; i < controlList.Count; i++)
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
                    if (control.Parent != null)
                        control.Parent.Remove(control);
                    RootControls.Add(control);
                    control.Parent = null;
                    if (_focusedControl == null)
                        control.Focused = true;
                    WindowResize += control.OnParentResize;
                }
            }
        } // Add

        internal void Remove(Control control)
        {
            if (control != null)
            {
                if (control.Focused)
                    control.Focused = false;
                RootControls.Remove(control);
                // Remove event
                WindowResize -= control.OnParentResize;
            }
        } // Remove



        public void PreRenderControls()
        {
            if (!Visible)
                return;
            if ((RootControls != null))
            {
                // Render each control in its own render target.
                foreach (Control control in RootControls)
                {
                    control.PreDrawControlOntoOwnTexture();
                }
                // Draw user interface texture.
                _renderTarget.EnableRenderTarget();
                //renderTarget.Clear(Color.Transparent);
                GraphicsDevice.Clear(ClearOptions.Target, Color.Transparent, 1.0f, 0);

                //GameInfo.Instance.Game.GraphicsDevice.Clear(Color.Transparent);
                foreach (Control control in RootControls)
                {
                    control.DrawControlOntoMainTexture();
                }
                _renderTarget.DisableRenderTarget();
            }
        } // DrawToTexture

        public void RenderUserInterfaceToScreen()
        {
            if (!Visible)
                return;
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
                Control parent = control.Parent;
                Control root = control.Root;

                Rectangle pr = new Rectangle(parent.ControlLeftAbsoluteCoordinate,
                                             parent.ControlTopAbsoluteCoordinate,
                                             parent.Width,
                                             parent.Height);

                Margins margins = root.SkinInformation.ClientMargins;
                Rectangle rr = new Rectangle(root.ControlLeftAbsoluteCoordinate + margins.Left,
                                             root.ControlTopAbsoluteCoordinate + margins.Top,
                                             root.ControlAndMarginsWidth - margins.Horizontal,
                                             root.ControlAndMarginsHeight - margins.Vertical);


                return (rr.Contains(pos) && pr.Contains(pos));
            }

            return true;
        } // CheckParent

        private bool CheckState(Control control)
        {
            bool modal = (ModalWindow == null) ? true : (ModalWindow == control.Root);

            return (control != null && !control.Passive && control.Visible && control.Enabled && modal);
        } // CheckState

        private bool CheckOrder(Control control, Point pos)
        {
            if (!CheckPosition(control, pos))
                return false;

            for (int i = OrderList.Count - 1; i > OrderList.IndexOf(control); i--)
            {
                Control c = OrderList[i];

                if (!c.Passive && CheckPosition(c, pos) && CheckParent(c, pos))
                {
                    return false;
                }
            }

            return true;
        } // CheckOrder

        private bool CheckDetached(Control control)
        {
            bool ret = control.Detached;
            if (control.Parent != null)
            {
                if (CheckDetached(control.Parent)) ret = true;
            }
            return ret;
        } // CheckDetached

        private bool CheckPosition(Control control, Point pos)
        {
            return (control.ControlLeftAbsoluteCoordinate <= pos.X &&
                    control.ControlTopAbsoluteCoordinate <= pos.Y &&
                    control.ControlLeftAbsoluteCoordinate + control.Width >= pos.X &&
                    control.ControlTopAbsoluteCoordinate + control.Height >= pos.Y &&
                    CheckParent(control, pos));
        } // CheckPosition

        private bool CheckButtons(int index)
        {
            return _states.Buttons.Where((t, i) => i != index).All(t => t == null);
        } // CheckButtons

        private void TabNextControl(Control control)
        {
            int start = OrderList.IndexOf(control);
            int i = start;

            do
            {
                if (i < OrderList.Count - 1)
                    i++;
                else
                    i = 0;
            }
            while ((OrderList[i].Root != control.Root || !OrderList[i].CanFocus || OrderList[i].IsRoot || !OrderList[i].Enabled) && i != start);

            OrderList[i].Focused = true;
        } // TabNextControl

        private void TabPrevControl(Control control)
        {
            int start = OrderList.IndexOf(control);
            int i = start;

            do
            {
                if (i > 0) i -= 1;
                else i = OrderList.Count - 1;
            }
            while ((OrderList[i].Root != control.Root || !OrderList[i].CanFocus || OrderList[i].IsRoot || !OrderList[i].Enabled) && i != start);
            OrderList[i].Focused = true;
        } // TabPrevControl

        private void ProcessArrows(Control control, KeyEventArgs kbe)
        {
            //Control control = control;
            if (control.Parent != null && control.Parent.ChildrenControls != null)
            {
                int index = -1;

                if (kbe.Key == Keys.Left && !kbe.Handled)
                {
                    int miny = int.MaxValue;
                    int minx = int.MinValue;
                    for (int i = 0; i < ((ControlsList)control.Parent.ChildrenControls).Count; i++)
                    {
                        Control cx = (control.Parent.ChildrenControls as ControlsList)[i];
                        if (cx == control || !cx.Visible || !cx.Enabled || cx.Passive || !cx.CanFocus) continue;

                        int cay = control.Top + (control.Height / 2);
                        int cby = cx.Top + (cx.Height / 2);

                        if (Math.Abs(cay - cby) <= miny && (cx.Left + cx.Width) >= minx && (cx.Left + cx.Width) <= control.Left)
                        {
                            miny = Math.Abs(cay - cby);
                            minx = cx.Left + cx.Width;
                            index = i;
                        }
                    }
                }
                else if (kbe.Key == Keys.Right && !kbe.Handled)
                {
                    int miny = int.MaxValue;
                    int minx = int.MaxValue;
                    for (int i = 0; i < ((ControlsList)control.Parent.ChildrenControls).Count; i++)
                    {
                        Control cx = ((ControlsList)control.Parent.ChildrenControls)[i];
                        if (cx == control || !cx.Visible || !cx.Enabled || cx.Passive || !cx.CanFocus) continue;

                        int cay = control.Top + (control.Height / 2);
                        int cby = cx.Top + (cx.Height / 2);

                        if (Math.Abs(cay - cby) <= miny && cx.Left <= minx && cx.Left >= (control.Left + control.Width))
                        {
                            miny = Math.Abs(cay - cby);
                            minx = cx.Left;
                            index = i;
                        }
                    }
                }
                else if (kbe.Key == Keys.Up && !kbe.Handled)
                {
                    int miny = int.MinValue;
                    int minx = int.MaxValue;
                    for (int i = 0; i < (control.Parent.ChildrenControls).Count; i++)
                    {
                        Control cx = (control.Parent.ChildrenControls)[i];
                        if (cx == control || !cx.Visible || !cx.Enabled || cx.Passive || !cx.CanFocus) continue;

                        int cax = control.Left + (control.Width / 2);
                        int cbx = cx.Left + (cx.Width / 2);

                        if (Math.Abs(cax - cbx) <= minx && (cx.Top + cx.Height) >= miny && (cx.Top + cx.Height) <= control.Top)
                        {
                            minx = Math.Abs(cax - cbx);
                            miny = cx.Top + cx.Height;
                            index = i;
                        }
                    }
                }
                else if (kbe.Key == Keys.Down && !kbe.Handled)
                {
                    int miny = int.MaxValue;
                    int minx = int.MaxValue;
                    for (int i = 0; i < (control.Parent.ChildrenControls).Count; i++)
                    {
                        Control cx = (control.Parent.ChildrenControls)[i];
                        if (cx == control || !cx.Visible || !cx.Enabled || cx.Passive || !cx.CanFocus) continue;

                        int cax = control.Left + (control.Width / 2);
                        int cbx = cx.Left + (cx.Width / 2);

                        if (Math.Abs(cax - cbx) <= minx && cx.Top <= miny && cx.Top >= (control.Top + control.Height))
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
            ControlsList controlList = new ControlsList();
            controlList.AddRange(OrderList);

            if (AutoUnfocus && _focusedControl != null && _focusedControl.Root != _modalWindow)
            {
                bool hit = RootControls.Any(cx => cx.ControlRectangle.Contains(e.Position));

                if (!hit)
                {
                    if (Control.ControlList.Any(t => t.Visible && t.Detached && t.ControlRectangle.Contains(e.Position)))
                    {
                        hit = true;
                    }
                }
                if (!hit) _focusedControl.Focused = false;
            }

            for (int i = controlList.Count - 1; i >= 0; i--)
            {
                if (CheckState(controlList[i]) && CheckPosition(controlList[i], e.Position))
                {
                    _states.Buttons[(int)e.Button] = controlList[i];
                    controlList[i].SendMessage(Message.MouseDown, e);

                    if (_states.Click == -1)
                    {
                        _states.Click = (int)e.Button;

                        if (FocusedControl != null)
                        {
                            FocusedControl.Invalidate();
                        }
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
            Control control = _states.Buttons[(int)e.Button];
            if (control != null)
            {
                bool res1 = CheckPosition(control, e.Position);
                bool res2 = CheckOrder(control, e.Position);
                bool res3 = _states.Click == (int)e.Button;
                bool res4 = CheckButtons((int)e.Button);

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
            Control control = _states.Buttons[(int)e.Button];
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
            ControlsList controlList = new ControlsList();
            controlList.AddRange(OrderList);

            for (int i = controlList.Count - 1; i >= 0; i--)
            {
                bool checkPosition = CheckPosition(controlList[i], e.Position);
                bool checkState = CheckState(controlList[i]);

                if (checkState && ((checkPosition && _states.Over == controlList[i]) || (_states.Buttons[(int)e.Button] == controlList[i])))
                {
                    controlList[i].SendMessage(Message.MouseMove, e);
                    break;
                }
            }

            for (int i = controlList.Count - 1; i >= 0; i--)
            {
                bool checkPosition = CheckPosition(controlList[i], e.Position);
                bool checkState = CheckState(controlList[i]) || (controlList[i].toolTip != null && !string.IsNullOrEmpty(controlList[i].ToolTip.Text) && controlList[i].Visible);

                if (checkState && !checkPosition && _states.Over == controlList[i] && _states.Buttons[(int)e.Button] == null)
                {
                    _states.Over = null;
                    controlList[i].SendMessage(Message.MouseOut, e);
                    break;
                }
            }

            for (int i = controlList.Count - 1; i >= 0; i--)
            {
                bool checkPosition = CheckPosition(controlList[i], e.Position);
                bool checkState = CheckState(controlList[i]) || (controlList[i].toolTip != null && !string.IsNullOrEmpty(controlList[i].ToolTip.Text) && controlList[i].Visible);

                if (checkState && checkPosition && _states.Over != controlList[i] && _states.Buttons[(int)e.Button] == null)
                {
                    if (_states.Over != null)
                    {
                        _states.Over.SendMessage(Message.MouseOut, e);
                    }
                    _states.Over = controlList[i];
                    controlList[i].SendMessage(Message.MouseOver, e);
                    break;
                }
                if (_states.Over == controlList[i])
                    break;
            }
        } // MouseMoveProcess

        private void KeyDownProcess(object sender, KeyEventArgs e)
        {
            Control focusedControl = FocusedControl;

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
            Control control = _states.Buttons[(int)MouseButton.None];

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
            Control control = _states.Buttons[(int)MouseButton.None];
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
                return false;
            if (!CheckPosition(control, pos))
                return false;
            for (int i = OrderList.Count - 1; i > OrderList.IndexOf(control); i--)
            {
                Control c = OrderList[i];

                if (!c.Passive && CheckPosition(c, pos) && CheckParent(c, pos))
                {
                    return false;
                }
            }
            return true;
        } // IsOverThisControl



        public void Invalidate()
        {
            foreach (Control rootControl in RootControls)
            {
                rootControl.Invalidate();
            }
        } // Invalidate


    } // UserInterfaceManager
} // // XNAFinalEngine.UserInterface