using System;

namespace CasaEngine.Demos.Demos;

/// <summary>
/// The keys named by the audio demo's on-screen text, taken from the input configuration rather than written in the code:
/// on AZERTY the loop point key reads A, not Q (ADR-0096). Formatted once, when the demo starts.
/// </summary>
internal sealed record AudioHudText(
    string LoopPoint,
    string Reverb,
    string LowPass,
    string Ducking,
    string Orbit,
    string Doppler,
    string Intensity,
    string[] HelpLines)
{
    /// <param name="keyOf">The label of the key bound to an action of the input configuration.</param>
    public static AudioHudText Create(Func<string, string> keyOf)
    {
        string oneShot = keyOf("Audio.OneShot");
        string loop = keyOf("Audio.Loop");
        string stopAll = keyOf("Audio.StopAll");
        string music = keyOf("Audio.Music");
        string loopPoint = keyOf("Audio.LoopPoint");
        string reverb = keyOf("Audio.Reverb");
        string lowPass = keyOf("Audio.LowPass");
        string ducking = keyOf("Audio.Ducking");
        string orbit = keyOf("Audio.Orbit");
        string doppler = keyOf("Audio.Doppler");
        string intensity = keyOf("Audio.Intensity");

        return new AudioHudText(
            loopPoint, reverb, lowPass, ducking, orbit, doppler, intensity,
            new[]
            {
                $"{oneShot} one-shot   {loop} loop on/off   {keyOf("Audio.FadeLoop")} fade out   {stopAll} stop all",
                $"{keyOf("Audio.StereoTest")} stereo beep: left, then right, then both",
                $"{keyOf("Audio.VariedClick")} varied click   {keyOf("Audio.PriorityTest")} click with priority 10 (steals a voice)",
                $"{keyOf("Audio.VoiceStealTest")} fill free voices with priority-1 loops ({stopAll} stops them)",
                $"{music} music on/off   {keyOf("Audio.Crossfade")} crossfade      {keyOf("Audio.MusicUp")}/{keyOf("Audio.MusicDown")} Music",
                $"{loopPoint} loop point: tone once, then beeps (loops from 1 s)",
                $"{keyOf("Audio.Stress")} GC stress on/off (loop + music + garbage + forced GC)",
                $"{reverb} reverb on Sfx   {lowPass} low-pass on Music   {ducking} ducking Music by Sfx",
                $"Ducking: music on ({music}), then play a sound effect ({oneShot} or {loop})",
                $"{orbit} spatial orbit on/off   {doppler} Doppler on/off   {intensity} intensity 0/0.5/1",
                $"{keyOf("Audio.MasterUp")}/{keyOf("Audio.MasterDown")} Master   {keyOf("Audio.SfxDown")}/{keyOf("Audio.SfxUp")} Sfx   " +
                $"{keyOf("Audio.MuteMaster")} mute Master   {keyOf("Audio.MuteSfx")} mute Sfx",
            });
    }
}
