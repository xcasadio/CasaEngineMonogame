
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
using Microsoft.Xna.Framework;
#endregion

namespace XNAFinalEngine.UserInterface
{

    /// <summary>
    /// Scrollbar.
    /// </summary>
    public class ScrollBar : Control
    {

        #region Variables

        private int range = 100;
        private int value;
        private int pageSize = 50;
        private int stepSize = 1;
        private readonly Orientation orientation;
        // Buttons
        private readonly Button buttonMinus;
        private readonly Button buttonPlus;
        private readonly Button buttonSlider;
        // Skin information
        private readonly string skinButton = "ScrollBar.ButtonVert";
        private readonly string skinRail = "ScrollBar.RailVert";
        private readonly string skinSlider = "ScrollBar.SliderVert";
        private readonly string skinGlyph = "ScrollBar.GlyphVert";
        private readonly string skinMinus = "ScrollBar.ArrowUp";
        private readonly string skinPlus = "ScrollBar.ArrowDown";

        #endregion

        #region Properties

        /// <summary>
        /// Value.
        /// </summary>
        public virtual int Value
        {
            get { return value; }
            set
            {
                if (this.value != value)
                {
                    this.value = value;
                    if (this.value < 0) 
                        this.value = 0;
                    if (this.value > range - pageSize) 
                        this.value = range - pageSize;
                    Invalidate();
                    if (!Suspended) 
                        OnValueChanged(new EventArgs());
                }
            }
        } // Value

