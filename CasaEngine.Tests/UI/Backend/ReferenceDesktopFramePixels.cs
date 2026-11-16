using Xunit;

namespace CasaEngine.Tests.UI.Backend;

/// <summary>
/// The pixels of the reference desktop frame of <see cref="DesktopFrameGpuAllocationTests"/> (default window, stack panel, text block, slider;
/// FontStashSharp; 640 x 360 target cleared to CornflowerBlue), as a size in bytes and a SHA-256 of R, G, B, A of each pixel, row by row:
/// at rest (two frames), and after the thumb of the slider was dragged to two positions (the frame drawn at the second one).<para/>
/// A hash is exact, and two GPUs or two drivers do not rasterize the same frame to the same bytes: there is one entry per machine on which
/// the frame was frozen. A frame matches when it is the frame of one of them. On a machine that has no entry the assertions fail with the
/// hashes to add here, after checking that the frame on screen is the expected one.
/// </summary>
internal static class ReferenceDesktopFramePixels
{
    private readonly record struct Entry(string Machine, string Rest, string Drag);

    private static readonly Entry[] Entries =
    {
        //  Taken from the code before the clip scopes of the backend were reused.
        new("first development machine (adapter not recorded)",
            "921600:124E0E0FFD9AB38C0A9FE580081317A93241F77B68177662060220B0438C9F36",
            "921600:A627D0D33AC2F348C4248983F467DA0000D31EA9DA8EA74A0022D86194D7BFA9"),
        //  Taken on 2026-11-16 from the code of that day, which draws the frame of the first entry on the first machine. The renderer of
        //  MGUI (MGUI.MonoGame.Integration) draws the same two frames to the same two hashes on this machine.
        new("AMD Radeon RX 9060 XT",
            "921600:D297B9493AB953FDE465AFF8D2946A7C73EC5ABCCAC25BF4A988EF9F413C9A6C",
            "921600:251A6C0ED7AA64B21B1CDE40B02509A73863D2EDB0DF67A1F0A01BABBFA15A47"),
    };

    /// <summary>Whether the pixels are those of the frame at rest on one of the known machines.</summary>
    public static bool IsRest(string pixels)
    {
        for (int i = 0; i < Entries.Length; i++)
        {
            if (Entries[i].Rest == pixels)
            {
                return true;
            }
        }

        return false;
    }

    public static void AssertRest(string pixels)
    {
        Assert.True(IsRest(pixels), Describe($"rest {pixels}"));
    }

    /// <summary>The rest frame and the drag frame must be those of the same machine.</summary>
    public static void AssertRestAndDrag(string rest, string drag)
    {
        for (int i = 0; i < Entries.Length; i++)
        {
            if (Entries[i].Rest == rest && Entries[i].Drag == drag)
            {
                return;
            }
        }

        Assert.Fail(Describe($"rest {rest}, drag {drag}"));
    }

    private static string Describe(string measured)
    {
        //  The name of the adapter is not read here: it is an OpenGL call, and the assertions run off the GPU thread.
        return $"The desktop frame is not the reference frame of a known machine. Measured: {measured}. See {nameof(ReferenceDesktopFramePixels)}.";
    }
}
