﻿using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

//used only to create the root node in the tree view
public class RootNodeComponentViewModel : ComponentViewModel
{
    public EntityViewModel EntityViewModel { get; }
    public override Entity Owner => EntityViewModel.Entity;

    public RootNodeComponentViewModel(EntityViewModel entityViewModel) : base(null)
    {
        EntityViewModel = entityViewModel;
        Name = $"{EntityViewModel.Name} (self)";
    }

    public override void AddComponent(ComponentViewModel componentViewModel)
    {
        if (EntityViewModel.Entity == null) // if the world is selected
        {
            return;
        }

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