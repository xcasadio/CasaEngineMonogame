using MGUI.Core.UI.Containers;
using CasaEngine.Framework.Entities.Components;

namespace CasaEngine.Editor.Controls.ComponentEditors;

public sealed class GenericComponentEditor : ComponentEditorBase
{
    public GenericComponentEditor(MGUI.Core.UI.MGWindow window, EntityComponent component)
        : base(window, component)
    {
    }

    protected override void BuildEditor(MGStackPanel root)
    {
        var genericSection = CreateGenericSection(Component, "Properties");
        if (genericSection != null)
        {
            root.TryAddChild(genericSection);
        }
        else
        {
            root.TryAddChild(CreateMessage("No editable properties are available yet for this component."));
        }
    }
}