using System.Text.Json;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.SceneManagement;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using BoundingBox = Microsoft.Xna.Framework.BoundingBox;
using Vector3 = Microsoft.Xna.Framework.Vector3;

#if EDITOR
using XNAGizmo;
#endif

namespace CasaEngine.Framework.Entities;

public class Entity : EntityBase
{
    private Entity? _parent;

    public Entity? Parent
    {
        get => _parent;
        set
        {
            _parent = value;
            Coordinates.Parent = _parent?.Coordinates;
        }
    }

    public ComponentManager ComponentManager { get; } = new();

    public override void Initialize(CasaEngineGame game)
    {
        base.Initialize(game);
        ComponentManager.Initialize(this);
    }

    protected override void UpdateInternal(float elapsedTime)
    {
        ComponentManager.Update(elapsedTime);
    }

    protected override void DrawInternal()
    {
        ComponentManager.Draw();
    }

    public Entity Clone()
    {
        var entity = new Entity();
        entity.CopyFrom(this);
        return entity;
    }

    public void CopyFrom(Entity entity)
    {
        ComponentManager.CopyFrom(entity.ComponentManager);
        Coordinates.CopyFrom(entity.Coordinates);
        Parent = entity.Parent;

        base.CopyFrom(entity);
    }

    public override void Load(JsonElement element, SaveOption option)
    {
        base.Load(element, option);

        foreach (var item in element.GetJsonPropertyByName("components").Value.EnumerateArray())
        {
            ComponentManager.Components.Add(GameSettings.ComponentLoader.Load(item));
        }
    }

    public override void ScreenResized(int width, int height)
    {
        var components = ComponentManager.Components;

        for (var index = 0; index < components.Count; index++)
        {
            components[index].ScreenResized(width, height);
        }

        base.ScreenResized(width, height);
    }

    protected override void OnEnabledValueChange()
    {
        var components = ComponentManager.Components;

        for (var index = 0; index < components.Count; index++)
        {
            components[index].OnEnabledValueChange();
        }

        base.OnEnabledValueChange();
    }

    public override void OnBeginPlay(World.World world)
    {
        foreach (var component in ComponentManager.Components)
        {
            if (component is GamePlayComponent gamePlayComponent)
            {
                gamePlayComponent.ExternalComponent?.OnBeginPlay(world);
            }
        }

        base.OnBeginPlay(world);
    }

    public override void OnEndPlay(World.World world)
    {
        foreach (var component in ComponentManager.Components)
        {
            if (component is GamePlayComponent gamePlayComponent)
            {
                gamePlayComponent.ExternalComponent?.OnEndPlay(world);
            }
        }

        base.OnEndPlay(world);
    }

    public override void Accept(CullVisitor cullVisitor)
    {
        cullVisitor.Apply(this);
    }

#if EDITOR
    private static readonly int Version = 1;

    public override void Save(JObject jObject, SaveOption option)
    {
        base.Save(jObject, option);

        //jObject.Add("version", Version);

        var componentsJArray = new JArray();
        foreach (var component in ComponentManager.Components)
        {
            JObject componentObject = new();
            component.Save(componentObject, option);
            componentsJArray.Add(componentObject);
        }
        jObject.Add("components", componentsJArray);
    }

    protected override BoundingBox ComputeBoundingBox()
    {
        var min = Vector3.One * int.MaxValue;
        var max = Vector3.One * int.MinValue;
        bool found = false;

        foreach (var component in ComponentManager.Components)
        {
            if (component is IBoundingBoxable boundingBoxComputable)
            {
                var boundingBox = boundingBoxComputable.BoundingBox;
                min = Vector3.Min(min, boundingBox.Min);
                max = Vector3.Max(max, boundingBox.Max);
                found = true;
            }
        }

        return found ? new BoundingBox(min, max) : new BoundingBox(Vector3.One / 2f, Vector3.One / 2f);
    }
#endif
}