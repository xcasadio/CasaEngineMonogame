using CasaEngine.Framework.SceneManagement;
using CasaEngine.Framework.SceneManagement.Components;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

//used only to create the root node in the tree view
public class ActorViewModelComponent : ComponentViewModel
{
    public EntityViewModel EntityViewModel { get; }
    public override AActor Owner => EntityViewModel.Entity;

    public ActorViewModelComponent(EntityViewModel entityViewModel) : base(null)
    {
        EntityViewModel = entityViewModel;
        Name = $"{EntityViewModel.Name} (self)";
    }

    public override void AddComponent(ComponentViewModel componentViewModel)
    {
        if (componentViewModel.Component is SceneComponent componentToAdd && EntityViewModel.Entity.RootComponent == null)
        {
            EntityViewModel.Entity.RootComponent = componentToAdd;
        }
        else
        {
            EntityViewModel.Entity.AddComponent(componentViewModel.Component);
        }

        componentViewModel.Parent = this;
        Children.Add(componentViewModel);
    }

    public override void RemoveComponent(ComponentViewModel componentViewModel)
    {
        if (Owner.RootComponent == componentViewModel.Component)
        {
            Owner.RootComponent = null;
        }
        else
        {
            Owner.RemoveComponent(componentViewModel.Component);
        }

        componentViewModel.Parent = null;
        Children.Remove(componentViewModel);
    }
}