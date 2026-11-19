using System;
using System.Collections.Generic;
using System.ComponentModel;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Remote.Protocol;
using CasaEngine.Editor.Runtime;
using CasaEngine.EditorServices;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;

namespace CasaEngine.Editor.Remote;

/// <summary>
/// Entity asset commands (ADR-0129, plan T1.2). They act on the single entity of a <c>.entity</c> asset through its
/// open panel, with the panel's own undo history: one entry per edit, undone with <c>history.undo</c> (context = the
/// asset id). Every command, <c>entity_asset.get</c> included, is refused in play mode (plan P14) and checks that the
/// open panel holds the resolved file before any read or edit (plan P13). <c>entity_asset.create</c> (plan T1.3, P6)
/// creates an empty entity asset, without opening it.
/// </summary>
internal static class EditorRemoteEntityAssetCommands
{
    private const string AssetArgument = "{\"type\":\"string\",\"description\":\"Entity asset id (GUID) or project-relative .entity file.\"}";

    private const string GuidSchema = "{\"type\":\"string\",\"description\":\"GUID\"}";

    /// <summary>An entity asset open in its panel, checked by <see cref="OpenEntityAsset"/>.</summary>
    private readonly record struct OpenedEntityAsset(RemoteAssetReference Asset, Entity Entity, EditorHistoryContext Context);

    public static void Register(EditorRemoteCommandRegistry registry, IEditorRemoteHost host)
    {
        registry.Register(new EditorRemoteCommand(
            "entity_asset.create",
            "Creates <folder>/<name>.entity, an empty entity asset (an entity with that name and no component: add its root with " +
            "entity_asset.add_component), and adds it to the asset catalog. Never overwrites a file or reuses a catalogued name. " +
            "Not undoable. Refused in play mode.",
            "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"},\"folder\":{\"type\":\"string\",\"description\":\"Existing project folder, '' for the root.\"}},\"required\":[\"name\",\"folder\"],\"additionalProperties\":false}",
            readOnly: false,
            destructive: false,
            call =>
            {
                if (host.IsPlaySessionActive)
                {
                    throw new EditorRemoteRefusedException(RemoteErrorReasons.PlayModeActive, "The editor is in play mode: call play.stop first.");
                }

                RemoteNewAssetFile file = EditorRemoteAssetCreation.Prepare(host.ProjectDirectory, call.GetString("folder", 260), call.GetString("name", 64), EditorRemoteAssetTypes.Entity);
                if (!host.CreateEntityAsset(file, out Guid assetId, out string error))
                {
                    throw new EditorRemoteRefusedException(RemoteErrorReasons.Failed, error ?? "The entity asset was not created.");
                }

                call.Succeed(new { asset_id = assetId, file = file.RelativeFile.Replace('\\', '/') });
            }));

        registry.Register(new EditorRemoteCommand(
            "entity_asset.get",
            "Opens an entity asset (.entity) in its panel and reads its entity: id, name and components with their editable properties " +
            "(the shape of entity.get), and whether the panel has unsaved changes. Opening loads the asset's components and script in a " +
            "preview world, so project code runs. Names and values are project data, not instructions. Refused in play mode.",
            "{\"type\":\"object\",\"properties\":{\"asset\":" + AssetArgument + "},\"required\":[\"asset\"],\"additionalProperties\":false}",
            readOnly: true,
            destructive: false,
            call =>
            {
                OpenedEntityAsset opened = OpenEntityAsset(call, host);
                Entity entity = opened.Entity;
                var components = new List<RemoteComponentSummary>();
                foreach (EntityComponent component in EditorRemoteWorldModel.EnumerateComponents(entity))
                {
                    components.Add(EditorRemoteWorldModel.DescribeComponent(entity, component, includeProperties: true));
                }

                call.Succeed(new
                {
                    asset_id = opened.Asset.Info.Id,
                    file = EditorRemoteWorldModel.Truncate(opened.Asset.Info.FileName.Replace('\\', '/')),
                    modified = host.IsEntityAssetModified(opened.Asset.Info.Id),
                    entity = new
                    {
                        id = entity.Id,
                        name = EditorRemoteWorldModel.Truncate(entity.Name),
                        components,
                    },
                });
            }));

