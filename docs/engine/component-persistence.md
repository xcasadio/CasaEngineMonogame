# Component persistence

How a component's data reaches a `.world` or `.entity` file and comes back.

## Loading

`ElementFactory.Load<T>(JObject)` creates the component from its `type` key, then calls the component's `Load(JObject)`, then reads its properties marked `[SerializedProperty]` (`SerializedProperties.Load`).

Every component node goes through it: the root component and the `components` of an entity, the `children_component` of a scene component, entity assets, inline entities of a world, and Play snapshots.

## Saving (editor)

`EditorEntityJsonSerializer` writes each component with the writer of its engine type (the `switch` of `SaveComponentCore`), then writes its marked properties (`EditorSerializedPropertyWriter`). The runtime never writes assets (AGENTS.md §9.9).

## Marking the properties of a game component

```csharp
public class RaceTrackRoadComponent : EntityComponent
{
    [SerializedProperty("track_asset_id")]
    public Guid TrackAssetId { get; set; }

    [SerializedProperty("width_scale")]
    public float WidthScale { get; set; } = 1f;

    public RaceTrackRoadComponent() { }

    private RaceTrackRoadComponent(RaceTrackRoadComponent other) : base(other)
    {
        SerializedProperties.Copy(other, this);
    }

    public override EntityComponent Clone() => new RaceTrackRoadComponent(this);
}
```

- Supported types: `bool`, `int`, `float`, `double`, `string`, `Guid`, enums (written by name), `Vector2`, `Vector3`, `Vector4`, `Quaternion` (`{x,y,z,w}`), `Color` (`{r,g,b,a}` bytes).
- The property must be public with a public getter and setter, and its JSON key unique in the type.
- A missing key keeps the property's default: adding a property needs no migration of existing files.
- An unreadable value (wrong type, unknown enum name) is logged with the type, the key and the component, and the property keeps its value.
- `Clone()` must copy the marked properties (`SerializedProperties.Copy`), otherwise entity templates and editor duplicates lose them.
- The component needs a public parameterless constructor and a type name unique among the loaded assemblies, since `ElementFactory` resolves types by simple name.

Sample: `Projects/CasaEngine.RPGDemo/Components/SampleAnnotatedComponent.cs`, on the `AnnotatedComponentSample` entity of `Projects/RPGDemo/DefaultWorld.world`.

## Limits

- No lists, no nested objects. A component that needs them overrides `Load` and needs its own editor writer.
- Decision: [ADR-0071](../decisions/0071-serialized-properties-of-game-components.md).
