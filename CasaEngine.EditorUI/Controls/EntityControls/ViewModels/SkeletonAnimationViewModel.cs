using CasaEngine.Framework.Graphics;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public class SkeletonAnimationViewModel : NotifyPropertyChangeBase
{
    private readonly RiggedModel.RiggedAnimation _riggedAnimation;

    public string Name
    {
        get => _riggedAnimation.AnimationName;
        set => SetField(ref _riggedAnimation.AnimationName, value);
    }

    public SkeletonAnimationViewModel(RiggedModel.RiggedAnimation riggedAnimation)
    {
        _riggedAnimation = riggedAnimation;
    }
}