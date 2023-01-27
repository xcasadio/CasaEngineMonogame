
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
using System.Linq;
#if (!XBOX)
    using System.Windows.Forms;
#endif
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using CasaEngine.Asset;
using CasaEngine.Game;
using CasaEngine.Input;
#endregion

namespace XNAFinalEngine.UserInterface
{

    /// <summary>
    /// User Interface Manager.
    /// This user interface is prepared to work for editors and window applications only.
    /// However if gamepad support is added and a cursor is rendered then it could work in XBOX 360 perfectly.
    /// </summary>
    public class UserInterfaceManager
    {

        #region Script Class

        /// <summary>
        /// Used to call the UI's update and render methods in the correct order without explicit calls. 
        /// </summary>
        /// <remarks>
        /// Most XNA Final Engine managers don’t work this way because the GameLoop class controls their functionality.
        /// This manager is in a higher level because of the GPL license (the code is based in Neo Force Controls) and because is not garbage free.
        /// I do baste improvement in the code and functionality of this UI but I’m not disposed to waste more time in this endeavor.
        /// The UI has a purpose and in its current state can accomplished.
        /// </remarks>
        /*private sealed class ScripUserInterface : Script
        {

            /// <summary>
            /// Update.
            /// </summary>
            public override void Update()
            {
                UserInterfaceManager.Update();
            }

            /// <summary>
            /// Tasks executed during the first stage of the scene render.
            /// </summary>
            public override void PreRenderUpdate()
            {
                UserInterfaceManager.PreRenderControls();
            }

            /// <summary>
            /// Tasks executed during the last stage of the scene render.
            /// </summary>
            public override void PostRenderUpdate()
            {
                UserInterfaceManager.RenderUserInterfaceToScreen();
            }

        } // ScripUserInterface
        */
        #endregion

        #region Structs

        private struct ControlStates
        {
            public Control[] Buttons;
            public int Click;
            public Control Over;
        } // ControlStates

        #endregion

        #region Variables

        #if (!XBOX)
            /// <summary>
            /// Current cursor.
            /// </summary>
            private CasaEngine.Asset.Cursors.Cursor cursor;

            /// <summary>
            /// Returns the form of the game runs in.
            /// </summary>
            private Form window;
        #endif

        // Main render target, when the UI will be render.
        private RenderTarget renderTarget;

        private Control focusedControl;
        private ModalContainer modalWindow;
        private ControlStates states;

        // To avoid more than one initialization.
        private bool initialized;

        // Used to call the update and render method in the correct order without explicit calls.
        //private GameObject userInterfaceGameObject;

        // Used to generate the resize event.
        private int oldScreenWidth, oldScreenHeight;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the GraphicsDevice
        /// </summary>
        public GraphicsDevice GraphicsDevice { get; private set; }

        #if (!XBOX)
            
            /// <summary>
            /// Gets or sets an application cursor.
            /// </summary>
            public CasaEngine.Asset.Cursors.Cursor Cursor
            {
                get { return cursor; }
                set
                {
                    cursor = value;

                    if (window.InvokeRequired == true)
                    {
                        window.Invoke(new Action(() => window.Cursor = value.Resource));
                    }
                    else
                    {
                        window.Cursor = value.Resource;
                    }
                }
            } // Cursor

        #endif

        /// <summary>
        /// Gets Skin
        /// </summary>
        internal Skin Skin { get; private set; }

