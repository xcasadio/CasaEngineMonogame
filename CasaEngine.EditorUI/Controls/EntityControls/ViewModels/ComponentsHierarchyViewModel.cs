using System.Collections.ObjectModel;
using CasaEngine.Framework.SceneManagement;
using CasaEngine.Framework.SceneManagement.Components;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public class ComponentsHierarchyViewModel : NotifyPropertyChangeBase
{
    public ComponentViewModel RootComponentViewModel { get; private set; }

    public ObservableCollection<ComponentViewModel> ComponentsViewModel { get; } = new();

    public EntityViewModel EntityViewModel { get; }

    public ComponentsHierarchyViewModel(EntityViewModel entityViewModel)
    {
        EntityViewModel = entityViewModel;

        RootComponentViewModel = new ActorViewModelComponent(EntityViewModel);
        ComponentsViewModel.Add(RootComponentViewModel);
        CreateComponentHierarchy(entityViewModel.Entity);
    }

    private void CreateComponentHierarchy(AActor actor)
    {
        if (actor.RootComponent != null)
        {
            var componentViewModel = new ComponentViewModel(actor.RootComponent);
            RootComponentViewModel.Children.Add(componentViewModel);

            foreach (var componentChild in actor.RootComponent.Children)
            {
                AddChild(componentViewModel, componentChild);
            }
        }

        foreach (var component in actor.Components)
        {
            var componentViewModel = new ComponentViewModel(component);
            RootComponentViewModel.Children.Add(componentViewModel);

            if (component is SceneComponent sceneComponent)
            {
                foreach (var componentChild in sceneComponent.Children)
                {
                    AddChild(RootComponentViewModel, componentChild);
                }
            }
        }
    }

    private void AddChild(ComponentViewModel componentViewModel, SceneComponent componentChild)
    {
        var componentChildViewModel = new ComponentViewModel(componentChild);
        componentViewModel.Children.Add(componentChildViewModel);

        foreach (var component in componentChild.Children)
        {
            AddChild(componentChildViewModel, component);
        }
    }

    public void RemoveComponent(ActorComponent component)
    {
        if (EntityViewModel.Entity.RootComponent == component)
        {
            EntityViewModel.Entity.RootComponent = null;
        }
        else
        {
            EntityViewModel.Entity.RemoveComponent(component);
        }
    }
}
