using CasaEngine.Framework.Rendering.Shaders;
using CasaEngine.Framework.UI.Backend.MonoGame.Clipping;
using CasaEngine.Framework.UI.Backend.MonoGame.Primitives;
using MGUI.Backend.MonoGame;
using MGUI.Shared.Assets;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.VectorDraw;
using WindingOrder = MonoGame.Extended.Triangulation.WindingOrder;

namespace CasaEngine.Framework.UI.Backend.MonoGame;
//TODO: Maybe a 'SetEffect'/'SetEffectTemporary'? Could put the Effect in DrawSettings, default value = null
//Anytime the effect is being changed, must call SetDrawSettings/SetDrawSettingsTemporary just like SetTransform/SetTransformTemporary do
// --> SetEffect/SetEffectTemporary are now implemented via DrawSettings.Effect; see SetEffect() and SetEffectTemporary() below.

public class CasaDrawTransaction : IMonoGameDrawContext
{
    public CasaDesktopRuntime Renderer { get; }
    IUIDesktopRuntime IUIRenderContext.Renderer => Renderer;
    Rectangle? IUIRenderContext.CurrentClipBounds => CurrentClipBounds;
    IDisposable IUIRenderContext.SetDrawSettingsTemporary(DrawSettings Settings)
        => SetDrawSettingsTemporary(Settings);
    IDisposable IUIRenderContext.SetRenderTargetTemporary(IUIRenderTarget New, Color? ClearColor)
        => SetRenderTargetTemporary(New, ClearColor);
    void IUIDrawContext.FillRectangle(Vector2 Origin, RectangleF Destination, Color Color)
        => FillRectangle(Origin, Destination, Color, null);
    void IUIDrawContext.FillPoint(Vector2 Center, Color Color, float Width)
        => FillPoint(Center, Color, Width, PointShape.Circle, null);
    void IUIDrawContext.StrokeRectangle(Vector2 Origin, RectangleF Destination, Color Color, Thickness Thickness)
        => StrokeRectangle(Origin, Destination, Color, Thickness, null);
    void IUIDrawContext.StrokeAndFillRectangle(Vector2 Origin, RectangleF Destination, Color StrokeColor, Color FillColor, Thickness StrokeThickness)
        => StrokeAndFillRectangle(Origin, Destination, StrokeColor, FillColor, StrokeThickness, null);
    void IUIDrawContext.StrokeAndFillPolygon(Vector2 Origin, IEnumerable<Vector2> Vertices, Color StrokeColor, Color FillColor, float StrokeThickness)
        => StrokeAndFillPolygon(Origin, Vertices as IReadOnlyList<Vector2> ?? Vertices.ToList(), StrokeColor, FillColor, StrokeThickness);
    void IUIDrawContext.StrokeLineSegment(Vector2 Origin, Vector2 Start, Vector2 End, Color Color, float Thickness)
        => StrokeLineSegment(Origin, Start, End, Color, Thickness, null);
    void IUIDrawContext.FillCircle(Vector2 Center, Color Color, float Radius, int NumSides)
        => FillCircle(Center, Color, Radius, NumSides, null);
    void IUIDrawContext.StrokeCircle(Vector2 Center, Color Color, float Radius, float Thickness, int NumSides)
        => StrokeCircle(Center, Color, Radius, Thickness, NumSides, null);
    void IUIDrawContext.StrokeAndFillCircle(Vector2 Center, Color StrokeColor, Color FillColor, float Radius, float StrokeThickness, int NumSides)
        => StrokeAndFillCircle(Center, StrokeColor, FillColor, Radius, StrokeThickness, NumSides, null);
    /// <summary>Delegates to <see cref="CasaDesktopRuntime.TextEngine"/>.</summary>
    public ITextMeasurementEngine TextEngine => Renderer.TextEngine;
    private ITextDrawEngine TextRenderer => Renderer.GetTextRenderer();
    public GraphicsDevice GraphicsDevice => Renderer.GraphicsDevice;
    SpriteBatch IMonoGameDrawContext.SpriteBatch => Renderer.SpriteBatch;
    public SpriteBatch SpriteBatch => Renderer.SpriteBatch;
    private PrimitiveBatch PrimitiveBatch => Renderer.PrimitiveBatch;
    public PrimitiveDrawing PrimitiveDrawing { get; }

    public SolidColorTexture BlackPixel => Renderer.GetOrCreateSolidColorTexture(Color.Black);
    /// <summary>A solid white, 1 pixel wide/tall Texture. Useful for drawing Colored squares. 
    /// (Color can be specified in the 'Color' mask parameter of SpriteBatch.Draw(...))</summary>
    public SolidColorTexture WhitePixel => Renderer.GetOrCreateSolidColorTexture(Color.White);

    private Matrix PrimitiveProjectionMatrix { get; }
    private CasaDrawStateController DrawState { get; }
    private CasaRenderTargetService RenderTargets { get; }
    private IShapeRenderer2D ShapeRenderer { get; }
    private CasaClipManager ClipManager { get; }
    public Rectangle? CurrentClipBounds => CurrentSettings.UsesScissorTest ? GraphicsDevice.ScissorRectangle : null;
    private RasterizerState CurrentRasterizerState => DrawState.CurrentRasterizerState;
    private BlendState CurrentBlendState => DrawState.CurrentBlendState;
    private SamplerState CurrentSamplerState => DrawState.CurrentSamplerState;
    private DepthStencilState CurrentDepthStencilState => DrawState.CurrentDepthStencilState;
    private DrawContext CurrentContext => DrawState.CurrentContext;

    public DrawSettings CurrentSettings => DrawState.CurrentSettings;
    public DrawSettings PreviousSettings => DrawState.PreviousSettings;
    public ClipDiagnosticsSnapshot ClipDiagnostics => ClipManager.GetDiagnostics();

