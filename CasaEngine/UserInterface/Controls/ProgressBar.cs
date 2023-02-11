
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/


namespace XNAFinalEngine.UserInterface
{


    public enum ProgressBarMode
    {
        Default,
        Infinite
    } // ProgressBarMode


    public class ProgressBar : Control
    {


        private int _range = 100;

        private int _value;

        private ProgressBarMode _mode = ProgressBarMode.Default;

        private double _time;

        private int _sign = 1;



        public int Value
        {
            get => _value;
            set
            {
                if (_mode == ProgressBarMode.Default)
                {
                    if (this._value != value)
                    {
                        this._value = value;
                        if (this._value > _range) this._value = _range;
                        if (this._value < 0) this._value = 0;
                        Invalidate();

                        if (!Suspended) OnValueChanged(new EventArgs());
                    }
                }
            }
        } // Value

        public ProgressBarMode Mode
        {
            get => _mode;
            set
            {
                if (_mode != value)
                {
                    _mode = value;
                    if (_mode == ProgressBarMode.Infinite)
                    {
                        _range = 100;
                        this._value = 0;
                        _time = 0;
                        _sign = 1;
                    }
                    else
                    {
                        this._value = 0;
                        _range = 100;
                    }
                    Invalidate();

                    if (!Suspended) OnModeChanged(new EventArgs());
                }
            }
        } // Mode

        public int Range
        {
            get => _range;
            set
            {
                if (_range != value)
                {
                    if (_mode == ProgressBarMode.Default)
                    {
                        _range = value;
                        if (_range < 0) _range = 0;
                        if (_range < this._value) this._value = _range;
                        Invalidate();

                        if (!Suspended) OnRangeChanged(new EventArgs());
                    }
                }
            }
        } // Range



        public event EventHandler ValueChanged;
        public event EventHandler RangeChanged;
        public event EventHandler ModeChanged;



        public ProgressBar(UserInterfaceManager userInterfaceManager)
            : base(userInterfaceManager)
        {
            Width = 128;
            Height = 16;
            MinimumHeight = 8;
            MinimumWidth = 32;
            Passive = true;
            CanFocus = false;
        } // ProgressBar



        protected override void DisposeManagedResources()
        {
            // A disposed object could be still generating events, because it is alive for a time, in a disposed state, but alive nevertheless.
            ValueChanged = null;
            RangeChanged = null;
            ModeChanged = null;
            base.DisposeManagedResources();
        } // DisposeManagedResources



        protected override void DrawControl(Rectangle rect)
        {
            CheckLayer(SkinInformation, "Control");
            CheckLayer(SkinInformation, "Scale");

            base.DrawControl(rect);

            if (Value > 0 || _mode == ProgressBarMode.Infinite)
            {
                SkinLayer p = SkinInformation.Layers["Control"];
                SkinLayer l = SkinInformation.Layers["Scale"];
                Rectangle r = new Rectangle(rect.Left + p.ContentMargins.Left,
                                            rect.Top + p.ContentMargins.Top,
                                            rect.Width - p.ContentMargins.Vertical,
                                            rect.Height - p.ContentMargins.Horizontal);

                float perc = ((float)_value / _range) * 100;
                int w = (int)((perc / 100) * r.Width);
                Rectangle rx;
                if (_mode == ProgressBarMode.Default)
                {
                    if (w < l.SizingMargins.Vertical) w = l.SizingMargins.Vertical;
                    rx = new Rectangle(r.Left, r.Top, w, r.Height);
                }
                else
                {
                    int s = r.Left + w;
                    if (s > r.Left + p.ContentMargins.Left + r.Width - (r.Width / 4)) s = r.Left + p.ContentMargins.Left + r.Width - (r.Width / 4);
                    rx = new Rectangle(s, r.Top, (r.Width / 4), r.Height);
                }

                UserInterfaceManager.Renderer.DrawLayer(this, l, rx);
            }
        } // DrawControl



        protected internal override void Update(float elapsedTime)
        {
            base.Update(elapsedTime);

            if (_mode == ProgressBarMode.Infinite && Enabled && Visible)
            {
                _time += elapsedTime; // From seconds to milliseconds.
                if (_time >= 33f)
                {
                    _value += _sign * (int)Math.Ceiling(_time / 20f);
                    if (_value >= Range - (Range / 4))
                    {
                        _value = Range - (Range / 4);
                        _sign = -1;
                    }
                    else if (_value <= 0)
                    {
                        _value = 0;
                        _sign = 1;
                    }
                    _time = 0;
                    Invalidate();
                }
            }
        } // Update



        protected virtual void OnValueChanged(EventArgs e)
        {
            if (ValueChanged != null) ValueChanged.Invoke(this, e);
        } // OnValueChanged

        protected virtual void OnRangeChanged(EventArgs e)
        {
            if (RangeChanged != null) RangeChanged.Invoke(this, e);
        } // OnRangeChanged

        protected virtual void OnModeChanged(EventArgs e)
        {
            if (ModeChanged != null) ModeChanged.Invoke(this, e);
        } // OnModeChanged


    } // ProgressBar
} // XNAFinalEngine.UserInterface