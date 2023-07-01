using System.ComponentModel;
using System.Text.Json;
using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Helpers;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.UserInterface.Controls;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using XNAGizmo;
using BoundingBox = Microsoft.Xna.Framework.BoundingBox;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace CasaEngine.Framework.Entities;

public class Entity : ISaveLoad
#if EDITOR
    , ITransformable
#endif
{
    public const int EntityNotRegistered = -1;

    [Category("Object"), ReadOnly(true)]
    public int Id { get; set; } = EntityNotRegistered;

    [Category("Object")]
    public string Name { get; set; } = string.Empty;

    [Browsable(false)]
    public Entity? Parent { get; set; }

    [Browsable(false)]
    public ComponentManager ComponentManager { get; }

    [Category("Object")]
    public Coordinates Coordinates { get; } = new();

    [Category("Object")]
    public bool IsEnabled { get; set; } = true;

    [Category("Object")]
    public bool IsVisible { get; set; } = true;

    [Browsable(false)]
    public bool ToBeRemoved { get; set; }

    [Category("Object"), ReadOnly(true)]
    public bool IsTemporary { get; internal set; }

    public Entity()
    {
        ComponentManager = new ComponentManager(this);
    }

    public void Initialize(CasaEngineGame game)
    {
        ComponentManager.Initialize(game);
    }

    public void Update(float elapsedTime)
    {
        if (IsEnabled == false)
        {
            return;
        }

        ComponentManager.Update(elapsedTime);
    }

    public void Draw()
    {
        if (IsVisible == false)
        {
            return;
        }

        ComponentManager.Draw();
    }

    public Entity Clone()
    {
        throw new NotImplementedException();
    }

    public void Destroy() { }

    public void CopyFrom(Entity ob)
    {
        IsTemporary = ob.IsTemporary;
    }

    public void Load(JsonElement element)
    {
        var version = element.GetJsonPropertyByName("version").Value.GetInt32();
        Name = element.GetJsonPropertyByName("name").Value.GetString();
        Id = element.GetJsonPropertyByName("id").Value.GetInt32();

        foreach (var item in element.GetJsonPropertyByName("components").Value.EnumerateArray())
        {
            ComponentManager.Components.Add(ComponentLoader.Load(this, item));
        }

        var jsonCoordinate = element.GetJsonPropertyByName("coordinates").Value;
        Coordinates.Load(jsonCoordinate);
    }

    public virtual void Load(XmlElement el, SaveOption option)
    {
        var rootNode = el.SelectSingleNode("Entity");
        var loadedVersion = int.Parse(rootNode.Attributes["version"].Value);
    }

    public virtual void Load(BinaryReader br, SaveOption option)
    {
        var loadedVersion = br.ReadUInt32();
        //int id = br_.ReadInt32();
        //TODO id
    }

    public void ScreenResized(int width, int height)
    {
        foreach (var component in ComponentManager.Components)
        {
            component.ScreenResized(width, height);
        }
    }

#if EDITOR
    public event EventHandler? PositionChanged;
    public event EventHandler? OrientationChanged;
    public event EventHandler? ScaleChanged;

    private static readonly int Version = 1;

    public virtual void Save(XmlElement el, SaveOption option)
    {
        var rootNode = el.OwnerDocument.CreateElement("Entity");
        el.AppendChild(rootNode);
        el.OwnerDocument.AddAttribute(rootNode, "version", Version.ToString());
    }

    public virtual void Save(BinaryWriter bw, SaveOption option)
    {
        bw.Write(Version);
    }

    public void Save(JObject jObject)
    {
        jObject.Add("version", 1);
        jObject.Add("id", Id);
        jObject.Add("name", Name);

        var coordinatesObject = new JObject();
        Coordinates.Save(coordinatesObject);
        jObject.Add("coordinates", coordinatesObject);

        var componentsJArray = new JArray();
        foreach (var component in ComponentManager.Components)
        {
            JObject componentObject = new();
            component.Save(componentObject);
            componentsJArray.Add(componentObject);
        }
        jObject.Add("components", componentsJArray);
    }

    public Vector3 Position
    {
        get => Coordinates.LocalPosition;
        set
        {
            Coordinates.LocalPosition = value;
            PositionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public Vector3 Scale
    {
        get => Coordinates.LocalScale;
        set
        {
            Coordinates.LocalScale = value;
            ScaleChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public Quaternion Orientation
    {
        get => Coordinates.LocalRotation;
        set
        {
            Coordinates.LocalRotation = value;
            OrientationChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public Vector3 Forward => Vector3.Transform(Vector3.Forward, Orientation);
    public Vector3 Up => Vector3.Transform(Vector3.Up, Orientation);

    //TODO : compute real BoundingBox

    public BoundingBox BoundingBox => ComputeBoundingBox();

    private BoundingBox ComputeBoundingBox()
    {
        var min = Vector3.One * int.MaxValue;
        var max = Vector3.One * int.MinValue;

        foreach (var component in ComponentManager.Components)
        {
            if (component is IBoundingBoxComputable boundingBoxComputable)
            {
                var boundingBox = boundingBoxComputable.BoundingBox;
                min = Vector3.Min(min, boundingBox.Min);
                max = Vector3.Max(max, boundingBox.Max);
            }
        }

        return new BoundingBox(min, max);
    }
#endif
}