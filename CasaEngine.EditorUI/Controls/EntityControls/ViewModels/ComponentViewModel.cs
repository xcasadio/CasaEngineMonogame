using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using CasaEngine.Core.Log;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.SceneManagement.Components;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public class ComponentViewModel : NotifyPropertyChangeBase
{
    private ComponentViewModel? _parent;
    public string Name { get; internal set; }
    public bool OnlyForEditor { get; set; }
    public EntityComponent Component { get; }
    public virtual Entity Owner => Component.Owner;

    public ComponentViewModel? Parent
    {
        get => _parent;
        internal set => SetField(ref _parent, value);
    }

    public ObservableCollection<ComponentViewModel> Children { get; } = new();

    public ComponentViewModel(EntityComponent component)
    {
        Component = component;
        var displayNameAttribute = component?.GetType().GetCustomAttribute<DisplayNameAttribute>();
        Name = displayNameAttribute?.DisplayName ?? "Name not set";
    }

    public virtual void AddComponent(ComponentViewModel componentViewModel)
    {
        if (Component is SceneComponent sceneComponent && componentViewModel.Component is SceneComponent componentToAdd)
        {
            componentViewModel.Parent = this;
            sceneComponent.AddChildComponent(componentToAdd);
            Children.Add(componentViewModel);
        }
        else
        {
            Logs.WriteError($"Can't add the component {componentViewModel.Component.GetType().Name} to the actor {componentViewModel.Owner.Name}");
        }
    }

    public virtual void RemoveComponent(ComponentViewModel componentViewModel)
    {
        if (Component is SceneComponent sceneComponent)
        {
            sceneComponent.RemoveChildComponent(componentViewModel.Component as SceneComponent);
        }

        componentViewModel.Parent = null;
        Children.Remove(componentViewModel);
    }
}