using System.Collections.Generic;
using System.Globalization;
using CasaEngine.EditorServices.ScreenEditor.DocumentModel;
using CasaEngine.EditorServices.ScreenEditor.Inspector;

namespace CasaEngine.EditorServices.ScreenEditor.Xaml;

/// <summary>Describes a single validation problem found in a <see cref="UIScreenDocument"/>.</summary>
public sealed record UIScreenValidationError(
    DocumentNodeId NodeId,
    string NodePath,
    string PropertyName,
    string Message);

/// <summary>
/// Validates a <see cref="UIScreenDocument"/> against the <see cref="UIPropertyRegistry"/>,
/// checking that known property values parse correctly for their declared CLR types.
/// </summary>
public sealed class UIScreenValidator
{
    private readonly UIPropertyRegistry _registry;

    public UIScreenValidator() : this(UIPropertyRegistry.Default)
    {
    }

    public UIScreenValidator(UIPropertyRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>Returns all validation errors found in <paramref name="document"/>.</summary>
    public IReadOnlyList<UIScreenValidationError> Validate(UIScreenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var errors = new List<UIScreenValidationError>();
        if (document.Root != null)
        {
            ValidateNode(document.Root, string.Empty, errors);
        }

        return errors;
    }

    private void ValidateNode(UIScreenNode node, string parentPath, List<UIScreenValidationError> errors)
    {
        var nodePath = BuildNodePath(parentPath, node);

        foreach (var desc in _registry.GetDescriptors(node.ControlType))
        {
            if (!node.Properties.TryGetValue(desc.Name, out var prop))
            {
                continue;
            }

            if (string.Equals(prop.ValueType, "xaml", StringComparison.Ordinal))
            {
                continue;
            }

            var error = ValidateValue(prop.SerializedValue, desc.ValueType);
            if (error != null)
            {
                errors.Add(new UIScreenValidationError(node.Id, nodePath, desc.Name, error));
            }
        }

        foreach (var child in node.Children)
        {
            ValidateNode(child, nodePath, errors);
        }
    }

    private static string BuildNodePath(string parentPath, UIScreenNode node)
    {
        var segment = string.IsNullOrWhiteSpace(node.Name)
            ? node.ControlType
            : $"{node.ControlType}[{node.Name}]";

        return string.IsNullOrEmpty(parentPath) ? segment : $"{parentPath}/{segment}";
    }

    private static string? ValidateValue(string? value, Type type)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null; // empty = use default, always valid
        }

        var trimmed = value.Trim();

        if (type == typeof(int) || type == typeof(int?))
        {
            return int.TryParse(trimmed, out _)
                ? null
                : $"Expected an integer, got \"{trimmed}\".";
        }

        if (type == typeof(float) || type == typeof(float?))
        {
            return float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
                ? null
                : $"Expected a number, got \"{trimmed}\".";
        }

        if (type == typeof(bool) || type == typeof(bool?))
        {
            return string.Equals(trimmed, "True", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(trimmed, "False", StringComparison.OrdinalIgnoreCase)
                ? null
                : $"Expected True or False, got \"{trimmed}\".";
        }

        return null;
    }
}
