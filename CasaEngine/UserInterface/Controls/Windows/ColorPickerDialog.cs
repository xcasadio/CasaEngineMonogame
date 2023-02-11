
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


//using XNAFinalEngine.Editor; // This can be avoided and ideally I have to avoid this.

//using XNAFinalEngine.Graphics;
using XNAFinalEngine.Helpers;

namespace XNAFinalEngine.UserInterface
{

    public class ColorPickerDialog : Dialog
    {


        // Square color lenght.
        const int SquareColorlenght = 132;

        // For the square color palette.
        // This is part of the implementation to capture the mouse movement outside the control's border.
        private const int SquareColorLeft = 5;
        private const int SquareColorTop = 5;



        private bool _updatingColorSquareAndIntensityBar;

        // The initial color.
        private readonly Color _oldColor;

        // The current square color's position.
        private Point _positionSquareColor;

        // Intensity level of the right bar.
        private float _intensityLevel = 0.5f;

        // The first position in the color palette when this sub control is changing its value (left mouse pressed and not released).
        private Point _positionBeginningMovement;

        // The control is updating the values. 
        // When a sub control updates one value of another control we don't want that the updated control update the values of the first one.
        private bool _update = true;

        // The first intensity level value when the control is updated (left mouse pressed and not released).
        private float _intensityLevelValueBeginningMovement;

        // The texture picker for screen picking.
        //private Picker picker;

        // If the control is in screen picking mode.
        private bool _isPicking;

        // Controls.
        private readonly Button _buttonPick;
        private readonly Button _buttonClose;
        private readonly Control _squareColorPalette;
        private readonly TextBox _textBoxRed, _textBoxGreen, _textBoxBlue;
        private readonly Control _intensityLevelBar, _background;



        public override Color Color
        {
            get => base.Color;
            set
            {
                base.Color = value;
                if (!_updatingColorSquareAndIntensityBar)
                    _positionSquareColor = PositionFromColor(Color);
            }
        } // Color



        public event MouseEventHandler SquareColorDown;
        public event MouseEventHandler SquareColorUp;
        public event MouseEventHandler SquareColorPress;



