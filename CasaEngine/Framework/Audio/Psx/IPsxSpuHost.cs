namespace CasaEngine.Framework.Audio.Psx;

/// <summary>
/// Optional capability of an audio backend (plan decision P9): it hosts the software PlayStation SPU
/// (<see cref="PsxSpu"/>) next to its own mixing, so a game drives it from the game thread and hears it
/// mixed with the rest of the engine audio. <see cref="IAudioBackend"/> does not change; only the
/// software backend implements this, and <see cref="AudioService.TryCreatePsxSpu"/> detects it.
/// </summary>
public interface IPsxSpuHost
{
    /// <summary>
    /// Creates the one live SPU of the backend. Returns false, with <paramref name="port"/> null, when the
    /// backend is unavailable, when an SPU is already alive (the console has one), or when the mixer could
    /// not take it. Game thread only. The port allocates everything it needs here and never afterwards.
    /// </summary>
    /// <param name="tables">Hardware tables supplied by the caller (see <see cref="PsxSpuHardwareTables"/>).</param>
    /// <param name="port">The game thread access to the SPU; dispose it to detach the SPU from the mixer.</param>
    bool TryCreatePsxSpu(PsxSpuHardwareTables tables, out PsxSpuPort port);
}
