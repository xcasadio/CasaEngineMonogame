using Xunit;

namespace CasaEngine.Tests.Audio.Output;

public class AlsoftIniTests
{
    [Fact]
    public void The_engine_alsoft_ini_asks_for_two_periods_of_480_frames()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "alsoft.ini");

        Assert.True(File.Exists(path), $"alsoft.ini is not copied beside the executable: {path}");

        var lines = File.ReadAllLines(path)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#') && !line.StartsWith(';'))
            .ToArray();

        Assert.Equal(new[] { "[general]", "period_size = 480", "periods = 2" }, lines);
    }
}