        public ColorPickerDialog(UserInterfaceManager userInterfaceManager, Color oldColor)
            : base(userInterfaceManager)
        {

            ClientWidth = 5 + 132 + 10;
            ClientHeight = 235;
            TopPanel.Visible = false;
            IconVisible = false;
            Resizable = false;
            BorderVisible = false;
            Movable = false;
            StayOnTop = true;
            AutoScroll = false;


            // The background object is invisible, it serves input actions.
            _background = new Control(UserInterfaceManager)
            {
                Left = 0,
                Top = 0,
                Width = UserInterfaceManager.Screen.Width,
                Height = UserInterfaceManager.Screen.Height,
                StayOnTop = true, // To bring it to the second place (first is the main control)
                Color = new Color(0, 0, 0, 0)
            };
            UserInterfaceManager.Add(_background);
            // If we click outside the window close it.
            _background.MouseDown += delegate (object sender, MouseEventArgs e)
                                         {
                                             if (e.Button == MouseButton.Left)
                                             {
                                                 if (_isPicking)
                                                 {
                                                     // If you want to use the user interface outside my engine you will need to change 
                                                     // only the following two lines (I presume the compiler it is telling this right now)
                                                     // Create an event in the dialog called like "PickRequested"
                                                     // In the color slider you will need to have the same event (to propagate this)
                                                     // And then you read the event and do whatever you need to do.
                                                     // The editor should read this event and not the other way around.
                                                     throw new NotImplementedException("ColorPickerDialog()");
                                                     /*EditorManager.colorPickerNeedsToPick = true;
                                                     EditorManager.colorPickerDialog = this;*/
                                                     _isPicking = false;
                                                     // The background control takes the first place (z order), now it needs to be in second.
                                                     _background.StayOnTop = false;  // We need to change this so that the main control can take first place.
                                                     BringToFront();
                                                 }
                                                 else
                                                     Close();
                                             }
                                         };



            // Button pick
            _buttonPick = new Button(UserInterfaceManager)
            {
                Top = 8,
                Glyph = new Glyph(UserInterfaceManager.Skin.Images["Dropper"].Texture) { SizeMode = SizeMode.Centered },
            };

            _buttonPick.Left = (BottomPanel.ClientWidth / 2) - _buttonPick.Width - 4;
            BottomPanel.Add(_buttonPick);
            _buttonPick.Click += delegate
            {
                //picker = new Picker();
                _isPicking = true;
                _background.StayOnTop = true;
                _background.BringToFront();
            };
            // Button close
            _buttonClose = new Button(UserInterfaceManager)
            {
                Left = (BottomPanel.ClientWidth / 2) + 4,
                Top = 8,
                Text = "Close",
                ModalResult = ModalResult.No
            };
            BottomPanel.Add(_buttonClose);
            _buttonClose.Click += delegate
            {
                Close();
            };
            DefaultControl = _buttonClose;



            // Square color
            _squareColorPalette = new Control(UserInterfaceManager)
            {
                Left = SquareColorLeft,
                Top = SquareColorTop,
                Width = SquareColorlenght,
                Height = SquareColorlenght,
                Color = new Color(0, 0, 0, 0),
                Movable = true, // To implement a roboust color picker when you can move the mouse outside the color palette limits.
            };
            Add(_squareColorPalette);



            // Intensity level bar
            _intensityLevelBar = new Control(UserInterfaceManager)
            {
                Left = 5 + SquareColorlenght,
                Top = 5,
                Width = 20,
                Height = SquareColorlenght,
                Color = new Color(0, 0, 0, 0),
                Movable = true, // To implement a roboust level picker when you can move the mouse outside the intensity level bar limits.
            };
            Add(_intensityLevelBar);



            // R
            var labelRed = new Label(UserInterfaceManager)
            {
                Parent = this,
                Text = " R",
                Width = 40,
                Top = 5 + SquareColorlenght + 50,
                Left = 5,
            };
            _textBoxRed = new TextBox(UserInterfaceManager)
            {
                Parent = this,
                Left = 5,
                Top = labelRed.Top + labelRed.Height + 2,
                Width = 40,
                Text = "1"
            };
            // G
            var labelGreen = new Label(UserInterfaceManager)
            {
                Parent = this,
                Text = " G",
                Width = 40,
                Top = 5 + SquareColorlenght + 50,
                Left = labelRed.Width + 10,
            };
            _textBoxGreen = new TextBox(UserInterfaceManager)
            {
                Parent = this,
                Left = labelRed.Width + 10,
                Top = labelRed.Top + labelRed.Height + 2,
                Width = 40,
                Text = "1"
            };
            // B
            var labelBlue = new Label(UserInterfaceManager)
            {
                Parent = this,
                Text = " B",
                Width = 40,
                Top = 5 + SquareColorlenght + 50,
                Left = labelRed.Width * 2 + 15,
            };
            _textBoxBlue = new TextBox(UserInterfaceManager)
            {
                Parent = this,
                Left = labelRed.Width * 2 + 15,
                Top = labelRed.Top + labelRed.Height + 2,
                Width = 40,
                Text = "1"
            };

            UpdateRgbFromColor();


            _background.BringToFront();
            this._oldColor = oldColor;
            Color = this._oldColor;
            _positionSquareColor = PositionFromColor(Color);


            _squareColorPalette.MouseDown += delegate { OnMouseDown(new MouseEventArgs()); };
            _squareColorPalette.MousePress += delegate { OnMousePress(new MouseEventArgs()); };
            _squareColorPalette.MouseUp += delegate { OnMouseUp(new MouseEventArgs()); };
            _intensityLevelBar.MouseDown += delegate { OnMouseDown(new MouseEventArgs()); };
            _intensityLevelBar.MousePress += delegate { OnMousePress(new MouseEventArgs()); };
            _intensityLevelBar.MouseUp += delegate { OnMouseUp(new MouseEventArgs()); };


        } // ColorPickerDialog



        protected override void DisposeManagedResources()
        {
            // A disposed object could be still generating events, because it is alive for a time, in a disposed state, but alive nevertheless.
            SquareColorDown = null;
            SquareColorUp = null;
            SquareColorPress = null;
            base.DisposeManagedResources();
        } // DisposeManagedResources



