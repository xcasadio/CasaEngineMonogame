
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/


using CasaEngine.Framework.UserInterface.Controls.Auxiliary;
using Button = CasaEngine.Framework.UserInterface.Controls.Buttons.Button;

namespace CasaEngine.Framework.UserInterface.Controls.Sliders
{


    public enum ScaleColor
    {
        Red,
        Green,
        Blue,
        Default,
    } // ScaleColor


    public class TrackBar : Control
    {


        private float _internalValue;

        private int _stepSize = 1;

        private int _pageSize = 5;

        private ScaleColor _scaleColor = ScaleColor.Default;

        private bool _drawScale = true;

        private readonly Button _buttonSlider;

        private float _minimumValue;

        private float _maximumValue = 100;



        public virtual float MinimumValue
        {
            get => _minimumValue;
            set
            {
                _minimumValue = value;
                RecalculateParameters();
                if (!Suspended)
                {
                    OnRangeChanged(new EventArgs());
                }
            }
        } // MinimumValue

        public virtual float MaximumValue
        {
            get => _maximumValue;
            set
            {
                _maximumValue = value;
                RecalculateParameters();
                if (!Suspended)
                {
                    OnRangeChanged(new EventArgs());
                }
            }
        } // MaximumValue

        public virtual bool IfOutOfRangeRescale { get; set; }

        public virtual bool ValueCanBeOutOfRange { get; set; }

        private float InternalValue
        {
            get => _internalValue;
            set
            {
                if (_internalValue != value)
                {
                    _internalValue = value;
                    if (!ValueCanBeOutOfRange) // Then, we need to check that the internal value is between 0 and 100.
                    {
                        if (_internalValue < 0)
                        {
                            _internalValue = 0;
                        }

                        if (_internalValue > 100)
                        {
                            _internalValue = 100;
                        }
                    }
                    else if (IfOutOfRangeRescale) // Is posible that we need to rescale. In other words the maximum or minimum value changes.
                    {
                        var tempCurrentRealValue = CalculateRealValue(_internalValue);
                        if (_internalValue < 0)
                        {
                            _minimumValue = _minimumValue - 2 * (_minimumValue - CalculateRealValue(_internalValue));
                            _internalValue = CalculateInternalValue(tempCurrentRealValue);
                        }
                        if (_internalValue > 100)
                        {
                            _maximumValue = _maximumValue + 2 * (CalculateRealValue(_internalValue) - _maximumValue);
                            _internalValue = CalculateInternalValue(tempCurrentRealValue);
                        }
                    }
                    Invalidate();
                    if (!Suspended)
                    {
                        OnValueChanged(new EventArgs());
                    }
                }
            }
        } // InternalValue

        public virtual float Value
        {
            get => CalculateRealValue(_internalValue);
            set => InternalValue = CalculateInternalValue(value);
        } // Value

        public virtual int PageSize
        {
            get => _pageSize;
            set
            {
                if (_pageSize != value)
                {
                    _pageSize = value;
                    if (_pageSize > 100)
                    {
                        _pageSize = 100;
                    }

                    RecalculateParameters();
                    if (!Suspended)
                    {
                        OnPageSizeChanged(new EventArgs());
                    }
                }
            }
        } // PageSize

        public virtual int StepSize
        {
            get => _stepSize;
            set
            {
                if (_stepSize != value)
                {
                    _stepSize = value;
                    if (_stepSize > 100)
                    {
                        _stepSize = 100;
                    }

                    if (!Suspended)
                    {
                        OnStepSizeChanged(new EventArgs());
                    }
                }
            }
        } // StepSize

        public virtual ScaleColor ScaleBarColor
        {
            get => _scaleColor;
            set => _scaleColor = value;
        } // ScaleBarColor

        public virtual bool DrawScaleBar
        {
            get => _drawScale;
            set => _drawScale = value;
        } // DrawScale



