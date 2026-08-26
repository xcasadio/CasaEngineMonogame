namespace CasaEngine.Framework.Audio.Mixing;

/// <summary>
/// Names of the buses the engine always creates. A project may add its own buses on top of
/// these, but these are the ones assets and engine systems are allowed to reference by default.
/// </summary>
public static class AudioBusNames
{
    /// <summary>Root bus: its volume and mute apply to everything.</summary>
    public const string Master = "Master";

    /// <summary>Background music and ambiences (streamed).</summary>
    public const string Music = "Music";

    /// <summary>Gameplay sound effects.</summary>
    public const string Sfx = "Sfx";

    /// <summary>Dialogue and voice over.</summary>
    public const string Voice = "Voice";

    /// <summary>User interface feedback.</summary>
    public const string Ui = "Ui";

    /// <summary>
    /// Editor-only playback (asset preview). Kept apart from the game buses so a preview is
    /// neither silenced by the game mix nor stopped when a play-in-editor session ends.
    /// </summary>
    public const string Editor = "Editor";

    /// <summary>Creates the default tree: every bus above hangs from <see cref="Master"/>.</summary>
    public static AudioMixer CreateDefaultMixer()
    {
        var mixer = new AudioMixer();
        mixer.CreateBus(Master, null);
        mixer.CreateBus(Music, Master);
        mixer.CreateBus(Sfx, Master);
        mixer.CreateBus(Voice, Master);
        mixer.CreateBus(Ui, Master);
        mixer.CreateBus(Editor, Master);
        return mixer;
    }
}
