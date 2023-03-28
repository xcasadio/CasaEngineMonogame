using System.ComponentModel;
using System.Text.Json;
using System.Xml;
using CasaEngine.Core.Design;
using CasaEngine.Core.Extension;
using CasaEngine.Core.Helpers;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.Gameplay.Actor;
using Microsoft.Xna.Framework;
using XNAGizmo;
using ICloneable = CasaEngine.Framework.Gameplay.Design.ICloneable;

namespace CasaEngine.Framework.Entities;

public class Entity : ISaveLoad, ICloneable
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
    public bool Temporary { get; internal set; }

    public Entity()
    {
        ComponentManager = new ComponentManager(this);
    }

    protected Entity(XmlElement el, SaveOption option)
    {
        throw new InvalidOperationException("To be removed");
        Load(el, option);
    }

    public void Initialize()
    {
        ComponentManager.Initialize();
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
        Temporary = ob.Temporary;
    }

    public void Load(JsonElement element)
    {
        var version = element.GetJsonPropertyByName("Version").Value.GetInt32();
        Name = element.GetJsonPropertyByName("Name").Value.GetString();
        Id = element.GetJsonPropertyByName("Id").Value.GetInt32();

        foreach (var item in element.GetJsonPropertyByName("Components").Value.EnumerateArray())
        {
            ComponentManager.Components.Add(ComponentLoader.Load(this, item));
        }

        var jsonCoordinate = element.GetJsonPropertyByName("Coordinates").Value;

        Coordinates.LocalPosition = new Vector3(
            jsonCoordinate.GetJsonPropertyByName("PositionX").Value.GetSingle(),
            jsonCoordinate.GetJsonPropertyByName("PositionY").Value.GetSingle(),
            jsonCoordinate.GetJsonPropertyByName("PositionZ").Value.GetSingle());

        Coordinates.LocalCenterOfRotation = new Vector3(
            jsonCoordinate.GetJsonPropertyByName("CenterOfRotationX").Value.GetSingle(),
            jsonCoordinate.GetJsonPropertyByName("CenterOfRotationY").Value.GetSingle(),
            jsonCoordinate.GetJsonPropertyByName("CenterOfRotationZ").Value.GetSingle());

        Coordinates.LocalScale = new Vector3(
            jsonCoordinate.GetJsonPropertyByName("ScaleX").Value.GetSingle(),
            jsonCoordinate.GetJsonPropertyByName("ScaleY").Value.GetSingle(),
            jsonCoordinate.GetJsonPropertyByName("ScaleZ").Value.GetSingle());

        Coordinates.LocalRotation = new Quaternion(
            jsonCoordinate.GetJsonPropertyByName("RotationX").Value.GetSingle(),
            jsonCoordinate.GetJsonPropertyByName("RotationY").Value.GetSingle(),
            jsonCoordinate.GetJsonPropertyByName("RotationZ").Value.GetSingle(),
            jsonCoordinate.GetJsonPropertyByName("RotationW").Value.GetSingle());
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

    public void Save(string fileName, SaveOption option)
    {
        string jsonString = JsonSerializer.Serialize(
            new
            {
                Version = 1,
                Name,
                Id,
                Components = ComponentManager.Components.Select(x => new { x.Type }),
                Coordinates = new
                {
                    PositionX = Coordinates.LocalPosition.X,
                    PositionY = Coordinates.LocalPosition.Y,
                    PositionZ = Coordinates.LocalPosition.Z,

                    CenterOfRotationX = Coordinates.LocalCenterOfRotation.X,
                    CenterOfRotationY = Coordinates.LocalCenterOfRotation.Y,
                    CenterOfRotationZ = Coordinates.LocalCenterOfRotation.Z,

                    ScaleX = Coordinates.LocalScale.X,
                    ScaleY = Coordinates.LocalScale.Y,
                    ScaleZ = Coordinates.LocalScale.Z,

                    RotationX = Coordinates.LocalRotation.X,
                    RotationY = Coordinates.LocalRotation.Y,
                    RotationZ = Coordinates.LocalRotation.Z,
                    RotationW = Coordinates.LocalRotation.W
                }
            }
            , new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(fileName, jsonString);
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
        Vector3 min = Vector3.One * int.MaxValue, max = Vector3.One * int.MinValue;

        var meshComponent = ComponentManager.GetComponent<StaticMeshComponent>();

        if (meshComponent?.Mesh != null)
        {
            var vertices = meshComponent.Mesh.GetVertices();

            foreach (var vertex in vertices)
            {
                var positionTransformed = Vector3.Transform(vertex.Position, Coordinates.WorldMatrix);
                //var positionTransformed = Position - Vector3.Transform(vertex.Position, Orientation) * Scale;
                min = Vector3.Min(min, positionTransformed);
                max = Vector3.Max(max, positionTransformed);
            }
        }
        else
        {
            const float length = 0.5f;
            //var min = Position - Vector3.Transform(Vector3.One * length, Orientation) * Scale;
            //var max = Position + Vector3.Transform(Vector3.One * length, Orientation) * Scale;
            min = Vector3.Transform(-(Vector3.One * length), Coordinates.WorldMatrix);
            max = Vector3.Transform((Vector3.One * length), Coordinates.WorldMatrix);
        }

        return new BoundingBox(min, max);
    }

    public float? Select(Ray selectionRay)
    {
        if (GameInfo.Instance.ActiveCamera?.Owner == this)
        {
            return null;
        }

        return selectionRay.Intersects(BoundingBox);
    }
#endif
}