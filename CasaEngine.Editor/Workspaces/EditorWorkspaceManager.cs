using System;
using System.Collections.Generic;
using MGUI.Core.UI.Docking.DockLayout;

namespace CasaEngine.Editor.Workspaces;

public sealed class EditorWorkspaceManager
{
    private readonly Dictionary<EditorWorkspaceId, IEditorWorkspace> _workspaces;
    private readonly Action<EditorWorkspaceId> _saveLayout;
    private readonly Func<EditorWorkspaceId, bool, bool> _tryLoadLayout;
    private readonly Action<DockNode> _applyLayout;
    private readonly Action _afterLayoutApplied;
    private bool _hasActivatedWorkspace;

    public EditorWorkspaceManager(
        IEnumerable<IEditorWorkspace> workspaces,
        Action<EditorWorkspaceId> saveLayout,
        Func<EditorWorkspaceId, bool, bool> tryLoadLayout,
        Action<DockNode> applyLayout,
        Action afterLayoutApplied)
    {
        ArgumentNullException.ThrowIfNull(workspaces);
        ArgumentNullException.ThrowIfNull(saveLayout);
        ArgumentNullException.ThrowIfNull(tryLoadLayout);
        ArgumentNullException.ThrowIfNull(applyLayout);
        ArgumentNullException.ThrowIfNull(afterLayoutApplied);

        _workspaces = new Dictionary<EditorWorkspaceId, IEditorWorkspace>();
        foreach (var workspace in workspaces)
        {
            _workspaces[workspace.Id] = workspace;
        }

        _saveLayout = saveLayout;
        _tryLoadLayout = tryLoadLayout;
        _applyLayout = applyLayout;
        _afterLayoutApplied = afterLayoutApplied;
    }

    public EditorWorkspaceId ActiveWorkspaceId { get; private set; } = EditorWorkspaceId.World;

    public IEditorWorkspace GetWorkspace(EditorWorkspaceId workspaceId) => _workspaces[workspaceId];

    public bool ActivateWorkspace(EditorWorkspaceId workspaceId, bool preferPersistedLayout, bool logOutcome)
    {
        if (_hasActivatedWorkspace && ActiveWorkspaceId == workspaceId && preferPersistedLayout)
        {
            return false;
        }

        if (_hasActivatedWorkspace && ActiveWorkspaceId != workspaceId)
        {
            _saveLayout(ActiveWorkspaceId);
        }

        ActiveWorkspaceId = workspaceId;
        _hasActivatedWorkspace = true;

        if (!preferPersistedLayout || !_tryLoadLayout(workspaceId, logOutcome))
        {
            _applyLayout(GetWorkspace(workspaceId).CreateDefaultLayout());
        }

        _afterLayoutApplied();
        return true;
    }

    public void ResetWorkspaceLayout(EditorWorkspaceId workspaceId)
    {
        ActiveWorkspaceId = workspaceId;
        _hasActivatedWorkspace = true;
        _applyLayout(GetWorkspace(workspaceId).CreateDefaultLayout());
        _afterLayoutApplied();
    }
}