using CasaEngine.Framework.Assets;
using CasaEngine.Framework.World;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

//used only to create the root node in the tree view
public class RootNodeEntityViewModel : EntityViewModel
{
    public World World { get; }

    public override string Name
    {
        get => World?.Name ?? "World";
        set
        {
            if (World != null && value != World.Name && AssetCatalog.CanRename(value))
            {
                AssetCatalog.Rename(World.Id, value);
                World.Name = value;
                OnPropertyChanged();
            }
        }
    }

    public RootNodeEntityViewModel(World world) : base(null)
    {
        World = world;
    }

    public override void AddActorChild(EntityViewModel entityViewModel)
    {
        entityViewModel.Parent = this;
        Children.Add(entityViewModel);
        World.AddEntityWithEditor(entityViewModel.Entity);
    }

    public override void RemoveActorChild(EntityViewModel entityViewModel)
    {
        entityViewModel.Parent = null;
        Children.Remove(entityViewModel);
        World.RemoveEntityWithEditor(entityViewModel.Entity);
    }
}