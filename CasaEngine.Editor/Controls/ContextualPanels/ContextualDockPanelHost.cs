using System;
using System.Collections.Generic;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MonoGame.Extended;

namespace CasaEngine.Editor.Controls.ContextualPanels;

public sealed class ContextualDockPanelHost
{
    private readonly MGWindow _window;
    private readonly EditorContextService _context;
    private readonly EditorPanelRole _role;
    private readonly string _defaultTitle;
    private readonly string _emptyMessage;
    private readonly Dictionary<EditorDocumentKind, ContextualPanelDefinition> _definitions = new();

    private MGDockPanel _root;
    private MGTextBlock _statusText;
    private MGStackPanel _contentHost;
    private ContextualPanelDefinition _activeDefinition;

    public ContextualDockPanelHost(
        MGWindow window,
        EditorContextService context,
        EditorPanelRole role,
        string defaultTitle,
        string emptyMessage)
    {
        _window = window;
        _context = context;
        _role = role;
        _defaultTitle = defaultTitle;
        _emptyMessage = emptyMessage;

        _context.ActiveDocumentChanged += _ => Refresh();
        _context.SelectionChanged += _ => Refresh();
    }

    public void Register(ContextualPanelDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Role != _role)
        {
            throw new ArgumentException($"Definition role mismatch for {_role}.", nameof(definition));
        }

        _definitions[definition.DocumentKind] = definition;
    }

    public MGElement CreateContent()
    {
        if (_root != null)
        {
            return _root;
        }

        _statusText = new MGTextBlock(_window, _emptyMessage)
        {
            Margin = new Thickness(8, 6, 8, 6),
            Opacity = 0.75f,
            WrapText = true,
        };

        _contentHost = new MGStackPanel(_window, Orientation.Vertical)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        _root = new MGDockPanel(_window);
        _root.TryAddChild(_statusText, Dock.Bottom);
        _root.TryAddChild(_contentHost, Dock.Top);

        Refresh();
        return _root;
    }

    public void Refresh()
    {
        if (_root == null || _statusText == null || _contentHost == null)
        {
            return;
        }

        var documentKind = _context.ActiveDocument?.Kind ?? EditorDocumentKind.None;
        _definitions.TryGetValue(documentKind, out var definition);

        if (!ReferenceEquals(_activeDefinition, definition))
        {
            _activeDefinition = definition;
            _contentHost.TryRemoveAll();

            if (definition != null)
            {
                _contentHost.TryAddChild(definition.ContentFactory());
            }
        }

        _statusText.Text = definition == null ? _emptyMessage : string.Empty;
        definition?.Refresh?.Invoke(_context);
    }
}