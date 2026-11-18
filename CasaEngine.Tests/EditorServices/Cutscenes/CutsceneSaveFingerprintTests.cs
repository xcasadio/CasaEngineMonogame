using System.Security.Cryptography;
using System.Text;
using CasaEngine.EditorServices;
using CasaEngine.EditorServices.Cutscenes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.EditorServices.Cutscenes;

/// <summary>
/// Pins the text the editor saves for each cutscene file that existed before the At action. The table is fixed on purpose:
/// it lists no folder, so that a cutscene added later does not change this test. A change of a fingerprint means that a
/// format change altered what the editor writes for a file that did not use it.
/// </summary>
public sealed class CutsceneSaveFingerprintTests
{
    public static IEnumerable<object[]> Fingerprints()
    {
        yield return ["CasaEngine.Demos/Content/Cutscenes/move_to_direct.cutscene",
            "d8fabec4a335f58ebac6a83ddbaa30cf7224ed2ce08e2b51ef9eb162530748b5"];
        yield return ["CasaEngine.Demos/Content/Cutscenes/navigate_to_grid.cutscene",
            "1e2a47fff0b63087e105b225b1ec9ade7851b0a09e9b19b573ffd0d5f4ee4ea4"];
        yield return ["Projects/SpriteCutsceneSample/Cutscenes/end.cutscene",
            "e16aad8eb5360420154f56bef27d902672ddd2ec015c1590432fedb6fe86cfb7"];
        yield return ["Projects/SpriteCutsceneSample/Cutscenes/exit.cutscene",
            "631947aa095dd0592a46e62ac10e18ac259f67554270f311bfd340d88e2772fc"];
        yield return ["Projects/SpriteCutsceneSample/Cutscenes/loop.cutscene",
            "1a32301651ac5e2e4e58344aa357ed3bb88f1b56a24caa7141d788492f499edf"];
        yield return ["Projects/TimedScreenSample/Cutscenes/first.cutscene",
            "984681b179ea06f6a162155d93130a42a98c2942380b74d445c226a5434da341"];
        yield return ["Projects/TimedScreenSample/Cutscenes/second.cutscene",
            "a0426cc7789a92e468f2e42d22bedc3c34c6ee6938222487b040a7dea3291521"];
        yield return ["Projects/VideoSample/Cutscenes/play.cutscene",
            "5f3cb2dcf38d5a8eba092fd5998bf07f666daf728aeada83876d0bdc96edaf79"];
        yield return ["Projects/VideoSpriteExitSample/Cutscenes/end.cutscene",
            "6579d7bada076fab905b6d15ed190cf9468a9262cf925f26967caa031fb7cc3d"];
        yield return ["Projects/VideoSpriteExitSample/Cutscenes/exit.cutscene",
            "84d1132d503b872b6208b5bbb69c6fa39c74ceb36132ab1d37e7ca5005f1eb0b"];
        yield return ["Projects/VideoSpriteExitSample/Cutscenes/loop.cutscene",
            "9a4a38f93c7964e93ed48741a886d65bc737b8a0b45c84b1416a98c44fc8bf54"];
        yield return ["Projects/VideoSpriteSample/Cutscenes/end.cutscene",
            "94d5143d179de85c776e30c900639c91ae02d3c612f8b5f3d85934719010cfa1"];
        yield return ["Projects/VideoSpriteSample/Cutscenes/exit.cutscene",
            "fa42537b52edc12b23d0114a7be82902eb058fdd4cffdaad36505ac886077ecc"];
        yield return ["Projects/VideoSpriteSample/Cutscenes/loop.cutscene",
            "8ae9742f1cd676e5de149057a2e5c9da5a13d4a7fc3024ef9bec9c93ec91eb60"];
    }

    [Fact]
    public void TheTableHoldsTheFourteenFilesOfBeforeTheAtAction()
    {
        Assert.Equal(14, Fingerprints().Count());
    }

    [Theory]
    [MemberData(nameof(Fingerprints))]
    public void TheSavedTextOfAFile_HasTheFingerprintTakenBeforeTheAtAction(string relativePath, string expectedSha256)
    {
        string path = Path.Combine(FindRepositoryRoot(), relativePath);
        Assert.True(File.Exists(path), path);

        var session = CutsceneEditSession.Load(JObject.Parse(File.ReadAllText(path)));
        Assert.True(EditorAssetJsonSerializer.TrySerialize(session.Asset, out var saved));
        string text = saved.ToString(Formatting.Indented).Replace("\r\n", "\n");

        string actual = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        Assert.Equal(expectedSha256, actual);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CasaEngine.MonoGame.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("CasaEngine.MonoGame.sln was not found above the test assembly.");
    }
}