        /// <summary>
        /// Gets Renderer
        /// </summary>
        internal Renderer Renderer { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        internal CasaEngine.CoreSystems.Screen Screen { get; private set; }

        #region Visible

        /// <summary>
        /// Are the controls visible?
        /// </summary>
        public bool Visible { get; set; }

        /// <summary>
        /// Enable or Disable Input.
        /// </summary>
        public bool InputEnabled { get; set; }
        
        #endregion

        /// <summary>
        /// Returns InputSystem instance responsible for managing user input.
        /// </summary>
        public Input InputSystem { get; set; }

        /// <summary>
        /// Returns list of controls added to the manager.
        /// </summary>
        public ControlsList RootControls { get; private set; }

        internal ControlsList OrderList { get; private set; }

        /// <summary>
        /// Gets or sets the time that passes before the ToolTip appears.
        /// </summary>
        public int ToolTipDelay { get; set; }

        /// <summary>
        /// Gets or sets the time that passes before a submenu appears when hovered over menu item.
        /// </summary>
        public int MenuDelay { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of milliseconds that can elapse between a first click and a second click to consider the mouse action a double-click.
        /// </summary>
        public int DoubleClickTime { get; set; }

        /// <summary>
        /// Gets or sets texture size increment in pixel while performing controls resizing.
        /// </summary>
        public int TextureResizeIncrement { get; set; }

        /// <summary>
        /// Enables or disables showing of tooltips globally.
        /// </summary>
        public bool ToolTipsEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if a control should unfocus if you click outside on the screen.
        /// </summary>
        public bool AutoUnfocus { get; set; }

        /// <summary>
        /// Returns currently active modal window.
        /// </summary>
        public ModalContainer ModalWindow
        {
            get { return modalWindow; }
            internal set
            {
                modalWindow = value;
                if (value != null)
                {
                    value.ModalResult = ModalResult.None;
                    value.Visible = true;
                    value.Focused = true;
                }
            }
        } // ModalWindow

        /// <summary>
        /// Returns currently focused control.
        /// </summary>
        public Control FocusedControl
        {
            get
            {
                if (Visible)
                    return focusedControl;
                return null;
            }
            internal set
            {
                if (value != null && value.Visible && value.Enabled)
                {
                    if (value.CanFocus)
                    {
                        if (focusedControl == null || (focusedControl != null && value.Root != focusedControl.Root) || !value.IsRoot)
                        {
                            if (focusedControl != null && focusedControl != value)
                            {
                                focusedControl.Focused = false;
                            }
                            focusedControl = value;
                        }
                    }
                    else
                    {
                        if (focusedControl != null && value.Root != focusedControl.Root)
                        {
                            if (focusedControl != value.Root)
                            {
                                focusedControl.Focused = false;
                            }
                            focusedControl = value.Root;
                        }
                        else if (focusedControl == null)
                        {
                            focusedControl = value.Root;
                        }
                    }
                    BringToFront(value.Root);
                }
                else
                    focusedControl = null;
            }
        } // FocusedControl

        /// <summary>
        /// User Interface Content Manager.
        /// </summary>
        //internal AssetContentManager UserInterfaceContentManager { get; private set; }
        
        #endregion

        #region Events

        /// <summary>
        /// Occurs when the GraphicsDevice settings are changed.
        /// </summary>
        internal event EventHandler DeviceReset;

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
        /// Occurs with the window change its size.
        /// </summary>
        public event ResizeEventHandler WindowResize;

        #endregion

        #region Initialize

        /// <summary>
        /// Initializes the User Interface Manager.
        /// </summary>
        /// <param name="graphicsDevice_">The GraphicsDevice used to load the skin</param>
        /// <param name="formHandle_">Handle of the form</param>
        /// <param name="gameWindowClientBounds_">Used by the class Input</param>
        public void Initialize(GraphicsDevice graphicsDevice_, IntPtr formHandle_, Rectangle gameWindowClientBounds_)
        {
            if (initialized)
                return;
            try
            {
                Skin = new Skin();
                Renderer = new Renderer();
                Screen = new CasaEngine.CoreSystems.Screen(graphicsDevice_);

                GraphicsDevice = graphicsDevice_;
                Visible = true;
                InputEnabled = true;
                initialized = true;
                // Set some public parameters.
                TextureResizeIncrement = 32;
                ToolTipDelay = 500;
                AutoUnfocus = true;
                ToolTipsEnabled = true;
            
                #if (WINDOWS)
                    MenuDelay = SystemInformation.MenuShowDelay;
                    DoubleClickTime = SystemInformation.DoubleClickTime;
                    window = (Form)System.Windows.Forms.Control.FromHandle(formHandle_);
                    window.FormClosing += FormClosing;
                #endif

                RootControls  = new ControlsList();
                OrderList = new ControlsList();

                graphicsDevice_.DeviceReset += OnDeviceReset;

                states.Buttons = new Control[32];
                states.Click = -1;
                states.Over = null;

                // Input events
                InputSystem = new Input(gameWindowClientBounds_);
                InputSystem.MouseDown  += MouseDownProcess;
                InputSystem.MouseUp    += MouseUpProcess;
                InputSystem.MousePress += MousePressProcess;
                InputSystem.MouseMove  += MouseMoveProcess;
                InputSystem.KeyDown    += KeyDownProcess;
                InputSystem.KeyUp      += KeyUpProcess;
                InputSystem.KeyPress   += KeyPressProcess;

                // Final render target.
                /*AssetContentManager userContentManager = AssetContentManager.CurrentContentManager;
                UserInterfaceContentManager = new AssetContentManager { Name = "User Interface Content Manager", Hidden = true };
                AssetContentManager.CurrentContentManager = UserInterfaceContentManager;*/
                XNAFinalEngine.Helpers.Size size = new XNAFinalEngine.Helpers.Size(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, Screen);
                renderTarget = new RenderTarget(graphicsDevice_, size.FullScreen, SurfaceFormat.Color, false, RenderTarget.AntialiasingType.NoAntialiasing)
                {
                    Name = "User Interface Render Target",
                };
                //AssetContentManager.CurrentContentManager = userContentManager;

                // Init User Interface UserInterfaceManager.Renderer.
                Renderer.Initialize(graphicsDevice_);

                // Set Default UserInterfaceManager.Skin.
                SetSkin("Default");

                // Window resize.
                oldScreenWidth = Screen.Width;
                oldScreenHeight = Screen.Height;
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

        #endregion

        #region On Device Reset

        /// <summary>
        /// If the device is recreated then the controls have to be invalidated so that they redraw.
        /// </summary>
        private void OnDeviceReset(object sender, System.EventArgs e)
        {
            if (DeviceReset != null)
                DeviceReset.Invoke(sender, new EventArgs());
        } // OnPrepareGraphicsDevice

        #endregion

        #region On Screen Size Changed

        /// <summary>
        /// Raised when the window size changes.
        /// </summary>
        private void OnScreenSizeChanged(object sender, System.EventArgs e)
        {
            if (WindowResize != null)
                WindowResize.Invoke(null, new ResizeEventArgs(GraphicsDevice.PresentationParameters.BackBufferWidth,
                                                              GraphicsDevice.PresentationParameters.BackBufferHeight,
                                                              oldScreenWidth, oldScreenHeight));
            oldScreenWidth = GraphicsDevice.PresentationParameters.BackBufferWidth;
            oldScreenHeight = GraphicsDevice.PresentationParameters.BackBufferHeight;
        } // OnScreenSizeChanged

        #endregion

        #region Form Closing

        #if (WINDOWS)

            /// <summary>
            /// If the form is closing
            /// </summary>
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

        #endregion

        #region Dispose Controls

        /// <summary>
        /// Dispose all controls added to the manager and its child controls.
        /// </summary>
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

        #endregion
        
        #region Set Skin

        /// <summary>
        /// Sets a new UserInterfaceManager.Skin.
        /// </summary>
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

        #endregion

        #region Bring to front or back

        /// <summary>
        /// Brings the control to the front (z-order).
        /// </summary>
        /// <param name="control">The control being brought to the front.</param>
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

        /// <summary>
        /// Sends the control to the back (z-order).
        /// </summary>
        /// <param name="control">The control being sent back.</param>
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

        #endregion

        #region Update

        /// <summary>
        /// Update.
        /// </summary>
        public void Update(float elapsedTime_)
        {
            if (!Visible || !InputEnabled)
                return;
            //try
            {
                // Init new controls.
                Control.InitializeNewControls();

                InputSystem.Update(elapsedTime_);


                // In the control's update the Root Control list could be modified so we need to create an auxiliary list.
                ControlsList controlList = new ControlsList(RootControls);
                foreach (Control control in controlList)
                {
                    control.Update(elapsedTime_);
                }
                OrderList.Clear();
                SortLevel(RootControls);
            }
            /*catch (Exception exception)
            {
                throw new InvalidOperationException("User Interface Manager: Update failed.", exception);
            }*/
        } // Update

        /// <summary>
        /// Sort the control and their children.
        /// </summary>
        /// <param name="controlList"></param>
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

        #endregion

        #region Add or Remove

        /// <summary>
        /// Adds a control to the manager.
        /// </summary>
        /// <param name="control">The control being added.</param>
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
                    if (focusedControl == null) 
                        control.Focused = true;
                    WindowResize += control.OnParentResize;
                }
            }
        } // Add

        /// <summary>
        /// Removes a component or a control from the manager.
        /// </summary>
        /// <param name="control">The control being removed.</param>
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

        #endregion

        #region Draw

        /// <summary>
        /// Renders all controls added to the manager to a render target.
        /// </summary>
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
                renderTarget.EnableRenderTarget();
                //renderTarget.Clear(Color.Transparent);
                GraphicsDevice.Clear(ClearOptions.Target, Color.Transparent, 1.0f, 0);

                    //GameInfo.Instance.Game.GraphicsDevice.Clear(Color.Transparent);
                    foreach (Control control in RootControls)
                    {
                        control.DrawControlOntoMainTexture();
                    }
                renderTarget.DisableRenderTarget();
            }
        } // DrawToTexture

