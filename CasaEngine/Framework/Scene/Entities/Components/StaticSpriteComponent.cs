using System.ComponentModel;
using CasaEngine.Core.Serialization;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Engine.Geometry;
using CasaEngine.Framework.Physics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

[DisplayName("Static Sprite")]
public class StaticSpriteComponent : SceneComponent, ICollideableComponent, IComponentDrawable, IBoundingBoxable
{
    private Sprite _sprite;
    private SpriteData _spriteData;

    // Name read from the data. It is resolved in InitializeWithWorld, once the world (and so the asset manager) is known:
    // Load runs before the component is attached to its entity.
    private string _spriteDataName;

    // ADR-0037: the sprite data is a shared asset, held through a counted handle for as long as the
    // component references it and given back in ReleaseSpriteAssetHandles (InitializeWithWorld's reset,
    // a reload, and Detach). The Sprite itself is disposed there too: it holds the sheet texture.
    private AssetHandle<SpriteData> _spriteDataHandle;
    private SpriteRendererComponent _spriteRendererComponent;
    private DepthSortable2DComponent _depthSortable2DComponent;
    private readonly List<(Collision2d, PhysicsBody)> _collisionObjects = new();

    // Motion in progress (C2): straight line to the target at a constant speed, advanced in Update by the received time.
    private bool _isMoving;
    private Vector3 _moveStart;
    private Vector2 _moveDirection;
    private Vector2 _moveTarget;
    private float _moveSpeed;
    private float _moveDistance;
    private float _moveElapsed;

    // Tint in progress (C2): linear from the start colour to the target over the duration.
    private bool _isTinting;
    private Color _tintStart;
    private Color _tintTarget;
    private float _tintDuration;
    private float _tintElapsed;
    private IPhysicsWorld _physicsWorldContext;

    public PhysicsType PhysicsType { get; }

    public HashSet<Collision> Collisions { get; }

    /// <summary>Tint passed to the renderer. White (no tint) by default.</summary>
    public Color Color { get; set; } = Color.White;

    /// <summary>
    /// Blend mode of the whole sprite. <see cref="SpriteBlendMode.Opaque"/> (the default) keeps the plain draw path;
    /// any other mode is applied to the whole sprite, without the per-texel semi-transparency of <see cref="SpritePsxSemiTransparency"/>.
    /// </summary>
    public SpriteBlendMode BlendMode { get; set; } = SpriteBlendMode.Opaque;

    /// <summary>
    /// Order among sprites of the same z (ADR-0131): at equal z the smaller value is drawn first, so the larger one is drawn
    /// over it. 0 by default, which keeps the plain draw calls. No effect on a sprite sorted by a sort key.
    /// </summary>
    public int DrawOrder { get; set; }
    public SpriteEffects SpriteEffect { get; set; }

    /// <summary>Name of the sprite read from the data, until the sprite itself is resolved in <see cref="InitializeWithWorld"/>.</summary>
    internal string PendingSpriteDataName => _spriteDataName;

    /// <summary>
    /// Optional asset ID used to pre-configure the sprite when creating an entity
    /// via drag-and-drop. Loaded during <see cref="InitializeWithWorld"/>.
    /// </summary>
    public Guid SpriteAssetId { get; set; } = Guid.Empty;

    public StaticSpriteComponent()
    {

    }

    public StaticSpriteComponent(StaticSpriteComponent other) : base(other)
    {
        _spriteData = other._spriteData;
        _spriteDataName = other._spriteDataName;
        SpriteAssetId = other.SpriteAssetId;
        Color = other.Color;
        BlendMode = other.BlendMode;
        DrawOrder = other.DrawOrder;
    }

    protected override void InitializePrivate()
    {
        base.InitializePrivate();
    }

    public override void InitializeWithWorld(CasaEngine.Framework.Scene.World.World world)
    {
        base.InitializeWithWorld(world);

        _spriteRendererComponent = Owner.World.Game.GetGameComponent<SpriteRendererComponent>();
    _depthSortable2DComponent = Owner.GetComponent<DepthSortable2DComponent>();
        _physicsWorldContext = Owner.World.PhysicsWorld;

        if (_sprite != null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(_spriteDataName))
        {
            LoadSpriteData(_spriteDataName);
        }
        else if (SpriteAssetId != Guid.Empty)
        {
            _spriteDataHandle = Owner.World.Game.AssetContentManager.Acquire<SpriteData>(SpriteAssetId);
            _spriteData = _spriteDataHandle.Asset;
            _sprite = Sprite.Create(_spriteData, Owner.World.Game.AssetContentManager);
            AddCollisions();
            IsBoundingBoxDirty = true;
        }
    }

    public override void Detach()
    {
        ReleaseSpriteAssetHandles();
        base.Detach();
    }