        /// <summary>
        /// Range.
        /// </summary>
        public virtual int Range
        {
            get { return range; }
            set
            {
                if (range != value)
                {
                    range = value;
                    if (pageSize > range) pageSize = range;
                    RecalculateParameters();
                    if (!Suspended) OnRangeChanged(new EventArgs());
                }
            }
        } // Range

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
                    if (pageSize > range) pageSize = range;
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
                    if (!Suspended) OnStepSizeChanged(new EventArgs());
                }
            }
        } // StepSize

        #endregion

        #region Events

        public event EventHandler ValueChanged;
        public event EventHandler RangeChanged;
        public event EventHandler StepSizeChanged;
        public event EventHandler PageSizeChanged;

        #endregion

        #region Constructor

        /// <summary>
        /// Scrollbar.
        /// </summary>
        /// <param name="orientation">Vertical or Horizontal</param>
        public ScrollBar(UserInterfaceManager userInterfaceManager_, Orientation orientation)
            : base(userInterfaceManager_)
        {
            this.orientation = orientation;
            CanFocus = false;
            
            if (orientation == Orientation.Horizontal)
            {
                skinButton = "ScrollBar.ButtonHorz";
                skinRail = "ScrollBar.RailHorz";
                skinSlider = "ScrollBar.SliderHorz";
                skinGlyph = "ScrollBar.GlyphHorz";
                skinMinus = "ScrollBar.ArrowLeft";
                skinPlus = "ScrollBar.ArrowRight";

                MinimumHeight = 16;
                MinimumWidth = 46;
                Width = 64;
                Height = 16;
            }
            else
            {
                skinButton = "ScrollBar.ButtonVert";
                skinRail = "ScrollBar.RailVert";
                skinSlider = "ScrollBar.SliderVert";
                skinGlyph = "ScrollBar.GlyphVert";
                skinMinus = "ScrollBar.ArrowUp";
                skinPlus = "ScrollBar.ArrowDown";

                MinimumHeight = 46;
                MinimumWidth = 16;
                Width = 16;
                Height = 64;
            }

            #region Buttons

            buttonMinus = new Button(UserInterfaceManager)
            {
                Text = "",
                CanFocus = false
            };
            buttonMinus.MousePress += ArrowPress;
            Add(buttonMinus);

            buttonSlider = new Button(UserInterfaceManager)
            {
                Text = "",
                CanFocus = false,
                MinimumHeight = 16,
                MinimumWidth = 16
            };
            buttonSlider.Move += ButtonSliderMove;
            Add(buttonSlider);

            buttonPlus = new Button(UserInterfaceManager)
            {
                Text = "",
                CanFocus = false
            };
            buttonPlus.MousePress += ArrowPress;
            Add(buttonPlus);

            #endregion

        } // ScrollBar

        #endregion

        #region Init

        protected internal override void Init()
        {
            base.Init();

            SkinControlInformation sc = new SkinControlInformation(buttonPlus.SkinInformation);
            sc.Layers["Control"] = new SkinLayer(SkinInformation.Layers[skinButton]);
            sc.Layers[skinButton].Name = "Control";
            buttonPlus.SkinInformation = buttonMinus.SkinInformation = sc;

            SkinControlInformation ss = new SkinControlInformation(buttonSlider.SkinInformation);
            ss.Layers["Control"] = new SkinLayer(SkinInformation.Layers[skinSlider]);
            ss.Layers[skinSlider].Name = "Control";
            buttonSlider.SkinInformation = ss;

            buttonMinus.Glyph = new Glyph(SkinInformation.Layers[skinMinus].Image.Texture)
            {
                SizeMode = SizeMode.Centered,
                Color = UserInterfaceManager.Skin.Controls["Button"].Layers["Control"].Text.Colors.Enabled
            };

            buttonPlus.Glyph = new Glyph(SkinInformation.Layers[skinPlus].Image.Texture)
            {
                SizeMode = SizeMode.Centered,
                Color = UserInterfaceManager.Skin.Controls["Button"].Layers["Control"].Text.Colors.Enabled
            };

            buttonSlider.Glyph = new Glyph(SkinInformation.Layers[skinGlyph].Image.Texture) { SizeMode = SizeMode.Centered };
        } // Init

        protected internal override void InitSkin()
        {
            base.InitSkin();
            SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls["ScrollBar"]);
        } // InitSkin

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
            base.DisposeManagedResources();
        } // DisposeManagedResources

        #endregion

        #region Draw

        /// <summary>
        /// Prerender the control into the control's render target.
        /// </summary>
        protected override void DrawControl(Rectangle rect)
        {
            RecalculateParameters();

            SkinLayer bg = SkinInformation.Layers[skinRail];
            UserInterfaceManager.Renderer.DrawLayer(bg, rect, Color.White, bg.States.Enabled.Index);
        } // DrawControl

        #endregion

        #region Arrow Press

        void ArrowPress(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButton.Left)
            {
                if (sender == buttonMinus)
                {
                    Value -= StepSize;
                    if (Value < 0) Value = 0;
                }
                else if (sender == buttonPlus)
                {
                    Value += StepSize;
                    if (Value > range - pageSize) Value = range - pageSize - 1;
                }
            }
        } // ArrowPress

        #endregion

        #region Recalculate Parameters

        /// <summary>
        /// Recalculate some parameters.
        /// </summary>
        private void RecalculateParameters()
        {
            if (buttonMinus != null && buttonPlus != null && buttonSlider != null)
            {
                if (orientation == Orientation.Horizontal)
                {
                    buttonMinus.Width = Height;
                    buttonMinus.Height = Height;

                    buttonPlus.Width = Height;
                    buttonPlus.Height = Height;
                    buttonPlus.Left = Width - Height;
                    buttonPlus.Top = 0;

                    buttonSlider.Movable = true;
                    int size = buttonMinus.Width + SkinInformation.Layers[skinSlider].OffsetX;

                    buttonSlider.MinimumWidth = Height;
                    int w = (Width - 2 * size);
                    buttonSlider.Width = (int)Math.Ceiling((pageSize * w) / (float)range);
                    buttonSlider.Height = Height;


                    float px = (float)(Range - PageSize) / (float)(w - buttonSlider.Width);
                    int pos = (int)(Math.Ceiling(Value / (float)px));
                    buttonSlider.SetPosition(size + pos, 0);
                    if (buttonSlider.Left < size) buttonSlider.SetPosition(size, 0);
                    if (buttonSlider.Left + buttonSlider.Width + size > Width) buttonSlider.SetPosition(Width - size - buttonSlider.Width, 0);
                }
                else
                {
                    buttonMinus.Width = Width;
                    buttonMinus.Height = Width;

                    buttonPlus.Width = Width;
                    buttonPlus.Height = Width;
                    buttonPlus.Top = Height - Width;

                    buttonSlider.Movable = true;
                    int size = buttonMinus.Height + SkinInformation.Layers[skinSlider].OffsetY;

                    buttonSlider.MinimumHeight = Width;
                    int h = (Height - 2 * size);
                    buttonSlider.Height = (int)Math.Ceiling((pageSize * h) / (float)range);
                    buttonSlider.Width = Width;

                    float px = (float)(Range - PageSize) / (float)(h - buttonSlider.Height);
                    int pos = (int)(Math.Ceiling(Value / (float)px));
                    buttonSlider.SetPosition(0, size + pos);
                    if (buttonSlider.Top < size) buttonSlider.SetPosition(0, size);
                    if (buttonSlider.Top + buttonSlider.Height + size > Height) buttonSlider.SetPosition(0, Height - size - buttonSlider.Height);
                }
            }
        } // RecalculateParameters

        #endregion
        
        #region Button Slider Move

        private void ButtonSliderMove(object sender, MoveEventArgs e)
        {
            if (orientation == Orientation.Horizontal)
            {
                int size = buttonMinus.Width + SkinInformation.Layers[skinSlider].OffsetX;
                buttonSlider.SetPosition(e.Left, 0);
                if (buttonSlider.Left < size) buttonSlider.SetPosition(size, 0);
                if (buttonSlider.Left + buttonSlider.Width + size > Width) buttonSlider.SetPosition(Width - size - buttonSlider.Width, 0);
            }
            else
            {
                int size = buttonMinus.Height + SkinInformation.Layers[skinSlider].OffsetY;
                buttonSlider.SetPosition(0, e.Top);
                if (buttonSlider.Top < size) buttonSlider.SetPosition(0, size);
                if (buttonSlider.Top + buttonSlider.Height + size > Height) buttonSlider.SetPosition(0, Height - size - buttonSlider.Height);
            }

            if (orientation == Orientation.Horizontal)
            {
                int size = buttonMinus.Width + SkinInformation.Layers[skinSlider].OffsetX;
                int w = (Width - 2 * size) - buttonSlider.Width;
                float px = (float)(Range - PageSize) / (float)w;
                Value = (int)(Math.Ceiling((buttonSlider.Left - size) * px));
            }
            else
            {
                int size = buttonMinus.Height + SkinInformation.Layers[skinSlider].OffsetY;
                int h = (Height - 2 * size) - buttonSlider.Height;
                float px = (float)(Range - PageSize) / (float)h;
                Value = (int)(Math.Ceiling((buttonSlider.Top - size) * px));
            }
        } // ButtonSliderMove

        #endregion

        #region On Mouse Up and Down

        protected override void OnMouseUp(MouseEventArgs e)
        {
            buttonSlider.Passive = false;
            base.OnMouseUp(e);
        } // OnMouseUp

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            buttonSlider.Passive = true;

            if (e.Button == MouseButton.Left)
            {
                if (orientation == Orientation.Horizontal)
                {
                    int pos = e.Position.X;

                    if (pos < buttonSlider.Left)
                    {
                        Value -= pageSize;
                        if (Value < 0) Value = 0;
                    }
                    else if (pos >= buttonSlider.Left + buttonSlider.Width)
                    {
                        Value += pageSize;
                        if (Value > range - pageSize) Value = range - pageSize;
                    }
                }
                else
                {
                    int pos = e.Position.Y;

                    if (pos < buttonSlider.Top)
                    {
                        Value -= pageSize;
                        if (Value < 0) Value = 0;
                    }
                    else if (pos >= buttonSlider.Top + buttonSlider.Height)
                    {
                        Value += pageSize;
                        if (Value > range - pageSize) Value = range - pageSize;
                    }
                }
            }
        } // OnMouseDown

        #endregion

        #region OnResize, OnValueChanged, OnRangeChanged, OnPageSizeChanged, OnStepSizeChanged

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            RecalculateParameters();
            if (Value + PageSize > Range) Value = Range - PageSize;
        } // OnResize

        protected virtual void OnValueChanged(EventArgs e)
        {
            if (ValueChanged != null) ValueChanged.Invoke(this, e);
        } // OnValueChanged

        protected virtual void OnRangeChanged(EventArgs e)
        {
            if (RangeChanged != null) RangeChanged.Invoke(this, e);
        } // OnRangeChanged

        protected virtual void OnPageSizeChanged(EventArgs e)
        {
            if (PageSizeChanged != null) PageSizeChanged.Invoke(this, e);
        } // OnPageSizeChanged

        protected virtual void OnStepSizeChanged(EventArgs e)
        {
            if (StepSizeChanged != null) StepSizeChanged.Invoke(this, e);
        } // OnStepSizeChanged

        #endregion

    } // ScrollBar
} // XNAFinalEngine.UserInterface