    /// <param name="Settings">See also: <see cref="DrawSettings.Default"/></param>
    /// <param name="DeferBegin">If true, <see cref="SpriteBatch.Begin(SpriteSortMode, BlendState, SamplerState, DepthStencilState, RasterizerState, Effect, Matrix?)"/> or <see cref="PrimitiveBatch.Begin(ref Matrix, ref Matrix)"/><para/>
    /// will not be invoked until you call a draw-related function within <see cref="CasaDrawTransaction"/>, such as <see cref="DrawTextureTo(Texture2D, Rectangle?, Rectangle)"/></param>
    /// <param name="DefaultContext">Only relevant if <paramref name="DeferBegin"/>==false. The default drawing context to immediately start.</param>
    public CasaDrawTransaction(CasaDesktopRuntime Renderer, DrawSettings Settings, bool DeferBegin, DrawContext DefaultContext = DrawContext.Sprites)
    {
        this.Renderer = Renderer ?? throw new ArgumentNullException(nameof(Renderer));
        PrimitiveDrawing = new PrimitiveDrawing(PrimitiveBatch);

        PrimitiveProjectionMatrix = Matrix.CreateOrthographicOffCenter(0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, 0, 0, 1);
        DrawState = new CasaDrawStateController(Renderer, Settings ?? throw new ArgumentNullException(nameof(Settings)), PrimitiveProjectionMatrix);
        RenderTargets = new CasaRenderTargetService(this);
        ShapeRenderer = Renderer.Options.CreateShapeRenderer?.Invoke(this) ?? new CasaLegacyShapeRenderer2D(this);
        ClipManager = new(this);

        if (!DeferBegin)
        {
            BeginDraw(DefaultContext);
        }
    }

    /// <summary>Should only be invoked if you intend to make a draw-related call that isn't already funneled through <see cref="CasaDrawTransaction"/>.<para/>
    /// Draw-related calls that are funneled through <see cref="CasaDrawTransaction"/> (such as <see cref="CasaDrawTransaction.StrokeRectangle(Vector2, RectangleF, Color, Thickness, DrawContext?)"/>) will already call the begin methods if necessary.</summary>
    public void ForceBeginDraw(DrawContext Context) => BeginDraw(Context);
    public void ForceEndDraw(DrawContext Context) => EndDraw(Context);
    internal void EndCurrentContext() => EndDraw(CurrentContext);
    internal void EnsureDrawContext(DrawContext context) => BeginDraw(context);
    internal DrawContext ResolveDrawContext(DrawContext? preferredContext, DrawContext fallback)
        => GetFirstValidDrawContext(preferredContext, CurrentContext, fallback);
    internal PrimitiveBatch PrimitiveBatchInternal => PrimitiveBatch;

    private void BeginDraw(DrawContext Context)
        => DrawState.Begin(Context);

    private void EndDraw(DrawContext Context)
        => DrawState.End(Context);

    #region Draw
    #region Draw Texture
    /// <summary>Draw a texture to a given <paramref name="Destination"/> <see cref="Rectangle"/></summary>
    public void DrawTextureTo(Texture2D Texture, Rectangle? Source, Rectangle Destination)
    {
        DrawTextureTo(Texture, Source, Destination, Color.White);
    }

    /// <summary>Draw a texture to a given <paramref name="Destination"/> <see cref="Rectangle"/></summary>
    public void DrawTextureTo(Texture2D Texture, Rectangle? Source, Rectangle Destination, Color ColorMask)
    {
        if (Destination.Width < 1 || Destination.Height < 1)
        {
            return;
        }

        BeginDraw(DrawContext.Sprites);
        SpriteBatch.Draw(Texture, Destination, Source, ColorMask);
    }

    public void DrawTextureTo(IUIImageResource Texture, Rectangle? Source, Rectangle Destination, Color ColorMask)
        => DrawTextureTo(Renderer.AdapterRegistry.GetTexture(Texture), Source, Destination, ColorMask);

    public void DrawTextureTo(IUIImageResource Texture, Rectangle? Source, Rectangle Destination, Color ColorMask,
        Vector2 Origin, float Rotation = 0f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None)
        => DrawTextureTo(Renderer.AdapterRegistry.GetTexture(Texture), Source, Destination, ColorMask, Origin, Rotation, Depth, CasaMonoGameRenderInterop.ToSpriteEffects(Flip));

    public void DrawTextureTo(IUIImageResource Texture, Rectangle? Source, Rectangle Destination, Color ColorMask,
        Vector2 Origin, float Rotation = 0f, float Depth = 0f, SpriteEffects Effects = SpriteEffects.None)
        => DrawTextureTo(Renderer.AdapterRegistry.GetTexture(Texture), Source, Destination, ColorMask, Origin, Rotation, Depth, Effects);

    /// <summary>Draw a texture to a given <paramref name="Destination"/> <see cref="Rectangle"/></summary>
    public void DrawTextureTo(Texture2D Texture, Rectangle? Source, Rectangle Destination, Color ColorMask,
        Vector2 Origin, float Rotation = 0f, float Depth = 0f, SpriteEffects Effects = SpriteEffects.None)
    {
        if (Destination.Width < 1 || Destination.Height < 1)
        {
            return;
        }

        BeginDraw(DrawContext.Sprites);
        SpriteBatch.Draw(Texture, Destination, Source, ColorMask, Rotation, Origin, Effects, Depth);
    }

    /// <summary>Draw a texture at a given <paramref name="Destination"/> point</summary>
    public void DrawTextureAt(Texture2D Texture, Rectangle? Source, Vector2 Destination)
    {
        DrawTextureAt(Texture, Source, Destination, Color.White);
    }

    /// <summary>Draw a texture at a given <paramref name="Destination"/> point</summary>
    public void DrawTextureAt(Texture2D Texture, Rectangle? Source, Vector2 Destination, Color ColorMask)
    {
        BeginDraw(DrawContext.Sprites);
        SpriteBatch.Draw(Texture, Destination, Source, ColorMask);
    }

