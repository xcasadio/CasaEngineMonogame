using System.Reflection;
using System.Security.Cryptography;
using CasaEngine.Framework.UI.Backend.MonoGame;
using CasaEngine.Framework.UI.Backend.MonoGame.Assets;
using FontStashSharp;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.FontStashSharp;
using MGUI.Shared.Assets;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Xunit;
using Xunit.Abstractions;

namespace CasaEngine.Tests.UI.Backend;

/// <summary>
/// Real-GPU proof that a <see cref="CasaDrawTransaction"/> that was re-armed is, for what can be observed, a new transaction built with the same
/// arguments at the same moment: the same scenario is played on a new transaction and on a re-armed one (on two runtimes, so that neither sees
/// the render target pool of the other), and each step is compared on <see cref="CasaDrawTransaction.CurrentSettings"/>,
/// <see cref="CasaDrawTransaction.PreviousSettings"/>, <see cref="CasaDrawTransaction.CurrentClipBounds"/>, <see cref="CasaDrawTransaction.ClipDiagnostics"/>
/// and the state of the <see cref="GraphicsDevice"/>, then on the pixels. All GPU work goes through <see cref="GpuDeviceHost"/>.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class DrawTransactionRearmGpuTests
{
    private const int TargetWidth = 640;
    private const int TargetHeight = 360;
    private const int WarmupIterations = 500;
    private const int MeasuredIterations = 500;

    private readonly ITestOutputHelper _output;

    public DrawTransactionRearmGpuTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private sealed class TestRenderHost : IRenderHost, IRawInputSource
    {
        private readonly HeadlessGame _game;
        private MouseState _mouse;

        public TestRenderHost(HeadlessGame game)
        {
            _game = game;
        }

        public GraphicsDevice GraphicsDevice => _game.GraphicsDevice;
        public Rectangle GetBounds() => new(0, 0, TargetWidth, TargetHeight);
        public object GetService(Type serviceType) => _game.Services.GetService(serviceType);
        public MouseState GetMouseState() => _mouse;
        public KeyboardState GetKeyboardState() => default;

        public event EventHandler<TimeSpan> PreviewUpdate;
        public event EventHandler<EventArgs> EndUpdate;

        public void RaiseFrameStart(MouseState mouse, TimeSpan elapsed)
        {
            _mouse = mouse;
            PreviewUpdate?.Invoke(this, elapsed);
        }

        public void RaiseFrameEnd() => EndUpdate?.Invoke(this, EventArgs.Empty);
    }

    private static CasaDesktopRuntime CreateRuntime()
        => CasaMonoGameBackendBootstrap.Create(new TestRenderHost(GpuDeviceHost.Instance.Game)).Runtime;

    //  What is compared at each step ---------------------------------------------------------------------------------------------------------

    /// <summary>The state of the device, with the render target named by its role (a mask target of one runtime is not the one of the other).</summary>
    private sealed record DeviceState(RasterizerState Rasterizer, BlendState Blend, DepthStencilState DepthStencil, SamplerState Sampler,
        Rectangle Scissor, Viewport Viewport, string Target);

    private sealed record Step(string Name, DrawSettings Current, DrawSettings Previous, Rectangle? ClipBounds, ClipDiagnosticsSnapshot Diagnostics,
        DeviceState Device);

    private sealed class Lab : IDisposable
    {
        private readonly RenderTarget2D _target;
        private readonly Texture2D _texture;

        public GraphicsDevice Device { get; } = GpuDeviceHost.Instance.GraphicsDevice;
        public List<Step> Steps { get; } = new();
        public IUIImageResource Image { get; }

        public Lab()
        {
            //  The contents must survive the switches of render target that a mask clip makes: the default usage discards them.
            _target = new RenderTarget2D(Device, TargetWidth, TargetHeight, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.PreserveContents);
            _texture = new Texture2D(Device, 2, 2);
            _texture.SetData(new[] { Color.Red, Color.Lime, Color.Blue, Color.Yellow });
            Image = new CasaMonoGameImageResource(_texture);
        }

        /// <summary>Puts the device in one known state, whatever the previous use of a transaction left on it, and clears the target.</summary>
        public void ResetDevice(Point viewportSize)
        {
            Device.SetRenderTarget(_target);
            Device.Viewport = new Viewport(0, 0, TargetWidth, TargetHeight);
            Device.ScissorRectangle = new Rectangle(0, 0, TargetWidth, TargetHeight);
            Device.RasterizerState = RasterizerState.CullNone;
            Device.BlendState = BlendState.AlphaBlend;
            Device.DepthStencilState = DepthStencilState.None;
            Device.SamplerStates[0] = SamplerState.PointClamp;
            Device.Clear(ClearOptions.Target | ClearOptions.Stencil | ClearOptions.DepthBuffer, Color.CornflowerBlue, 1.0f, 0);
            Device.Viewport = new Viewport(0, 0, viewportSize.X, viewportSize.Y);
            Device.ScissorRectangle = new Rectangle(0, 0, viewportSize.X, viewportSize.Y);
        }

        public void Take(string name, CasaDrawTransaction transaction)
        {
            RenderTargetBinding[] bindings = Device.GetRenderTargets();
            string target = bindings.Length == 0 ? "backbuffer"
                : ReferenceEquals(bindings[0].RenderTarget, _target) ? "main"
                : $"other {((RenderTarget2D)bindings[0].RenderTarget).Width}x{((RenderTarget2D)bindings[0].RenderTarget).Height}";
            Steps.Add(new Step(
                name,
                transaction.CurrentSettings,
                transaction.PreviousSettings,
                transaction.CurrentClipBounds,
                transaction.ClipDiagnostics,
                new DeviceState(Device.RasterizerState, Device.BlendState, Device.DepthStencilState, Device.SamplerStates[0], Device.ScissorRectangle,
                    Device.Viewport, target)));
        }

        public string ReadPixels()
        {
            Device.SetRenderTarget(null);
            Color[] pixels = new Color[TargetWidth * TargetHeight];
            _target.GetData(pixels);
            byte[] bytes = new byte[pixels.Length * 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                bytes[i * 4] = pixels[i].R;
                bytes[i * 4 + 1] = pixels[i].G;
                bytes[i * 4 + 2] = pixels[i].B;
                bytes[i * 4 + 3] = pixels[i].A;
            }

            return $"{bytes.Length}:{Convert.ToHexString(SHA256.HashData(bytes))}";
        }

        public static string Describe(DeviceState state)
            => $"rasterizer scissor={state.Rasterizer.ScissorTestEnable} fill={state.Rasterizer.FillMode}, blend src={state.Blend.ColorSourceBlend} " +
               $"dst={state.Blend.ColorDestinationBlend} write={state.Blend.ColorWriteChannels}, stencil enable={state.DepthStencil.StencilEnable} " +
               $"ref={state.DepthStencil.ReferenceStencil} func={state.DepthStencil.StencilFunction} pass={state.DepthStencil.StencilPass}, " +
               $"sampler {state.Sampler.Name}, scissor {state.Scissor}, viewport {state.Viewport}, target {state.Target}";

        public void Dispose()
        {
            Device.SetRenderTarget(null);
            _texture.Dispose();
            _target.Dispose();
        }
    }

    //  The scenario played on both transactions ---------------------------------------------------------------------------------------------

    private static readonly Rectangle ClipRectangle = new(20, 20, 200, 100);
    private static readonly Rectangle GeometryBounds = new(30, 150, 120, 80);
    private static readonly Vector2[] TexturedVertices = { new(210, 5), new(270, 5), new(270, 45), new(210, 45) };
    private static readonly Vector2[] TexturedCoordinates = { new(0, 0), new(1, 0), new(1, 1), new(0, 1) };
    private static readonly int[] TexturedIndices = { 0, 1, 2, 0, 2, 3 };

    private static ClipDefinition GeometryClip()
        => ClipDefinition.ArbitraryGeometry(
            GeometryBounds,
            new ClipGeometry(
                new List<Vector2> { new(30, 150), new(150, 150), new(30, 230) },
                new List<int> { 0, 1, 2 }));

    private static object ClipManagerOf(CasaDrawTransaction transaction)
        => typeof(CasaDrawTransaction).GetProperty("ClipManager", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(transaction);

    /// <summary>A mask clip: the resolver of the backend never picks the mask strategy for the capabilities it has, so it is pushed on the manager
    /// directly, the way the stencil and scissor strategies are.</summary>
    private static ClipHandle PushMaskClip(CasaDrawTransaction transaction)
    {
        ClipDefinition definition = GeometryClip();
        object manager = ClipManagerOf(transaction);
        MethodInfo push = manager.GetType().GetMethod("PushMask", BindingFlags.Instance | BindingFlags.NonPublic);
        return (ClipHandle)push.Invoke(manager, new object[] { new ClipResolveResult(definition, definition, ClipStrategy.Mask, true) });
    }

    private static void FillSprites(CasaDrawTransaction transaction, float x, float y, Color color)
        => transaction.FillRectangle(Vector2.Zero, new RectangleF(x, y, 60, 40), color);

    private static void FillPrimitives(CasaDrawTransaction transaction, float x, float y, Color color)
        => transaction.FillRectangle(Vector2.Zero, new RectangleF(x, y, 60, 40), color, DrawContext.Primitives);

    /// <summary>Draws in both contexts, through scissor, no-clip, stencil and mask clips, and changes the settings, recording a step after each
    /// call. Everything it opens it disposes.</summary>
    private static void PlayScenario(CasaDrawTransaction transaction, Lab lab)
    {
        lab.Take("start", transaction);
        FillSprites(transaction, 5, 5, Color.Red);
        lab.Take("sprite fill", transaction);
        FillPrimitives(transaction, 70, 5, Color.Lime);
        lab.Take("primitive fill", transaction);
        transaction.FillTriangle(Vector2.Zero, new Vector2(140, 5), Color.Yellow, new Vector2(200, 5), Color.Yellow, new Vector2(140, 45), Color.Yellow);
        lab.Take("triangle", transaction);
        //  The only draw whose shader takes the projection of the primitives from the transaction.
        transaction.DrawTexturedTriangleList(Vector2.Zero, lab.Image, TexturedVertices, TexturedCoordinates, TexturedIndices, Color.White);
        lab.Take("textured triangles", transaction);

        ClipHandle rectangle = transaction.PushRectangleClip(ClipRectangle, true);
        lab.Take("rectangle clip pushed", transaction);
        FillPrimitives(transaction, 10, 60, Color.Orange);
        FillSprites(transaction, 150, 60, Color.Cyan);
        lab.Take("filled in the rectangle clip", transaction);
        ClipHandle none = transaction.PushRectangleClip(null, false);
        lab.Take("no clip pushed", transaction);
        FillPrimitives(transaction, 250, 60, Color.Magenta);
        lab.Take("filled with no clip", transaction);
        none.Dispose();
        lab.Take("no clip disposed", transaction);
        rectangle.Dispose();
        lab.Take("rectangle clip disposed", transaction);

        transaction.SetDrawSettings(transaction.CurrentSettings with { BlendType = BlendType.Additive });
        lab.Take("settings changed", transaction);
        FillSprites(transaction, 320, 5, Color.DarkSlateBlue);
        using (transaction.SetDrawSettingsTemporary(transaction.CurrentSettings with { BlendType = BlendType.Opaque, SamplerType = SamplerType.LinearClamp }))
        {
            lab.Take("temporary settings", transaction);
            FillPrimitives(transaction, 400, 5, Color.White);
        }

        lab.Take("temporary settings disposed", transaction);
        transaction.SetDrawSettings(transaction.CurrentSettings with { BlendType = BlendType.AlphaBlend });
        lab.Take("settings changed back", transaction);

        ClipHandle stencil = transaction.PushClipTemporary(GeometryClip());
        lab.Take("stencil clip pushed", transaction);
        FillPrimitives(transaction, 40, 160, Color.Red);
        FillSprites(transaction, 60, 180, Color.Blue);
        lab.Take("filled in the stencil clip", transaction);
        stencil.Dispose();
        lab.Take("stencil clip disposed", transaction);

        ClipHandle mask = PushMaskClip(transaction);
        lab.Take("mask clip pushed", transaction);
        FillPrimitives(transaction, 40, 160, Color.Green);
        lab.Take("filled in the mask clip", transaction);
        mask.Dispose();
        lab.Take("mask clip disposed", transaction);

        transaction.EndCurrentContext();
        lab.Take("end", transaction);
    }

    /// <summary>What a case sets: how the transaction is armed the second time, and what happens to it (and to the device) in between.</summary>
    private sealed class Case
    {
        public DrawSettings Initial { get; init; } = DrawSettings.Default;
        public bool DeferBegin { get; init; } = true;
        public DrawContext DefaultContext { get; init; } = DrawContext.Sprites;
        /// <summary>The size of the viewport the scenario runs under.</summary>
        public Point ViewportSize { get; init; } = new(TargetWidth, TargetHeight);
        /// <summary>The size of the viewport of the first use, when it differs.</summary>
        public Point? PreviousViewportSize { get; init; }
        /// <summary>Arguments of the first use of the transaction (its constructor): initial settings, defer begin and context.</summary>
        public DrawSettings PreviousInitial { get; init; }
        public bool PreviousDeferBegin { get; init; } = true;
        public DrawContext PreviousDefaultContext { get; init; } = DrawContext.Sprites;
        /// <summary>The first use: runs on the transaction, which it may re-arm itself, and leaves behind what the case is about.</summary>
        public Action<CasaDrawTransaction, Lab> PreviousUse { get; init; }
    }

    /// <summary>The second use of a runtime by a new transaction: a first transaction, built with the arguments of the first use, is used and
    /// ended, then the scenario is played on a transaction built for it. The runtime keeps what the first use left in it (the render targets that
    /// its mask clips gave back to the pool, for one), the transaction does not.</summary>
    private static (List<Step> Steps, string Pixels) PlayOnNew(Case testCase)
    {
        CasaDesktopRuntime runtime = CreateRuntime();
        using Lab lab = new();
        lab.ResetDevice(testCase.PreviousViewportSize ?? testCase.ViewportSize);
        CasaDrawTransaction previous = new(runtime, testCase.PreviousInitial ?? testCase.Initial, testCase.PreviousDeferBegin, testCase.PreviousDefaultContext);
        testCase.PreviousUse?.Invoke(previous, lab);
        previous.EndCurrentContext();
        lab.Steps.Clear();

        lab.ResetDevice(testCase.ViewportSize);
        CasaDrawTransaction transaction = new(runtime, testCase.Initial, testCase.DeferBegin, testCase.DefaultContext);
        PlayScenario(transaction, lab);
        return (lab.Steps, lab.ReadPixels());
    }

    /// <summary>The same two uses of a runtime, the second one by the transaction of the first, re-armed.</summary>

    private static (List<Step> Steps, string Pixels) PlayOnRearmed(Case testCase)
    {
        CasaDesktopRuntime runtime = CreateRuntime();
        using Lab lab = new();
        lab.ResetDevice(testCase.PreviousViewportSize ?? testCase.ViewportSize);
        CasaDrawTransaction transaction = new(runtime, testCase.PreviousInitial ?? testCase.Initial, testCase.PreviousDeferBegin, testCase.PreviousDefaultContext);
        testCase.PreviousUse?.Invoke(transaction, lab);
        transaction.EndCurrentContext();
        lab.Steps.Clear();

        //  What the first use left on the device stays on it when a transaction is re-armed, as it does when a new one is built: both are
        //  played from the same device state.
        lab.ResetDevice(testCase.ViewportSize);
        transaction.Rearm(testCase.Initial, testCase.DeferBegin, testCase.DefaultContext);
        PlayScenario(transaction, lab);
        return (lab.Steps, lab.ReadPixels());
    }

    private static void AssertRearmedLikeNew(Case testCase)
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            try
            {
                (List<Step> newSteps, string newPixels) = PlayOnNew(testCase);
                (List<Step> rearmedSteps, string rearmedPixels) = PlayOnRearmed(testCase);

                Assert.Equal(newSteps.Count, rearmedSteps.Count);
                for (int i = 0; i < newSteps.Count; i++)
                {
                    Step expected = newSteps[i];
                    Step actual = rearmedSteps[i];
                    Assert.True(expected.Name == actual.Name, $"step {i}: {expected.Name} / {actual.Name}");
                    Assert.True(expected.Current.Equals(actual.Current), $"[{expected.Name}] CurrentSettings differ");
                    Assert.True(expected.Previous.Equals(actual.Previous), $"[{expected.Name}] PreviousSettings differ");
                    Assert.True(expected.ClipBounds == actual.ClipBounds, $"[{expected.Name}] CurrentClipBounds {expected.ClipBounds} / {actual.ClipBounds}");
                    Assert.True(expected.Diagnostics == actual.Diagnostics,
                        $"[{expected.Name}] ClipDiagnostics {expected.Diagnostics.ToDebugString()} / {actual.Diagnostics.ToDebugString()}");
                    Assert.True(expected.Device == actual.Device,
                        $"[{expected.Name}] device: new = {Lab.Describe(expected.Device)}; re-armed = {Lab.Describe(actual.Device)}");
                }

                Assert.Equal(newPixels, rearmedPixels);

                //  A scenario that drew nothing would prove nothing.
                Assert.NotEqual($"{TargetWidth * TargetHeight * 4}:{ClearedPixels()}", newPixels);
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });
    }

    private static string ClearedPixels()
    {
        using Lab lab = new();
        lab.ResetDevice(new Point(TargetWidth, TargetHeight));
        return lab.ReadPixels().Split(':')[1];
    }

    private static DrawSettings SettingsWith(Func<DrawSettings, DrawSettings> change) => change(DrawSettings.Default);

    //  The cases -----------------------------------------------------------------------------------------------------------------------------

    [GpuFact]
    public void Rearm_AfterAUseThatEndedCleanly_IsLikeANewTransaction()
        => AssertRearmedLikeNew(new Case
        {
            PreviousUse = (transaction, lab) => PlayScenario(transaction, lab),
        });

    [GpuFact]
    public void Rearm_AfterAUseThatLeftChangedSettings_IsLikeANewTransaction()
        => AssertRearmedLikeNew(new Case
        {
            PreviousUse = (transaction, lab) =>
            {
                transaction.SetDrawSettings(transaction.CurrentSettings with
                {
                    BlendType = BlendType.Additive,
                    SamplerType = SamplerType.LinearWrap,
                    Transform = Matrix.CreateTranslation(7, 9, 0),
                    RasterizerType = RasterizerType.Solid,
                });
                FillSprites(transaction, 100, 100, Color.Red);
                transaction.SetDrawSettings(transaction.CurrentSettings with { BlendType = BlendType.Opaque });
                FillPrimitives(transaction, 100, 100, Color.Red);
                Assert.NotEqual(DrawSettings.Default, transaction.PreviousSettings);
            },
        });

    [GpuFact]
    public void Rearm_AfterAnUndisposedRectangleClip_IsLikeANewTransaction()
        => AssertRearmedLikeNew(new Case
        {
            PreviousUse = (transaction, lab) =>
            {
                ClipHandle left = transaction.PushRectangleClip(new Rectangle(50, 50, 40, 40), true);
                FillSprites(transaction, 55, 55, Color.Red);
                Assert.Equal(new Rectangle(50, 50, 40, 40), transaction.CurrentClipBounds);
                GC.KeepAlive(left);
            },
        });

    [GpuFact]
    public void Rearm_AfterAnUndisposedNoClip_IsLikeANewTransaction()
        => AssertRearmedLikeNew(new Case
        {
            PreviousUse = (transaction, lab) =>
            {
                ClipHandle left = transaction.PushRectangleClip(null, false);
                FillSprites(transaction, 55, 55, Color.Red);
                Assert.False(transaction.CurrentSettings.UsesScissorTest);
                GC.KeepAlive(left);
            },
        });

    [GpuFact]
    public void Rearm_AfterAnUndisposedStencilClip_IsLikeANewTransaction()
        => AssertRearmedLikeNew(new Case
        {
            PreviousUse = (transaction, lab) =>
            {
                ClipHandle left = transaction.PushClipTemporary(GeometryClip());
                FillPrimitives(transaction, 40, 160, Color.Red);
                Assert.Equal(1, transaction.ClipDiagnostics.MaxStencilDepth);
                GC.KeepAlive(left);
            },
        });

    [GpuFact]
    public void Rearm_AfterAnUndisposedMaskClip_IsLikeANewTransaction()
        => AssertRearmedLikeNew(new Case
        {
            PreviousUse = (transaction, lab) =>
            {
                ClipHandle left = PushMaskClip(transaction);
                FillPrimitives(transaction, 40, 160, Color.Red);
                Assert.Equal(1, transaction.ClipDiagnostics.MaskClipCount);
                Assert.Equal(1, transaction.ClipDiagnostics.TemporaryRenderTargetRentCount);
                //  The clip is open: the device draws to the render target that it rented, not to the target of the lab.
                Assert.Equal(GeometryBounds.Width, ((RenderTarget2D)lab.Device.GetRenderTargets()[0].RenderTarget).Width);
                GC.KeepAlive(left);
            },
        });

    /// <summary>The render target that a mask clip gives back belongs to the pool of the runtime, not to the transaction: a re-arm resets what the
    /// diagnostics count and leaves the pool alone, so the next mask clip is a rent that reuses.</summary>
    [GpuFact]
    public void Rearm_AfterADisposedMaskClip_CountsFromZeroAndReusesThePooledTarget()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            try
            {
                CasaDesktopRuntime runtime = CreateRuntime();
                using Lab lab = new();
                lab.ResetDevice(new Point(TargetWidth, TargetHeight));
                CasaDrawTransaction transaction = new(runtime, DrawSettings.Default, true);

                PushMaskClip(transaction).Dispose();
                transaction.EndCurrentContext();
                Assert.Equal(new ClipDiagnosticsSnapshot(0, 0, 1, 0, 1, 0), transaction.ClipDiagnostics);

                lab.ResetDevice(new Point(TargetWidth, TargetHeight));
                transaction.Rearm(DrawSettings.Default, true);
                Assert.Equal(new ClipDiagnosticsSnapshot(0, 0, 0, 0, 0, 0), transaction.ClipDiagnostics);

                PushMaskClip(transaction).Dispose();
                transaction.EndCurrentContext();
                Assert.Equal(new ClipDiagnosticsSnapshot(0, 0, 1, 0, 1, 1), transaction.ClipDiagnostics);
            }
            finally
            {
                GpuDeviceHost.Instance.GraphicsDevice.SetRenderTarget(null);
            }
        });
    }

    /// <summary>The settings that a clip that turns the scissor test off hands out are kept by the transaction across a re-arm: settings hold
    /// nothing but their parameters (MGUI ADR-0029), so the kept ones behave as the ones a new transaction would create.</summary>
    [GpuFact]
    public void Rearm_AfterRasterizerVariantsWereCreated_IsLikeANewTransaction()
    {
        DrawSettings initial = DrawSettings.Default;
        AssertRearmedLikeNew(new Case
        {
            Initial = initial,
            PreviousUse = (transaction, lab) =>
            {
                using (transaction.PushRectangleClip(null, false))
                {
                    Assert.False(transaction.CurrentSettings.UsesScissorTest);
                    Assert.NotSame(initial, transaction.CurrentSettings);
                }
            },
        });
    }

    /// <summary>The pair is kept for the settings a use starts with only: a use that starts with other settings drops it, so the transaction
    /// does not keep settings (and their effect) that it can no longer be asked for.</summary>
    [GpuFact]
    public void Rearm_WithOtherInitialSettings_DropsTheRasterizerVariants()
    {
        DrawSettings initial = DrawSettings.Default;
        DrawSettings other = DrawSettings.Default with { BlendType = BlendType.Additive };
        GpuDeviceHost.Instance.Invoke(() =>
        {
            try
            {
                CasaDesktopRuntime runtime = CreateRuntime();
                using Lab lab = new();
                lab.ResetDevice(new Point(TargetWidth, TargetHeight));
                CasaDrawTransaction transaction = new(runtime, initial, true);
                DrawSettings firstNoClipSettings;
                using (transaction.PushRectangleClip(null, false))
                {
                    firstNoClipSettings = transaction.CurrentSettings;
                }

                transaction.EndCurrentContext();
                transaction.Rearm(other, true);
                transaction.EndCurrentContext();
                transaction.Rearm(initial, true);
                using (transaction.PushRectangleClip(null, false))
                {
                    Assert.NotSame(firstNoClipSettings, transaction.CurrentSettings);
                    Assert.Equal(firstNoClipSettings, transaction.CurrentSettings);
                }

                transaction.EndCurrentContext();
            }
            finally
            {
                GpuDeviceHost.Instance.GraphicsDevice.SetRenderTarget(null);
            }
        });
    }

    [GpuFact]
    public void Rearm_KeepsTheRasterizerVariantsAndTheDefaultPreviousSettings()
    {
        DrawSettings initial = DrawSettings.Default;
        GpuDeviceHost.Instance.Invoke(() =>
        {
            try
            {
                CasaDesktopRuntime runtime = CreateRuntime();
                using Lab lab = new();
                lab.ResetDevice(new Point(TargetWidth, TargetHeight));
                CasaDrawTransaction transaction = new(runtime, initial, true);
                DrawSettings previousDefault = null;
                DrawSettings noClipSettings = null;
                for (int use = 0; use < 4; use++)
                {
                    //  The first use is the one of the constructor: it creates the variants, and every re-armed use finds them.
                    if (use > 0)
                    {
                        transaction.Rearm(initial, true);
                    }

                    Assert.Same(initial, transaction.CurrentSettings);
                    Assert.True(DrawSettings.Default.Equals(transaction.PreviousSettings));
                    if (use == 0)
                    {
                        previousDefault = transaction.PreviousSettings;
                    }
                    else
                    {
                        Assert.Same(previousDefault, transaction.PreviousSettings);
                    }

                    using (transaction.PushRectangleClip(null, false))
                    {
                        if (use == 0)
                        {
                            noClipSettings = transaction.CurrentSettings;
                        }
                        else
                        {
                            Assert.Same(noClipSettings, transaction.CurrentSettings);
                        }
                    }

                    transaction.EndCurrentContext();
                    Assert.Same(initial, transaction.CurrentSettings);
                }
            }
            finally
            {
                GpuDeviceHost.Instance.GraphicsDevice.SetRenderTarget(null);
            }
        });
    }

    [GpuFact]
    public void Rearm_WithADifferentViewportSize_FollowsThePrimitiveProjection()
        => AssertRearmedLikeNew(new Case
        {
            PreviousViewportSize = new Point(TargetWidth, TargetHeight),
            ViewportSize = new Point(TargetWidth / 2, TargetHeight / 2),
            PreviousUse = (transaction, lab) => PlayScenario(transaction, lab),
        });

    [GpuFact]
    public void Rearm_WithALargerViewportSize_FollowsThePrimitiveProjection()
        => AssertRearmedLikeNew(new Case
        {
            PreviousViewportSize = new Point(TargetWidth / 2, TargetHeight / 2),
            ViewportSize = new Point(TargetWidth, TargetHeight),
            PreviousUse = (transaction, lab) => PlayScenario(transaction, lab),
        });

    [GpuFact]
    public void Rearm_ThatStartsTheBatch_AfterAUseThatDeferredIt_IsLikeANewTransaction()
        => AssertRearmedLikeNew(new Case
        {
            DeferBegin = false,
            PreviousDeferBegin = true,
            PreviousUse = (transaction, lab) => FillSprites(transaction, 100, 100, Color.Red),
        });

    [GpuFact]
    public void Rearm_ThatDefersTheBatch_AfterAUseThatStartedIt_IsLikeANewTransaction()
        => AssertRearmedLikeNew(new Case
        {
            DeferBegin = true,
            PreviousDeferBegin = false,
            PreviousDefaultContext = DrawContext.Primitives,
            PreviousUse = (transaction, lab) => FillPrimitives(transaction, 100, 100, Color.Red),
        });

    /// <summary>A batch that stopped without the transaction knowing (the disposal of a transaction whose <c>End</c> threw leaves it so) is not
    /// in the way of the next use: the re-armed transaction has no batch open, and begins its own.</summary>
    [GpuFact]
    public void Rearm_AfterABatchThatEndedWithoutTheTransaction_BeginsItsOwn()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            try
            {
                CasaDesktopRuntime runtime = CreateRuntime();
                using Lab lab = new();
                lab.ResetDevice(new Point(TargetWidth, TargetHeight));
                CasaDrawTransaction transaction = new(runtime, DrawSettings.Default, true);
                FillSprites(transaction, 5, 5, Color.Red);
                runtime.SpriteBatch.End();

                transaction.Rearm(DrawSettings.Default, false);
                FillSprites(transaction, 5, 5, Color.Red);
                transaction.EndCurrentContext();

                transaction.Rearm(DrawSettings.Default, true);
                FillPrimitives(transaction, 5, 5, Color.Red);
                transaction.EndCurrentContext();
            }
            finally
            {
                GpuDeviceHost.Instance.GraphicsDevice.SetRenderTarget(null);
            }
        });
    }

    [GpuFact]
    public void Rearm_ThatStartsThePrimitiveBatch_IsLikeANewTransaction()
        => AssertRearmedLikeNew(new Case
        {
            DeferBegin = false,
            DefaultContext = DrawContext.Primitives,
            PreviousDeferBegin = false,
            PreviousUse = (transaction, lab) => FillSprites(transaction, 100, 100, Color.Red),
        });

    [GpuFact]
    public void Rearm_WithDifferentInitialSettings_IsLikeANewTransaction()
        => AssertRearmedLikeNew(new Case
        {
            Initial = SettingsWith(settings => settings with
            {
                BlendType = BlendType.Opaque,
                RasterizerType = RasterizerType.Solid,
                SamplerType = SamplerType.LinearClamp,
                Transform = Matrix.CreateTranslation(11, 13, 0),
            }),
            PreviousInitial = SettingsWith(settings => settings with { BlendType = BlendType.Additive, SamplerType = SamplerType.PointWrap }),
            PreviousUse = (transaction, lab) => PlayScenario(transaction, lab),
        });

    [GpuFact]
    public void Rearm_WithNullSettings_ThrowsAndLeavesTheTransactionAsItWas()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            DrawSettings initial = DrawSettings.Default;
            CasaDrawTransaction transaction = new(CreateRuntime(), initial, true);
            Assert.Throws<ArgumentNullException>(() => transaction.Rearm(null, false));
            Assert.Same(initial, transaction.CurrentSettings);
            Assert.Equal(DrawSettings.Default, transaction.PreviousSettings);
        });
    }

    //  The desktop frame ---------------------------------------------------------------------------------------------------------------------

    /// <summary>The fixture of <c>DesktopFrameGpuAllocationTests</c> (default window, stack panel, text block, slider; FontStashSharp), with one
    /// transaction that is re-armed for each frame instead of being kept as it is.</summary>
    private sealed class Harness
    {
        private readonly TestRenderHost _host;
        private readonly DrawSettings _settings = DrawSettings.Default;
        private int _elapsedMilliseconds;

        public MGDesktop Desktop { get; }
        public CasaDrawTransaction Transaction { get; }

        public Harness()
        {
            _host = new TestRenderHost(GpuDeviceHost.Instance.Game);
            CasaDesktopRuntime runtime = CasaMonoGameBackendBootstrap.Create(_host).Runtime;
            Desktop = new MGDesktop(runtime);

            byte[] ttfData = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Content", "Fonts", "ttf", "arial.ttf"));
            FontStashSharpTextEngine engine = new();
            FontSystem fontSystem = new();
            fontSystem.AddFont(ttfData);
            engine.AddFontSystem(runtime.DefaultFontFamily, CustomFontStyles.Normal, fontSystem, ttfData);
            engine.MatchSpriteFontSizing(runtime.FontManager);
            Desktop.TextEngine = engine;

            MGWindow window = new(Desktop, 0, 0, 300, 200);
            Desktop.Windows.Add(window);
            MGStackPanel panel = new(window, Orientation.Vertical);
            panel.TryAddChild(new MGTextBlock(window, "Value"));
            MGSlider slider = new(window, 0, 1, 0.5f) { ShowValueLabel = true, ValueLabelFormat = "F0" };
            panel.TryAddChild(slider);
            window.SetContent(panel);

            Transaction = new CasaDrawTransaction(runtime, _settings, true);
            Frame();
            Frame();
        }

        public void Update()
        {
            MouseState mouse = new(600, 340, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _host.RaiseFrameStart(mouse, TimeSpan.FromMilliseconds(_elapsedMilliseconds));
            Desktop.Update();
            _host.RaiseFrameEnd();
            _elapsedMilliseconds += 16;
        }

        /// <summary>Re-arms the transaction, draws the desktop with it, and ends its batch (what the disposal of a per-frame transaction does).</summary>
        public void Draw()
        {
            Transaction.Rearm(_settings, true);
            Desktop.Draw(Transaction);
            Transaction.EndCurrentContext();
        }

        public void Frame()
        {
            Update();
            Draw();
        }
    }

    private static long StartWindow()
    {
        GC.Collect(0, GCCollectionMode.Forced, blocking: true);
        return GC.GetAllocatedBytesForCurrentThread();
    }

    /// <summary>A clip that turns the scissor test off makes the transaction look for the settings it keeps for that: a re-arm that keeps them
    /// (while they are as they were created) leaves that clip as free as the others.</summary>
    [GpuFact]
    public void RearmThenAClipThatTurnsTheScissorOff_AllocatesNothing()
    {
        long bytes = GpuDeviceHost.Instance.Invoke(() =>
        {
            try
            {
                CasaDesktopRuntime runtime = CreateRuntime();
                using Lab lab = new();
                lab.ResetDevice(new Point(TargetWidth, TargetHeight));
                DrawSettings initial = DrawSettings.Default;
                CasaDrawTransaction transaction = new(runtime, initial, true);

                void Frame()
                {
                    transaction.Rearm(initial, true);
                    using (transaction.PushRectangleClip(ClipRectangle, true))
                    {
                        FillSprites(transaction, 25, 25, Color.Red);
                        using (transaction.PushRectangleClip(null, false))
                        {
                            FillSprites(transaction, 5, 5, Color.Lime);
                        }
                    }

                    transaction.EndCurrentContext();
                }

                for (int i = 0; i < WarmupIterations; i++)
                {
                    Frame();
                }

                long before = StartWindow();
                for (int i = 0; i < MeasuredIterations; i++)
                {
                    Frame();
                }

                return GC.GetAllocatedBytesForCurrentThread() - before;
            }
            finally
            {
                GpuDeviceHost.Instance.GraphicsDevice.SetRenderTarget(null);
            }
        });

        _output.WriteLine($"re-arm + no-clip frame: {bytes} bytes over {MeasuredIterations} frames");
        Assert.Equal(0, bytes);
    }

    [GpuFact]
    public void RearmThenDrawingTheDesktopFrame_AllocatesNothing_AndDrawsTheReferencePixels()
    {
        (long bytes, string pixels, int clips) = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            try
            {
                using Lab lab = new();
                lab.ResetDevice(new Point(TargetWidth, TargetHeight));
                Harness harness = new();

                for (int i = 0; i < WarmupIterations; i++)
                {
                    harness.Draw();
                }

                long before = StartWindow();
                for (int i = 0; i < MeasuredIterations; i++)
                {
                    harness.Draw();
                }

                long after = GC.GetAllocatedBytesForCurrentThread();
                //  A re-arm sets the diagnostics to zero: what they count is the last frame.
                int clipsDrawn = harness.Transaction.ClipDiagnostics.ScissorClipCount;

                lab.ResetDevice(new Point(TargetWidth, TargetHeight));
                harness.Draw();
                return (after - before, lab.ReadPixels(), clipsDrawn);
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });

        _output.WriteLine($"re-arm + Draw: {bytes} bytes over {MeasuredIterations} frames");
        //  The diagnostics are those of the last frame only: it pushed clips, so the draw did something.
        Assert.True(clips > 0);
        Assert.Equal(0, bytes);
        ReferenceDesktopFramePixels.AssertRest(pixels);
    }
}
