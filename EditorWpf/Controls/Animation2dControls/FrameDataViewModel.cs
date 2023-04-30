using System.Collections.Generic;
using CasaEngine.Framework.Assets.Animations;
using EditorWpf.Controls;

public class FrameDataViewModel : NotifyPropertyChangeBase
{
    public FrameData FrameData { get; }

    public float Duration
    {
        get => FrameData.Duration;
        set
        {
            if (EqualityComparer<float>.Default.Equals(FrameData.Duration, value)) return;
            FrameData.Duration = value;
            OnPropertyChanged();
        }
    }

    public string SpriteId
    {
        get => FrameData.SpriteId;
        set
        {
            if (EqualityComparer<string>.Default.Equals(FrameData.SpriteId, value)) return;
            FrameData.SpriteId = value;
            OnPropertyChanged();
        }
    }

    public FrameDataViewModel(FrameData frameData)
    {
        FrameData = frameData;
    }
}