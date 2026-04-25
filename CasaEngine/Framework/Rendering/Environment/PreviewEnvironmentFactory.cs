namespace CasaEngine.Framework.Rendering.Environment;

public static class PreviewEnvironmentFactory
{
    public static WorldEnvironmentSettings CreateNeutralPreview(Color backgroundColor)
    {
        return new WorldEnvironmentSettings
        {
            Type = EnvironmentType.None,
            BackgroundMode = EnvironmentBackgroundMode.SolidColor,
            BackgroundColor = backgroundColor,
            AmbientColor = EnvironmentResolver.LegacyAmbientColor,
            AmbientIntensity = 1.0f,
            SpecularIntensity = 1.0f,
        };
    }
}