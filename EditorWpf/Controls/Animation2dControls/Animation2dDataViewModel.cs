using System.Collections.Generic;
using System.Collections.ObjectModel;
using CasaEngine.Framework.Assets.Animations;
using EditorWpf.Controls;

public class Animation2dDataViewModel : NotifyPropertyChangeBase
{
    public Animation2dData Animation2dData { get; }

    public string Name
    {
        get => Animation2dData.Name;
        set
        {
            if (EqualityComparer<string>.Default.Equals(Animation2dData.Name, value)) return;
            Animation2dData.Name = value;
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