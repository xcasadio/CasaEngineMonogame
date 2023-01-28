
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

using System;


using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using CasaEngine.CoreSystems;


namespace XNAFinalEngine.UserInterface
{

    /// <summary>
    /// Slider for color values.
    /// </summary>
    public class SliderColor : Control
    {


        private readonly TextBox textBoxR, textBoxG, textBoxB;
        private readonly TrackBar sliderR, sliderG, sliderB;

        // If the system is updating the color, then the color won't be updated by the RGB sliders.
        private bool updatingColor;

        // If the system is updating the RGB sliders, then the RGB sliders won't be updated by the color.
        private bool updatingRGB;

        private ColorPickerDialog colorPickerDialog;



        public event MouseEventHandler SliderDown;
        public event MouseEventHandler SliderUp;
        public event MouseEventHandler SliderPress;



        /// <summary>
        /// Slider for numeric values.
        /// </summary>
        public SliderColor(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
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
            Control squareColor = new Panel(UserInterfaceManager)
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
                                        colorPickerDialog = new ColorPickerDialog(UserInterfaceManager, Color);
                                        UserInterfaceManager.Add(colorPickerDialog);
                                        colorPickerDialog.SquareColorDown += OnSliderDown;
                                        colorPickerDialog.SquareColorUp += OnSliderUp;
                                        colorPickerDialog.SquareColorPress += OnSliderPress;

                                        colorPickerDialog.Closed += delegate
                                        {
                                            Focused = true;
                                            colorPickerDialog.SquareColorDown -= OnSliderDown;
                                            colorPickerDialog.SquareColorUp -= OnSliderUp;
                                            colorPickerDialog.SquareColorPress -= OnSliderPress;
                                        };


                                        int left = squareColor.ControlLeftAbsoluteCoordinate;
                                        if (left + colorPickerDialog.Width > UserInterfaceManager.Screen.Width)
                                            left -= colorPickerDialog.Width;
                                        int top = squareColor.ControlTopAbsoluteCoordinate + squareColor.Height;
                                        if (top + colorPickerDialog.Height > UserInterfaceManager.Screen.Height)
                                            top -= colorPickerDialog.Height + squareColor.Height;
                                        colorPickerDialog.SetPosition(left, top);


                                        colorPickerDialog.ColorChanged += delegate
                                        {
                                            Color = colorPickerDialog.Color;
                                        };
                                    };



            textBoxR = new TextBox(UserInterfaceManager)
            {
                Parent = this,
                Top = 4,
                Width = 40,
                Left = label.Left + label.Width + 5 + 45,
                Text = "1",
            };
            sliderR = new TrackBar(UserInterfaceManager)
            {
                Parent = this,
                Anchor = Anchors.Left | Anchors.Top | Anchors.Right,
                Left = textBoxR.Left + textBoxR.Width + 4,
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
            sliderR.ValueChanged += delegate
                                    {
                                        updatingRGB = true;
                                        textBoxR.Text = Math.Round(sliderR.Value, 3).ToString();
                                        if (!updatingColor)
                                        {
                                            if (XNAFinalEngine.Input.Keyboard.KeyPressed(Keys.LeftControl))
                                            {
                                                sliderG.Value = sliderR.Value;
                                                sliderB.Value = sliderR.Value;
                                            }
                                            UpdateColorFromRGB();
                                        }
                                        updatingRGB = false;
                                    };
            textBoxR.KeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.Key == Keys.Enter)
                {
                    updatingRGB = true;
                    try
                    {
                        sliderR.Value = (float)double.Parse(textBoxR.Text);
                        UpdateColorFromRGB();
                    }
                    catch // If not numeric
                    {
                        textBoxR.Text = sliderR.Value.ToString();
                    }
                    updatingRGB = false;
                }
            };
            // For tabs and other not so common things.
            textBoxR.FocusLost += delegate
            {
                updatingRGB = true;
                try
                {
                    sliderR.Value = (float)double.Parse(textBoxR.Text);
                    UpdateColorFromRGB();
                }
                catch // If not numeric
                {
                    textBoxR.Text = sliderR.Value.ToString();
                }
                updatingRGB = false;
            };



