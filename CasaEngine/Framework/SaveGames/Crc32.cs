namespace CasaEngine.Framework.SaveGames;

/// <summary>
/// CRC-32 (IEEE 802.3, reflected polynomial 0xEDB88320, initial value and final XOR 0xFFFFFFFF), the checksum of
/// the binary save-game envelope (ADR-0044). Written here with a precomputed table instead of a new package.
/// It detects accidental corruption only: anyone can recompute it, so it authenticates nothing.
/// </summary>
internal static class Crc32
{
    private const uint Polynomial = 0xEDB88320u;

    private static readonly uint[] Table = CreateTable();

    /// <summary>The CRC-32 of <paramref name="data"/>; <c>"123456789"</c> gives <c>0xCBF43926</c>.</summary>
    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFFu;
        for (int i = 0; i < data.Length; i++)
        {
            crc = Table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] CreateTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint value = i;
            for (int bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? (value >> 1) ^ Polynomial : value >> 1;
            }

            table[i] = value;
        }

        return table;
    }
}
