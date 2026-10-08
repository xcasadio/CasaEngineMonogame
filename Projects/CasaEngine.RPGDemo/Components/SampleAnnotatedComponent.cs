using System;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;

namespace CasaEngine.RPGDemo.Components;

/// <summary>
/// Sample of the serialized properties of game components (ADR-0071): a component declared by the gameplay DLL whose
/// annotated properties the engine reads when it loads a world and the editor writes when it saves one, Play snapshots
/// included. It has no runtime behaviour; DefaultWorld.world carries one on its "AnnotatedComponentSample" entity.
/// </summary>
public class SampleAnnotatedComponent : EntityComponent
{
    [SerializedProperty("label")]
    public string Label { get; set; } = string.Empty;

    [SerializedProperty("weight")]
    public float Weight { get; set; } = 1f;

    [SerializedProperty("tint")]
    public Color Tint { get; set; } = Color.White;

    [SerializedProperty("linked_asset_id")]
    public Guid LinkedAssetId { get; set; }

    public SampleAnnotatedComponent()
    {
    }

    private SampleAnnotatedComponent(SampleAnnotatedComponent other) : base(other)
    {
        //  A clone keeps the serialized values: entity templates and editor duplicates go through Clone().
        SerializedProperties.Copy(other, this);
    }

    public override EntityComponent Clone() => new SampleAnnotatedComponent(this);
}
