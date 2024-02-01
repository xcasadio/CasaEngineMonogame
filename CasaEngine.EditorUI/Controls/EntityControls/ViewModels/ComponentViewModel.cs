using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using CasaEngine.Core.Log;
using CasaEngine.Framework.SceneManagement;
using CasaEngine.Framework.SceneManagement.Components;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public class ComponentViewModel : NotifyPropertyChangeBase
{
    public string Name { get; protected set; }
    public bool OnlyForEditor { get; set; }
    public ActorComponent Component { get; }
    public virtual AActor Owner => Component.Owner;

    public ObservableCollection<ComponentViewModel> Children { get; } = new();

    public ComponentViewModel(ActorComponent component)
    {
        Component = component;
        var displayNameAttribute = component?.GetType().GetCustomAttribute<DisplayNameAttribute>();
        Name = displayNameAttribute?.DisplayName ?? "Name not set";
    }

    public virtual void AddComponent(ComponentViewModel componentViewModel)
    {
        if (Component is SceneComponent sceneComponent && componentViewModel.Component is SceneComponent componentToAdd)
        {
            sceneComponent.AddChildComponent(componentToAdd);
            Children.Add(componentViewModel);
        }
        else
        {
            Logs.WriteError($"Can't add the component {componentViewModel.Component.GetType().Name} to the actor {componentViewModel.Owner.Name}");
        }
    }

    public void RemoveComponent(ComponentViewModel componentViewModel)
    {
        if (Owner.RootComponent == componentViewModel.Component)
        {
            Owner.RootComponent = null;
        }
        else
        {
            Owner.RemoveComponent(componentViewModel.Component);
        }

        Children.Remove(componentViewModel);
    }
}