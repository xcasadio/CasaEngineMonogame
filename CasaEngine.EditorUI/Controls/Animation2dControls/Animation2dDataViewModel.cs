using System.Collections.Generic;
using System.Collections.ObjectModel;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Editor.Controls;

public class Animation2dDataViewModel : NotifyPropertyChangeBase
{
    public Animation2dData Animation2dData { get; }

    public AssetInfo AssetInfo => Animation2dData.AssetInfo;

    public string Name
    {
        get => Animation2dData.AssetInfo.Name;
        set
        {
            if (EqualityComparer<string>.Default.Equals(Animation2dData.AssetInfo.Name, value)) return;
            Animation2dData.AssetInfo.Name = value;
            OnPropertyChanged();
        }
    }

    public AnimationType AnimationType
    {
        get => Animation2dData.AnimationType;
        set
        {
            if (EqualityComparer<AnimationType>.Default.Equals(Animation2dData.AnimationType, value)) return;
            Animation2dData.AnimationType = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<FrameDataViewModel> Frames { get; } = new();

    public Animation2dDataViewModel(Animation2dData animation2dData)
    {
        Animation2dData = animation2dData;

        foreach (var frame in Animation2dData.Frames)
        {
            Frames.Add(new FrameDataViewModel(frame));
        }
    }
}