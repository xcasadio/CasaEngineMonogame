using System;
using System.ComponentModel;
using System.IO;
using CasaEngine.Editor.History;
using CasaEngine.Editor.Remote.Protocol;
using CasaEngine.Editor.Runtime;
using CasaEngine.Editor.Workspaces;
using CasaEngine.EditorServices.History;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;

namespace CasaEngine.Editor.Remote;

/// <summary>
/// Remote commands that change the project (ADR-0092, plan T3.1). World edits are one undoable entry each in the
/// world history context, whatever panel has the focus, and every command is refused during play mode (plan P5).
/// </summary>
internal static class EditorRemoteEditCommands
{
    /// <summary>History context of the remote world edits and of history.undo / history.redo (plan P5).</summary>
    public static readonly EditorHistoryContext WorldContext = new(EditorHistoryContextKind.World, EditorPanelIds.WorldViewport);

    public const double WorldLoadTimeoutSeconds = 60;

    private const string GuidSchema = "{\"type\":\"string\",\"description\":\"GUID\"}";

    public static void Register(EditorRemoteCommandRegistry registry, IEditorRemoteHost host)
    {
        registry.Register(new EditorRemoteCommand(
            "world.create",
            "Creates <name>.world at the project root, adds it to the asset catalog and opens it. Refused when the current world has unsaved changes. Not undoable.",
            "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\",\"description\":\"Letters, digits, space, '_' or '-', 64 characters at most.\"}},\"required\":[\"name\"],\"additionalProperties\":false}",
            readOnly: false,
            destructive: false,
            call =>
            {
                RequireEditable(host);
                RequireSaved(host);
                string name = call.GetString("name", 64);
                if (!EditorRemoteValidation.IsValidWorldName(name))
                {
                    throw new EditorRemoteArgumentException($"'{name}' is not a valid world name.");
                }

                string fileName = EditorRemoteValidation.GetWorldFileName(name);
                if (!EditorRemoteValidation.TryResolveInsideProject(host.ProjectDirectory, fileName, out string fullPath))
                {
                    throw new EditorRemoteRefusedException(RemoteErrorReasons.NotFound, "No project is open.");
                }

                if (File.Exists(fullPath))
                {
                    throw new EditorRemoteRefusedException(RemoteErrorReasons.InvalidArgument, $"'{fileName}' already exists.");
                }

                if (!host.CreateWorld(name, fileName, out string error) || !host.BeginOpenWorld(fullPath))
                {
                    throw new EditorRemoteRefusedException(RemoteErrorReasons.Failed, error ?? $"'{fileName}' could not be opened.");
                }

                DeferUntilWorldLoaded(call, host, fileName);
            }));

        registry.Register(new EditorRemoteCommand(
            "world.open",
            "Opens a world of the asset catalog by its project-relative file (e.g. 'Level1.world'). Refused when the current world has unsaved changes. Not undoable.",
            "{\"type\":\"object\",\"properties\":{\"file\":{\"type\":\"string\",\"description\":\"Project-relative .world file.\"}},\"required\":[\"file\"],\"additionalProperties\":false}",
            readOnly: false,
            destructive: false,
            call =>
            {
                RequireEditable(host);
                RequireSaved(host);
                string file = call.GetString("file", 260).Replace('\\', '/');
                if (!file.EndsWith(".world", StringComparison.OrdinalIgnoreCase)
                    || !EditorRemoteValidation.TryResolveInsideProject(host.ProjectDirectory, file, out string fullPath))
                {
                    throw new EditorRemoteArgumentException($"'{file}' is not a .world file inside the project.");
                }

                if (!host.BeginOpenWorld(fullPath))
                {
                    throw new EditorRemoteRefusedException(RemoteErrorReasons.NotFound, $"'{file}' is not a world of the asset catalog.");
                }

                DeferUntilWorldLoaded(call, host, file);
            }));

        registry.Register(new EditorRemoteCommand(
            "project.save",
            "Saves the project and the current world. Fails if anything could not be written.",
            null,
            readOnly: false,
            destructive: false,
            call =>
            {
                RequireEditable(host);
                if (string.IsNullOrWhiteSpace(host.ProjectFilePath))
                {
                    throw new EditorRemoteRefusedException(RemoteErrorReasons.NotFound, "No project is open.");
                }

                if (!host.SaveProject(out string error))
                {
                    throw new EditorRemoteRefusedException(RemoteErrorReasons.Failed, error ?? "The project was not saved.");
                }

                call.Succeed(new { saved = true });
            }));

        registry.Register(new EditorRemoteCommand(
            "entity.create",
            "Creates an entity, at the root of the world or under a parent entity. Returns its id. Undoable.",
            "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"},\"parent_id\":" + GuidSchema + "},\"required\":[\"name\"],\"additionalProperties\":false}",
            readOnly: false,
            destructive: false,
            call =>
            {
                RequireEditable(host);
                World world = EditorRemoteReadCommands.RequireWorld(host);
                string name = EditorRemoteValidation.ValidateEntityName(call.GetString("name", EditorRemoteValidation.MaxEntityNameLength));
                Entity parent = call.GetOptionalGuid("parent_id") is { } parentId ? EditorRemoteReadCommands.RequireEntity(host, parentId) : null;
                var entity = new Entity { Name = name };

                EditorWorldOperations.AddEntity(WorldContext, world, entity, parent, host.SelectedEntity, e => host.Select(e, null));
                Completed(call, host, new { entity_id = entity.Id });
            }));

