using CasaEngine.Framework.GUI.NoesisGUIWrapper.Helpers.DeviceState;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Noesis;
using SharpDX.Direct3D11;

namespace CasaEngine.Framework.GUI.NoesisGUIWrapper
{
    //using Input;
    using View = Noesis.View;

    public class NoesisViewWrapper
    {
        private readonly Device deviceD3D11;

        private readonly DeviceStateHelper deviceState;

        private readonly GraphicsDevice graphicsDevice;

        private readonly FrameworkElement rootElement;

        private readonly TimeSpan startupTotalGameTime;

        private uint _antiAliasingOffscreenSampleCount;

        private bool isPPAAEnabled;

        private TimeSpan lastUpdateTotalGameTime;

        private TessellationMaxPixelError quality = TessellationMaxPixelError.HighQuality;

        private RenderDeviceD3D11 renderDeviceD3D11;

        private Renderer renderer;

        private RenderFlags renderFlags;

        private View view;

        /// <summary>
        /// Create view wrapper.
        /// <param name="currentTotalGameTime">Current game time (needed to do proper Update() calls).</param>
        /// <param name="rootElement">Root UI element.</param>
        /// <param name="graphicsDevice">MonoGame Graphics Device instance.</param>
        /// </summary>
        public NoesisViewWrapper(
            FrameworkElement rootElement,
            GraphicsDevice graphicsDevice,
            TimeSpan currentTotalGameTime)
        {
            this.rootElement = rootElement;
            this.graphicsDevice = graphicsDevice;
            deviceD3D11 = (Device)this.graphicsDevice.Handle;
            deviceState = new DeviceStateHelperD3D11(deviceD3D11);
            startupTotalGameTime = lastUpdateTotalGameTime = currentTotalGameTime;

            CreateView();
        }

        public uint AntiAliasingOffscreenSampleCount
        {
            get => _antiAliasingOffscreenSampleCount;
            set
            {
                if (_antiAliasingOffscreenSampleCount == value)
                {
                    return;
                }

                _antiAliasingOffscreenSampleCount = value;
                // TODO: waiting for a fix https://www.noesisengine.com/bugs/view.php?id=1686
                // then we can use a new method to apply the change
            }
        }

        /// <summary>
        /// Gets or sets the anti-aliasing mode.
        /// </summary>
        public bool IsPPAAEnabled
        {
            get => isPPAAEnabled;
            set
            {
                if (isPPAAEnabled == value)
                {
                    return;
                }

                isPPAAEnabled = value;
                ApplyAntiAliasingSetting();
            }
        }

        /// <summary>
        /// Gets or sets the tesselation quality.
        /// </summary>
        public TessellationMaxPixelError Quality
        {
            get => quality;
            set
            {
                if (quality.Equals(value))
                {
                    return;
                }

                quality = value;
                ApplyQualitySetting();
            }
        }

        /// <summary>
        /// Gets or sets the render flags.
        /// </summary>
        public RenderFlags RenderFlags
        {
            get => renderFlags;
            set
            {
                if (renderFlags == value)
                {
                    return;
                }

                renderFlags = value;
                ApplyRenderingFlagsSetting();
            }
        }

        /// <summary>
        /// Please note - it could change if the view is recreated.
        /// </summary>
        public View View => view;

        public void ApplyAntiAliasingSetting()
        {
            var content = view?.Content;
            if (content is null)
            {
                return;
            }

            content.PPAAMode = IsPPAAEnabled
                                   ? PPAAMode.Default
                                   : PPAAMode.Disabled;
            ApplyRenderingFlagsSetting();
        }

        //public InputManager CreateInputManager(NoesisConfig config, Form form)
        //{
        //    return new(this, config, form);
        //}

        public void PreRender()
        {
            using (deviceState.Remember())
            {
                // TODO: consider not restoring device state if result was off (however we need to dispose temporary DX objects)
                renderer.RenderOffscreen();
            }
        }

        public void Render()
        {
            using (deviceState.Remember())
            {
                renderer.Render();
            }
        }

        public void SetSize(ushort width, ushort height)
        {
            view.SetSize(width, height);
            view.Update(lastUpdateTotalGameTime.TotalSeconds);
            // required in NoesisGUI 3.0, even if we don't render anything
            renderer.UpdateRenderTree();
        }

        public void Shutdown()
        {
            DestroyViewAndRenderer();
        }

        public void Update(GameTime gameTime)
        {
            lastUpdateTotalGameTime = gameTime.TotalGameTime;

            gameTime = CalculateRelativeGameTime(gameTime);
            view.Update(gameTime.TotalGameTime.TotalSeconds);
            // required in NoesisGUI 3.0, even if we don't render anything
            renderer.UpdateRenderTree();
        }

        /// <summary>
        /// Calculate game time since time of construction of this wrapper object (startup time).
        /// </summary>
        /// <param name="gameTime">MonoGame game time.</param>
        /// <returns>Time since startup of this wrapper object.</returns>
        internal GameTime CalculateRelativeGameTime(GameTime gameTime)
        {
            return new(gameTime.TotalGameTime - startupTotalGameTime,
                       gameTime.ElapsedGameTime);
        }

        private void ApplyQualitySetting()
        {
            view?.SetTessellationMaxPixelError(quality);
        }

        private void ApplyRenderingFlagsSetting()
        {
            var flags = renderFlags;
            flags |= RenderFlags.LCD;

            if (isPPAAEnabled)
            {
                flags |= RenderFlags.PPAA;
            }

            view?.SetFlags(flags);
        }

        private void CreateView()
        {
            if (view is not null)
            {
                return;
            }

            using (deviceState.Remember())
            {
                view = Noesis.GUI.CreateView(rootElement);
                renderDeviceD3D11 = new RenderDeviceD3D11(deviceD3D11.ImmediateContext.NativePointer, sRGB: false);

                // TODO: increased to deal with the glyph cache crash - refactor to move to NoesisConfig
                renderDeviceD3D11.GlyphCacheWidth = renderDeviceD3D11.GlyphCacheHeight = 2048;
                renderDeviceD3D11.OffscreenSampleCount = _antiAliasingOffscreenSampleCount;
                renderer = view.Renderer;
                renderer.Init(renderDeviceD3D11);

                ApplyQualitySetting();
                ApplyAntiAliasingSetting();
                ApplyRenderingFlagsSetting();
            }
        }

        private void DestroyViewAndRenderer()
        {
            using (deviceState.Remember())
            {
                renderer.Shutdown();
            }

            view = null;
            renderer = null;
        }
    }
}