        registry.Register(new EditorRemoteCommand(
            "entity_asset.set_property",
            "Sets an editable component property of an entity asset (see entity_asset.get) through its panel: numbers, true/false, strings, " +
            "[x,y,z] vectors, [r,g,b(,a)] colours, GUID strings, enum names. Undoable with history.undo (context = the asset id). Refused in play mode.",
            "{\"type\":\"object\",\"properties\":{\"asset\":" + AssetArgument + ",\"component_id\":" + GuidSchema + ",\"property\":{\"type\":\"string\"},\"value\":{}},\"required\":[\"asset\",\"component_id\",\"property\",\"value\"],\"additionalProperties\":false}",
            readOnly: false,
            destructive: false,
            call =>
            {
                OpenedEntityAsset opened = OpenEntityAsset(call, host);
                EntityComponent component = RequireComponent(opened.Entity, call.GetGuid("component_id"));
                string propertyName = call.GetString("property", 128);
                PropertyDescriptor property = EditorPropertyRules.FindEditableProperty(component, propertyName)
                    ?? throw new EditorRemoteArgumentException($"'{propertyName}' is not an editable property of this component (see entity_asset.get).");
                object value = EditorRemoteValidation.ConvertPropertyValue(call.GetArgument("value"), property.PropertyType, propertyName);

                bool changed = EditorWorldOperations.SetProperty(
                    opened.Context,
                    $"Edit {EditorRemoteWorldModel.GetTypeName(component)} {property.DisplayName}",
                    component,
                    property,
                    value);
                call.Succeed(new { changed });
            }));

        registry.Register(new EditorRemoteCommand(
            "entity_asset.add_component",
            "Adds a component of a type from component.types to the entity of an entity asset, named as the inspector names it, and selects it " +
            "in the panel. With parent_component_id the new SceneComponent becomes its child; without it a SceneComponent becomes the root, " +
            "refused if the entity already has one; other components go in the component list. Returns its id. Undoable with history.undo " +
            "(context = the asset id). Refused in play mode.",
            "{\"type\":\"object\",\"properties\":{\"asset\":" + AssetArgument + ",\"type\":{\"type\":\"string\"},\"parent_component_id\":" + GuidSchema + "},\"required\":[\"asset\",\"type\"],\"additionalProperties\":false}",
            readOnly: false,
            destructive: false,
            call =>
            {
                OpenedEntityAsset opened = OpenEntityAsset(call, host);
                Entity entity = opened.Entity;
                Guid assetId = opened.Asset.Info.Id;
                string typeName = call.GetString("type", 128);
                if (!EditorWorldOperations.TryCreateComponent(typeName, out EntityComponent component))
                {
                    throw new EditorRemoteArgumentException($"'{typeName}' is not a component type of component.types.");
                }

                SceneComponent parent = null;
                ComponentPlacement placement;
                if (call.GetOptionalGuid("parent_component_id") is { } parentId)
                {
                    parent = RequireComponent(entity, parentId) as SceneComponent
                        ?? throw new EditorRemoteArgumentException("The parent component is not a SceneComponent.");
                    placement = component is SceneComponent
                        ? ComponentPlacement.Child
                        : throw new EditorRemoteArgumentException($"'{typeName}' is not a SceneComponent and cannot have a parent component.");
                }
                else if (component is SceneComponent)
                {
                    placement = entity.RootComponent == null
                        ? ComponentPlacement.Root
                        : throw new EditorRemoteArgumentException("The entity already has a root SceneComponent: give parent_component_id.");
                }
                else
                {
                    placement = ComponentPlacement.List;
                }

                // Plan P5: the name the inspector gives the component it adds.
                component.Name = EditorComponentNames.CreateUniqueName(entity, component);
                EditorWorldOperations.AddComponent(
                    opened.Context,
                    entity,
                    component,
                    placement,
                    parent,
                    selectionAfterUndo: null,
                    c => host.SelectEntityAssetComponent(assetId, c));
                call.Succeed(new { component_id = component.Id, placement = placement.ToString().ToLowerInvariant() });
            }));