        registry.Register(new EditorRemoteCommand(
            "entity.delete",
            "Deletes an entity and its children. Undoable.",
            EntitySchema(),
            readOnly: false,
            destructive: true,
            call =>
            {
                RequireEditable(host);
                World world = EditorRemoteReadCommands.RequireWorld(host);
                Entity entity = EditorRemoteReadCommands.RequireEntity(host, call.GetGuid("entity_id"));

                EditorWorldOperations.DeleteEntity(WorldContext, world, entity, e => host.Select(e, null));
                Completed(call, host, new { deleted = true });
            }));

        registry.Register(new EditorRemoteCommand(
            "entity.rename",
            "Renames an entity. Undoable.",
            "{\"type\":\"object\",\"properties\":{\"entity_id\":" + GuidSchema + ",\"name\":{\"type\":\"string\"}},\"required\":[\"entity_id\",\"name\"],\"additionalProperties\":false}",
            readOnly: false,
            destructive: false,
            call =>
            {
                RequireEditable(host);
                Entity entity = EditorRemoteReadCommands.RequireEntity(host, call.GetGuid("entity_id"));
                string name = EditorRemoteValidation.ValidateEntityName(call.GetString("name", EditorRemoteValidation.MaxEntityNameLength));

                bool changed = EditorWorldOperations.RenameEntity(WorldContext, entity, name, e => host.Select(e, null));
                Completed(call, host, new { changed });
            }));

        registry.Register(new EditorRemoteCommand(
            "entity.duplicate",
            "Duplicates an entity next to it. Returns the new id. Undoable.",
            EntitySchema(),
            readOnly: false,
            destructive: false,
            call =>
            {
                RequireEditable(host);
                World world = EditorRemoteReadCommands.RequireWorld(host);
                Entity source = EditorRemoteReadCommands.RequireEntity(host, call.GetGuid("entity_id"));
                Entity duplicate = source.Clone();
                duplicate.Name = EditorRemoteValidation.ValidateEntityName(TruncateName(source.Name + " Copy"));

                EditorWorldOperations.DuplicateEntity(WorldContext, world, duplicate, source.Parent, source, e => host.Select(e, null));
                Completed(call, host, new { entity_id = duplicate.Id });
            }));

        registry.Register(new EditorRemoteCommand(
            "component.add",
            "Adds a component of a type from component.types. With parent_component_id the new SceneComponent becomes its child; without it a SceneComponent becomes the root, refused if the entity already has one; other components go in the component list. Returns its id. Undoable.",
            "{\"type\":\"object\",\"properties\":{\"entity_id\":" + GuidSchema + ",\"type\":{\"type\":\"string\"},\"parent_component_id\":" + GuidSchema + "},\"required\":[\"entity_id\",\"type\"],\"additionalProperties\":false}",
            readOnly: false,
            destructive: false,
            call =>
            {
                RequireEditable(host);
                Entity entity = EditorRemoteReadCommands.RequireEntity(host, call.GetGuid("entity_id"));
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

                EditorWorldOperations.AddComponent(WorldContext, entity, component, placement, parent, host.SelectedComponent, c => host.Select(entity, c));
                Completed(call, host, new { component_id = component.Id, placement = placement.ToString().ToLowerInvariant() });
            }));

        registry.Register(new EditorRemoteCommand(
            "component.remove",
            "Removes a component. Refused for a SceneComponent that has children. Undoable: undo puts it back at the same place.",
            "{\"type\":\"object\",\"properties\":{\"entity_id\":" + GuidSchema + ",\"component_id\":" + GuidSchema + "},\"required\":[\"entity_id\",\"component_id\"],\"additionalProperties\":false}",
            readOnly: false,
            destructive: true,
            call =>
            {
                RequireEditable(host);
                Entity entity = EditorRemoteReadCommands.RequireEntity(host, call.GetGuid("entity_id"));
                EntityComponent component = RequireComponent(entity, call.GetGuid("component_id"));
                if (!EditorWorldOperations.TryRemoveComponent(WorldContext, entity, component, out string error, c => host.Select(entity, c)))
                {
                    throw new EditorRemoteArgumentException(error);
                }

                Completed(call, host, new { removed = true });
            }));