    /// <summary>Gives back the sprite data hold and disposes the sprite (its sheet texture hold with it).</summary>
    private void ReleaseSpriteAssetHandles()
    {
        _sprite?.Dispose();
        _sprite = null;
        _spriteDataHandle?.Dispose();
        _spriteDataHandle = null;
    }

    public override StaticSpriteComponent Clone()
    {
        return new StaticSpriteComponent(this);
    }

    /// <summary>True while a motion started by <see cref="StartMove"/> has not arrived nor been stopped.</summary>
    public bool IsMoving => _isMoving;

    /// <summary>True while a tint started by <see cref="StartTint"/> has not ended nor been stopped.</summary>
    public bool IsTinting => _isTinting;

    /// <summary>
    /// Moves the sprite from its current X and Y to the target at a constant speed, in world units per second of the
    /// time received by <see cref="Update"/> (so the scaled game time). Z does not change. The target is set exactly on
    /// arrival. Replaces a motion in progress. A target equal to the current position is set at once.
    /// </summary>
    public void StartMove(Vector2 target, float speed)
    {
        if (!(speed > 0.0f) || float.IsInfinity(speed))
        {
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "The speed must be a finite number greater than 0.");
        }

        if (!float.IsFinite(target.X) || !float.IsFinite(target.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(target), target, "The target must be finite.");
        }

        var start = Position;
        var delta = target - new Vector2(start.X, start.Y);
        var distance = delta.Length();
        if (distance <= 0.0f)
        {
            _isMoving = false;
            Position = new Vector3(target.X, target.Y, start.Z);
            return;
        }