    public void DrawTextureAt(IUIImageResource Texture, Rectangle? Source, Vector2 Destination, Color ColorMask,
        Vector2 Origin, float Rotation = 0f, float ScaleX = 1f, float ScaleY = 1f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None)
        => DrawTextureAt(Renderer.AdapterRegistry.GetTexture(Texture), Source, Destination, ColorMask, Origin, Rotation, ScaleX, ScaleY, Depth, CasaMonoGameRenderInterop.ToSpriteEffects(Flip));

    public void DrawTextureAt(IUIImageResource Texture, Rectangle? Source, Vector2 Destination, Color ColorMask,
        Vector2 Origin, float Rotation = 0f, float ScaleX = 1f, float ScaleY = 1f, float Depth = 0f, SpriteEffects Effects = SpriteEffects.None)
        => DrawTextureAt(Renderer.AdapterRegistry.GetTexture(Texture), Source, Destination, ColorMask, Origin, Rotation, ScaleX, ScaleY, Depth, Effects);

    /// <summary>Draw a texture at a given <paramref name="Destination"/> point</summary>
    public void DrawTextureAt(Texture2D Texture, Rectangle? Source, Vector2 Destination, Color ColorMask,
        Vector2 Origin, float Rotation = 0f, float ScaleX = 1f, float ScaleY = 1f, float Depth = 0f, SpriteEffects Effects = SpriteEffects.None)
    {
        BeginDraw(DrawContext.Sprites);
        if (ScaleX == ScaleY)
        {
            SpriteBatch.Draw(Texture, Destination, Source, ColorMask, Rotation, Origin, ScaleX, Effects, Depth);
        }
        else
        {
            SpriteBatch.Draw(Texture, Destination, Source, ColorMask, Rotation, Origin, new Vector2(ScaleX, ScaleY), Effects, Depth);
        }
    }
    #endregion Draw Texture

    #region Draw Text

    /// <summary>
    /// Draws <paramref name="Text"/> using the active <see cref="ITextEngine"/>, while
    /// ensuring the correct <see cref="DrawContext"/> has been started on this transaction.
    /// The <see cref="Microsoft.Xna.Framework.Graphics.SpriteBatch"/> (<see cref="SpriteBatch"/>) owned by this transaction is passed
    /// automatically and is therefore not a parameter here, unlike the lower-level
    /// <see cref="ITextDrawEngine.DrawText"/> overload.
    /// </summary>
    public void DrawTextViaEngine(
        ResolvedFont Font,
        string Text,
        Vector2 Position,
        Color Color,
        Vector2 Origin,
        float Scale,
        float Rotation = 0f,
        float Depth = 0f,
        UIDrawFlip Flip = UIDrawFlip.None)
    {
        if (string.IsNullOrEmpty(Text) || Font?.NativeFont == null)
        {
            return;
        }

        BeginDraw(DrawContext.Sprites);
        TextRenderer.DrawText(this, Font, Text, Position, Color, Origin, Scale, Rotation, Depth, Flip);
    }

    public void DrawTextViaEngine(
        ResolvedFont Font,
        string Text,
        Vector2 Position,
        Color Color,
        Vector2 Origin,
        float Scale,
        float Rotation = 0f,
        float Depth = 0f,
        SpriteEffects Effects = SpriteEffects.None)
    {
        if (string.IsNullOrEmpty(Text) || Font?.NativeFont == null)
        {
            return;
        }

        BeginDraw(DrawContext.Sprites);
        UIDrawFlip flip = UIDrawFlip.None;
        if ((Effects & SpriteEffects.FlipHorizontally) != 0)
        {
            flip |= UIDrawFlip.Horizontal;
        }

        if ((Effects & SpriteEffects.FlipVertically) != 0)
        {
            flip |= UIDrawFlip.Vertical;
        }

        TextRenderer.DrawText(this, Font, Text, Position, Color, Origin, Scale, Rotation, Depth, flip);
    }

    /// <param name="Family">The font to use</param>
    /// <param name="DesiredFontSize">The desired size of the <see cref="SpriteFont"/>, in points.</param>
    /// <param name="Exact">If true, will attempt to render the text at exactly the given <paramref name="DesiredFontSize"/>.<br/>
    /// If false, treats <paramref name="DesiredFontSize"/> as an approximation, and may render the text slightly larger or smaller to avoid blurriness</param>
    public Vector2 DrawShadowedText(string Family, string Text, Vector2 Position, Color TextColor, Color ShadowColor,
        int DesiredFontSize, float XOffset = 1, float YOffset = 1, bool Exact = false)
        => DrawShadowedText(Family, CustomFontStyles.Normal, Text, Position, TextColor, ShadowColor, DesiredFontSize, XOffset, YOffset, Exact);

    // Both DrawShadowedText and MeasureText guard with `resolved.NativeFont == null`
    // (not `&& resolved.IsFallback`) so the null-check is consistent across all
    // text-rendering paths in CasaDrawTransaction.  Verified as part of PR #35 review.
    /// <param name="Family">The font to use</param>
    /// <param name="DesiredFontSize">The desired size of the <see cref="SpriteFont"/>, in points.</param>
    /// <param name="Exact">If true, will attempt to render the text at exactly the given <paramref name="DesiredFontSize"/>.<br/>
    /// If false, treats <paramref name="DesiredFontSize"/> as an approximation, and may render the text slightly larger or smaller to avoid blurriness</param>
    public Vector2 DrawShadowedText(string Family, CustomFontStyles Style, string Text, Vector2 Position, Color TextColor, Color ShadowColor,
        int DesiredFontSize, float XOffset = 1, float YOffset = 1, bool Exact = false)
    {
        // Resolve and measure once; draw twice (shadow + text) to avoid redundant font resolves
        var resolved = TextEngine.ResolveFont(new FontSpec(Family, DesiredFontSize, Style));
        if (resolved.NativeFont == null)
        {
            return Vector2.Zero;
        }

        float scale = Exact ? resolved.ExactScale : resolved.SuggestedScale;
        Vector2 suggested = TextEngine.MeasureText(resolved, Text);

        BeginDraw(DrawContext.Sprites);
        TextRenderer.DrawText(this, resolved, Text, Position + new Vector2(XOffset, YOffset), ShadowColor, resolved.DrawOrigin, scale);
        TextRenderer.DrawText(this, resolved, Text, Position, TextColor, resolved.DrawOrigin, scale);

        if (!Exact || resolved.SuggestedScale == resolved.ExactScale)
        {
            return suggested;
        }

        return suggested * (resolved.ExactScale / resolved.SuggestedScale);
    }

