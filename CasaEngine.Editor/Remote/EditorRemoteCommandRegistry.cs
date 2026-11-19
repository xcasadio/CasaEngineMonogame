using System;
using System.Collections.Generic;
using System.Text.Json;
using CasaEngine.Editor.Remote.Protocol;

namespace CasaEngine.Editor.Remote;

/// <summary>One command the editor serves (ADR-0092). The handler runs on the game thread.</summary>
internal sealed class EditorRemoteCommand
{
    public EditorRemoteCommand(string name, string description, string inputSchemaJson, bool readOnly, bool destructive, Action<EditorRemoteCall> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(handler);

        using JsonDocument schema = JsonDocument.Parse(string.IsNullOrWhiteSpace(inputSchemaJson) ? EmptyObjectSchema : inputSchemaJson);
        if (schema.RootElement.ValueKind != JsonValueKind.Object
            || !schema.RootElement.TryGetProperty("type", out JsonElement type)
            || type.GetString() != "object")
        {
            throw new ArgumentException($"The input schema of '{name}' must be an object schema.", nameof(inputSchemaJson));
        }

        Name = name;
        Description = description;
        InputSchema = schema.RootElement.Clone();
        ReadOnly = readOnly;
        Destructive = destructive;
        Handler = handler;
    }

    public const string EmptyObjectSchema = "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}";

    public string Name { get; }

    public string Description { get; }

    public JsonElement InputSchema { get; }

    public bool ReadOnly { get; }

    public bool Destructive { get; }

    public Action<EditorRemoteCall> Handler { get; }

    public RemoteCommandDescriptor ToDescriptor() => new()
    {
        Name = Name,
        Description = Description,
        InputSchema = InputSchema,
        ReadOnly = ReadOnly,
        Destructive = Destructive,
    };
}

/// <summary>The commands of the editor, by name. Registration happens on the game thread before the server starts.</summary>
internal sealed class EditorRemoteCommandRegistry
{
    private readonly Dictionary<string, EditorRemoteCommand> _commands = new(StringComparer.Ordinal);
    private readonly List<EditorRemoteCommand> _ordered = new();

    public IReadOnlyList<EditorRemoteCommand> Commands => _ordered;

    public void Register(EditorRemoteCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!_commands.TryAdd(command.Name, command))
        {
            throw new InvalidOperationException($"The remote command '{command.Name}' is already registered.");
        }

        _ordered.Add(command);
    }

    public bool TryGet(string name, out EditorRemoteCommand command) => _commands.TryGetValue(name, out command);

    public RemoteCommandList Describe()
    {
        var descriptors = new List<RemoteCommandDescriptor>(_ordered.Count);
        for (int i = 0; i < _ordered.Count; i++)
        {
            descriptors.Add(_ordered[i].ToDescriptor());
        }

        return new RemoteCommandList { Commands = descriptors };
    }
}
