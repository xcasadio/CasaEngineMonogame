using CasaEngine.Framework.Entities.Components;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public static class ComponentViewModelFactory
{
    public static ComponentViewModel Create(EntityComponent componentChild)
    {
        if (componentChild is StaticMeshComponent)
        {
            return new StaticMeshComponentViewModel(componentChild);
        }

        return new ComponentViewModel(componentChild);
    }
}