    public Vector2 MeasureText(string Family, CustomFontStyles Style, string Text, int DesiredFontSize, bool Exact = false)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return Vector2.Zero;
        }

        var resolved = TextEngine.ResolveFont(new FontSpec(Family, DesiredFontSize, Style));
        if (resolved.NativeFont == null)
        {
            return Vector2.Zero;
        }

        Vector2 suggested = TextEngine.MeasureText(resolved, Text);
        if (!Exact || resolved.SuggestedScale == resolved.ExactScale)
        {
            return suggested;
        }

        // Adjust from SuggestedScale to ExactScale proportionally
        float ratio = resolved.ExactScale / resolved.SuggestedScale;
        return suggested * ratio;
    }

    /// <summary>Renders the given <paramref name="Text"/> using <see cref="CustomFontStyles.Normal"/> style.</summary>
    /// <param name="Family">The font to use</param>
    /// <param name="DesiredFontSize">The desired size of the <see cref="SpriteFont"/>, in points.</param>
    /// <param name="Exact">If true, will attempt to render the text at exactly the given <paramref name="DesiredFontSize"/>.<br/>
    /// If false, treats <paramref name="DesiredFontSize"/> as an approximation, and may render the text slightly larger or smaller to avoid blurriness</param>
    public Vector2 DrawText(string Family, string Text, Vector2 Position, Color Color, int DesiredFontSize, bool Exact = false)
        => DrawText(Family, CustomFontStyles.Normal, Text, Position, Color, DesiredFontSize, Exact);

    /// <param name="Family">The font to use</param>
    /// <param name="DesiredFontSize">The desired size of the <see cref="SpriteFont"/>, in points.</param>
    /// <param name="Exact">If true, will attempt to render the text at exactly the given <paramref name="DesiredFontSize"/>.<br/>
    /// If false, treats <paramref name="DesiredFontSize"/> as an approximation, and may render the text slightly larger or smaller to avoid blurriness</param>
    public Vector2 DrawText(string Family, CustomFontStyles Style, string Text, Vector2 Position, Color Color, int DesiredFontSize, bool Exact = false)
    {
        var resolved = TextEngine.ResolveFont(new FontSpec(Family, DesiredFontSize, Style));
        if (resolved.NativeFont == null)
        {
#if DEBUG
            throw new KeyNotFoundException($"No font found for Family={Family} and Style={Style}");
#else
                return Vector2.Zero;
#endif
        }

        // Measure before drawing so we return the correct size without a second engine call.
        float scale = Exact ? resolved.ExactScale : resolved.SuggestedScale;
        Vector2 suggested = TextEngine.MeasureText(resolved, Text);

        BeginDraw(DrawContext.Sprites);
        TextRenderer.DrawText(this, resolved, Text, Position, Color, resolved.DrawOrigin, scale);

        if (!Exact || resolved.SuggestedScale == resolved.ExactScale)
        {
            return suggested;
        }

        return suggested * (resolved.ExactScale / resolved.SuggestedScale);
    }
    #endregion Draw Text

    #region Draw Geometry
    /// <summary>Returns the first non-null, non-None <see cref="DrawContext"/> from given <paramref name="Values"/></summary>
    private static DrawContext GetFirstValidDrawContext(params DrawContext?[] Values)
        => Values == null ? DrawContext.Sprites : Values.First(x => x.HasValue && x != DrawContext.None).Value;

    #region Rectangles
    public void StrokeAndFillRectangle(Vector2 Origin, RectangleF Destination, Color StrokeColor, Color FillColor, int StrokeThickness, DrawContext? PreferredContext = null)
        => StrokeAndFillRectangle(Origin, Destination, StrokeColor, FillColor, new Thickness(StrokeThickness), PreferredContext);
    public void StrokeAndFillRectangle(Vector2 Origin, RectangleF Destination, Color StrokeColor, Color FillColor, Thickness StrokeThickness, DrawContext? PreferredContext = null)
        => ShapeRenderer.StrokeAndFillRectangle(Origin, Destination, StrokeColor, FillColor, StrokeThickness, PreferredContext);

    public void StrokeRectangle(Vector2 Origin, RectangleF Destination, Color Color, Thickness Thickness, DrawContext? PreferredContext = null)
        => ShapeRenderer.StrokeRectangle(Origin, Destination, Color, Thickness, PreferredContext);

    public void FillRectangle(Vector2 Origin, RectangleF Destination, Color Color, DrawContext? PreferredContext = null)
        => ShapeRenderer.FillRectangle(Origin, Destination, Color, PreferredContext);
    #endregion Rectangles

    #region Circle / Ellipse
    /// <param name="NumSides">How many sides to use when approximating the geometry of the circle. Recommended: 16-32. Max value = <see cref="CircleMaxSides"/></param>
    public void StrokeAndFillCircle(Vector2 Center, Color StrokeColor, Color FillColor, float Radius, float StrokeThickness = 1.0f, int NumSides = 32, DrawContext? PreferredContext = null)
        => ShapeRenderer.StrokeAndFillCircle(Center, StrokeColor, FillColor, Radius, StrokeThickness, NumSides, PreferredContext);

    /// <param name="NumSides">How many sides to use when approximating the geometry of the circle. Recommended: 16-32. Max value = <see cref="CircleMaxSides"/></param>
    public void StrokeCircle(Vector2 Center, Color Color, float Radius, float Thickness = 1.0f, int NumSides = 32, DrawContext? PreferredContext = null)
        => ShapeRenderer.StrokeCircle(Center, Color, Radius, Thickness, NumSides, PreferredContext);

    /// <param name="NumSides">How many sides to use when approximating the geometry of the circle. Recommended: 16-32. Max value = <see cref="CircleMaxSides"/></param>
    public void FillCircle(Vector2 Center, Color Color, float Radius, int NumSides = 32, DrawContext? PreferredContext = null)
        => ShapeRenderer.FillCircle(Center, Color, Radius, NumSides, PreferredContext);

    //TODO: StrokeAndFillEllipse StrokeEllipse FillEllipse

    /// <param name="NumSides">How many sides to use when approximating the geometry of the ellipse. Recommended: 16-32. Max value = <see cref="CircleMaxSides"/></param>
    public void FillEllipse(Vector2 Center, float RadiusX, float RadiusY, Color Color, int NumSides = 32, DrawContext? PreferredContext = null)
        => ShapeRenderer.FillEllipse(Center, RadiusX, RadiusY, Color, NumSides, PreferredContext);

    /// <param name="NumSides">How many sides to use when approximating the geometry of the ellipse. Recommended: 16-32. Max value = <see cref="CircleMaxSides"/></param>
    public void StrokeEllipse(Vector2 Center, float RadiusX, float RadiusY, Color Color, float Thickness = 1.0f, int NumSides = 32, DrawContext? PreferredContext = null)
        => ShapeRenderer.StrokeEllipse(Center, RadiusX, RadiusY, Color, Thickness, NumSides, PreferredContext);

    /// <param name="NumSides">How many sides to use when approximating the geometry of the ellipse. Recommended: 16-32. Max value = <see cref="CircleMaxSides"/></param>
    public void StrokeAndFillEllipse(Vector2 Center, float RadiusX, float RadiusY, Color StrokeColor, Color FillColor, float StrokeThickness = 1.0f, int NumSides = 32, DrawContext? PreferredContext = null)
        => ShapeRenderer.StrokeAndFillEllipse(Center, RadiusX, RadiusY, StrokeColor, FillColor, StrokeThickness, NumSides, PreferredContext);

    //Adapted from:
    //https://github.com/craftworkgames/MonoGame.Extended/blob/a3373ac26d90c9801b71b55679f1199fa4ec22a6/src/cs/MonoGame.Extended/Math/ShapeExtensions.cs
    //https://github.com/craftworkgames/MonoGame.Extended/blob/a3373ac26d90c9801b71b55679f1199fa4ec22a6/src/cs/MonoGame.Extended/VectorDraw/PrimitiveDrawing.cs
    public static Vector2[] GetCircleVertices(Vector2 origin, double radius, int sides, double angleOffset = 0.0)
        => CasaShapeGeometry.GetCircleVertices(origin, radius, sides, angleOffset);
    #endregion Circle / Ellipse

    #region Polygons
    public void StrokeAndFillPolygon(Vector2 Origin, IReadOnlyList<Vector2> Vertices, Color StrokeColor, Color FillColor, float StrokeThickness = 1.0f,
        bool CenterLinesOnVertices = true, WindingOrder? Order = null)
        => ShapeRenderer.StrokeAndFillPolygon(Origin, Vertices, StrokeColor, FillColor, StrokeThickness, CenterLinesOnVertices, Order);

    /// <param name="CenterLinesOnVertices">If true, the <paramref name="Vertices"/> will represent the center of the line strokes. (I.E. the stroke will extend left of the vertex by half the <paramref name="Thickness"/>, and right of the vertex by half the <paramref name="Thickness"/>)<br/>
    /// If false, the <paramref name="Vertices"/> will represent the outer portion of the line strokes, meaning that the line stroke will extend inwards towards the center of the polygon by the <paramref name="Thickness"/> amount.</param>
    /// <param name="Order">The <see cref="WindingOrder"/> of the <paramref name="Vertices"/>.<para/>
    /// Will be dynamically computed if null. (Only used if <paramref name="CenterLinesOnVertices"/> is false)</param>
    public void StrokePolygon(Vector2 Origin, IReadOnlyList<Vector2> Vertices, Color Color, float Thickness = 1.0f, bool CenterLinesOnVertices = true, WindingOrder? Order = null, DrawContext? PreferredContext = null)
        => ShapeRenderer.StrokePolygon(Origin, Vertices, Color, Thickness, CenterLinesOnVertices, Order, PreferredContext);

    public void FillPolygon(Vector2 Origin, IEnumerable<Vector2> Vertices, Color Color)
        => ShapeRenderer.FillPolygon(Origin, Vertices, Color);
    #endregion Polygons

    #region Points
    public enum PointShape
    {
        Square,
        Circle
    }

    public void StrokeAndFillPoint(Vector2 Position, Color StrokeColor, Color FillColor, float Radius = 3.0f, int StrokeThickness = 1, PointShape Shape = PointShape.Circle, DrawContext? PreferredContext = null)
        => ShapeRenderer.StrokeAndFillPoint(Position, StrokeColor, FillColor, Radius, StrokeThickness, Shape, PreferredContext);

    public void StrokePoint(Vector2 Position, Color Color, float Radius = 1.0f, int Thickness = 1, PointShape Shape = PointShape.Circle, DrawContext? PreferredContext = null)
        => ShapeRenderer.StrokePoint(Position, Color, Radius, Thickness, Shape, PreferredContext);

    public void FillPoint(Vector2 Position, Color Color, float Radius = 1.0f, PointShape Shape = PointShape.Circle, DrawContext? PreferredContext = null)
        => ShapeRenderer.FillPoint(Position, Color, Radius, Shape, PreferredContext);
    #endregion Points

    public void StrokeLineSegment(Vector2 Origin, Vector2 Start, Vector2 End, Color Color, float Thickness = 1.0f, DrawContext? PreferredContext = null)
        => ShapeRenderer.StrokeLineSegment(Origin, Start, End, Color, Thickness, PreferredContext);

    internal void FillTrianglePrimitiveCore(Vector2 Origin, Vector2 v0, Vector2 v1, Vector2 v2, Color Color)
    {
        if (!PrimitiveBatch.IsReady() || CurrentContext != DrawContext.Primitives)
        {
            throw new InvalidOperationException($"{nameof(global::MonoGame.Extended.VectorDraw.PrimitiveBatch)}.{nameof(global::MonoGame.Extended.VectorDraw.PrimitiveBatch.Begin)} must be called before drawing anything.");
        }
        else if (CurrentRasterizerState.CullMode != CullMode.None)
        {
            string ErrorMessage = $"{nameof(CasaDrawTransaction)}.{nameof(FillTrianglePrimitiveCore)} does not account for the winding order of the vertices and may not work correctly " +
                                  $"if the {nameof(RasterizerState)}'s {nameof(CullMode)} is not set to '{nameof(CullMode.None)}'.";
            throw new NotImplementedException(ErrorMessage);
        }

        PrimitiveBatch.AddVertex(v0 + Origin, Color, PrimitiveType.TriangleList);
        PrimitiveBatch.AddVertex(v1 + Origin, Color, PrimitiveType.TriangleList);
        PrimitiveBatch.AddVertex(v2 + Origin, Color, PrimitiveType.TriangleList);
    }

    public void FillTriangle(Vector2 Origin, Vector2 v0, Color c0, Vector2 v1, Color c1, Vector2 v2, Color c2)
        => ShapeRenderer.FillTriangle(Origin, v0, c0, v1, c1, v2, c2);

    internal void FillTriangleCore(Vector2 Origin, Vector2 v0, Color c0, Vector2 v1, Color c1, Vector2 v2, Color c2)
    {
        if (CurrentRasterizerState.CullMode != CullMode.None)
        {
            string ErrorMessage = $"{nameof(CasaDrawTransaction)}.{nameof(FillTriangle)} does not account for the winding order of the vertices and may not work correctly " +
                                  $"if the {nameof(RasterizerState)}'s {nameof(CullMode)} is not set to '{nameof(CullMode.None)}'.";
            throw new NotImplementedException(ErrorMessage);
        }

        BeginDraw(DrawContext.Primitives);
        PrimitiveBatch.AddVertex(v0 + Origin, c0, PrimitiveType.TriangleList);
        PrimitiveBatch.AddVertex(v1 + Origin, c1, PrimitiveType.TriangleList);
        PrimitiveBatch.AddVertex(v2 + Origin, c2, PrimitiveType.TriangleList);
    }

    private VertexPositionColorTexture[] TexturedVertexBuffer = Array.Empty<VertexPositionColorTexture>();
    private short[] TexturedIndexBuffer = Array.Empty<short>();

    /// <summary>Textured triangle-list path: the counterpart of <see cref="FillTriangle"/> for textured paints on rounded geometry.
    /// Issues its own GPU call through <see cref="CasaDesktopRuntime.TexturedPrimitiveEffect"/> with the same projection and transform as the
    /// <see cref="DrawContext.Primitives"/> context and the same device states, so it composes with the scissor/stencil clip pipeline and
    /// honours <see cref="DrawSettings.SamplerType"/> (a Wrap sampler tiles the texture).</summary>
    public void DrawTexturedTriangleList(Vector2 Origin, IUIImageResource Texture, IReadOnlyList<Vector2> Vertices, IReadOnlyList<Vector2> TextureCoordinates,
        IReadOnlyList<int> Indices, Color ColorMask)
    {
        if (Texture == null || Texture.IsDisposed || Vertices == null || Vertices.Count == 0 || Indices == null || Indices.Count < 3)
        {
            return;
        }

        if (TextureCoordinates == null || TextureCoordinates.Count != Vertices.Count)
        {
            throw new ArgumentException($"{nameof(TextureCoordinates)} must contain exactly one entry per vertex.", nameof(TextureCoordinates));
        }

        if (Vertices.Count > short.MaxValue)
        {
            throw new NotSupportedException($"{nameof(DrawTexturedTriangleList)} supports at most {short.MaxValue} vertices per call (16-bit index buffer).");
        }

        if (CurrentRasterizerState.CullMode != CullMode.None)
        {
            string ErrorMessage = $"{nameof(CasaDrawTransaction)}.{nameof(DrawTexturedTriangleList)} does not account for the winding order of the vertices and may not work correctly " +
                                  $"if the {nameof(RasterizerState)}'s {nameof(CullMode)} is not set to '{nameof(CullMode.None)}'.";
            throw new NotImplementedException(ErrorMessage);
        }

        Texture2D texture = Renderer.AdapterRegistry.GetTexture(Texture);
        int primitiveCount = Indices.Count / 3;
        int indexCount = primitiveCount * 3;

        if (TexturedVertexBuffer.Length < Vertices.Count)
        {
            TexturedVertexBuffer = new VertexPositionColorTexture[Math.Max(Vertices.Count, TexturedVertexBuffer.Length * 2)];
        }

        if (TexturedIndexBuffer.Length < indexCount)
        {
            TexturedIndexBuffer = new short[Math.Max(indexCount, TexturedIndexBuffer.Length * 2)];
        }

        for (int i = 0; i < Vertices.Count; i++)
        {
            Vector2 position = Vertices[i] + Origin;
            TexturedVertexBuffer[i] = new VertexPositionColorTexture(new Vector3(position.X, position.Y, 0f), ColorMask, TextureCoordinates[i]);
        }

        for (int i = 0; i < indexCount; i++)
        {
            TexturedIndexBuffer[i] = checked((short)Indices[i]);
        }

        //  Flush whichever batch is open, then draw immediately with the primitive device states; the next call re-begins a batch as needed.
        EndDraw(CurrentContext);
        DrawState.ApplyPrimitiveDeviceStates();

        Effect effect = Renderer.TexturedPrimitiveEffect;
        //  Row-vector convention: world * view * projection, with an identity world - same order as every other engine shader call site.
        effect.Parameters[ShaderParameterNames.WorldViewProj].SetValue(CurrentSettings.Transform * PrimitiveProjectionMatrix);
        effect.Parameters[ShaderParameterNames.BasColorTexture].SetValue(texture);

        foreach (EffectPass pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, TexturedVertexBuffer, 0, Vertices.Count, TexturedIndexBuffer, 0, primitiveCount);
        }
    }

    /// <summary>Fills the given quadrilateral using <see cref="SamplerType.LinearClamp"/> to produce gradient color interpolation.</summary>
    public void FillQuadrilateralLinearClamp(Vector2 Origin, Vector2 v0, Color c0, Vector2 v1, Color c1, Vector2 v2, Color c2, Vector2 v3, Color c3)
        => ShapeRenderer.FillQuadrilateralLinearClamp(Origin, v0, c0, v1, c1, v2, c2, v3, c3);

    internal void FillQuadrilateralLinearClampCore(Vector2 Origin, Vector2 v0, Color c0, Vector2 v1, Color c1, Vector2 v2, Color c2, Vector2 v3, Color c3)
    {
        if (CurrentRasterizerState.CullMode != CullMode.None)
        {
            string ErrorMessage = $"{nameof(CasaDrawTransaction)}.{nameof(FillQuadrilateralLinearClamp)} does not account for the winding order of the vertices and may not work correctly " +
                                  $"if the {nameof(RasterizerState)}'s {nameof(CullMode)} is not set to '{nameof(CullMode.None)}'.";
            throw new NotImplementedException(ErrorMessage);
        }

        using (SetDrawSettingsTemporary(CurrentSettings with { SamplerType = SamplerType.LinearClamp }))
        {
            BeginDraw(DrawContext.Primitives);

            PrimitiveBatch.AddVertex(v0 + Origin, c0, PrimitiveType.TriangleList);
            PrimitiveBatch.AddVertex(v1 + Origin, c1, PrimitiveType.TriangleList);
            PrimitiveBatch.AddVertex(v2 + Origin, c2, PrimitiveType.TriangleList);

            PrimitiveBatch.AddVertex(v2 + Origin, c2, PrimitiveType.TriangleList);
            PrimitiveBatch.AddVertex(v3 + Origin, c3, PrimitiveType.TriangleList);
            PrimitiveBatch.AddVertex(v0 + Origin, c0, PrimitiveType.TriangleList);
        }
    }

    /*
    #if DEBUG
                if (DateTime.Now.Second % 2 == 0)
                    Ctx = DrawContext.Primitives;
                else
                {
                    Ctx = DrawContext.Sprites;
                    Color = Color * 0.9f;
                }
    #endif
    */

    //Assume Transform.M11/Transform.M12 are the scale
    //if both values are the same, then you can convert a scalar to another scaled/unscaled scalar value
    #endregion Draw Geometry
    #endregion Draw

    /// <param name="ClearColor">Optional. If not null, the new render target will be immediately cleared with this color. Only used if the render target actually changed.</param>
    public void SetRenderTarget(RenderTarget2D New, Color? ClearColor)
        => RenderTargets.SetRenderTarget(New, ClearColor);

    /// <summary>
    /// Drawing to a temporary render target example:<para/>
    /// <code xml:space="preserve">
    /// <see cref="RenderTarget2D"/> Temp = <see cref="RenderUtils.CreateRenderTarget(Microsoft.Xna.Framework.Graphics.GraphicsDevice, Rectangle, bool)"/>;<br/>
    /// using (Temp)<br/>
    /// {<br/>
    /// ⠀⠀⠀⠀using (<see cref="SetRenderTargetTemporary(RenderTarget2D, Color?)"/>)<br/>
    /// ⠀⠀⠀⠀{<br/>
    /// ⠀⠀⠀⠀⠀⠀⠀⠀//Draw something<br/>
    /// ⠀⠀⠀⠀}<para/>
    /// ⠀⠀⠀⠀//  Optionally, draw the temporary render target to the backbuffer<br/>
    /// ⠀⠀⠀⠀<see cref="DrawTextureAt(Texture2D, Rectangle?, Vector2)"/>;<br/>
    /// }</code>
    /// </summary>
    /// <param name="ClearColor">Optional. If not null, the new render target will be immediately cleared with this color. Only used if the render target actually changed.</param>
    public IDisposable SetRenderTargetTemporary(IUIRenderTarget New, Color? ClearColor)
    {
        return SetRenderTargetTemporary(Renderer.AdapterRegistry.GetRenderTarget(New), ClearColor);
    }

    /// </summary>
    /// <param name="ClearColor">Optional. If not null, the new render target will be immediately cleared with this color. Only used if the render target actually changed.</param>
    public IDisposable SetRenderTargetTemporary(RenderTarget2D New, Color? ClearColor)
        => RenderTargets.SetRenderTargetTemporary(New, ClearColor);

    /// <param name="New">To change current settings, consider using '<see cref="CurrentSettings"/> with { ... }' record syntax.</param>
    public void SetDrawSettings(DrawSettings New)
        => DrawState.SetDrawSettings(New);

    /// <param name="New">To change current settings, consider using '<see cref="CurrentSettings"/> with { ... }' record syntax.</param>
    public IDisposable SetDrawSettingsTemporary(DrawSettings New)
        => DrawState.SetDrawSettingsTemporary(New);

    public IDisposable SetTransformTemporary(Matrix Transform)
    {
        return SetDrawSettingsTemporary(CurrentSettings with { Transform = Transform });
    }

    public ClipResolveResult ResolveClip(ClipDefinition Definition)
        => ClipManager.Resolve(Definition);

    public ClipScope PushClipTemporary(ClipDefinition Definition)
        => ClipManager.Push(Definition);

    public string GetClipDiagnosticsDebugText()
        => ClipDiagnostics.ToDebugString();

    public ClipScope PushRectangleClip(Rectangle? Bounds, bool IntersectWithCurrentClipTarget)
    {
        if (!Bounds.HasValue)
        {
            Rectangle previousBounds = SpriteBatch.GraphicsDevice.ScissorRectangle;
            bool previousScissorState = CurrentSettings.UsesScissorTest;
            SetClipTarget(null, false);

            ClipDefinition requested = ClipDefinition.None(false);
            ClipResolveResult resolution = new(requested, requested, ClipStrategy.None, false);
            return new ClipScope(resolution, () =>
            {
                EndDraw(CurrentContext);
                SpriteBatch.GraphicsDevice.ScissorRectangle = previousBounds;

                bool currentScissorState = CurrentSettings.UsesScissorTest;
                if (previousScissorState && !currentScissorState)
                {
                    SetDrawSettings(CurrentSettings with { RasterizerType = RasterizerType.SolidScissorTest });
                }
                else if (!previousScissorState && currentScissorState)
                {
                    SetDrawSettings(CurrentSettings with { RasterizerType = RasterizerType.Solid });
                }
            });
        }

        ClipDefinition definition = Bounds.HasValue
            ? ClipDefinition.Rectangle(Bounds.Value, IntersectWithCurrentClipTarget)
            : ClipDefinition.None();
        return PushClipTemporary(definition);
    }

    internal ClipScope PushRectangleClipCore(Rectangle Bounds, bool IntersectWithCurrentClipTarget, ClipResolveResult Resolution)
    {
        Rectangle PreviousBounds = SpriteBatch.GraphicsDevice.ScissorRectangle;
        bool PreviousScissorState = CurrentSettings.UsesScissorTest;

        SetClipTarget(Bounds, IntersectWithCurrentClipTarget);

        return new ClipScope(Resolution, () =>
        {
            EndDraw(CurrentContext);
            SpriteBatch.GraphicsDevice.ScissorRectangle = PreviousBounds;

            bool CurrentScissorState = CurrentSettings.UsesScissorTest;
            if (PreviousScissorState && !CurrentScissorState)
            {
                SetDrawSettings(CurrentSettings with { RasterizerType = RasterizerType.SolidScissorTest });
            }
            else if (!PreviousScissorState && CurrentScissorState)
            {
                SetDrawSettings(CurrentSettings with { RasterizerType = RasterizerType.Solid });
            }
        });
    }

    /// <summary>Sets the shader <see cref="Effect"/> applied during sprite batch rendering.</summary>
    public void SetEffect(Effect Effect)
        => SetDrawSettings(CurrentSettings with { BackendEffect = Effect });

    /// <summary>Sets the shader <see cref="Effect"/> applied during sprite batch rendering, and restores the previous effect when the returned <see cref="IDisposable"/> is disposed.</summary>
    public IDisposable SetEffectTemporary(Effect Effect)
        => SetDrawSettingsTemporary(CurrentSettings with { BackendEffect = Effect });

    internal void ClearStencil(int StencilValue = 0)
    {
        EndDraw(CurrentContext);
        GraphicsDevice.Clear(ClearOptions.Stencil, Color.Transparent, 0.0f, StencilValue);
    }

    internal void DrawClipGeometry(ClipGeometry Geometry)
    {
        if (Geometry.IsEmpty)
        {
            return;
        }

        for (int i = 0; i + 2 < Geometry.Indices.Count; i += 3)
        {
            Vector2 v0 = Geometry.Vertices[Geometry.Indices[i]];
            Vector2 v1 = Geometry.Vertices[Geometry.Indices[i + 1]];
            Vector2 v2 = Geometry.Vertices[Geometry.Indices[i + 2]];
            FillTriangle(Vector2.Zero, v0, Color.White, v1, Color.White, v2, Color.White);
        }
    }

    /// <param name="IntersectWithCurrentClipTarget">If true, rather than replacing the clip target with the given <paramref name="Bounds"/>,<br/>
    /// the clip target will be the intersection of the current clip target and the given <paramref name="Bounds"/></param>
    public void SetClipTarget(Rectangle? Bounds, bool IntersectWithCurrentClipTarget)
    {
        Rectangle CurrentBounds = SpriteBatch.GraphicsDevice.ScissorRectangle;

        bool IsScissorTesting = CurrentSettings.UsesScissorTest;
        bool ShouldScissorTest = Bounds.HasValue;

        if (IsScissorTesting && Bounds.HasValue && IntersectWithCurrentClipTarget)
        {
            Bounds = Rectangle.Intersect(Bounds.Value, CurrentBounds);
        }

        if (Bounds != CurrentBounds || IsScissorTesting != ShouldScissorTest)
        {
            EndDraw(CurrentContext);
            SpriteBatch.GraphicsDevice.ScissorRectangle = Bounds ?? Renderer.GetViewport(0);
            if (ShouldScissorTest && !IsScissorTesting)
            {
                SetDrawSettings(CurrentSettings with { RasterizerType = RasterizerType.SolidScissorTest });
            }
            else if (!ShouldScissorTest && IsScissorTesting)
            {
                SetDrawSettings(CurrentSettings with { RasterizerType = RasterizerType.Solid });
            }
        }
    }

    /// <summary>Compatibility wrapper kept for rectangle-only migration paths.</summary>
    /// <param name="IntersectWithCurrentClipTarget">If true, rather than replacing the clip target with the given <paramref name="Bounds"/>,<br/>
    /// the clip target will be the intersection of the current clip target and the given <paramref name="Bounds"/></param>
    public IDisposable SetClipTargetTemporary(Rectangle? Bounds, bool IntersectWithCurrentClipTarget)
    {
        return PushRectangleClip(Bounds, IntersectWithCurrentClipTarget);
    }

    public void DisableClipTarget()
    {
        if (CurrentSettings.UsesScissorTest)
        {
            EndDraw(CurrentContext);
            SetDrawSettings(CurrentSettings with { RasterizerType = RasterizerType.Solid });
        }
    }

    public void Dispose()
    {
        EndDraw(CurrentContext);
    }
}