        protected internal override void Init()
        {
            base.Init();


            // When the user clicks in the square color control
            _squareColorPalette.MouseDown += delegate (object sender, MouseEventArgs e)
            {
                _updatingColorSquareAndIntensityBar = true;
                Color = ColorFromPositionWithIntensity(e.Position);
                _positionSquareColor = e.Position;
                _positionBeginningMovement = e.Position;
                _updatingColorSquareAndIntensityBar = false;
            };
            // When the user clicks and without releasing it he moves the mouse.
            _squareColorPalette.Move += delegate (object sender, MoveEventArgs e)
            {
                if (_update)
                {
                    _updatingColorSquareAndIntensityBar = true;
                    Point position = new Point(_positionBeginningMovement.X + (e.Left - SquareColorLeft), _positionBeginningMovement.Y + (e.Top - SquareColorTop));
                    if (position.X < 0)
                        position.X = 0;
                    else if (position.X > SquareColorlenght)
                        position.X = SquareColorlenght;
                    if (position.Y < 0)
                        position.Y = 0;
                    else if (position.Y >= SquareColorlenght)
                        position.Y = SquareColorlenght;
                    Color = ColorFromPositionWithIntensity(position);
                    _positionSquareColor = position;
                    _updatingColorSquareAndIntensityBar = false;
                }
            };
            _squareColorPalette.MoveEnd += delegate
            {
                _update = false;
                _squareColorPalette.Left = 5;
                _squareColorPalette.Top = 5;
                _update = true;
            };
            _squareColorPalette.KeyPress += delegate (object sender, KeyEventArgs e)
            {
                if (e.Key == Keys.Escape)
                {
                    Close();
                }
            };



            // Intensity Level
            _intensityLevelBar.MouseDown += delegate (object sender, MouseEventArgs e)
            {
                _updatingColorSquareAndIntensityBar = true;
                _intensityLevel = 1 - (e.Position.Y / (float)SquareColorlenght);
                _intensityLevelValueBeginningMovement = _intensityLevel;
                Color = ColorFromPositionWithIntensity(_positionSquareColor);
                _updatingColorSquareAndIntensityBar = false;
            };
            _intensityLevelBar.Move += delegate (object sender, MoveEventArgs e)
            {
                if (_update)
                {
                    _updatingColorSquareAndIntensityBar = true;
                    float intensity = 1 - (_intensityLevelValueBeginningMovement - (e.Top - SquareColorTop) / (float)SquareColorlenght);
                    if (intensity < 0)
                        intensity = 0;
                    else if (intensity > 1)
                        intensity = 1;
                    _intensityLevel = 1 - intensity;
                    Color = ColorFromPositionWithIntensity(_positionSquareColor);
                    _updatingColorSquareAndIntensityBar = false;
                }
            };
            _intensityLevelBar.MoveEnd += delegate
            {
                _update = false;
                _intensityLevelBar.Left = 5 + SquareColorlenght;
                _intensityLevelBar.Top = 5;
                _update = true;
            };
            _intensityLevelBar.KeyPress += delegate (object sender, KeyEventArgs e)
            {
                if (e.Key == Keys.Escape)
                {
                    Close();
                }
            };



            _textBoxRed.KeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.Key == Keys.Enter)
                {
                    if (_textBoxRed.Text.IsNumericFloat())
                    {
                        if ((float)double.Parse(_textBoxRed.Text) < 0)
                            _textBoxRed.Text = "0";
                        if ((float)double.Parse(_textBoxRed.Text) > 1)
                            _textBoxRed.Text = "1";
                        UpdateColorFromRgb();
                    }
                    else
                    {
                        UpdateRgbFromColor();
                    }
                }
            };
            // For tabs and other not so common things.
            _textBoxRed.FocusLost += delegate
            {
                if (_textBoxRed.Text.IsNumericFloat())
                {
                    if ((float)double.Parse(_textBoxRed.Text) < 0)
                        _textBoxRed.Text = "0";
                    if ((float)double.Parse(_textBoxRed.Text) > 1)
                        _textBoxRed.Text = "1";
                    UpdateColorFromRgb();
                }
                else
                {
                    UpdateRgbFromColor();
                }
            };



