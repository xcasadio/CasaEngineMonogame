
#region License
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
#endregion

#region Using directives
using Microsoft.Xna.Framework;
using System;
using Microsoft.Xna.Framework.Input;
#endregion

namespace XNAFinalEngine.UserInterface
{

    /// <summary>
    /// Slider for numeric values.
    /// </summary>
    public class SliderNumeric : Control
    {
        
        #region Variables

        // Controls
        private readonly TextBox textBox;
        private readonly TrackBar slider;

        #endregion

        #region Properties

        /// <summary>
        /// Minimum value that can has the slider.
        /// </summary>
        public virtual float MinimumValue
        {
            get { return slider.MinimumValue; }
            set { slider.MinimumValue = value; }
        } // MinimumValue

        /// <summary>
        /// Maximum value that can has the slider.
        /// </summary>
        public virtual float MaximumValue
        {
            get { return slider.MaximumValue; }
            set { slider.MaximumValue = value; }
        } // MaximumValue

        /// <summary>
        /// If out of range rescale. In other words the maximum or minimum value changes.
        /// </summary>
        public virtual bool IfOutOfRangeRescale
        {
            get { return slider.IfOutOfRangeRescale; }
            set { slider.IfOutOfRangeRescale = value; }
        } // IfOutOfRangeRescale

        /// <summary>
        /// Indicates if the value can be out of range.
        /// </summary>
        public virtual bool ValueCanBeOutOfRange
        {
            get { return slider.ValueCanBeOutOfRange; }
            set { slider.ValueCanBeOutOfRange = value; }
        } // ValueCanBeOutOfRange

        /// <summary>
        /// Current value.
        /// </summary>
        public virtual float Value
        {
            get { return slider.Value; }
            set { slider.Value = value; }
        } // Value

        /// <summary>
        /// Page size, this value is expressed in percentages.
        /// </summary>
        public virtual int PageSize
        {
            get { return slider.PageSize; }
            set { slider.PageSize = value; }
        } // PageSize

        /// <summary>
        /// Step size, this value is expressed in percentages.
        /// </summary>
        public virtual int StepSize
        {
            get { return slider.StepSize; }
            set { slider.StepSize = value; }
        } // StepSize

        #endregion

        #region Events

        public event EventHandler ValueChanged;
        public event EventHandler RangeChanged;
        public event EventHandler StepSizeChanged;
        public event EventHandler PageSizeChanged;
        public event MouseEventHandler SliderDown;
        public event MouseEventHandler SliderUp;
        public event MouseEventHandler SliderPress;

        #endregion
        
        #region Constructor

        /// <summary>
        /// Slider for numeric values.
        /// </summary>
        public SliderNumeric(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            Anchor = Anchors.Left | Anchors.Right | Anchors.Top;
            Width = 420;
            Height = 30;
            CanFocus = false;
            Passive = true;
            var label = new Label(UserInterfaceManager)
            {
                Parent = this,
                Width = 150,
                Height = 25,
            };
            TextChanged += delegate { label.Text = Text; };
            textBox = new TextBox(UserInterfaceManager)
            {
                Parent = this,
                Width = 60,
                Left = label.Width + 4,
                Text = "1",
            };
            slider = new TrackBar(UserInterfaceManager)
            {
                Parent = this,
                Left = textBox.Left + textBox.Width + 4,
                MinimumValue = 0,
                MaximumValue = 1,
                Width = 200,
                MinimumWidth = 100,
                ValueCanBeOutOfRange = true,
                IfOutOfRangeRescale = true,
                Anchor = Anchors.Left | Anchors.Right | Anchors.Top,
            };

            #region Events

            slider.ValueChanged += delegate { textBox.Text = Math.Round(slider.Value, 3).ToString(); };
            textBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Keys.Enter)
                {
                    try
                    {
                        slider.Value = (float)double.Parse(textBox.Text);
                    }
                    catch // If not numeric
                    {
                        textBox.Text = slider.Value.ToString();
                    }
                }
            };
            // For tabs and other not so common things.
            textBox.FocusLost += delegate
            {
                try
                {
                    slider.Value = (float)double.Parse(textBox.Text);
                }
                catch // If not numeric
                {
                    textBox.Text = slider.Value.ToString();
                }
            };
            textBox.Text = Math.Round(slider.Value, 3).ToString();
            
            #endregion

            slider.ValueChanged    += OnValueChanged;
            slider.RangeChanged    += OnRangeChanged;
            slider.StepSizeChanged += OnStepSizeChanged;
            slider.PageSizeChanged += OnPageSizeChanged;
            slider.SliderDown      += OnSliderDown;
            slider.SliderUp        += OnSliderUp;
            slider.SliderPress     += OnSliderPress;

        } // SliderNumeric

        #endregion

        #region Dispose

        /// <summary>
        /// Dispose managed resources.
        /// </summary>
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

        #endregion

        #region Draw

        protected override void DrawControl(Rectangle rect)
        {
            // Only the children will be rendered.
        } // DrawControl

        #endregion

        #region On Events

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

        protected virtual void OnValueChanged(object obj, EventArgs e)
        {
            if (ValueChanged != null) 
                ValueChanged(this, e);
        } // OnValueChanged

        protected virtual void OnRangeChanged(object obj, EventArgs e)
        {
            if (RangeChanged != null) 
                RangeChanged(this, e);
        } // OnRangeChanged

        protected virtual void OnPageSizeChanged(object obj, EventArgs e)
        {
            if (PageSizeChanged != null) 
                PageSizeChanged(this, e);
        } // OnPageSizeChanged

        protected virtual void OnStepSizeChanged(object obj, EventArgs e)
        {
            if (StepSizeChanged != null) 
                StepSizeChanged(this, e);
        } // OnStepSizeChanged

        #endregion

    } // SliderNumeric
} // XNAFinalEngine.UserInterface