        /// <summary>
        /// Draws User Interface's render target to screen.
        /// </summary>
        public void RenderUserInterfaceToScreen()
        {
            if (!Visible)
                return;
            if (RootControls != null)
            {
                Renderer.Begin();
                Renderer.Draw(renderTarget.Resource, new Rectangle(0, 0, Screen.Width, Screen.Height), Color.White);
                Renderer.End();
            }
        } // DrawTextureToScreen

        #endregion
        
        #region Input

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

        /// <summary>
        /// True is the control is on this position and the other controls on this position are parents of this control.
        /// </summary>
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
            return states.Buttons.Where((t, i) => i != index).All(t => t == null);
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

                if (kbe.Key == Microsoft.Xna.Framework.Input.Keys.Left && !kbe.Handled)
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
                else if (kbe.Key == Microsoft.Xna.Framework.Input.Keys.Right && !kbe.Handled)
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
                else if (kbe.Key == Microsoft.Xna.Framework.Input.Keys.Up && !kbe.Handled)
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
                else if (kbe.Key == Microsoft.Xna.Framework.Input.Keys.Down && !kbe.Handled)
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

            if (AutoUnfocus && focusedControl != null && focusedControl.Root != modalWindow)
            {
                bool hit = RootControls.Any(cx => cx.ControlRectangle.Contains(e.Position));

                if (!hit)
                {
                    if (Control.ControlList.Any(t => t.Visible && t.Detached && t.ControlRectangle.Contains(e.Position)))
                    {
                        hit = true;
                    }
                }
                if (!hit) focusedControl.Focused = false;
            }

