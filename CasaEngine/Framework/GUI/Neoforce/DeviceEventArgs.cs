using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.GUI.Neoforce;

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