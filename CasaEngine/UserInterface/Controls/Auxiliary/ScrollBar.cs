
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/


using Button = CasaEngine.UserInterface.Controls.Buttons.Button;

namespace CasaEngine.UserInterface.Controls.Auxiliary
{

    public class ScrollBar : Control
    {


        private int _range = 100;
        private int _value;
        private int _pageSize = 50;
        private int _stepSize = 1;
        private readonly Orientation _orientation;
        // Buttons
        private readonly Button _buttonMinus;
        private readonly Button _buttonPlus;
        private readonly Button _buttonSlider;
        // Skin information
        private readonly string _skinButton = "ScrollBar.ButtonVert";
        private readonly string _skinRail = "ScrollBar.RailVert";
        private readonly string _skinSlider = "ScrollBar.SliderVert";
        private readonly string _skinGlyph = "ScrollBar.GlyphVert";
        private readonly string _skinMinus = "ScrollBar.ArrowUp";
        private readonly string _skinPlus = "ScrollBar.ArrowDown";



        public virtual int Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    if (_value < 0)
                    {
                        _value = 0;
                    }

                    if (_value > _range - _pageSize)
                    {
                        _value = _range - _pageSize;
                    }

                    Invalidate();
                    if (!Suspended)
                    {
                        OnValueChanged(new EventArgs());
                    }
                }
            }
        } // Value

        public virtual int Range
        {
            get => _range;
            set
            {
                if (_range != value)
                {
                    _range = value;
                    if (_pageSize > _range)
                    {
                        _pageSize = _range;
                    }

                    RecalculateParameters();
                    if (!Suspended)
                    {
                        OnRangeChanged(new EventArgs());
                    }
                }
            }
        } // Range

        public virtual int PageSize
        {
            get => _pageSize;
            set
            {
                if (_pageSize != value)
                {
                    _pageSize = value;
                    if (_pageSize > _range)
                    {
                        _pageSize = _range;
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
                    if (!Suspended)
                    {
                        OnStepSizeChanged(new EventArgs());
                    }
                }
            }
        } // StepSize



        public event EventHandler ValueChanged;
        public event EventHandler RangeChanged;
        public event EventHandler StepSizeChanged;
        public event EventHandler PageSizeChanged;



        public ScrollBar(UserInterfaceManager userInterfaceManager, Orientation orientation)
            : base(userInterfaceManager)
        {
            _orientation = orientation;
            CanFocus = false;

            if (orientation == Orientation.Horizontal)
            {
                _skinButton = "ScrollBar.ButtonHorz";
                _skinRail = "ScrollBar.RailHorz";
                _skinSlider = "ScrollBar.SliderHorz";
                _skinGlyph = "ScrollBar.GlyphHorz";
                _skinMinus = "ScrollBar.ArrowLeft";
                _skinPlus = "ScrollBar.ArrowRight";

                MinimumHeight = 16;
                MinimumWidth = 46;
                Width = 64;
                Height = 16;
            }
            else
            {
                _skinButton = "ScrollBar.ButtonVert";
                _skinRail = "ScrollBar.RailVert";
                _skinSlider = "ScrollBar.SliderVert";
                _skinGlyph = "ScrollBar.GlyphVert";
                _skinMinus = "ScrollBar.ArrowUp";
                _skinPlus = "ScrollBar.ArrowDown";

                MinimumHeight = 46;
                MinimumWidth = 16;
                Width = 16;
                Height = 64;
            }


            _buttonMinus = new Button(UserInterfaceManager)
            {
                Text = "",
                CanFocus = false
            };
            _buttonMinus.MousePress += ArrowPress;
            Add(_buttonMinus);

            _buttonSlider = new Button(UserInterfaceManager)
            {
                Text = "",
                CanFocus = false,
                MinimumHeight = 16,
                MinimumWidth = 16
            };
            _buttonSlider.Move += ButtonSliderMove;
            Add(_buttonSlider);

            _buttonPlus = new Button(UserInterfaceManager)
            {
                Text = "",
                CanFocus = false
            };
            _buttonPlus.MousePress += ArrowPress;
            Add(_buttonPlus);


        } // ScrollBar



        protected internal override void Init()
        {
            base.Init();

            var sc = new SkinControlInformation(_buttonPlus.SkinInformation);
            sc.Layers["Control"] = new SkinLayer(SkinInformation.Layers[_skinButton]);
            sc.Layers[_skinButton].Name = "Control";
            _buttonPlus.SkinInformation = _buttonMinus.SkinInformation = sc;

            var ss = new SkinControlInformation(_buttonSlider.SkinInformation);
            ss.Layers["Control"] = new SkinLayer(SkinInformation.Layers[_skinSlider]);
            ss.Layers[_skinSlider].Name = "Control";
            _buttonSlider.SkinInformation = ss;

            _buttonMinus.Glyph = new Glyph(SkinInformation.Layers[_skinMinus].Image.Texture)
            {
                SizeMode = SizeMode.Centered,
                Color = UserInterfaceManager.Skin.Controls["Button"].Layers["Control"].Text.Colors.Enabled
            };

            _buttonPlus.Glyph = new Glyph(SkinInformation.Layers[_skinPlus].Image.Texture)
            {
                SizeMode = SizeMode.Centered,
                Color = UserInterfaceManager.Skin.Controls["Button"].Layers["Control"].Text.Colors.Enabled
            };

            _buttonSlider.Glyph = new Glyph(SkinInformation.Layers[_skinGlyph].Image.Texture) { SizeMode = SizeMode.Centered };
        } // Init

        protected internal override void InitSkin()
        {
            base.InitSkin();
            SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["ScrollBar"]);
        } // InitSkin



        protected override void DisposeManagedResources()
        {
            // A disposed object could be still generating events, because it is alive for a time, in a disposed state, but alive nevertheless.
            ValueChanged = null;
            RangeChanged = null;
            StepSizeChanged = null;
            PageSizeChanged = null;
            base.DisposeManagedResources();
        } // DisposeManagedResources



        protected override void DrawControl(Rectangle rect)
        {
            RecalculateParameters();

            var bg = SkinInformation.Layers[_skinRail];
            UserInterfaceManager.Renderer.DrawLayer(bg, rect, Color.White, bg.States.Enabled.Index);
        } // DrawControl



        void ArrowPress(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButton.Left)
            {
                if (sender == _buttonMinus)
                {
                    Value -= StepSize;
                    if (Value < 0)
                    {
                        Value = 0;
                    }
                }
                else if (sender == _buttonPlus)
                {
                    Value += StepSize;
                    if (Value > _range - _pageSize)
                    {
                        Value = _range - _pageSize - 1;
                    }
                }
            }
        } // ArrowPress



        private void RecalculateParameters()
        {
            if (_buttonMinus != null && _buttonPlus != null && _buttonSlider != null)
            {
                if (_orientation == Orientation.Horizontal)
                {
                    _buttonMinus.Width = Height;
                    _buttonMinus.Height = Height;

                    _buttonPlus.Width = Height;
                    _buttonPlus.Height = Height;
                    _buttonPlus.Left = Width - Height;
                    _buttonPlus.Top = 0;

                    _buttonSlider.Movable = true;
                    var size = _buttonMinus.Width + SkinInformation.Layers[_skinSlider].OffsetX;

                    _buttonSlider.MinimumWidth = Height;
                    var w = (Width - 2 * size);
                    _buttonSlider.Width = (int)Math.Ceiling((_pageSize * w) / (float)_range);
                    _buttonSlider.Height = Height;


                    var px = (float)(Range - PageSize) / (float)(w - _buttonSlider.Width);
                    var pos = (int)(Math.Ceiling(Value / (float)px));
                    _buttonSlider.SetPosition(size + pos, 0);
                    if (_buttonSlider.Left < size)
                    {
                        _buttonSlider.SetPosition(size, 0);
                    }

                    if (_buttonSlider.Left + _buttonSlider.Width + size > Width)
                    {
                        _buttonSlider.SetPosition(Width - size - _buttonSlider.Width, 0);
                    }
                }
                else
                {
                    _buttonMinus.Width = Width;
                    _buttonMinus.Height = Width;

                    _buttonPlus.Width = Width;
                    _buttonPlus.Height = Width;
                    _buttonPlus.Top = Height - Width;

                    _buttonSlider.Movable = true;
                    var size = _buttonMinus.Height + SkinInformation.Layers[_skinSlider].OffsetY;

                    _buttonSlider.MinimumHeight = Width;
                    var h = (Height - 2 * size);
                    _buttonSlider.Height = (int)Math.Ceiling((_pageSize * h) / (float)_range);
                    _buttonSlider.Width = Width;

                    var px = (float)(Range - PageSize) / (float)(h - _buttonSlider.Height);
                    var pos = (int)(Math.Ceiling(Value / (float)px));
                    _buttonSlider.SetPosition(0, size + pos);
                    if (_buttonSlider.Top < size)
                    {
                        _buttonSlider.SetPosition(0, size);
                    }

                    if (_buttonSlider.Top + _buttonSlider.Height + size > Height)
                    {
                        _buttonSlider.SetPosition(0, Height - size - _buttonSlider.Height);
                    }
                }
            }
        } // RecalculateParameters



        private void ButtonSliderMove(object sender, MoveEventArgs e)
        {
            if (_orientation == Orientation.Horizontal)
            {
                var size = _buttonMinus.Width + SkinInformation.Layers[_skinSlider].OffsetX;
                _buttonSlider.SetPosition(e.Left, 0);
                if (_buttonSlider.Left < size)
                {
                    _buttonSlider.SetPosition(size, 0);
                }

                if (_buttonSlider.Left + _buttonSlider.Width + size > Width)
                {
                    _buttonSlider.SetPosition(Width - size - _buttonSlider.Width, 0);
                }
            }
            else
            {
                var size = _buttonMinus.Height + SkinInformation.Layers[_skinSlider].OffsetY;
                _buttonSlider.SetPosition(0, e.Top);
                if (_buttonSlider.Top < size)
                {
                    _buttonSlider.SetPosition(0, size);
                }

                if (_buttonSlider.Top + _buttonSlider.Height + size > Height)
                {
                    _buttonSlider.SetPosition(0, Height - size - _buttonSlider.Height);
                }
            }

            if (_orientation == Orientation.Horizontal)
            {
                var size = _buttonMinus.Width + SkinInformation.Layers[_skinSlider].OffsetX;
                var w = (Width - 2 * size) - _buttonSlider.Width;
                var px = (float)(Range - PageSize) / (float)w;
                Value = (int)(Math.Ceiling((_buttonSlider.Left - size) * px));
            }
            else
            {
                var size = _buttonMinus.Height + SkinInformation.Layers[_skinSlider].OffsetY;
                var h = (Height - 2 * size) - _buttonSlider.Height;
                var px = (float)(Range - PageSize) / (float)h;
                Value = (int)(Math.Ceiling((_buttonSlider.Top - size) * px));
            }
        } // ButtonSliderMove



        protected override void OnMouseUp(MouseEventArgs e)
        {
            _buttonSlider.Passive = false;
            base.OnMouseUp(e);
        } // OnMouseUp

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            _buttonSlider.Passive = true;

            if (e.Button == MouseButton.Left)
            {
                if (_orientation == Orientation.Horizontal)
                {
                    var pos = e.Position.X;

                    if (pos < _buttonSlider.Left)
                    {
                        Value -= _pageSize;
                        if (Value < 0)
                        {
                            Value = 0;
                        }
                    }
                    else if (pos >= _buttonSlider.Left + _buttonSlider.Width)
                    {
                        Value += _pageSize;
                        if (Value > _range - _pageSize)
                        {
                            Value = _range - _pageSize;
                        }
                    }
                }
                else
                {
                    var pos = e.Position.Y;

                    if (pos < _buttonSlider.Top)
                    {
                        Value -= _pageSize;
                        if (Value < 0)
                        {
                            Value = 0;
                        }
                    }
                    else if (pos >= _buttonSlider.Top + _buttonSlider.Height)
                    {
                        Value += _pageSize;
                        if (Value > _range - _pageSize)
                        {
                            Value = _range - _pageSize;
                        }
                    }
                }
            }
        } // OnMouseDown



        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            RecalculateParameters();
            if (Value + PageSize > Range)
            {
                Value = Range - PageSize;
            }
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


    } // ScrollBar
} // XNAFinalEngine.UserInterface