        _moveStart = start;
        _moveTarget = target;
        _moveDirection = delta / distance;
        _moveDistance = distance;
        _moveSpeed = speed;
        _moveElapsed = 0.0f;
        _isMoving = true;
    }

    /// <summary>
    /// Changes <see cref="Color"/> linearly from its current value to the target over the duration in seconds of the
    /// time received by <see cref="Update"/>. The target is set exactly at the end; a duration of 0 sets it at once.
    /// Replaces a tint in progress.
    /// </summary>
    public void StartTint(Color target, float duration)
    {
        if (!(duration >= 0.0f) || float.IsInfinity(duration))
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "The duration must be a finite number of at least 0.");
        }

        if (duration == 0.0f)
        {
            _isTinting = false;
            Color = target;
            return;
        }

        _tintStart = Color;
        _tintTarget = target;
        _tintDuration = duration;
        _tintElapsed = 0.0f;
        _isTinting = true;
    }

    /// <summary>Stops the motion in progress, if any, where the sprite is.</summary>
    public void StopMove()
    {
        _isMoving = false;
    }

    /// <summary>Stops the tint in progress, if any, on the current colour.</summary>
    public void StopTint()
    {
        _isTinting = false;
    }

    /// <summary>Stops both the motion and the tint. A show or a hide of the sprite does this.</summary>
    public void StopMotionAndTint()
    {
        _isMoving = false;
        _isTinting = false;
    }

    private void AdvanceMove(float elapsedTime)
    {
        _moveElapsed += elapsedTime;
        var travelled = _moveSpeed * _moveElapsed;
        var z = _moveStart.Z;
        if (travelled >= _moveDistance)
        {
            _isMoving = false;
            Position = new Vector3(_moveTarget.X, _moveTarget.Y, z);
            return;
        }

        Position = new Vector3(_moveStart.X + _moveDirection.X * travelled, _moveStart.Y + _moveDirection.Y * travelled, z);
    }

    private void AdvanceTint(float elapsedTime)
    {
        _tintElapsed += elapsedTime;
        if (_tintElapsed >= _tintDuration)
        {
            _isTinting = false;
            Color = _tintTarget;
            return;
        }

        var t = _tintElapsed / _tintDuration;
        Color = new Color(
            LerpChannel(_tintStart.R, _tintTarget.R, t),
            LerpChannel(_tintStart.G, _tintTarget.G, t),
            LerpChannel(_tintStart.B, _tintTarget.B, t),
            LerpChannel(_tintStart.A, _tintTarget.A, t));
    }

    private static byte LerpChannel(byte from, byte to, float t)
    {
        return (byte)MathF.Round(from + (to - from) * t, MidpointRounding.AwayFromZero);
    }

    public override void Update(float elapsedTime)
    {
        if (_isMoving)
        {
            AdvanceMove(elapsedTime);
        }

        if (_isTinting)
        {
            AdvanceTint(elapsedTime);
        }

        foreach (var (collision2d, collisionObject) in _collisionObjects)
        {
            SpriteCollisionHelper.UpdateBodyTransformation(Position, Orientation, Scale,
                collisionObject, collision2d, _spriteData.Origin, _spriteData.PositionInTexture);
        }

        base.Update(elapsedTime);
    }

    public override BoundingBox GetBoundingBox()
    {
        if (_spriteData != null)
        {
            return SpriteDataBoundsCalculator.CalculateLocalBounds(_spriteData).Transform(WorldMatrixWithScale);
        }

        const float length = 0.5f;
        return new BoundingBox(Vector3.One * -length, Vector3.One * length).Transform(WorldMatrixWithScale);
    }

    public override void Draw(float elapsedTime)
    {
        if (_spriteData == null || _sprite?.Texture?.Resource == null)
        {
            return;
        }

        Submit(_spriteRendererComponent.GraphicsDevice.ScissorRectangle);
    }

    /// <summary>
    /// Queues the sprite on the renderer. A sprite sorted by a <see cref="DepthSortable2DComponent"/> goes through its
    /// sort key; any other goes through its z-order, with or without a blend mode.
    /// </summary>
    internal void Submit(Rectangle scissorRectangle)
    {
        var position = new Vector2(Position.X, Position.Y);
        var scale = new Vector2(Scale.X, Scale.Y);
        if (_depthSortable2DComponent != null)
        {
            var sortKey = _depthSortable2DComponent.BuildSortKey(Position, Owner.World.CurrentRenderFrame);
            if (BlendMode == SpriteBlendMode.Opaque)
            {
                _spriteRendererComponent.DrawSprite(_sprite, position, 0.0f, scale, Color, Position.Z, in sortKey,
                    SpriteEffects.None, scissorRectangle);
            }
            else
            {
                _spriteRendererComponent.DrawSprite(_sprite, position, 0.0f, scale, Color, Position.Z, in sortKey,
                    drawDebug: true, SpriteEffects.None, scissorRectangle, BlendMode);
            }

            return;
        }

        if (DrawOrder != 0)
        {
            if (BlendMode == SpriteBlendMode.Opaque)
            {
                _spriteRendererComponent.DrawSprite(_sprite, position, 0.0f, scale, Color, Position.Z,
                    drawDebug: true, SpriteEffects.None, scissorRectangle, DrawOrder);
            }
            else
            {
                _spriteRendererComponent.DrawSprite(_sprite, position, 0.0f, scale, Color, Position.Z, BlendMode, scissorRectangle, DrawOrder);
            }

            return;
        }

        if (BlendMode == SpriteBlendMode.Opaque)
        {
            _spriteRendererComponent.DrawSprite(_sprite, position, 0.0f, scale, Color, Position.Z,
                drawDebug: true, SpriteEffects.None, scissorRectangle);
            return;
        }

        _spriteRendererComponent.DrawSprite(_sprite, position, 0.0f, scale, Color, Position.Z, BlendMode, scissorRectangle);
    }

    public override void OnEnabledValueChange()
    {
        base.OnEnabledValueChange();

        if (Owner.IsEnabled)
        {
            AddCollisions();
        }
        else
        {
            RemoveCollisions();
        }
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        var spriteDataName = element["spriteDataName"]?.GetString();
        _spriteDataName = string.IsNullOrEmpty(spriteDataName) || string.Equals(spriteDataName, "null", StringComparison.CurrentCultureIgnoreCase)
            ? null
            : spriteDataName;

        Color = element.ContainsKey("color") ? ReadColor(element["color"]) : Color.White;
        BlendMode = element.ContainsKey("blend_mode") ? ReadBlendMode(element["blend_mode"]) : SpriteBlendMode.Opaque;
        DrawOrder = element.ContainsKey("draw_order") ? ReadDrawOrder(element["draw_order"]) : 0;
    }

    private static int ReadDrawOrder(JToken token)
    {
        // An integer outside the Int64 range is held as a BigInteger: it falls through to the error below.
        if (token is JValue { Type: JTokenType.Integer, Value: long value } && value is >= int.MinValue and <= int.MaxValue)
        {
            return (int)value;
        }

        throw new InvalidDataException(
            $"{nameof(StaticSpriteComponent)}: invalid 'draw_order' {token.ToString(Newtonsoft.Json.Formatting.None)}: expected a JSON integer.");
    }

    private static Color ReadColor(JToken token)
    {
        if (token is not JObject node
            || !TryReadChannel(node, "r", out var r) || !TryReadChannel(node, "g", out var g)
            || !TryReadChannel(node, "b", out var b) || !TryReadChannel(node, "a", out var a))
        {
            throw new InvalidDataException(
                $"{nameof(StaticSpriteComponent)}: invalid 'color' {token.ToString(Newtonsoft.Json.Formatting.None)}: expected an object with the integer keys r, g, b, a, each from 0 to 255.");
        }

        return new Color(r, g, b, a);
    }

    private static bool TryReadChannel(JObject node, string key, out byte value)
    {
        value = 0;
        if (!node.TryGetValue(key, out var token) || token.Type != JTokenType.Integer)
        {
            return false;
        }

        var channel = token.Value<long>();
        if (channel is < 0 or > 255)
        {
            return false;
        }

        value = (byte)channel;
        return true;
    }

    private static SpriteBlendMode ReadBlendMode(JToken token)
    {
        if (token.Type == JTokenType.String)
        {
            var name = token.Value<string>();
            foreach (var candidate in Enum.GetValues<SpriteBlendMode>())
            {
                if (string.Equals(candidate.ToString(), name, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }
        }

        throw new InvalidDataException(
            $"{nameof(StaticSpriteComponent)}: invalid 'blend_mode' {token.ToString(Newtonsoft.Json.Formatting.None)}: expected one of {string.Join(", ", Enum.GetNames<SpriteBlendMode>())}.");
    }

    /// <summary>Resolves a sprite data by its catalog name, then holds it through a handle (P6).</summary>
    private void LoadSpriteData(string spriteDataName)
    {
        var assetContentManager = Owner.World.Game.AssetContentManager;

        var assetInfo = AssetCatalog.Get(spriteDataName);
        if (assetInfo == null)
        {
            ReleaseSpriteAssetHandles();
            _spriteData = null;
            throw new InvalidDataException($"{nameof(StaticSpriteComponent)}: sprite '{spriteDataName}' is not in the asset catalog.");
        }

        // The new sprite is made before the current one is given back: a sprite that fails to load leaves the current one in place.
        var newHandle = assetContentManager.Acquire<SpriteData>(assetInfo.Id);
        Sprite newSprite;
        try
        {
            newSprite = Sprite.Create(newHandle.Asset, assetContentManager);
        }
        catch
        {
            newHandle.Dispose();
            throw;
        }

        ReleaseSpriteAssetHandles();
        _spriteDataHandle = newHandle;
        _spriteData = newHandle.Asset;
        _spriteDataName = spriteDataName;
        if (_spriteData.AssetId != Guid.Empty)
        {
            SpriteAssetId = _spriteData.AssetId;
        }

        _sprite = newSprite;
        RemoveCollisions();
        AddCollisions();
        IsBoundingBoxDirty = true;
    }

    public bool ReloadSpriteAsset(Guid spriteAssetId, SpriteData spriteData)
    {
        ArgumentNullException.ThrowIfNull(spriteData);

        bool isMatchingSprite = SpriteAssetId == spriteAssetId
            || (_spriteData != null
                && (_spriteData.AssetId == spriteAssetId
                    || _spriteData.Id == spriteAssetId
                    || string.Equals(_spriteData.Name, spriteData.Name, StringComparison.Ordinal)));
        if (!isMatchingSprite)
        {
            return false;
        }

        // spriteData is supplied directly by the reload service, not resolved through Acquire here: give
        // back whatever sprite data hold this component took itself, so it does not keep pinning a stale
        // instance the manager may have already replaced.
        _spriteDataHandle?.Dispose();
        _spriteDataHandle = null;
        _spriteData = spriteData;
        if (spriteData.AssetId != Guid.Empty)
        {
            SpriteAssetId = spriteData.AssetId;
        }

        RemoveCollisions();

        var assetContentManager = Owner?.World?.Game?.AssetContentManager;
        if (assetContentManager != null)
        {
            _sprite?.Dispose();
            _sprite = Sprite.Create(spriteData, assetContentManager);
        }

        if (Owner?.IsEnabled != false)
        {
            AddCollisions();
        }

        IsBoundingBoxDirty = true;
        return true;
    }

    private void AddCollisions()
    {
        foreach (var collisionShape in _spriteData.CollisionShapes)
        {
            var collisionObject = SpriteCollisionHelper.CreateCollisionBody(collisionShape, LocalScale, WorldMatrixNoScale, _physicsWorldContext, this);
            if (collisionObject != null)
            {
                SpriteCollisionHelper.UpdateBodyTransformation(Position, Orientation, Scale,
                    collisionObject, collisionShape, _spriteData.Origin, _spriteData.PositionInTexture);
                _physicsWorldContext.AddCollisionObject(collisionObject);
                _collisionObjects.Add(new(collisionShape, collisionObject));
            }
        }
    }

    public void RemoveCollisions()
    {
        foreach (var (_, collisionObject) in _collisionObjects)
        {
            _physicsWorldContext.RemoveCollisionObject(collisionObject);
            _physicsWorldContext.ClearCollisionDataFrom(this);
        }
    }

    public void TryLoadSpriteData(string spriteDataName)
    {
        if (spriteDataName == null)
        {
            return;
        }

        LoadSpriteData(spriteDataName);
    }

}