using System;
using System.Collections.ObjectModel;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.SceneManagement;
using CasaEngine.Framework.World;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public class EntityListViewModel
{
    private bool _lock;
    private readonly World _world;

    public ObservableCollection<EntityViewModel> Entities { get; } = new();

    public EntityListViewModel(World world)
    {
        _world = world;
        world.EntityAdded += OnEntityAdded;
        world.EntityRemoved += OnEntityRemoved;
        world.EntitiesClear += OnEntitiesClear;

        foreach (var worldEntity in world.Entities)
        {
            Entities.Add(new EntityViewModel(worldEntity));
        }
    }

    private void OnEntitiesClear(object? sender, EventArgs e)
    {
        Entities.Clear();
    }

    private void OnEntityRemoved(object? sender, AActor entity)
    {
        if (!_lock)
        {
            foreach (var entityViewModel in Entities)
            {
                if (entityViewModel.Entity == entity)
                {
                    Entities.Remove(entityViewModel);
                    break;
                }
            }
        }
    }

    private void OnEntityAdded(object? sender, AActor entity)
    {
        if (!_lock)
        {
            Entities.Add(new EntityViewModel(entity));
        }
    }

    public EntityViewModel? GetFromEntity(AActor entity)
    {
        foreach (var entityViewModel in Entities)
        {
            if (entityViewModel.Entity == entity)
            {
                return entityViewModel;
            }
        }

        return null;
    }

    public EntityViewModel Add(AActor entity)
    {
        _lock = true;

        _world.AddEntityWithEditor(entity);
        var entityViewModel = new EntityViewModel(entity);
        Entities.Add(entityViewModel);

        _lock = false;

        return entityViewModel;
    }

    public void Remove(EntityViewModel entityViewModel)
    {
        _lock = true;

        Entities.Remove(entityViewModel);
        _world.RemoveEntityWithEditor(entityViewModel.Entity);

        _lock = false;
    }
}