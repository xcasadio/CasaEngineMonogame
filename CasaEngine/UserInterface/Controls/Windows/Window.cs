
/*

 Based in the project Neoforce Controls (http://neoforce.codeplex.com/)
 GNU Library General Public License (LGPL)

-----------------------------------------------------------------------------------------------------------------------------------------------
Modified by: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using Microsoft.Xna.Framework.Graphics;
using Button = CasaEngine.UserInterface.Controls.Buttons.Button;

namespace CasaEngine.UserInterface.Controls.Windows
{

    public class Window : ModalContainer
    {


        private const string SkinWindow = "Window";
        private const string LayerWindow = "Control";
        private const string LayerCaption = "Caption";
        private const string LayerFrameTop = "FrameTop";
        private const string LayerFrameLeft = "FrameLeft";
        private const string LayerFrameRight = "FrameRight";
        private const string LayerFrameBottom = "FrameBottom";
        private const string LayerIcon = "Icon";
        private const string SkinButton = "Window.CloseButton";
        private const string LayerButton = "Control";
        private const string SkinShadow = "Window.Shadow";
        private const string LayerShadow = "Control";



        private readonly Button _buttonClose;

        private bool _closeButtonVisible = true;

        private bool _iconVisible = true;

        private bool _shadow = true;

        private bool _captionVisible = true;

        private bool _borderVisible = true;

        private byte _dragAlpha = 200;
        private byte _oldAlpha = 255;



        public Texture2D Icon { get; set; }

        public virtual bool Shadow
        {
            get => _shadow;
            set => _shadow = value;
        } // Shadow

        public virtual bool CloseButtonVisible
        {
            get => _closeButtonVisible;
            set
            {
                _closeButtonVisible = value;
                if (_buttonClose != null)
                {
                    _buttonClose.Visible = value;
                }
            }
        } // CloseButtonVisible

        public virtual bool IconVisible
        {
            get => _iconVisible;
            set => _iconVisible = value;
        } // IconVisible

        public virtual bool CaptionVisible
        {
            get => _captionVisible;
            set
            {
                _captionVisible = value;
                AdjustMargins();
            }
        } // CaptionVisible

        public virtual bool BorderVisible
        {
            get => _borderVisible;
            set
            {
                _borderVisible = value;
                AdjustMargins();
            }
        } // BorderVisible

        public virtual byte DragAlpha
        {
            get => _dragAlpha;
            set => _dragAlpha = value;
        } // DragAlpha



        public Window(UserInterfaceManager userInterfaceManager)
            : base(userInterfaceManager)
        {
            SetDefaultSize(640, 480);
            SetMinimumSize(100, 75);

            _buttonClose = new Button(UserInterfaceManager)
            {
                SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls[SkinButton]),
                Detached = true,
                CanFocus = false,
                Text = null,
            };
            _buttonClose.Click += ButtonClose_Click;
            _buttonClose.SkinChanged += ButtonClose_SkinChanged;

            AdjustMargins();

            AutoScroll = true;
            Movable = true;
            Resizable = true;
            CenterWindow();

            Add(_buttonClose, false);

            _oldAlpha = Alpha;
        } // Window



        protected internal override void Init()
        {
            base.Init();
            var skinLayer = _buttonClose.SkinInformation.Layers[LayerButton];
            _buttonClose.Width = skinLayer.Width - _buttonClose.SkinInformation.OriginMargins.Horizontal;
            _buttonClose.Height = skinLayer.Height - _buttonClose.SkinInformation.OriginMargins.Vertical;
            _buttonClose.Left = ControlAndMarginsWidth - SkinInformation.OriginMargins.Right - _buttonClose.Width + skinLayer.OffsetX;
            _buttonClose.Top = SkinInformation.OriginMargins.Top + skinLayer.OffsetY;
            _buttonClose.Anchor = Anchors.Top | Anchors.Right;
        } // Init

        protected internal override void InitSkin()
        {
            base.InitSkin();
            SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls[SkinWindow]);
            AdjustMargins();

            CheckLayer(SkinInformation, LayerWindow);
            CheckLayer(SkinInformation, LayerCaption);
            CheckLayer(SkinInformation, LayerFrameTop);
            CheckLayer(SkinInformation, LayerFrameLeft);
            CheckLayer(SkinInformation, LayerFrameRight);
            CheckLayer(SkinInformation, LayerFrameBottom);
            CheckLayer(UserInterfaceManager.Skin.Controls[SkinButton], LayerButton);
            CheckLayer(UserInterfaceManager.Skin.Controls[SkinShadow], LayerShadow);
        } // InitSkin



        private void ButtonClose_SkinChanged(object sender, EventArgs e)
        {
            _buttonClose.SkinInformation = new SkinControlInformation(UserInterfaceManager.Skin.Controls[SkinButton]);
        } // ButtonClose_SkinChanged

        private void ButtonClose_Click(object sender, EventArgs e)
        {
            Close(ModalResult = ModalResult.Cancel);
        } // ButtonClose_Click



        internal override void DrawControlOntoMainTexture()
        {


            if (Visible && Shadow)
            {
                var skinControlShadow = UserInterfaceManager.Skin.Controls[SkinShadow];
                var skinLayerShadow = skinControlShadow.Layers[LayerShadow];

                var shadowColor = Color.FromNonPremultiplied(skinLayerShadow.States.Enabled.Color.R,
                                                               skinLayerShadow.States.Enabled.Color.G,
                                                               skinLayerShadow.States.Enabled.Color.B, Alpha);

                UserInterfaceManager.Renderer.Begin();
                UserInterfaceManager.Renderer.DrawLayer(skinLayerShadow,
                                       new Rectangle(Left - skinControlShadow.OriginMargins.Left,
                                                     Top - skinControlShadow.OriginMargins.Top,
                                                     Width + skinControlShadow.OriginMargins.Horizontal,
                                                     Height + skinControlShadow.OriginMargins.Vertical),
                                       shadowColor, 0);
                UserInterfaceManager.Renderer.End();
            }


            base.DrawControlOntoMainTexture();
        } // Render

        private Rectangle GetIconRectangle()
        {
            var skinLayerCaption = SkinInformation.Layers[LayerCaption];
            var skinLayerIcon = SkinInformation.Layers[LayerIcon];

            var iconHeight = skinLayerCaption.Height - skinLayerCaption.ContentMargins.Vertical;
            return new Rectangle(DrawingRectangle.Left + skinLayerCaption.ContentMargins.Left + skinLayerIcon.OffsetX,
                                 DrawingRectangle.Top + skinLayerCaption.ContentMargins.Top + skinLayerIcon.OffsetY,
                                 iconHeight, iconHeight);

        } // GetIconRectangle

        protected override void DrawControl(Rectangle rect)
        {
            var skinLayerFrameTop = _captionVisible ? SkinInformation.Layers[LayerCaption] : SkinInformation.Layers[LayerFrameTop];
            var skinLayerFrameLeft = SkinInformation.Layers[LayerFrameLeft];
            var skinLayerFrameRight = SkinInformation.Layers[LayerFrameRight];
            var skinLayerFrameBottom = SkinInformation.Layers[LayerFrameBottom];
            var skinLayerIcon = SkinInformation.Layers[LayerIcon];
            LayerStates layerStateFrameTop, layerStateFrameLeft, layerStateFrameRight, layerStateFrameButtom;
            var font = skinLayerFrameTop.Text.Font.Font;
            Color color;

            if ((Focused || (UserInterfaceManager.FocusedControl != null && UserInterfaceManager.FocusedControl.Root == Root)) && ControlState != ControlState.Disabled)
            {
                layerStateFrameTop = skinLayerFrameTop.States.Focused;
                layerStateFrameLeft = skinLayerFrameLeft.States.Focused;
                layerStateFrameRight = skinLayerFrameRight.States.Focused;
                layerStateFrameButtom = skinLayerFrameBottom.States.Focused;
                color = skinLayerFrameTop.Text.Colors.Focused;
            }
            else if (ControlState == ControlState.Disabled)
            {
                layerStateFrameTop = skinLayerFrameTop.States.Disabled;
                layerStateFrameLeft = skinLayerFrameLeft.States.Disabled;
                layerStateFrameRight = skinLayerFrameRight.States.Disabled;
                layerStateFrameButtom = skinLayerFrameBottom.States.Disabled;
                color = skinLayerFrameTop.Text.Colors.Disabled;
            }
            else
            {
                layerStateFrameTop = skinLayerFrameTop.States.Enabled;
                layerStateFrameLeft = skinLayerFrameLeft.States.Enabled;
                layerStateFrameRight = skinLayerFrameRight.States.Enabled;
                layerStateFrameButtom = skinLayerFrameBottom.States.Enabled;
                color = skinLayerFrameTop.Text.Colors.Enabled;
            }
            // Render Background plane
            UserInterfaceManager.Renderer.DrawLayer(SkinInformation.Layers[LayerWindow], rect, SkinInformation.Layers[LayerWindow].States.Enabled.Color, SkinInformation.Layers[LayerWindow].States.Enabled.Index);
            // Render border
            if (_borderVisible)
            {
                UserInterfaceManager.Renderer.DrawLayer(skinLayerFrameTop, new Rectangle(rect.Left, rect.Top, rect.Width, skinLayerFrameTop.Height), layerStateFrameTop.Color, layerStateFrameTop.Index);
                UserInterfaceManager.Renderer.DrawLayer(skinLayerFrameLeft, new Rectangle(rect.Left, rect.Top + skinLayerFrameTop.Height, skinLayerFrameLeft.Width, rect.Height - skinLayerFrameTop.Height - skinLayerFrameBottom.Height), layerStateFrameLeft.Color, layerStateFrameLeft.Index);
                UserInterfaceManager.Renderer.DrawLayer(skinLayerFrameRight, new Rectangle(rect.Right - skinLayerFrameRight.Width, rect.Top + skinLayerFrameTop.Height, skinLayerFrameRight.Width, rect.Height - skinLayerFrameTop.Height - skinLayerFrameBottom.Height), layerStateFrameRight.Color, layerStateFrameRight.Index);
                UserInterfaceManager.Renderer.DrawLayer(skinLayerFrameBottom, new Rectangle(rect.Left, rect.Bottom - skinLayerFrameBottom.Height, rect.Width, skinLayerFrameBottom.Height), layerStateFrameButtom.Color, layerStateFrameButtom.Index);

                if (_iconVisible && (Icon != null || skinLayerIcon != null) && _captionVisible)
                {
                    var i = Icon ?? skinLayerIcon.Image.Texture.Resource;
                    UserInterfaceManager.Renderer.Draw(i, GetIconRectangle(), Color.White);
                }

                var icosize = 0;
                if (skinLayerIcon != null && _iconVisible && _captionVisible)
                {
                    icosize = skinLayerFrameTop.Height - skinLayerFrameTop.ContentMargins.Vertical + 4 + skinLayerIcon.OffsetX;
                }
                var closesize = 0;
                if (_buttonClose.Visible)
                {
                    closesize = _buttonClose.Width - (_buttonClose.SkinInformation.Layers[LayerButton].OffsetX);
                }

                var r = new Rectangle(rect.Left + skinLayerFrameTop.ContentMargins.Left + icosize,
                                            rect.Top + skinLayerFrameTop.ContentMargins.Top,
                                            rect.Width - skinLayerFrameTop.ContentMargins.Horizontal - closesize - icosize,
                                            skinLayerFrameTop.Height - skinLayerFrameTop.ContentMargins.Top - skinLayerFrameTop.ContentMargins.Bottom);
                var ox = skinLayerFrameTop.Text.OffsetX;
                var oy = skinLayerFrameTop.Text.OffsetY;
                UserInterfaceManager.Renderer.DrawString(font, Text, r, color, skinLayerFrameTop.Text.Alignment, ox, oy, true);
            }
        } // DrawControl



        public virtual void CenterWindow()
        {
            Left = (UserInterfaceManager.Screen.Width / 2) - (Width / 2);
            Top = (UserInterfaceManager.Screen.Height - Height) / 2;
        } // Center



        protected override void OnResize(ResizeEventArgs e)
        {
            SetMovableArea();
            base.OnResize(e);
        } // OnResize

        protected override void OnMoveBegin(EventArgs e)
        {
            base.OnMoveBegin(e);
            _oldAlpha = Alpha;
            Alpha = _dragAlpha;
        } // OnMoveBegin

        protected override void OnMoveEnd(EventArgs e)
        {
            base.OnMoveEnd(e);
            Alpha = _oldAlpha;
        } // OnMoveEnd

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);

            var ex = (e is MouseEventArgs) ? (MouseEventArgs)e : new MouseEventArgs();

            if (IconVisible && ex.Button == MouseButton.Left)
            {
                var r = GetIconRectangle();
                r.Offset(ControlLeftAbsoluteCoordinate, ControlTopAbsoluteCoordinate);
                if (r.Contains(ex.Position))
                {
                    Close();
                }
            }
        } // OnDoubleClick



        protected override void AdjustMargins()
        {
            if (_captionVisible && _borderVisible)
            {
                ClientMargins = new Margins(SkinInformation.ClientMargins.Left, SkinInformation.Layers[LayerCaption].Height,
                                            SkinInformation.ClientMargins.Right, SkinInformation.ClientMargins.Bottom);
            }
            else if (!_captionVisible && _borderVisible)
            {
                ClientMargins = new Margins(SkinInformation.ClientMargins.Left, SkinInformation.ClientMargins.Top,
                                            SkinInformation.ClientMargins.Right, SkinInformation.ClientMargins.Bottom);
            }
            else if (!_borderVisible)
            {
                ClientMargins = new Margins(0, 0, 0, 0);
            }

            if (_buttonClose != null)
            {
                _buttonClose.Visible = _closeButtonVisible && _captionVisible && _borderVisible;
            }

            SetMovableArea();

            base.AdjustMargins();
        } // AdjustMargins



        private void SetMovableArea()
        {
            if (_captionVisible && _borderVisible)
            {
                MovableArea = new Rectangle(SkinInformation.OriginMargins.Left, SkinInformation.OriginMargins.Top, Width, SkinInformation.Layers[LayerCaption].Height - SkinInformation.OriginMargins.Top);
            }
            else if (!_captionVisible)
            {
                MovableArea = new Rectangle(0, 0, Width, Height);
            }
        } // SetMovableArea


    } // Window
} // XNAFinalEngine.UserInterface