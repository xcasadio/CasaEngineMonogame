using Microsoft.Xna.Framework.Graphics;

namespace TomShane.Neoforce.Controls;

public class DeviceEventArgs : EventArgs
{

    public PresentationParameters DeviceSettings;

    public DeviceEventArgs()
    {
    }

    public DeviceEventArgs(PresentationParameters deviceSettings)
    {
        DeviceSettings = deviceSettings;
    }

}