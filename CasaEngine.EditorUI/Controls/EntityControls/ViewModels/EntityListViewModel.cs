using System;
using System.Collections.ObjectModel;
using CasaEngine.Framework.Entities;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public class EntityListViewModel
{
    private bool _lock;
    private readonly RootNodeEntityViewModel _worldViewModel;

    public ObservableCollection<EntityViewModel> Entities { get; } = new();

    public EntityListViewModel(RootNodeEntityViewModel worldViewModelViewModel)
    {
        _worldViewModel = worldViewModelViewModel;
        //worldViewModelViewModel.World.EntityAdded += OnEntityAdded;
        //worldViewModelViewModel.World.EntityRemoved += OnEntityRemoved;
        //worldViewModelViewModel.World.EntitiesClear += OnEntitiesClear;

        Entities.Add(worldViewModelViewModel);

        foreach (var entity in worldViewModelViewModel.World.Entities)
        {
            var entityViewModel = new EntityViewModel(entity);
            entityViewModel.Parent = worldViewModelViewModel;
            worldViewModelViewModel.Children.Add(entityViewModel);
        }
    }

    private void OnEntitiesClear(object? sender, EventArgs e)
    {
        Entities.Clear();
    }

    private void OnEntityRemoved(object? sender, Entity entity)
    {
        if (!_lock)
        {
            foreach (var entityViewModel in Entities)
            {
                if (entityViewModel.Entity == entity)
                {
                    //Entities.Remove(entityViewModel);
                    _worldViewModel.RemoveActorChild(entityViewModel);
                    break;
                }
            }
        }
    }

    private void OnEntityAdded(object? sender, Entity entity)
    {
        if (!_lock)
        {
            var entityViewModel = new EntityViewModel(entity);
            entityViewModel.Parent = _worldViewModel;
            _worldViewModel.Children.Add(entityViewModel);
        }
    }

    public EntityViewModel? GetFromEntity(Entity entity)
    {
        return GetFromEntity(Entities[0], entity); // start with root
    }

    private EntityViewModel? GetFromEntity(EntityViewModel entityViewModel, Entity entity)
    {
        if (entityViewModel.Entity == entity)
        {
            return entityViewModel;
        }

        foreach (var childEntityViewModel in entityViewModel.Children)
        {
            var found = GetFromEntity(childEntityViewModel, entity);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    public EntityViewModel Add(Entity entity)
    {
        _lock = true;
        _worldViewModel.AddActorChild(new EntityViewModel(entity));
        _lock = false;

        return new EntityViewModel(entity);
    }

    public void Remove(EntityViewModel entityViewModel)
    {
        _lock = true;
        _worldViewModel.RemoveActorChild(entityViewModel);
        _lock = false;
    }
}