        registry.Register(new EditorRemoteCommand(
            "property.set",
            "Sets an editable component property (see entity.get): numbers, true/false, strings, [x,y,z] vectors, [r,g,b(,a)] colours, GUID strings, enum names. Undoable.",
            "{\"type\":\"object\",\"properties\":{\"entity_id\":" + GuidSchema + ",\"component_id\":" + GuidSchema + ",\"property\":{\"type\":\"string\"},\"value\":{}},\"required\":[\"entity_id\",\"component_id\",\"property\",\"value\"],\"additionalProperties\":false}",
            readOnly: false,
            destructive: false,
            call =>
            {
                RequireEditable(host);
                Entity entity = EditorRemoteReadCommands.RequireEntity(host, call.GetGuid("entity_id"));
                EntityComponent component = RequireComponent(entity, call.GetGuid("component_id"));
                string propertyName = call.GetString("property", 128);
                PropertyDescriptor property = EditorPropertyRules.FindEditableProperty(component, propertyName)
                    ?? throw new EditorRemoteArgumentException($"'{propertyName}' is not an editable property of this component (see entity.get).");
                object value = EditorRemoteValidation.ConvertPropertyValue(call.GetArgument("value"), property.PropertyType, propertyName);

                bool changed = EditorWorldOperations.SetProperty(
                    WorldContext,
                    $"Edit {EditorRemoteWorldModel.GetTypeName(component)} {property.DisplayName}",
                    component,
                    property,
                    value);
                Completed(call, host, new { changed });
            }));

        registry.Register(new EditorRemoteCommand(
            "selection.set",
            "Selects an entity, and optionally one of its components, in the editor. Without entity_id clears the selection.",
            "{\"type\":\"object\",\"properties\":{\"entity_id\":" + GuidSchema + ",\"component_id\":" + GuidSchema + "},\"additionalProperties\":false}",
            readOnly: false,
            destructive: false,
            call =>
            {
                Entity entity = call.GetOptionalGuid("entity_id") is { } entityId ? EditorRemoteReadCommands.RequireEntity(host, entityId) : null;
                EntityComponent component = null;
                if (call.GetOptionalGuid("component_id") is { } componentId)
                {
                    component = entity != null
                        ? RequireComponent(entity, componentId)
                        : throw new EditorRemoteArgumentException("component_id needs entity_id.");
                }

                host.Select(entity, component);
                call.Succeed(new { entity_id = entity?.Id, component_id = component?.Id });
            }));

        registry.Register(new EditorRemoteCommand(
            "history.undo",
            "Undoes the last change of the world history (remote commands and world panels alike), whatever panel has the focus.",
            null,
            readOnly: false,
            destructive: false,
            call => StepHistory(call, host, undo: true)));

        registry.Register(new EditorRemoteCommand(
            "history.redo",
            "Redoes the last undone change of the world history.",
            null,
            readOnly: false,
            destructive: false,
            call => StepHistory(call, host, undo: false)));
    }

    private static string EntitySchema()
        => "{\"type\":\"object\",\"properties\":{\"entity_id\":" + GuidSchema + "},\"required\":[\"entity_id\"],\"additionalProperties\":false}";

    private static void RequireEditable(IEditorRemoteHost host)
    {
        if (host.IsPlaySessionActive)
        {
            throw new EditorRemoteRefusedException(RemoteErrorReasons.PlayModeActive, "The editor is in play mode: call play.stop first.");
        }
    }

    private static void RequireSaved(IEditorRemoteHost host)
    {
        if (host.HasUnsavedWorldChanges)
        {
            throw new EditorRemoteRefusedException(RemoteErrorReasons.UnsavedChanges, "The current world has unsaved changes: call project.save first.");
        }
    }

    private static EntityComponent RequireComponent(Entity entity, Guid id)
        => EditorRemoteWorldModel.FindComponent(entity, id)
            ?? throw new EditorRemoteRefusedException(RemoteErrorReasons.NotFound, $"No component {id} on entity {entity.Id}.");

    private static void Completed<T>(EditorRemoteCall call, IEditorRemoteHost host, T result)
    {
        host.RefreshWorldViews();
        call.Succeed(result);
    }

    private static void StepHistory(EditorRemoteCall call, IEditorRemoteHost host, bool undo)
    {
        RequireEditable(host);
        EditorHistoryStack stack = EditorHistoryService.Current.GetOrCreate(WorldContext);
        if (undo ? !stack.CanUndo : !stack.CanRedo)
        {
            call.Succeed(new { done = false });
            return;
        }

        string description = undo ? stack.UndoDescription : stack.RedoDescription;
        if (undo)
        {
            stack.Undo();
        }
        else
        {
            stack.Redo();
        }

        Completed(call, host, new { done = true, description });
    }

    private static void DeferUntilWorldLoaded(EditorRemoteCall call, IEditorRemoteHost host, string fileName)
    {
        string expected = fileName.Replace('\\', '/');
        call.Defer(
            c =>
            {
                World world = host.CurrentWorld;
                if (world == null || !string.Equals(world.FileName?.Replace('\\', '/'), expected, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                c.Succeed(new { world_name = world.Name, file = expected });
                return true;
            },
            WorldLoadTimeoutSeconds);
    }

    private static string TruncateName(string name)
        => name.Length <= EditorRemoteValidation.MaxEntityNameLength ? name : name[..EditorRemoteValidation.MaxEntityNameLength];
}
