using CasaEngine.Editor.Controls;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;

namespace CasaEngine.Editor.Workspaces;

public sealed class WorldWorkspaceContext
{
    public WorldViewportPanel? ViewportPanel { get; set; }

    public Entity? SelectedEntity { get; set; }

    public EntityComponent? SelectedComponent { get; set; }
}