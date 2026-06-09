using System.Collections.Generic;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Editor.Runtime;

internal sealed class EditorSpritePreviewComponent : SceneComponent, IComponentDrawable, IBoundingBoxable
{
    private SpriteRendererComponent _spriteRendererComponent;
    private SpriteData _spriteData;
    private Sprite _sprite;
    private int _drawInvocationCount;

    public Color Color { get; set; } = Color.White;

    public SpriteEffects SpriteEffect { get; set; } = SpriteEffects.None;

    public IReadOnlyList<string> GetDebugStateSnapshot()
    {
        string boundsText = _spriteData == null
            ? "<none>"
            : SpriteDataBoundsCalculator.CalculateLocalBounds(_spriteData).ToString();
        return
        [
            $"Preview component sprite loaded: {_sprite != null}",
            $"Preview component texture loaded: {_sprite?.Texture?.Resource != null}",
            $"Preview component draw calls: {_drawInvocationCount}",
            $"Preview component bounds: {boundsText}",
        ];
    }

    public override void InitializeWithWorld(CasaEngine.Framework.Scene.World.World world)
    {
        base.InitializeWithWorld(world);
        _spriteRendererComponent = Owner.World.Game.GetGameComponent<SpriteRendererComponent>();
        RebuildSprite();
        IsBoundingBoxDirty = true;
    }

    public void SetSpriteData(SpriteData spriteData)
    {
        _spriteData = spriteData;
        _drawInvocationCount = 0;
        RebuildSprite();
        IsBoundingBoxDirty = true;
    }

    public override EditorSpritePreviewComponent Clone()
    {
        var clone = new EditorSpritePreviewComponent
        {
            Color = Color,
            SpriteEffect = SpriteEffect,
        };
        clone.SetSpriteData(_spriteData);
        return clone;
    }

    public override BoundingBox GetBoundingBox()
    {
        if (_spriteData != null)
        {
            return SpriteDataBoundsCalculator.CalculateLocalBounds(_spriteData).Transform(WorldMatrixWithScale);
        }

        return base.GetBoundingBox();
    }

    public override void Draw(float elapsedTime)
    {
        if (_spriteRendererComponent == null || _sprite == null || _sprite.Texture?.Resource == null)
        {
            return;
        }

        var position = new Vector2(Position.X, Position.Y);
        var scale = new Vector2(Scale.X, Scale.Y);
        _drawInvocationCount++;
        _spriteRendererComponent.DrawSprite(_sprite, position, 0.0f, scale, Color, Position.Z, drawDebug: false, effects: SpriteEffect);
    }

    private void RebuildSprite()
    {
        _sprite = null;
        if (_spriteData == null)
        {
            return;
        }

        var assetContentManager = Owner?.World?.Game?.AssetContentManager;
        if (assetContentManager == null)
        {
            return;
        }

        _sprite = Sprite.Create(_spriteData, assetContentManager);
    }
}