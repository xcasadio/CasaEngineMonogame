using System;
using System.Collections.Generic;
using System.Windows;
using Microsoft.Xna.Framework.Input;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using CasaEngine.Framework.Game;
using CasaEngine.Engine.Input;

namespace EditorWpf.Controls
{
    /// <summary>
    /// Interaction logic for GameControl.xaml
    /// </summary>
    public partial class GameControl : UserControl
    {
        private List<Keys> _keys = new();
        public bool IsMouseFocus { get; set; }

        private Point _mousePosition;
        private int _mouseDelta;
        private bool[] _mouseButtons = new bool[5];

        public GameControl()
        {
            InitializeComponent();

            MouseEnter += OnMouseEnter;
            MouseLeave += OnMouseLeave;
            MouseDown += OnMouseDown;
            MouseUp += OnMouseUp;
            MouseMove += OnMouseMove;
            MouseWheel += OnMouseWheel;
            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;

            //GameScreen.MouseEnter += OnMouseEnter;
            //GameScreen.MouseLeave += OnMouseLeave;
            //GameScreen.MouseDown += OnMouseDown;
            //GameScreen.MouseUp += OnMouseUp;
            //GameScreen.MouseWheel += OnMouseWheel;
            //GameScreen.KeyDown += OnKeyDown;
            //GameScreen.KeyUp += OnKeyUp;

            PreviewKeyDown += OnPreviewKeyDown;
            //GameScreen.PreviewKeyDown += OnPreviewKeyDown;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Key GameControl {e.Key}");
        }

        private void OnPreviewKeyDown2(object sender, KeyEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Key GameScreen {e.Key}");
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (KeyMapper.TryGetValue(e.Key, out Keys keys))
            {
                _keys.Remove(keys);
                UpdateStates();


                ;
            }

            System.Windows.Input.Keyboard.IsKeyDown(Key.RightShift);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (KeyMapper.TryGetValue(e.Key, out Keys keys))
            {
                _keys.Add(keys);
                UpdateStates();
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Mouse position {e.GetPosition(this)}");
            _mousePosition = e.GetPosition(this);
            UpdateStates();
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"Mouse wheel {e.Delta}");
            _mouseDelta = e.Delta / 120;
            UpdateStates();
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Mouse up {e.ChangedButton} = {e.ButtonState}");
            switch (e.ChangedButton)
            {
                case MouseButton.Left:
                    _mouseButtons[0] = false;
                    break;
                case MouseButton.Middle:
                    _mouseButtons[1] = false;
                    break;
                case MouseButton.Right:
                    _mouseButtons[2] = false;
                    break;
                case MouseButton.XButton1:
                    _mouseButtons[3] = false;
                    break;
                case MouseButton.XButton2:
                    _mouseButtons[4] = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            UpdateStates();
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Mouse down {e.ChangedButton} = {e.ButtonState}");
            switch (e.ChangedButton)
            {
                case MouseButton.Left:
                    _mouseButtons[0] = true;
                    break;
                case MouseButton.Middle:
                    _mouseButtons[1] = true;
                    break;
                case MouseButton.Right:
                    _mouseButtons[2] = true;
                    break;
                case MouseButton.XButton1:
                    _mouseButtons[3] = true;
                    break;
                case MouseButton.XButton2:
                    _mouseButtons[4] = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            UpdateStates();
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"Mouse leave");
            IsMouseFocus = false;
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"Mouse enter");
            IsMouseFocus = true;
        }

        private void UpdateStates()
        {
            var keyboardState = new KeyboardState(_keys.ToArray(), false, false);

            Engine.Instance.Game.GetGameComponent<InputComponent>().SetStates(
                keyboardState,
                new MouseState((int)_mousePosition.X,
                    (int)_mousePosition.Y,
                    _mouseDelta,
                    _mouseButtons[0] ? ButtonState.Pressed : ButtonState.Released,
                    _mouseButtons[1] ? ButtonState.Pressed : ButtonState.Released,
                    _mouseButtons[2] ? ButtonState.Pressed : ButtonState.Released,
            _mouseButtons[3] ? ButtonState.Pressed : ButtonState.Released,
            _mouseButtons[4] ? ButtonState.Pressed : ButtonState.Released));
        }

