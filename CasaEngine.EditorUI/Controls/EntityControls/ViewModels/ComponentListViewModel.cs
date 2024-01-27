using System.Collections.ObjectModel;
using CasaEngine.Framework.SceneManagement;
using CasaEngine.Framework.SceneManagement.Components;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public class ComponentListViewModel : NotifyPropertyChangeBase
{
    public ObservableCollection<ComponentViewModel> Components { get; } = new();

    public EntityViewModel EntityViewModel { get; }

    public ComponentListViewModel(EntityViewModel entityViewModel)
    {
        EntityViewModel = entityViewModel;

        if (entityViewModel.Entity.RootComponent != null)
        {
            Components.Add(new ComponentViewModel(entityViewModel.Entity.RootComponent));
        }

        foreach (var component in entityViewModel.Entity.Components)
        {
            Components.Add(new ComponentViewModel(component));
        }

        entityViewModel.Entity.ComponentAdded += OnComponentAdded;
        entityViewModel.Entity.ComponentRemoved += OnComponentRemoved;
    }

    private void OnComponentAdded(object? sender, ActorComponent component)
    {
        Components.Add(new ComponentViewModel(component));
    }

    private void OnComponentRemoved(object? sender, ActorComponent component)
    {
        foreach (var componentViewModel in Components)
        {
            if (componentViewModel.Component == component)
            {
                Components.Remove(componentViewModel);
                break;
            }
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