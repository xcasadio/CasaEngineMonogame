
/*
Copyright (c) 2008-2012, Laboratorio de Investigación y Desarrollo en Visualización y Computación Gráfica - 
                         Departamento de Ciencias e Ingeniería de la Computación - Universidad Nacional del Sur.
All rights reserved.
Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

•	Redistributions of source code must retain the above copyright, this list of conditions and the following disclaimer.

•	Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer
    in the documentation and/or other materials provided with the distribution.

•	Neither the name of the Universidad Nacional del Sur nor the names of its contributors may be used to endorse or promote products derived
    from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS ''AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR
CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE,
EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

-----------------------------------------------------------------------------------------------------------------------------------------------
Author: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/


using CasaEngine.Engine.Input;
using CasaEngine.Framework.UserInterface.Controls.Auxiliary;
using CasaEngine.Framework.UserInterface.Controls.Windows;
using TextBox = CasaEngine.Framework.UserInterface.Controls.Text.TextBox;

namespace CasaEngine.Framework.UserInterface.Controls.Sliders
{

    public class SliderColor : Control
    {


        private readonly TextBox _textBoxR, _textBoxG, _textBoxB;
        private readonly TrackBar _sliderR, _sliderG, _sliderB;

        // If the system is updating the color, then the color won't be updated by the RGB sliders.
        private bool _updatingColor;

        // If the system is updating the RGB sliders, then the RGB sliders won't be updated by the color.
        private bool _updatingRgb;

        private ColorPickerDialog _colorPickerDialog;



        public event MouseEventHandler SliderDown;
        public event MouseEventHandler SliderUp;
        public event MouseEventHandler SliderPress;



        public SliderColor(UserInterfaceManager userInterfaceManager)
            : base(userInterfaceManager)
        {

            Anchor = Anchors.Left | Anchors.Right | Anchors.Top;
            CanFocus = false;
            Passive = true;
            Width = 420;
            Height = 75;
            var label = new Label(UserInterfaceManager)
            {
                Parent = this,
                Width = 150,
                Top = 25,
            };
            TextChanged += delegate { label.Text = Text; };


            // Square color
            Control squareColor = new Panel.Panel(UserInterfaceManager)
            {
                Left = label.Left + label.Width + 5,
                Top = 17,
                Width = 40,
                Height = 40,
                Color = Color,
                BevelBorder = BevelBorder.All,
                BevelStyle = BevelStyle.Etched,
                BevelColor = Color.Black,
            };
            Add(squareColor);
            squareColor.MouseDown += delegate
                                    {
                                        _colorPickerDialog = new ColorPickerDialog(UserInterfaceManager, Color);
                                        UserInterfaceManager.Add(_colorPickerDialog);
                                        _colorPickerDialog.SquareColorDown += OnSliderDown;
                                        _colorPickerDialog.SquareColorUp += OnSliderUp;
                                        _colorPickerDialog.SquareColorPress += OnSliderPress;

                                        _colorPickerDialog.Closed += delegate
                                        {
                                            Focused = true;
                                            _colorPickerDialog.SquareColorDown -= OnSliderDown;
                                            _colorPickerDialog.SquareColorUp -= OnSliderUp;
                                            _colorPickerDialog.SquareColorPress -= OnSliderPress;
                                        };


                                        var left = squareColor.ControlLeftAbsoluteCoordinate;
                                        if (left + _colorPickerDialog.Width > UserInterfaceManager.Screen.Width)
                                        {
                                            left -= _colorPickerDialog.Width;
                                        }

                                        var top = squareColor.ControlTopAbsoluteCoordinate + squareColor.Height;
                                        if (top + _colorPickerDialog.Height > UserInterfaceManager.Screen.Height)
                                        {
                                            top -= _colorPickerDialog.Height + squareColor.Height;
                                        }

                                        _colorPickerDialog.SetPosition(left, top);


                                        _colorPickerDialog.ColorChanged += delegate
                                        {
                                            Color = _colorPickerDialog.Color;
                                        };
                                    };



            _textBoxR = new TextBox(UserInterfaceManager)
            {
                Parent = this,
                Top = 4,
                Width = 40,
                Left = label.Left + label.Width + 5 + 45,
                Text = "1",
            };
            _sliderR = new TrackBar(UserInterfaceManager)
            {
                Parent = this,
                Anchor = Anchors.Left | Anchors.Top | Anchors.Right,
                Left = _textBoxR.Left + _textBoxR.Width + 4,
                Top = 6,
                MinimumWidth = 100,
                Height = 15,
                MinimumValue = 0,
                MaximumValue = 1,
                Width = 176,
                ValueCanBeOutOfRange = false,
                IfOutOfRangeRescale = false,
                ScaleBarColor = ScaleColor.Red,
            };
            _sliderR.ValueChanged += delegate
                                    {
                                        _updatingRgb = true;
                                        _textBoxR.Text = Math.Round(_sliderR.Value, 3).ToString();
                                        if (!_updatingColor)
                                        {
                                            if (Keyboard.KeyPressed(Keys.LeftControl))
                                            {
                                                _sliderG.Value = _sliderR.Value;
                                                _sliderB.Value = _sliderR.Value;
                                            }
                                            UpdateColorFromRgb();
                                        }
                                        _updatingRgb = false;
                                    };
            _textBoxR.KeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.Key == Keys.Enter)
                {
                    _updatingRgb = true;
                    try
                    {
                        _sliderR.Value = (float)double.Parse(_textBoxR.Text);
                        UpdateColorFromRgb();
                    }
                    catch // If not numeric
                    {
                        _textBoxR.Text = _sliderR.Value.ToString();
                    }
                    _updatingRgb = false;
                }
            };
            // For tabs and other not so common things.
            _textBoxR.FocusLost += delegate
            {
                _updatingRgb = true;
                try
                {
                    _sliderR.Value = (float)double.Parse(_textBoxR.Text);
                    UpdateColorFromRgb();
                }
                catch // If not numeric
                {
                    _textBoxR.Text = _sliderR.Value.ToString();
                }
                _updatingRgb = false;
            };



            _textBoxG = new TextBox(UserInterfaceManager)
            {
                Parent = this,
                Top = 4 + _textBoxR.Top + _textBoxR.Height,
                Width = 40,
                Left = label.Left + label.Width + 4 + 45,
                Text = "1"
            };
            _sliderG = new TrackBar(UserInterfaceManager)
            {
                Parent = this,
                Anchor = Anchors.Left | Anchors.Top | Anchors.Right,
                Left = _textBoxG.Left + _textBoxG.Width + 4,
                Top = 6 + _textBoxR.Top + _textBoxR.Height,
                Height = 15,
                MinimumWidth = 100,
                MinimumValue = 0,
                MaximumValue = 1,
                Width = 176,
                ValueCanBeOutOfRange = false,
                IfOutOfRangeRescale = false,
                ScaleBarColor = ScaleColor.Green,
            };
            _sliderG.ValueChanged += delegate
                                        {
                                            _updatingRgb = true;
                                            _textBoxG.Text = Math.Round(_sliderG.Value, 3).ToString();
                                            if (!_updatingColor)
                                            {
                                                if (Keyboard.KeyPressed(Keys.LeftControl))
                                                {
                                                    _sliderR.Value = _sliderG.Value;
                                                    _sliderB.Value = _sliderG.Value;
                                                }
                                                UpdateColorFromRgb();
                                            }
                                            _updatingRgb = false;
                                        };
            _textBoxG.KeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.Key == Keys.Enter)
                {
                    _updatingRgb = true;
                    try
                    {
                        _sliderG.Value = (float)double.Parse(_textBoxG.Text);
                        UpdateColorFromRgb();
                    }
                    catch // If not numeric
                    {
                        _textBoxG.Text = _sliderG.Value.ToString();
                    }
                    _updatingRgb = false;
                }
            };
            // For tabs and other not so common things.
            _textBoxG.FocusLost += delegate
            {
                _updatingRgb = true;
                try
                {
                    _sliderG.Value = (float)double.Parse(_textBoxG.Text);
                    UpdateColorFromRgb();
                }
                catch // If not numeric
                {
                    _textBoxG.Text = _sliderG.Value.ToString();
                }
                _updatingRgb = false;
            };



            _textBoxB = new TextBox(UserInterfaceManager)
            {
                Parent = this,
                Top = 4 + _textBoxG.Top + _textBoxG.Height,
                Width = 40,
                Left = label.Left + label.Width + 4 + 45,
                Text = "1"
            };
            _sliderB = new TrackBar(UserInterfaceManager)
            {
                Parent = this,
                Anchor = Anchors.Left | Anchors.Top | Anchors.Right,
                Left = _textBoxB.Left + _textBoxB.Width + 4,
                Top = 6 + _textBoxG.Top + _textBoxG.Height,
                Height = 15,
                MinimumWidth = 100,
                MinimumValue = 0,
                MaximumValue = 1,
                Width = 176,
                ValueCanBeOutOfRange = false,
                IfOutOfRangeRescale = false,
                ScaleBarColor = ScaleColor.Blue,
            };
            _sliderB.ValueChanged += delegate
                                    {
                                        _updatingRgb = true;
                                        _textBoxB.Text = Math.Round(_sliderB.Value, 3).ToString();
                                        if (!_updatingColor)
                                        {
                                            if (Keyboard.KeyPressed(Keys.LeftControl))
                                            {
                                                _sliderR.Value = _sliderB.Value;
                                                _sliderG.Value = _sliderB.Value;
                                            }
                                            UpdateColorFromRgb();
                                        }
                                        _updatingRgb = false;
                                    };
            _textBoxB.KeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.Key == Keys.Enter)
                {
                    _updatingRgb = true;
                    try
                    {
                        _sliderB.Value = (float)double.Parse(_textBoxB.Text);
                        UpdateColorFromRgb();
                    }
                    catch // If not numeric
                    {
                        _textBoxB.Text = _sliderB.Value.ToString();
                    }
                    _updatingRgb = false;
                }
            };
            // For tabs and other not so common things.
            _textBoxB.FocusLost += delegate
            {
                _updatingRgb = true;
                try
                {
                    _sliderB.Value = (float)double.Parse(_textBoxB.Text);
                    UpdateColorFromRgb();
                }
                catch // If not numeric
                {
                    _textBoxB.Text = _sliderB.Value.ToString();
                }
                _updatingRgb = false;
            };


            ColorChanged += delegate
            {
                _updatingColor = true;
                squareColor.Color = Color;
                if (!_updatingRgb)
                {
                    UpdateRgbFromColor();
                }

                _updatingColor = false;
                if (_colorPickerDialog != null && !_colorPickerDialog.IsDisposed && _colorPickerDialog.Color != Color)
                {
                    _colorPickerDialog.Color = Color;
                }
            };

            // To init all values with a color
            Color = Color.Gray;

            _sliderR.SliderDown += OnSliderDown;
            _sliderR.SliderUp += OnSliderUp;
            _sliderR.SliderPress += OnSliderPress;
            _sliderG.SliderDown += OnSliderDown;
            _sliderG.SliderUp += OnSliderUp;
            _sliderG.SliderPress += OnSliderPress;
            _sliderB.SliderDown += OnSliderDown;
            _sliderB.SliderUp += OnSliderUp;
            _sliderB.SliderPress += OnSliderPress;

        } // SliderColor



        protected override void DisposeManagedResources()
        {
            // A disposed object could be still generating events, because it is alive for a time, in a disposed state, but alive nevertheless.
            SliderDown = null;
            SliderUp = null;
            SliderPress = null;
            base.DisposeManagedResources();
        } // DisposeManagedResources



        private void UpdateColorFromRgb()
        {
            Color = new Color(_sliderR.Value, _sliderG.Value, _sliderB.Value);
        } // UpdateColorFromRGB

        private void UpdateRgbFromColor()
        {
            _sliderR.Value = Color.R / 255f;
            _sliderG.Value = Color.G / 255f;
            _sliderB.Value = Color.B / 255f;
        } // UpdateRGBFromColor



        protected override void DrawControl(Rectangle rect)
        {
            // Only the children will be rendered.
        } // DrawControl



        protected virtual void OnSliderDown(object obj, MouseEventArgs e)
        {
            if (SliderDown != null)
            {
                SliderDown(this, e);
            }
        } // OnSliderDown

        protected virtual void OnSliderUp(object obj, MouseEventArgs e)
        {
            if (SliderUp != null)
            {
                SliderUp(this, e);
            }
        } // OnSliderUp

        protected virtual void OnSliderPress(object obj, MouseEventArgs e)
        {
            if (SliderPress != null)
            {
                SliderPress(this, e);
            }
        } // OnSliderPress


    } // SliderColor
} // XNAFinalEngine.UserInterface