        private Dictionary<Key, Keys> KeyMapper = new()
        {
            { Key.None, Keys.None },
            { Key.Back, Keys.Back },
            { Key.Tab, Keys.Tab },
            { Key.Enter, Keys.Enter },
            { Key.Pause, Keys.Pause },
            { Key.CapsLock, Keys.CapsLock },
            { Key.KanaMode, Keys.Kana },
            { Key.KanjiMode, Keys.Kanji },
            { Key.Escape, Keys.Escape },
            { Key.ImeConvert, Keys.ImeConvert },
            { Key.ImeNonConvert, Keys.ImeNoConvert },
            { Key.Space, Keys.Space },
            { Key.PageUp, Keys.PageUp },
            { Key.PageDown, Keys.PageDown },
            { Key.End, Keys.End },
            { Key.Home, Keys.Home },
            { Key.Left, Keys.Left },
            { Key.Up, Keys.Up },
            { Key.Right, Keys.Right },
            { Key.Down, Keys.Down },
            { Key.Select, Keys.Select },
            { Key.Print, Keys.Print },
            { Key.Execute, Keys.Execute },
            { Key.PrintScreen, Keys.PrintScreen },
            { Key.Insert, Keys.Insert },
            { Key.Delete, Keys.Delete },
            { Key.Help, Keys.Help },
            { Key.D0, Keys.D0 },
            { Key.D1, Keys.D1 },
            { Key.D2, Keys.D2 },
            { Key.D3, Keys.D3 },
            { Key.D4, Keys.D4 },
            { Key.D5, Keys.D5 },
            { Key.D6, Keys.D6 },
            { Key.D7, Keys.D7 },
            { Key.D8, Keys.D8 },
            { Key.D9, Keys.D9 },
            { Key.A, Keys.A },
            { Key.B, Keys.B },
            { Key.C, Keys.C },
            { Key.D, Keys.D },
            { Key.E, Keys.E },
            { Key.F, Keys.F },
            { Key.G, Keys.G },
            { Key.H, Keys.H },
            { Key.I, Keys.I },
            { Key.J, Keys.J },
            { Key.K, Keys.K },
            { Key.L, Keys.L },
            { Key.M, Keys.M },
            { Key.N, Keys.N },
            { Key.O, Keys.O },
            { Key.P, Keys.P },
            { Key.Q, Keys.Q },
            { Key.R, Keys.R },
            { Key.S, Keys.S },
            { Key.T, Keys.T },
            { Key.U, Keys.U },
            { Key.V, Keys.V },
            { Key.W, Keys.W },
            { Key.X, Keys.X },
            { Key.Y, Keys.Y },
            { Key.Z, Keys.Z },
            { Key.LWin, Keys.LeftWindows },
            { Key.RWin, Keys.RightWindows },
            { Key.Apps, Keys.Apps },
            { Key.Sleep, Keys.Sleep },
            { Key.NumPad0, Keys.NumPad0 },
            { Key.NumPad1, Keys.NumPad1 },
            { Key.NumPad2, Keys.NumPad2 },
            { Key.NumPad3, Keys.NumPad3 },
            { Key.NumPad4, Keys.NumPad4 },
            { Key.NumPad5, Keys.NumPad5 },
            { Key.NumPad6, Keys.NumPad6 },
            { Key.NumPad7, Keys.NumPad7 },
            { Key.NumPad8, Keys.NumPad8 },
            { Key.NumPad9, Keys.NumPad9 },
            { Key.Multiply, Keys.Multiply },
            { Key.Add, Keys.Add },
            { Key.Separator, Keys.Separator },
            { Key.Subtract, Keys.Subtract },
            { Key.Decimal, Keys.Decimal },
            { Key.Divide, Keys.Divide },
            { Key.F1, Keys.F1 },
            { Key.F2, Keys.F2 },
            { Key.F3, Keys.F3 },
            { Key.F4, Keys.F4 },
            { Key.F5, Keys.F5 },
            { Key.F6, Keys.F6 },
            { Key.F7, Keys.F7 },
            { Key.F8, Keys.F8 },
            { Key.F9, Keys.F9 },
            { Key.F10, Keys.F10 },
            { Key.F11, Keys.F11 },
            { Key.F12, Keys.F12 },
            { Key.F13, Keys.F13 },
            { Key.F14, Keys.F14 },
            { Key.F15, Keys.F15 },
            { Key.F16, Keys.F16 },
            { Key.F17, Keys.F17 },
            { Key.F18, Keys.F18 },
            { Key.F19, Keys.F19 },
            { Key.F20, Keys.F20 },
            { Key.F21, Keys.F21 },
            { Key.F22, Keys.F22 },
            { Key.F23, Keys.F23 },
            { Key.F24, Keys.F24 },
            { Key.NumLock, Keys.NumLock },
            { Key.Scroll, Keys.Scroll },
            { Key.LeftShift, Keys.LeftShift },
            { Key.RightShift, Keys.RightShift },
            { Key.LeftCtrl, Keys.LeftControl },
            { Key.RightCtrl, Keys.RightControl },
            { Key.LeftAlt, Keys.LeftAlt },
            { Key.RightAlt, Keys.RightAlt },
            { Key.BrowserBack, Keys.BrowserBack },
            { Key.BrowserForward, Keys.BrowserForward },
            { Key.BrowserRefresh, Keys.BrowserRefresh },
            { Key.BrowserStop, Keys.BrowserStop },
            { Key.BrowserSearch, Keys.BrowserSearch },
            { Key.BrowserFavorites, Keys.BrowserFavorites },
            { Key.BrowserHome, Keys.BrowserHome },
            { Key.VolumeMute, Keys.VolumeMute },
            { Key.VolumeDown, Keys.VolumeDown },
            { Key.VolumeUp, Keys.VolumeUp },
            { Key.MediaNextTrack, Keys.MediaNextTrack },
            { Key.MediaPreviousTrack, Keys.MediaPreviousTrack },
            { Key.MediaStop, Keys.MediaStop },
            { Key.MediaPlayPause, Keys.MediaPlayPause },
            { Key.LaunchMail, Keys.LaunchMail },
            { Key.SelectMedia, Keys.SelectMedia },
            { Key.LaunchApplication1, Keys.LaunchApplication1 },
            { Key.LaunchApplication2, Keys.LaunchApplication2 },
            { Key.OemSemicolon, Keys.OemSemicolon },
            { Key.OemPlus, Keys.OemPlus },
            { Key.OemComma, Keys.OemComma },
            { Key.OemMinus, Keys.OemMinus },
            { Key.OemPeriod, Keys.OemPeriod },
            { Key.OemQuestion, Keys.OemQuestion },
            { Key.OemTilde, Keys.OemTilde },
//{ Key.ChatPadGreen, Keys.ChatPadGreen },
//{ Key.ChatPadOrange, Keys.ChatPadOrange },
            { Key.OemOpenBrackets, Keys.OemOpenBrackets },
            { Key.OemPipe, Keys.OemPipe },
            { Key.OemCloseBrackets, Keys.OemCloseBrackets },
            { Key.OemQuotes, Keys.OemQuotes },
            { Key.Oem8, Keys.Oem8 },
            { Key.OemBackslash, Keys.OemBackslash },
            { Key.ImeProcessed, Keys.ProcessKey },
            { Key.OemCopy, Keys.OemCopy },
            { Key.OemAuto, Keys.OemAuto },
            { Key.OemEnlw, Keys.OemEnlW },
            { Key.Attn, Keys.Attn },
            { Key.CrSel, Keys.Crsel },
            { Key.ExSel, Keys.Exsel },
            { Key.EraseEof, Keys.EraseEof },
            { Key.Play, Keys.Play },
            { Key.Zoom, Keys.Zoom },
            { Key.Pa1, Keys.Pa1 },
            { Key.OemClear, Keys.OemClear }
        };
    }
}