            for (int i = controlList.Count - 1; i >= 0; i--)
            {
                if (CheckState(controlList[i]) && CheckPosition(controlList[i], e.Position))
                {
                    states.Buttons[(int)e.Button] = controlList[i];
                    controlList[i].SendMessage(Message.MouseDown, e);

                    if (states.Click == -1)
                    {
                        states.Click = (int)e.Button;

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
            Control control = states.Buttons[(int)e.Button];
            if (control != null)
            {
                bool res1 = CheckPosition(control, e.Position);
                bool res2 = CheckOrder(control, e.Position);
                bool res3 = states.Click == (int)e.Button;
                bool res4 = CheckButtons((int)e.Button);

                if (res1 && res2 && res3 && res4)
                {
                    control.SendMessage(Message.Click, e);
                }
                states.Click = -1;
                control.SendMessage(Message.MouseUp, e);
                states.Buttons[(int)e.Button] = null;
                MouseMoveProcess(sender, e);
            }
        } // MouseUpProcess

        private void MousePressProcess(object sender, MouseEventArgs e)
        {
            Control control = states.Buttons[(int)e.Button];
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

                if (checkState && ((checkPosition && states.Over == controlList[i]) || (states.Buttons[(int)e.Button] == controlList[i])))
                {
                    controlList[i].SendMessage(Message.MouseMove, e);
                    break;
                }
            }

            for (int i = controlList.Count - 1; i >= 0; i--)
            {
                bool checkPosition = CheckPosition(controlList[i], e.Position);
                bool checkState = CheckState(controlList[i]) || (controlList[i].toolTip != null && !string.IsNullOrEmpty(controlList[i].ToolTip.Text) && controlList[i].Visible);

                if (checkState && !checkPosition && states.Over == controlList[i] && states.Buttons[(int)e.Button] == null)
                {
                    states.Over = null;
                    controlList[i].SendMessage(Message.MouseOut, e);
                    break;
                }
            }

            for (int i = controlList.Count - 1; i >= 0; i--)
            {
                bool checkPosition = CheckPosition(controlList[i], e.Position);
                bool checkState = CheckState(controlList[i]) || (controlList[i].toolTip != null && !string.IsNullOrEmpty(controlList[i].ToolTip.Text) && controlList[i].Visible);

                if (checkState && checkPosition && states.Over != controlList[i] && states.Buttons[(int)e.Button] == null)
                {
                    if (states.Over != null)
                    {
                        states.Over.SendMessage(Message.MouseOut, e);
                    }
                    states.Over = controlList[i];
                    controlList[i].SendMessage(Message.MouseOver, e);
                    break;
                }
                if (states.Over == controlList[i])
                    break;
            }
        } // MouseMoveProcess

        private void KeyDownProcess(object sender, KeyEventArgs e)
        {
            Control focusedControl = FocusedControl;

            if (focusedControl != null && CheckState(focusedControl))
            {
                if (states.Click == -1)
                {
                    states.Click = (int)MouseButton.None;
                }
                states.Buttons[(int)MouseButton.None] = focusedControl;
                focusedControl.SendMessage(Message.KeyDown, e);

                if (e.Key == Microsoft.Xna.Framework.Input.Keys.Enter)
                {
                    focusedControl.SendMessage(Message.Click, new MouseEventArgs(new MouseState(), MouseButton.None, Point.Zero));
                }
            }
        } // KeyDownProcess

        private void KeyUpProcess(object sender, KeyEventArgs e)
        {
            Control control = states.Buttons[(int)MouseButton.None];

            if (control != null)
            {
                if (e.Key == Microsoft.Xna.Framework.Input.Keys.Space)
                {
                    control.SendMessage(Message.Click, new MouseEventArgs(new MouseState(), MouseButton.None, Point.Zero));
                }
                states.Click = -1;
                states.Buttons[(int)MouseButton.None] = null;
                control.SendMessage(Message.KeyUp, e);
            }
        } // KeyUpProcess

        private void KeyPressProcess(object sender, KeyEventArgs e)
        {
            Control control = states.Buttons[(int)MouseButton.None];
            if (control != null)
            {
                control.SendMessage(Message.KeyPress, e);

                if ((e.Key == Microsoft.Xna.Framework.Input.Keys.Right ||
                     e.Key == Microsoft.Xna.Framework.Input.Keys.Left ||
                     e.Key == Microsoft.Xna.Framework.Input.Keys.Up ||
                     e.Key == Microsoft.Xna.Framework.Input.Keys.Down) && !e.Handled && CheckButtons((int)MouseButton.None))
                {
                    ProcessArrows(control, e);
                    KeyDownProcess(sender, e);
                }
                else if (e.Key == Microsoft.Xna.Framework.Input.Keys.Tab && !e.Shift && !e.Handled && CheckButtons((int)MouseButton.None))
                {
                    TabNextControl(control);
                    KeyDownProcess(sender, e);
                }
                else if (e.Key == Microsoft.Xna.Framework.Input.Keys.Tab && e.Shift && !e.Handled && CheckButtons((int)MouseButton.None))
                {
                    TabPrevControl(control);
                    KeyDownProcess(sender, e);
                }
            }
        } // KeyPressProcess

        #endregion

        #region Is Over Controls

        /// <summary>
        /// True is the control is on this position and the other controls on this position are parents of this control.
        /// I.e. if it is the control visible in this position.
        /// </summary>
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

        #endregion

        #region Invalidate

        /// <summary>
        /// Invalidate all controls
        /// </summary>
        public void Invalidate()
        {
            foreach (Control rootControl in RootControls)
            {
                rootControl.Invalidate();
            }
        } // Invalidate

        #endregion

    } // UserInterfaceManager
} // // XNAFinalEngine.UserInterface