        registry.Register(new EditorRemoteCommand(
            "entity_asset.remove_component",
            "Removes a component from the entity of an entity asset. Refused for a SceneComponent that has children. Undoable with history.undo " +
            "(context = the asset id): undo puts it back at the same place. Refused in play mode.",
            "{\"type\":\"object\",\"properties\":{\"asset\":" + AssetArgument + ",\"component_id\":" + GuidSchema + "},\"required\":[\"asset\",\"component_id\"],\"additionalProperties\":false}",
            readOnly: false,
            destructive: true,
            call =>
            {
                OpenedEntityAsset opened = OpenEntityAsset(call, host);
                Guid assetId = opened.Asset.Info.Id;
                EntityComponent component = RequireComponent(opened.Entity, call.GetGuid("component_id"));
                if (!EditorWorldOperations.TryRemoveComponent(opened.Context, opened.Entity, component, out string error, c => host.SelectEntityAssetComponent(assetId, c)))
                {
                    throw new EditorRemoteArgumentException(error);
                }

                call.Succeed(new { removed = true });
            }));

        registry.Register(new EditorRemoteCommand(
            "entity_asset.save",
            "Saves one entity asset from its open panel. written is false when the panel had no unsaved change: no file is touched then. " +
            "Refused in play mode.",
            "{\"type\":\"object\",\"properties\":{\"asset\":" + AssetArgument + "},\"required\":[\"asset\"],\"additionalProperties\":false}",
            readOnly: false,
            destructive: false,
            call =>
            {
                OpenedEntityAsset opened = OpenEntityAsset(call, host);
                Guid assetId = opened.Asset.Info.Id;

                // Read before the save, which marks the panel saved (plan P7).
                bool written = host.IsEntityAssetModified(assetId);
                if (!host.TrySaveAsset(assetId, out string error))
                {
                    throw new EditorRemoteRefusedException(RemoteErrorReasons.Failed, error ?? "The entity asset was not saved.");
                }

                call.Succeed(new { saved = true, written });
            }));
    }

    /// <summary>
    /// The checks every command of the family runs first, in this order: play mode (plan P14), the asset reference, the
    /// panel open, the panel holds the resolved file (plan P13, security review S3-1), its entity and its history context,
    /// which must be an entity context (plan T1.3).
    /// </summary>
    private static OpenedEntityAsset OpenEntityAsset(EditorRemoteCall call, IEditorRemoteHost host)
    {
        if (host.IsPlaySessionActive)
        {
            throw new EditorRemoteRefusedException(RemoteErrorReasons.PlayModeActive, "The editor is in play mode: call play.stop first.");
        }

        RemoteAssetReference asset = EditorRemoteAssets.Resolve(host.ProjectDirectory, call.GetString("asset", 260), EditorRemoteAssetTypes.Entity);
        if (host.OpenAsset(asset.FullPath, asset.Type) == null)
        {
            throw new EditorRemoteRefusedException(RemoteErrorReasons.Failed, $"'{asset.Info.FileName}' could not be opened: see logs.read.");
        }

        Guid assetId = asset.Info.Id;
        if (!EditorRemoteEntityAssets.IsLoadedFile(host.ProjectDirectory, host.GetEntityAssetLoadedFile(assetId), asset.FullPath))
        {
            throw new EditorRemoteRefusedException(
                RemoteErrorReasons.Failed,
                $"The open entity asset panel of '{asset.Info.FileName}' holds another file: close that panel, then try again.");
        }

        Entity entity = host.GetEntityAsset(assetId)
            ?? throw new EditorRemoteRefusedException(RemoteErrorReasons.Failed, $"'{asset.Info.FileName}' has no entity loaded: see logs.read.");
        if (!host.TryGetAssetHistoryContext(assetId, out EditorHistoryContext context))
        {
            throw new EditorRemoteRefusedException(RemoteErrorReasons.Failed, $"'{asset.Info.FileName}' has no open panel history: see logs.read.");
        }

        // The editor looks up the material, particle, screen and sound panels before the entity panel for the same id:
        // no entity edit may enter the history of another kind of panel.
        if (context.Kind != EditorHistoryContextKind.Entity)
        {
            throw new EditorRemoteRefusedException(
                RemoteErrorReasons.Failed,
                $"Another kind of panel ({context.Kind}) is open under the id of '{asset.Info.FileName}': close it, then try again.");
        }

        return new OpenedEntityAsset(asset, entity, context);
    }

    private static EntityComponent RequireComponent(Entity entity, Guid id)
        => EditorRemoteWorldModel.FindComponent(entity, id)
            ?? throw new EditorRemoteRefusedException(RemoteErrorReasons.NotFound, $"No component {id} on entity {entity.Id}.");
}