        public event EventHandler ValueChanged;
        public event EventHandler RangeChanged;
        public event EventHandler StepSizeChanged;
        public event EventHandler PageSizeChanged;
        public event MouseEventHandler SliderDown;
        public event MouseEventHandler SliderUp;
        public event MouseEventHandler SliderPress;



        public TrackBar(UserInterfaceManager userInterfaceManager)
            : base(userInterfaceManager)
        {
            Width = 64;
            Height = 20;
            CanFocus = false;

            _buttonSlider = new Button(UserInterfaceManager)
            {
                Text = "",
                CanFocus = false,
                Parent = this,
                Anchor = Anchors.Left | Anchors.Top | Anchors.Bottom,
                Detached = true,
                Movable = true
            };

            _buttonSlider.MouseDown += delegate { OnMouseDown(new MouseEventArgs()); };
            _buttonSlider.MousePress += delegate { OnMousePress(new MouseEventArgs()); };
            _buttonSlider.MouseUp += delegate { OnMouseUp(new MouseEventArgs()); };
        } // TrackBar



        protected internal override void Init()
        {
            base.Init();
            _buttonSlider.SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["TrackBar.Button"]);
            _buttonSlider.Move += ButtonSlider_Move;
            _buttonSlider.KeyPress += ButtonSlider_KeyPress;
        } // Init

