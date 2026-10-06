namespace CasaEngine.Framework.Audio.Mixing;

/// <summary>A send of an <see cref="AudioBus"/> to a return bus: the target and the level of the signal sent, in [0, 1].</summary>
public readonly record struct AudioBusSend(AudioBus Target, float Level);
