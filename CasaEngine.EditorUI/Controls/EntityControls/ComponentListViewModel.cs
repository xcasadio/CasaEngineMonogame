using System.Collections.ObjectModel;

namespace CasaEngine.Editor.Controls.EntityControls;

public class ComponentListViewModel : NotifyPropertyChangeBase
{
    public ObservableCollection<ComponentViewModel> Components { get; } = new();

    public EntityViewModel EntityViewModel { get; }

    public ComponentListViewModel(EntityViewModel entityViewModel)
    {
        EntityViewModel = entityViewModel;

        foreach (var component in entityViewModel.Entity.ComponentManager.Components)
        {
            Components.Add(new ComponentViewModel(component));
        }

        entityViewModel.Entity.ComponentManager.ComponentAdded += OnComponentAdded;
        entityViewModel.Entity.ComponentManager.ComponentRemoved += OnComponentRemoved;
        entityViewModel.Entity.ComponentManager.ComponentClear += OnComponentClear;
    }

    private void OnComponentAdded(object? sender, Framework.Entities.Components.Component component)
    {
        Components.Add(new ComponentViewModel(component));
    }

    private void OnComponentRemoved(object? sender, Framework.Entities.Components.Component component)
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

    private void OnComponentClear(object? sender, System.EventArgs e)
    {
        Components.Clear();
    }
}