        protected internal override void InitSkin()
        {
            base.InitSkin();
            SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["TrackBar"]);
        } // InitSkin



        protected override void DisposeManagedResources()
        {
            // A disposed object could be still generating events, because it is alive for a time, in a disposed state, but alive nevertheless.
            ValueChanged = null;
            RangeChanged = null;
            StepSizeChanged = null;
            PageSizeChanged = null;
            SliderDown = null;
            SliderUp = null;
            SliderPress = null;
            base.DisposeManagedResources();
        } // DisposeManagedResources



        protected override void DrawControl(Rectangle rect)
        {
            RecalculateParameters();

            var p = SkinInformation.Layers["Control"];
            var l = SkinInformation.Layers["ScaleOrange"];

            const float ratio = 0.66f;
            var h = (int)(ratio * rect.Height);
            var t = rect.Top + (Height - h) / 2;

            var px = _internalValue / 100;
            var w = (int)Math.Ceiling(px * (rect.Width - p.ContentMargins.Horizontal - _buttonSlider.Width)) + 2;

            if (w < l.SizingMargins.Vertical)
            {
                w = l.SizingMargins.Vertical;
            }

            if (w > rect.Width - p.ContentMargins.Horizontal)
            {
                w = rect.Width - p.ContentMargins.Horizontal;
            }

            // Draw control
            base.DrawControl(new Rectangle(rect.Left, t, rect.Width, h));
            // Draw progress line.
            var r1 = new Rectangle(rect.Left + p.ContentMargins.Left, t + p.ContentMargins.Top, w, h - p.ContentMargins.Vertical);
            if (_drawScale)
            {
                switch (_scaleColor)
                {
                    case ScaleColor.Red: UserInterfaceManager.Renderer.DrawLayer(this, SkinInformation.Layers["ScaleRed"], r1); break;
                    case ScaleColor.Green: UserInterfaceManager.Renderer.DrawLayer(this, SkinInformation.Layers["ScaleGreen"], r1); break;
                    case ScaleColor.Blue: UserInterfaceManager.Renderer.DrawLayer(this, SkinInformation.Layers["ScaleBlue"], r1); break;
                    case ScaleColor.Default: UserInterfaceManager.Renderer.DrawLayer(this, l, r1); break;
                }
            }
        } // DrawControl



        private float CalculateRealValue(float value)
        {
            return value * (_maximumValue - _minimumValue) / 100f + _minimumValue;
        } // CalculateRealValue

        private float CalculateInternalValue(float value)
        {
            return (value - _minimumValue) * 100f / (_maximumValue - _minimumValue);
        } // CalculateInternalValue



        private void ButtonSlider_Move(object sender, MoveEventArgs e)
        {
            var p = SkinInformation.Layers["Control"];
            var size = _buttonSlider.Width;
            var w = Width - p.ContentMargins.Horizontal - size;
            var pos = e.Left;

            if (pos < p.ContentMargins.Left)
            {
                pos = p.ContentMargins.Left;
            }

            if (pos > w + p.ContentMargins.Left)
            {
                pos = w + p.ContentMargins.Left;
            }

            _buttonSlider.SetPosition(pos, 0);
            var px = 100 / (float)w;

            // Update value. But in this case the value can't be out of range.
            var temp = ValueCanBeOutOfRange;
            ValueCanBeOutOfRange = false;
            InternalValue = (pos - p.ContentMargins.Left) * px;
            ValueCanBeOutOfRange = temp;
        } // ButtonSlider_Move

        private void ButtonSlider_KeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Keys.Left || e.Key == Keys.Down)
            {
                InternalValue -= _stepSize;
            }
            else if (e.Key == Keys.Right || e.Key == Keys.Up)
            {
                InternalValue += _stepSize;
            }
            else if (e.Key == Keys.PageDown)
            {
                InternalValue -= _pageSize;
            }
            else if (e.Key == Keys.PageUp)
            {
                InternalValue += _pageSize;
            }
            else if (e.Key == Keys.Home)
            {
                InternalValue = 0;
            }
            else if (e.Key == Keys.End)
            {
                InternalValue = 100;
            }
        } // ButtonSlider_KeyPress



        private void RecalculateParameters()
        {
            if (_buttonSlider != null)
            {
                if (_buttonSlider.Width > 12)
                {
                    _buttonSlider.Glyph = new Glyph(UserInterfaceManager.Skin.Images["Shared.Glyph"].Texture) { SizeMode = SizeMode.Centered };
                }
                else
                {
                    _buttonSlider.Glyph = null;
                }

                var p = SkinInformation.Layers["Control"];
                _buttonSlider.Width = (int)(Height * 0.8);
                _buttonSlider.Height = Height;
                var size = _buttonSlider.Width;
                var w = Width - p.ContentMargins.Horizontal - size;

                var px = 100 / (float)w;
                var pos = p.ContentMargins.Left + (int)Math.Ceiling(_internalValue / px);

                if (pos < p.ContentMargins.Left)
                {
                    pos = p.ContentMargins.Left;
                }

                if (pos > w + p.ContentMargins.Left)
                {
                    pos = w + p.ContentMargins.Left;
                }

                _buttonSlider.SetPosition(pos, 0);
            }
        } // RecalculateParameters



        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (SliderDown != null)
            {
                SliderDown(this, e);
            }
        } // OnMouseDown

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (SliderUp != null)
            {
                SliderUp(this, e);
            }
        } // OnMouseUp

        protected override void OnMousePress(MouseEventArgs e)
        {
            base.OnMousePress(e);
            if (e.Button == MouseButton.Left)
            {
                _buttonSlider.Left = e.Position.X - _buttonSlider.Width / 2;
            }

            if (SliderPress != null)
            {
                SliderPress(this, e);
            }
        } // OnMousePress

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            RecalculateParameters();
        } // OnResize

        protected virtual void OnValueChanged(EventArgs e)
        {
            if (ValueChanged != null)
            {
                ValueChanged.Invoke(this, e);
            }
        } // OnValueChanged

        protected virtual void OnRangeChanged(EventArgs e)
        {
            if (RangeChanged != null)
            {
                RangeChanged.Invoke(this, e);
            }
        } // OnRangeChanged

        protected virtual void OnPageSizeChanged(EventArgs e)
        {
            if (PageSizeChanged != null)
            {
                PageSizeChanged.Invoke(this, e);
            }
        } // OnPageSizeChanged

        protected virtual void OnStepSizeChanged(EventArgs e)
        {
            if (StepSizeChanged != null)
            {
                StepSizeChanged.Invoke(this, e);
            }
        } // OnStepSizeChanged


    } // TrackBar
} // XNAFinalEngine.UserInterface