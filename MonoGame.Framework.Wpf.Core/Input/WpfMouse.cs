using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Media;

namespace Microsoft.Xna.Framework.Input
{
    public class WpfMouse
    {
        [DllImport("User32.dll")]
        static extern bool SetCursorPos(int x, int y);

        readonly WpfGame _focusElement;

        MouseState _mouseState;
        bool _captureMouseWithin = true;

        public WpfMouse(WpfGame focusElement)
        {
            _focusElement = focusElement ?? throw new ArgumentNullException(nameof(focusElement));
            _focusElement.MouseWheel += HandleMouse;
            // movement
            _focusElement.MouseMove += HandleMouse;
            _focusElement.MouseEnter += HandleMouse;
            _focusElement.MouseLeave += HandleMouse;
            // clicks
            _focusElement.MouseLeftButtonDown += HandleMouse;
            _focusElement.MouseLeftButtonUp += HandleMouse;
            _focusElement.MouseRightButtonDown += HandleMouse;
            _focusElement.MouseRightButtonUp += HandleMouse;
        }

        public bool CaptureMouseWithin
        {
            get => _captureMouseWithin;
            set
            {
                if (!value && _focusElement.IsMouseCaptured)
                {
                    _focusElement.ReleaseMouseCapture();
                }

                _captureMouseWithin = value;
            }
        }

        public MouseState GetState() => _mouseState;

        void HandleMouse(object sender, MouseEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            var pos = e.GetPosition(_focusElement);
            if (!CaptureMouseWithin)
            {
                pos = new System.Windows.Point(Clamp(pos.X, 0.0, _focusElement.ActualWidth), Clamp(pos.Y, 0.0, _focusElement.ActualHeight));
            }

            if (_focusElement.IsMouseDirectlyOver && System.Windows.Input.Keyboard.FocusedElement != _focusElement)
            {
                if (_focusElement.IsControlOnActiveWindow())
                {
                    // however, only focus if we are the active window, otherwise the window will become active and pop into foreground just by hovering the mouse over the game panel
                    // finally check if user wants us to focus already on mouse over
                    if (_focusElement.FocusOnMouseOver)
                    {
                        _focusElement.Focus();
                    }
                    // otherwise focus only when the user clicks into the game on windows this behaviour doesn't require an explicit left click
                    // instead, left, middle, right and even xbuttons work (the only thing that doesn't trigger focus is scrolling) so mimic that exactly
                    else if (e.LeftButton == MouseButtonState.Pressed ||
                             e.RightButton == MouseButtonState.Pressed ||
                             e.MiddleButton == MouseButtonState.Pressed ||
                             e.XButton1 == MouseButtonState.Pressed ||
                             e.XButton2 == MouseButtonState.Pressed)
                    {
                        _focusElement.Focus();
                    }
                }
            }

            if ((!_focusElement.IsMouseDirectlyOver || _focusElement.IsMouseCaptured) && CaptureMouseWithin)
            {
                if (_focusElement.IsMouseCaptured)
                {
                    var hit = false;
                    VisualTreeHelper.HitTest(_focusElement, filterTarget => HitTestFilterBehavior.Continue, target =>
                    {
                        if (target.VisualHit == _focusElement)
                        {
                            hit = true;
                        }

                        return HitTestResultBehavior.Continue;
                    }, new PointHitTestParameters(pos));
                    if (!hit)
                    {
                        // when the mouse is leaving the control we need to register button releases
                        // when the user clicks in the control, holds the button and moves it outside the control and releases there it normally does not registered
                        // the control would thus think that the button is still pressed
                        // using capture allows us to receive this event, propagate it and then free the mouse
                        _mouseState = new MouseState(_mouseState.X, _mouseState.Y, _mouseState.ScrollWheelValue, (ButtonState)e.LeftButton, (ButtonState)e.MiddleButton, (ButtonState)e.RightButton, (ButtonState)e.XButton1, (ButtonState)e.XButton2);
                        // only release if LeftMouse is up
                        if (e.LeftButton == MouseButtonState.Released)
                        {
                            _focusElement.ReleaseMouseCapture();
                        }

                        e.Handled = true;
                        return;
                    }
                }
                else
                {
                    return;
                }
            }

            if (CaptureMouseWithin)
            {
                if (!_focusElement.IsMouseCaptured)
                {
                    if (_focusElement.IsControlOnActiveWindow())
                    {
                        _focusElement.CaptureMouse();
                    }
                    else
                    {
                        return;
                    }
                }
            }
            else
            {
                if (_focusElement.IsFocused && !_focusElement.IsControlOnActiveWindow())
                {
                    return;
                }
            }
            e.Handled = true;
            var m = _mouseState;
            var w = e as MouseWheelEventArgs;
            _mouseState = new MouseState((int)pos.X, (int)pos.Y, m.ScrollWheelValue + (w?.Delta ?? 0), (ButtonState)e.LeftButton, (ButtonState)e.MiddleButton, (ButtonState)e.RightButton, (ButtonState)e.XButton1, (ButtonState)e.XButton2);
        }

        static double Clamp(double v, double min, double max) => v < min ? min : (v > max ? max : v);

        public void SetCursor(int x, int y)
        {
            var p = _focusElement.PointToScreen(new System.Windows.Point(x, y));
            SetCursorPos((int)p.X, (int)p.Y);
        }
    }
}