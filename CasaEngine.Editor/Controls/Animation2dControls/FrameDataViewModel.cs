﻿using System.Collections.Generic;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Game;
using CasaEngine.Editor.Controls;

public class FrameDataViewModel : NotifyPropertyChangeBase
{
    private AssetInfo _assetInfo;
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
        get => _assetInfo.Name;
        //set
        //{
        //    if (EqualityComparer<string>.Default.Equals(FrameData.SpriteId, value)) return;
        //    FrameData.SpriteId = value;
        //    OnPropertyChanged();
        //}
    }

    public FrameDataViewModel(FrameData frameData)
    {
        FrameData = frameData;
        _assetInfo = GameSettings.AssetInfoManager.Get(frameData.SpriteId);
    }
}