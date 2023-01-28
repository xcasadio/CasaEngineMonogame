
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using System;


using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace XNAFinalEngine.UserInterface
{


    public enum ScaleColor
    {
        Red,
        Green,
        Blue,
        /// <summary>
        ///  Default color: Blue gray.
        /// </summary>
        Default,
    } // ScaleColor


    /// <summary>
    /// Track Bar (for sliders)
    /// </summary>
    public class TrackBar : Control
    {


        /// <summary>
        /// Current value. Ranges always between 0 to 100.
        /// </summary>
        private float internalValue;

        /// <summary>
        /// Step size, this value is expressed in percentages.
        /// </summary>
        private int stepSize = 1;

        /// <summary>
        /// /// <summary>
        /// Page size, this value is expressed in percentages.
        /// </summary>
        /// </summary>
        private int pageSize = 5;

        /// <summary>
        /// Scale bar color. Default blue.
        /// </summary>
        private ScaleColor scaleColor = ScaleColor.Default;

        /// <summary>
        /// Draw scale bar?
        /// </summary>
        private bool drawScale = true;

        /// <summary>
        /// Slider button.
        /// </summary>
        private readonly Button buttonSlider;

        /// <summary>
        /// Minimum value that can has the slider.
        /// </summary>
        private float minimumValue;

        /// <summary>
        /// Maximum value that can has the slider.
        /// </summary>
        private float maximumValue = 100;



        /// <summary>
        /// Minimum value that can has the slider.
        /// </summary>
        public virtual float MinimumValue
        {
            get { return minimumValue; }
            set
            {
                minimumValue = value;
                RecalculateParameters();
                if (!Suspended) OnRangeChanged(new EventArgs());
            }
        } // MinimumValue

        /// <summary>
        /// Maximum value that can has the slider.
        /// </summary>
        public virtual float MaximumValue
        {
            get { return maximumValue; }
            set
            {
                maximumValue = value;
                RecalculateParameters();
                if (!Suspended) OnRangeChanged(new EventArgs());
            }
        } // MaximumValue

        /// <summary>
        /// If out of range rescale. In other words the maximum or minimum value changes.
        /// </summary>
        public virtual bool IfOutOfRangeRescale { get; set; }

        /// <summary>
        /// Indicates if the value can be out of range.
        /// </summary>
        public virtual bool ValueCanBeOutOfRange { get; set; }

        private float InternalValue
        {
            get { return internalValue; }
            set
            {
                if (internalValue != value)
                {
                    internalValue = value;
                    if (!ValueCanBeOutOfRange) // Then, we need to check that the internal value is between 0 and 100.
                    {
                        if (internalValue < 0) internalValue = 0;
                        if (internalValue > 100) internalValue = 100;
                    }
                    else if (IfOutOfRangeRescale) // Is posible that we need to rescale. In other words the maximum or minimum value changes.
                    {
                        float tempCurrentRealValue = CalculateRealValue(internalValue);
                        if (internalValue < 0)
                        {
                            minimumValue = minimumValue - (2 * (minimumValue - CalculateRealValue(internalValue)));
                            internalValue = CalculateInternalValue(tempCurrentRealValue);
                        }
                        if (internalValue > 100)
                        {
                            maximumValue = maximumValue + (2 * (CalculateRealValue(internalValue) - maximumValue));
                            internalValue = CalculateInternalValue(tempCurrentRealValue);
                        }
                    }
                    Invalidate();
                    if (!Suspended)
                        OnValueChanged(new EventArgs());
                }
            }
        } // InternalValue

        /// <summary>
        /// Current value.
        /// </summary>
        public virtual float Value
        {
            get
            {
                return CalculateRealValue(internalValue);
            }
            set
            {
                InternalValue = CalculateInternalValue(value);
            }
        } // Value

        /// <summary>
        /// Page size, this value is expressed in percentages.
        /// </summary>
        public virtual int PageSize
        {
            get { return pageSize; }
            set
            {
                if (pageSize != value)
                {
                    pageSize = value;
                    if (pageSize > 100) pageSize = 100;
                    RecalculateParameters();
                    if (!Suspended) OnPageSizeChanged(new EventArgs());
                }
            }
        } // PageSize

        /// <summary>
        /// Step size, this value is expressed in percentages.
        /// </summary>
        public virtual int StepSize
        {
            get { return stepSize; }
            set
            {
                if (stepSize != value)
                {
                    stepSize = value;
                    if (stepSize > 100) stepSize = 100;
                    if (!Suspended) OnStepSizeChanged(new EventArgs());
                }
            }
        } // StepSize

        /// <summary>
        /// Scale bar color.
        /// </summary>
        public virtual ScaleColor ScaleBarColor
        {
            get { return scaleColor; }
            set { scaleColor = value; }
        } // ScaleBarColor

        /// <summary>
        /// Draw scale bar?
        /// </summary>
        public virtual bool DrawScaleBar
        {
            get { return drawScale; }
            set { drawScale = value; }
        } // DrawScale



        public event EventHandler ValueChanged;
        public event EventHandler RangeChanged;
        public event EventHandler StepSizeChanged;
        public event EventHandler PageSizeChanged;
        public event MouseEventHandler SliderDown;
        public event MouseEventHandler SliderUp;
        public event MouseEventHandler SliderPress;



        /// <summary>
        /// Track Bar (for sliders)
        /// </summary>
        public TrackBar(UserInterfaceManager userInterfaceManager_)
            : base(userInterfaceManager_)
        {
            Width = 64;
            Height = 20;
            CanFocus = false;

            buttonSlider = new Button(UserInterfaceManager)
            {
                Text = "",
                CanFocus = false,
                Parent = this,
                Anchor = Anchors.Left | Anchors.Top | Anchors.Bottom,
                Detached = true,
                Movable = true
            };

            buttonSlider.MouseDown += delegate { OnMouseDown(new MouseEventArgs()); };
            buttonSlider.MousePress += delegate { OnMousePress(new MouseEventArgs()); };
            buttonSlider.MouseUp += delegate { OnMouseUp(new MouseEventArgs()); };
        } // TrackBar



        protected internal override void Init()
        {
            base.Init();
            buttonSlider.SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["TrackBar.Button"]);
            buttonSlider.Move += ButtonSlider_Move;
            buttonSlider.KeyPress += ButtonSlider_KeyPress;
        } // Init

        protected internal override void InitSkin()
        {
            base.InitSkin();
            SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["TrackBar"]);
        } // InitSkin



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



        /// <summary>
        /// Prerender the control into the control's render target.
        /// </summary>
        protected override void DrawControl(Rectangle rect)
        {
            RecalculateParameters();

            SkinLayer p = SkinInformation.Layers["Control"];
            SkinLayer l = SkinInformation.Layers["ScaleOrange"];

            const float ratio = 0.66f;
            int h = (int)(ratio * rect.Height);
            int t = rect.Top + (Height - h) / 2;

            float px = ((float)internalValue / (float)100);
            int w = (int)Math.Ceiling(px * (rect.Width - p.ContentMargins.Horizontal - buttonSlider.Width)) + 2;

            if (w < l.SizingMargins.Vertical)
                w = l.SizingMargins.Vertical;
            if (w > rect.Width - p.ContentMargins.Horizontal)
                w = rect.Width - p.ContentMargins.Horizontal;

            // Draw control
            base.DrawControl(new Rectangle(rect.Left, t, rect.Width, h));
            // Draw progress line.
            Rectangle r1 = new Rectangle(rect.Left + p.ContentMargins.Left, t + p.ContentMargins.Top, w, h - p.ContentMargins.Vertical);
            if (drawScale)
            {
                switch (scaleColor)
                {
                    case ScaleColor.Red: UserInterfaceManager.Renderer.DrawLayer(this, SkinInformation.Layers["ScaleRed"], r1); break;
                    case ScaleColor.Green: UserInterfaceManager.Renderer.DrawLayer(this, SkinInformation.Layers["ScaleGreen"], r1); break;
                    case ScaleColor.Blue: UserInterfaceManager.Renderer.DrawLayer(this, SkinInformation.Layers["ScaleBlue"], r1); break;
                    case ScaleColor.Default: UserInterfaceManager.Renderer.DrawLayer(this, l, r1); break;
                }
            }
        } // DrawControl



        /// <summary>
        /// Calculate real value from an internal value.
        /// </summary>
        private float CalculateRealValue(float value)
        {
            return (value * (maximumValue - minimumValue) / 100f) + minimumValue;
        } // CalculateRealValue

        /// <summary>
        /// Calculate internal value from a real value.
        /// </summary>
        private float CalculateInternalValue(float value)
        {
            return (value - minimumValue) * 100f / (maximumValue - minimumValue);
        } // CalculateInternalValue



        private void ButtonSlider_Move(object sender, MoveEventArgs e)
        {
            SkinLayer p = SkinInformation.Layers["Control"];
            int size = buttonSlider.Width;
            int w = Width - p.ContentMargins.Horizontal - size;
            int pos = e.Left;

            if (pos < p.ContentMargins.Left) pos = p.ContentMargins.Left;
            if (pos > w + p.ContentMargins.Left) pos = w + p.ContentMargins.Left;

            buttonSlider.SetPosition(pos, 0);
            float px = (float)100 / (float)w;

            // Update value. But in this case the value can't be out of range.
            bool temp = ValueCanBeOutOfRange;
            ValueCanBeOutOfRange = false;
            InternalValue = (pos - p.ContentMargins.Left) * px;
            ValueCanBeOutOfRange = temp;
        } // ButtonSlider_Move

        void ButtonSlider_KeyPress(object sender, KeyEventArgs e)
        {
            if (e.Key == Keys.Left || e.Key == Keys.Down)
                InternalValue -= stepSize;
            else if (e.Key == Keys.Right || e.Key == Keys.Up)
                InternalValue += stepSize;
            else if (e.Key == Keys.PageDown)
                InternalValue -= pageSize;
            else if (e.Key == Keys.PageUp)
                InternalValue += pageSize;
            else if (e.Key == Keys.Home)
                InternalValue = 0;
            else if (e.Key == Keys.End)
                InternalValue = 100;
        } // ButtonSlider_KeyPress



        /// <summary>
        /// Recalculate some parameters, like button size, slider position, etc.
        /// </summary>
        private void RecalculateParameters()
        {
            if (buttonSlider != null)
            {
                if (buttonSlider.Width > 12)
                {
                    buttonSlider.Glyph = new Glyph(UserInterfaceManager.Skin.Images["Shared.Glyph"].Texture) { SizeMode = SizeMode.Centered };
                }
                else
                {
                    buttonSlider.Glyph = null;
                }

                SkinLayer p = SkinInformation.Layers["Control"];
                buttonSlider.Width = (int)(Height * 0.8);
                buttonSlider.Height = Height;
                int size = buttonSlider.Width;
                int w = Width - p.ContentMargins.Horizontal - size;

                float px = (float)100 / (float)w;
                int pos = p.ContentMargins.Left + (int)(Math.Ceiling(internalValue / (float)px));

                if (pos < p.ContentMargins.Left)
                    pos = p.ContentMargins.Left;
                if (pos > w + p.ContentMargins.Left)
                    pos = w + p.ContentMargins.Left;

                buttonSlider.SetPosition(pos, 0);
            }
        } // RecalculateParameters



        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (SliderDown != null)
                SliderDown(this, e);
        } // OnMouseDown

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (SliderUp != null)
                SliderUp(this, e);
        } // OnMouseUp

        protected override void OnMousePress(MouseEventArgs e)
        {
            base.OnMousePress(e);
            if (e.Button == MouseButton.Left)
                buttonSlider.Left = e.Position.X - buttonSlider.Width / 2;
            if (SliderPress != null)
                SliderPress(this, e);
        } // OnMousePress

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            RecalculateParameters();
        } // OnResize

        protected virtual void OnValueChanged(EventArgs e)
        {
            if (ValueChanged != null)
                ValueChanged.Invoke(this, e);
        } // OnValueChanged

        protected virtual void OnRangeChanged(EventArgs e)
        {
            if (RangeChanged != null)
                RangeChanged.Invoke(this, e);
        } // OnRangeChanged

        protected virtual void OnPageSizeChanged(EventArgs e)
        {
            if (PageSizeChanged != null)
                PageSizeChanged.Invoke(this, e);
        } // OnPageSizeChanged

        protected virtual void OnStepSizeChanged(EventArgs e)
        {
            if (StepSizeChanged != null)
                StepSizeChanged.Invoke(this, e);
        } // OnStepSizeChanged


    } // TrackBar
} // XNAFinalEngine.UserInterface