            _textBoxGreen.KeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.Key == Keys.Enter)
                {
                    if (_textBoxGreen.Text.IsNumericFloat())
                    {
                        if ((float)double.Parse(_textBoxGreen.Text) < 0)
                            _textBoxGreen.Text = "0";
                        if ((float)double.Parse(_textBoxGreen.Text) > 1)
                            _textBoxGreen.Text = "1";
                        UpdateColorFromRgb();
                    }
                    else
                    {
                        UpdateRgbFromColor();
                    }
                }
            };
            // For tabs and other not so common things.
            _textBoxGreen.FocusLost += delegate
            {
                if (_textBoxGreen.Text.IsNumericFloat())
                {
                    if ((float)double.Parse(_textBoxGreen.Text) < 0)
                        _textBoxGreen.Text = "0";
                    if ((float)double.Parse(_textBoxGreen.Text) > 1)
                        _textBoxGreen.Text = "1";
                    UpdateColorFromRgb();
                }
                else
                {
                    UpdateRgbFromColor();
                }
            };



            _textBoxBlue.KeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.Key == Keys.Enter)
                {
                    if (_textBoxBlue.Text.IsNumericFloat())
                    {
                        if ((float)double.Parse(_textBoxBlue.Text) < 0)
                            _textBoxBlue.Text = "0";
                        if ((float)double.Parse(_textBoxBlue.Text) > 1)
                            _textBoxBlue.Text = "1";
                        UpdateColorFromRgb();
                    }
                    else
                    {
                        UpdateRgbFromColor();
                    }
                }
            };
            // For tabs and other not so common things.
            _textBoxBlue.FocusLost += delegate
            {
                if (_textBoxBlue.Text.IsNumericFloat())
                {
                    if ((float)double.Parse(_textBoxBlue.Text) < 0)
                        _textBoxBlue.Text = "0";
                    if ((float)double.Parse(_textBoxBlue.Text) > 1)
                        _textBoxBlue.Text = "1";
                    UpdateColorFromRgb();
                }
                else
                {
                    UpdateRgbFromColor();
                }
            };


            Focused = true;

        } // Init



        protected override void DrawControl(Rectangle rect)
        {
            base.DrawControl(rect);

            throw new NotImplementedException("ColorPickerDialog.DrawControl()");
            /*LineManager.Begin2D(PrimitiveType.LineList);


            // Initial color (left top corner)
            Color color = new Color(255, 0, 0, 255);
            // Relative square position
            Vector2 position = new Vector2(5, 5);
            
            // I divide the problem into six steps.
            for (int i = 0; i < squareColorlenght; i++)
            {
                int j = i % (squareColorlenght / 6); // the position in the current step
                float porcentaje = (j / (squareColorlenght / 6f)); // The porcentaje of advance in the current step
                if (i < squareColorlenght / 6f)                    // Red to Yellow
                {
                    color.G = (byte)(255 * porcentaje);
                }
                else if (i < 2 * squareColorlenght / 6f)           // Yellow to green
                {
                    color.R = (byte)(255 - 255 * porcentaje);
                }
                else if (i < 3 * squareColorlenght / 6f)           // green to cyan
                {
                    color.B = (byte)(255 * porcentaje);
                }
                else if (i < 4 * squareColorlenght / 6f)           // cyan to blue
                {
                    color.G = (byte)(255 - 255 * porcentaje);
                }
                else if (i < 5 * squareColorlenght / 6f)           // blue to violet
                {
                    color.R = (byte)(255 * porcentaje);
                }
                else                                               // violet to red
                {
                    color.B = (byte)(255 - 255 * porcentaje);
                }
                LineManager.AddVertex(new Vector2(i + position.X, position.Y), MultiplyColorByFloat(color, intensityLevel));
                LineManager.AddVertex(new Vector2(i + position.X, position.Y + 132), MultiplyColorByFloat(new Color(255, 255, 255), intensityLevel));
            }


            // Square color pointer
            float colorPointerScale;
            if (intensityLevel < 0.6f)
                colorPointerScale = 1.0f;
            else
                colorPointerScale = 1 - intensityLevel;
            LineManager.Draw2DPlane(new Rectangle(positionSquareColor.X + 2, positionSquareColor.Y + 2, 6, 6), new Color(colorPointerScale, colorPointerScale, colorPointerScale));
            // Color planes
            LineManager.DrawSolid2DPlane(new Rectangle(5, squareColorlenght + 10, 40, 40), oldColor);
            LineManager.DrawSolid2DPlane(new Rectangle(45, squareColorlenght + 10, 40, 40), Color);
            // Intensity Level Bar
            LineManager.DrawSolid2DPlane(new Rectangle(squareColorlenght + 5, 5, 20, squareColorlenght),
                                         positionSquareColor.Y == squareColorlenght ? Color.White : ColorFromPositionWithIntensity(positionSquareColor, 1), Color.Black);

            LineManager.Draw2DPlane(new Rectangle(squareColorlenght + 5, (int)(squareColorlenght * (1 - intensityLevel)) - 3 + 5, 20, 6), new Color(200, 200, 200));

            LineManager.End();*/
        } // DrawControl



        private void UpdateColorFromRgb()
        {
            Color = new Color((float)double.Parse(_textBoxRed.Text), (float)double.Parse(_textBoxGreen.Text), (float)double.Parse(_textBoxBlue.Text));
            _positionSquareColor = PositionFromColor(Color);
        } // UpdateColorFromRGB

        private void UpdateRgbFromColor()
        {
            _textBoxRed.Text = Math.Round(Color.R / 255f, 3).ToString();
            _textBoxGreen.Text = Math.Round(Color.G / 255f, 3).ToString();
            _textBoxBlue.Text = Math.Round(Color.B / 255f, 3).ToString();
        } // UpdateRGBFromColor



        private Color ColorFromPositionWithIntensity(Point position)
        {
            return ColorFromPositionWithIntensity(position, _intensityLevel);
        } // ColorFromPositionWithIntensity

        private static Color ColorFromPositionWithIntensity(Point position, float intensityLevel)
        {
            return Color.Lerp(MultiplyColorByFloat(ColorFromPosition(position), intensityLevel), MultiplyColorByFloat(new Color(255, 255, 255), intensityLevel), position.Y / 132f);
        } // ColorFromPositionWithIntensity

        private static Color ColorFromPosition(Point position)
        {
            Color color = new Color(0, 0, 0, 255);
            // the position in the step or band (unknown for now)
            int j = position.X % (SquareColorlenght / 6);
            float porcentaje = (j / (SquareColorlenght / 6f/* - 1*/)); // The porcentaje of advance in the step
            if (position.X < SquareColorlenght / 6f)               // Red to Yellow
            {
                color.R = 255;
                color.G = (byte)(255 * porcentaje);
                color.B = 0;
            }
            else if (position.X < 2 * SquareColorlenght / 6f)      // Yellow to green
            {
                color.R = (byte)(255 - 255 * porcentaje);
                color.G = 255;
                color.B = 0;
            }
            else if (position.X < 3 * SquareColorlenght / 6f)      // green to cyan
            {
                color.R = 0;
                color.G = 255;
                color.B = (byte)(255 * porcentaje);
            }
            else if (position.X < 4 * SquareColorlenght / 6f)      // cyan to blue
            {
                color.R = 0;
                color.G = (byte)(255 - 255 * porcentaje);
                color.B = 255;
            }
            else if (position.X < 5 * SquareColorlenght / 6f)      // blue to violet
            {
                color.R = (byte)(255 * porcentaje);
                color.G = 0;
                color.B = 255;
            }
            else                                                  // violet to red
            {
                color.R = 255;
                color.G = 0;
                if (position.X == SquareColorlenght) // Last column is a special case.
                    color.B = 0;
                else
                    color.B = (byte)(255 - 255 * porcentaje);
            }
            return color;
        } // ColorFromPosition



        private Point PositionFromColor(Color color)
        {
            Point position = new Point();
            float percentage;
            // The higher color tells us the intensity level.
            // The lowest color tells us the position Y, but directly, it has to take the intensity level into consideration.
            // The middle one gives us the position X, but the range is between the lowest color to the higher one.
            if (color.R == color.G && color.R == color.B)
            {
                _intensityLevel = color.R / 255f;
                position.X = 0;
                position.Y = SquareColorlenght;
            }
            else if (color.R >= color.G && color.R >= color.B)
            {
                _intensityLevel = color.R / 255f;
                if (color.G >= color.B)                                                         // Red to Yellow
                {
                    percentage = (((color.G - color.B) / 255f) / ((color.R - color.B) / 255f));
                    position.X = (int)(percentage * (SquareColorlenght / 6));
                    position.Y = (int)(color.B / _intensityLevel / 255f * SquareColorlenght);
                }
                else                                                                            // violet to red
                {
                    percentage = (((color.B - color.G) / 255f) / ((color.R - color.G) / 255f));
                    position.X = (int)(SquareColorlenght - (percentage * (SquareColorlenght / 6f)));
                    position.Y = (int)(color.G / _intensityLevel / 255f * SquareColorlenght);
                }
            }
            else if (color.G >= color.R && color.G >= color.B)
            {
                _intensityLevel = color.G / 255f;
                if (color.R >= color.B)                                                         // Yellow to green
                {
                    percentage = (((color.R - color.B) / 255f) / ((color.G - color.B) / 255f));
                    position.X = (int)(SquareColorlenght / 3f - (percentage * (SquareColorlenght / 6f)));
                    position.Y = (int)(color.B / _intensityLevel / 255f * SquareColorlenght);
                }
                else                                                                            // green to cyan
                {
                    percentage = (((color.B - color.R) / 255f) / ((color.G - color.R) / 255f));
                    position.X = (int)(percentage * (SquareColorlenght / 6) + SquareColorlenght / 3f);
                    position.Y = (int)(color.R / _intensityLevel / 255f * SquareColorlenght);
                }
            }
            else
            {
                _intensityLevel = color.B / 255f;
                if (color.G >= color.R)                                                         // cyan to blue
                {
                    percentage = (((color.G - color.R) / 255f) / ((color.B - color.R) / 255f));
                    position.X = (int)(2f * SquareColorlenght / 3f - (percentage * (SquareColorlenght / 6f)));
                    position.Y = (int)(color.R / _intensityLevel / 255f * SquareColorlenght);
                }
                else                                                                            // blue to violet
                {
                    percentage = (((color.R - color.G) / 255f) / ((color.B - color.G) / 255f));
                    position.X = (int)(percentage * (SquareColorlenght / 6) + 2f * SquareColorlenght / 3f);
                    position.Y = (int)(color.G / _intensityLevel / 255f * SquareColorlenght);
                }
            }
            return position;
        } // PositionFromColor



        private static Color MultiplyColorByFloat(Color color, float intensityLevel)
        {
            Color result = Color.White;
            result.R = (byte)(color.R * intensityLevel);
            result.G = (byte)(color.G * intensityLevel);
            result.B = (byte)(color.B * intensityLevel);
            return result;
        } // MultiplyColorByFloat



        public override void Close()
        {
            base.Close();
            UserInterfaceManager.Remove(_background);
            _background.Dispose();
        } // Close



        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (SquareColorDown != null)
                SquareColorDown(this, e);
        } // OnMouseDown

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (SquareColorUp != null)
                SquareColorUp(this, e);
        } // OnMouseUp

        protected override void OnMousePress(MouseEventArgs e)
        {
            base.OnMousePress(e);
            if (SquareColorPress != null)
                SquareColorPress(this, e);
        } // OnMousePress

        protected override void OnKeyPress(KeyEventArgs e)
        {
            if (e.Key == Keys.Escape)
                Close();
            base.OnKeyPress(e);
        } // OnKeyPress

        protected override void OnColorChanged(EventArgs e)
        {
            UpdateRgbFromColor();
            base.OnColorChanged(e);
        } // OnColorChanged


    } // ColorPickerDialog
} // XNAFinalEngine.UserInterface