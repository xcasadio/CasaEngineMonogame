using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

/// <summary>
/// Stands for a component whose <c>type</c> no loaded assembly declares (a gameplay DLL that is missing, outdated or
/// renamed its class). The load reports the error and keeps going; this placeholder keeps the original JSON node, which
/// the editor writes back, so that saving a world or an entity loses nothing (ADR-0072).
/// <para/>It behaves as an empty scene component: it keeps the node's transform, and its children stay in the node
/// without being created.
/// </summary>
public sealed class UnresolvedComponent : SceneComponent
{
    private JObject _sourceNode = new();

    public UnresolvedComponent()
    {
    }

    private UnresolvedComponent(UnresolvedComponent other) : base(other)
    {
        _sourceNode = (JObject)other._sourceNode.DeepClone();
        UnresolvedTypeName = other.UnresolvedTypeName;
    }

    /// <summary>The <c>type</c> the node names.</summary>
    public string UnresolvedTypeName { get; private set; }

    /// <summary>A copy of the node this component was loaded from.</summary>
    public JObject GetSourceNode() => (JObject)_sourceNode.DeepClone();

    protected override bool AllowsMissingSceneData => true;

    public override void Load(JObject element)
    {
        _sourceNode = (JObject)element.DeepClone();
        UnresolvedTypeName = element["type"]?.Type == JTokenType.String ? element["type"]!.Value<string>() : null;

        //  Id, name and transform only: the children stay in the kept node instead of being created under a component
        //  that does nothing.
        var sceneData = new JObject
        {
            ["id"] = element["id"],
            ["name"] = element["name"],
        };
        var transformKey = element.ContainsKey("coordinates") ? "coordinates" : "local_transform";
        if (element[transformKey] is JObject transformNode)
        {
            sceneData[transformKey] = transformNode.DeepClone();
        }

        base.Load(sceneData);
    }

    public override EntityComponent Clone() => new UnresolvedComponent(this);
}
