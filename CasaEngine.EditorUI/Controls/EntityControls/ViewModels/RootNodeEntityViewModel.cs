using CasaEngine.Framework.World;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

//used only to create the root node in the tree view
public class RootNodeEntityViewModel : EntityViewModel
{
    public World World { get; }

    public override string Name => "World";

    public RootNodeEntityViewModel(World world) : base(null)
    {
        World = world;
    }

    public override void AddActorChild(EntityViewModel entityViewModel)
    {
        entityViewModel.Parent = this;
        World.AddEntityWithEditor(entityViewModel.Entity);
        Children.Add(entityViewModel);
    }

    public override void RemoveActorChild(EntityViewModel entityViewModel)
    {
        entityViewModel.Parent = null;
        World.RemoveEntityWithEditor(entityViewModel.Entity);
        Children.Remove(entityViewModel);
    }
}