            textBoxG = new TextBox(UserInterfaceManager)
            {
                Parent = this,
                Top = 4 + textBoxR.Top + textBoxR.Height,
                Width = 40,
                Left = label.Left + label.Width + 4 + 45,
                Text = "1"
            };
            sliderG = new TrackBar(UserInterfaceManager)
            {
                Parent = this,
                Anchor = Anchors.Left | Anchors.Top | Anchors.Right,
                Left = textBoxG.Left + textBoxG.Width + 4,
                Top = 6 + textBoxR.Top + textBoxR.Height,
                Height = 15,
                MinimumWidth = 100,
                MinimumValue = 0,
                MaximumValue = 1,
                Width = 176,
                ValueCanBeOutOfRange = false,
                IfOutOfRangeRescale = false,
                ScaleBarColor = ScaleColor.Green,
            };
            sliderG.ValueChanged += delegate
                                        {
                                            updatingRGB = true;
                                            textBoxG.Text = Math.Round(sliderG.Value, 3).ToString();
                                            if (!updatingColor)
                                            {
                                                if (XNAFinalEngine.Input.Keyboard.KeyPressed(Keys.LeftControl))
                                                {
                                                    sliderR.Value = sliderG.Value;
                                                    sliderB.Value = sliderG.Value;
                                                }
                                                UpdateColorFromRGB();
                                            }
                                            updatingRGB = false;
                                        };
            textBoxG.KeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.Key == Keys.Enter)
                {
                    updatingRGB = true;
                    try
                    {
                        sliderG.Value = (float)double.Parse(textBoxG.Text);
                        UpdateColorFromRGB();
                    }
                    catch // If not numeric
                    {
                        textBoxG.Text = sliderG.Value.ToString();
                    }
                    updatingRGB = false;
                }
            };
            // For tabs and other not so common things.
            textBoxG.FocusLost += delegate
            {
                updatingRGB = true;
                try
                {
                    sliderG.Value = (float)double.Parse(textBoxG.Text);
                    UpdateColorFromRGB();
                }
                catch // If not numeric
                {
                    textBoxG.Text = sliderG.Value.ToString();
                }
                updatingRGB = false;
            };



            textBoxB = new TextBox(UserInterfaceManager)
            {
                Parent = this,
                Top = 4 + textBoxG.Top + textBoxG.Height,
                Width = 40,
                Left = label.Left + label.Width + 4 + 45,
                Text = "1"
            };
            sliderB = new TrackBar(UserInterfaceManager)
            {
                Parent = this,
                Anchor = Anchors.Left | Anchors.Top | Anchors.Right,
                Left = textBoxB.Left + textBoxB.Width + 4,
                Top = 6 + textBoxG.Top + textBoxG.Height,
                Height = 15,
                MinimumWidth = 100,
                MinimumValue = 0,
                MaximumValue = 1,
                Width = 176,
                ValueCanBeOutOfRange = false,
                IfOutOfRangeRescale = false,
                ScaleBarColor = ScaleColor.Blue,
            };
            sliderB.ValueChanged += delegate
                                    {
                                        updatingRGB = true;
                                        textBoxB.Text = Math.Round(sliderB.Value, 3).ToString();
                                        if (!updatingColor)
                                        {
                                            if (XNAFinalEngine.Input.Keyboard.KeyPressed(Keys.LeftControl))
                                            {
                                                sliderR.Value = sliderB.Value;
                                                sliderG.Value = sliderB.Value;
                                            }
                                            UpdateColorFromRGB();
                                        }
                                        updatingRGB = false;
                                    };
            textBoxB.KeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.Key == Keys.Enter)
                {
                    updatingRGB = true;
                    try
                    {
                        sliderB.Value = (float)double.Parse(textBoxB.Text);
                        UpdateColorFromRGB();
                    }
                    catch // If not numeric
                    {
                        textBoxB.Text = sliderB.Value.ToString();
                    }
                    updatingRGB = false;
                }
            };
            // For tabs and other not so common things.
            textBoxB.FocusLost += delegate
            {
                updatingRGB = true;
                try
                {
                    sliderB.Value = (float)double.Parse(textBoxB.Text);
                    UpdateColorFromRGB();
                }
                catch // If not numeric
                {
                    textBoxB.Text = sliderB.Value.ToString();
                }
                updatingRGB = false;
            };


            ColorChanged += delegate
            {
                updatingColor = true;
                squareColor.Color = Color;
                if (!updatingRGB)
                    UpdateRGBFromColor();
                updatingColor = false;
                if (colorPickerDialog != null && !colorPickerDialog.IsDisposed && colorPickerDialog.Color != Color)
                    colorPickerDialog.Color = Color;
            };

            // To init all values with a color
            Color = Color.Gray;

            sliderR.SliderDown += OnSliderDown;
            sliderR.SliderUp += OnSliderUp;
            sliderR.SliderPress += OnSliderPress;
            sliderG.SliderDown += OnSliderDown;
            sliderG.SliderUp += OnSliderUp;
            sliderG.SliderPress += OnSliderPress;
            sliderB.SliderDown += OnSliderDown;
            sliderB.SliderUp += OnSliderUp;
            sliderB.SliderPress += OnSliderPress;

        } // SliderColor



        /// <summary>
        /// Dispose managed resources.
        /// </summary>
        protected override void DisposeManagedResources()
        {
            // A disposed object could be still generating events, because it is alive for a time, in a disposed state, but alive nevertheless.
            SliderDown = null;
            SliderUp = null;
            SliderPress = null;
            base.DisposeManagedResources();
        } // DisposeManagedResources



        /// <summary>
        /// Update Color from the R G B sliders values.
        /// </summary>
        private void UpdateColorFromRGB()
        {
            Color = new Color(sliderR.Value, sliderG.Value, sliderB.Value);
        } // UpdateColorFromRGB

        /// <summary>
        /// Update the R G B sliders values with the control color.
        /// </summary>
        private void UpdateRGBFromColor()
        {
            sliderR.Value = Color.R / 255f;
            sliderG.Value = Color.G / 255f;
            sliderB.Value = Color.B / 255f;
        } // UpdateRGBFromColor



        protected override void DrawControl(Rectangle rect)
        {
            // Only the children will be rendered.
        } // DrawControl



        protected virtual void OnSliderDown(object obj, MouseEventArgs e)
        {
            if (SliderDown != null)
                SliderDown(this, e);
        } // OnSliderDown

        protected virtual void OnSliderUp(object obj, MouseEventArgs e)
        {
            if (SliderUp != null)
                SliderUp(this, e);
        } // OnSliderUp

        protected virtual void OnSliderPress(object obj, MouseEventArgs e)
        {
            if (SliderPress != null)
                SliderPress(this, e);
        } // OnSliderPress


    } // SliderColor
} // XNAFinalEngine.UserInterface