using System.Globalization;
using CasaEngine.Editor.Runtime;
using Xunit;

namespace CasaEngine.Tests.Editor;

/// <summary>ADR-0091: per-user editor preferences, tolerant Json and a single delayed write.</summary>
public sealed class EditorPreferencesTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "casa-editor-preferences-" + Guid.NewGuid().ToString("N"));
    private double _now;

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string FilePath => Path.Combine(_directory, "sub", "editor.preferences.json");

    private EditorPreferencesStore CreateStore(List<string> warnings = null)
        => new(FilePath, () => _now, message => warnings?.Add(message));

    private static EditorPreferences Sample() => new()
    {
        Camera3d = new Camera3dPreferences
        {
            RotationSensitivity = 0.007f,
            PanSensitivity = 0.02f,
            WheelStepRatio = 0.2f,
            FlySpeed = 42f,
            FlySpeedWheelFactor = 1.5f,
            FieldOfViewRateDegrees = 90f,
            DragThresholdPixels = 6f,
            InvertY = true,
        },
        Camera2d = new Camera2dPreferences { PanSpeed = 2f, ZoomStepsPerNotch = 3, InvertY = true },
    };

    [Fact]
    public void Serializer_RoundTrips()
    {
        string json = EditorPreferencesSerializer.Write(Sample());

        Assert.True(EditorPreferencesSerializer.TryRead(json, null, out var read));
        Assert.Equal(Sample(), read);
    }

    [Fact]
    public void Serializer_OutputIsStableAndSnakeCase()
    {
        string first = EditorPreferencesSerializer.Write(Sample());
        string second = EditorPreferencesSerializer.Write(Sample());

        Assert.Equal(first, second);
        Assert.Contains("\"version\": 1", first);
        Assert.Contains("\"camera_3d\"", first);
        Assert.Contains("\"rotation_sensitivity\"", first);
        Assert.Contains("\"zoom_steps_per_notch\": 3", first);
        Assert.True(first.IndexOf("\"camera_3d\"", StringComparison.Ordinal) < first.IndexOf("\"camera_2d\"", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{ \"version\": 1 }")]
    public void Serializer_MissingKeys_GiveTheDefaults(string json)
    {
        var warnings = new List<string>();

        Assert.True(EditorPreferencesSerializer.TryRead(json, warnings.Add, out var read));

        Assert.Equal(EditorPreferences.Default, read);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Serializer_ClampsOutOfRangeValues_AndWarnsPerKey()
    {
        var warnings = new List<string>();
        const string json = "{ \"camera_3d\": { \"rotation_sensitivity\": 5, \"fly_speed\": -1 }, \"camera_2d\": { \"zoom_steps_per_notch\": 99 } }";

        Assert.True(EditorPreferencesSerializer.TryRead(json, warnings.Add, out var read));

        Assert.Equal(0.05f, read.Camera3d.RotationSensitivity);
        Assert.Equal(0.1f, read.Camera3d.FlySpeed);
        Assert.Equal(5, read.Camera2d.ZoomStepsPerNotch);
        Assert.Equal(3, warnings.Count);
        Assert.Contains(warnings, w => w.Contains("rotation_sensitivity"));
        Assert.Contains(warnings, w => w.Contains("fly_speed"));
        Assert.Contains(warnings, w => w.Contains("zoom_steps_per_notch"));
    }

    [Fact]
    public void Serializer_MistypedValues_GiveTheDefaults_WithAWarning()
    {
        var warnings = new List<string>();
        const string json = "{ \"camera_3d\": { \"pan_sensitivity\": \"fast\", \"invert_y\": 1 }, \"camera_2d\": 3 }";

        Assert.True(EditorPreferencesSerializer.TryRead(json, warnings.Add, out var read));

        Assert.Equal(new Camera3dPreferences().PanSensitivity, read.Camera3d.PanSensitivity);
        Assert.False(read.Camera3d.InvertY);
        Assert.Equal(new Camera2dPreferences(), read.Camera2d);
        Assert.Equal(3, warnings.Count);
    }

    /// <summary>Verifier finding F1: integer literals beyond Int64 / Int32 used to throw instead of being clamped.</summary>
    [Fact]
    public void Serializer_HugeIntegerLiterals_AreClampedOrReported_NotThrown()
    {
        var warnings = new List<string>();

        Assert.True(EditorPreferencesSerializer.TryRead("{\"camera_3d\":{\"fly_speed\":100000000000000000000000}}", warnings.Add, out var read));
        Assert.Equal(1000f, read.Camera3d.FlySpeed);
        Assert.Contains(warnings, w => w.Contains("fly_speed"));

        warnings.Clear();
        Assert.True(EditorPreferencesSerializer.TryRead("{\"version\":3000000000}", warnings.Add, out read));
        Assert.Equal(EditorPreferences.Default, read);
        Assert.Contains(warnings, w => w.Contains("3000000000"));
    }

    [Fact]
    public void Store_FileWithAHugeInteger_LoadsWithoutThrowing()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
        File.WriteAllText(FilePath, "{\"version\":3000000000,\"camera_3d\":{\"fly_speed\":100000000000000000000000}}");
        var warnings = new List<string>();
        var store = CreateStore(warnings);

        store.Load();

        Assert.Equal(1000f, store.Current.Camera3d.FlySpeed);
        Assert.Equal(2, warnings.Count);
    }

    [Fact]
    public void Serializer_Warnings_UseTheInvariantCulture()
    {
        var warnings = new List<string>();
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
        try
        {
            EditorPreferencesSerializer.TryRead("{\"camera_3d\":{\"wheel_step_ratio\":0.001}}", warnings.Add, out _);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }

        Assert.Contains("0.001", Assert.Single(warnings));
    }

    [Fact]
    public void Serializer_InvalidJson_ReturnsFalseWithTheDefaults()
    {
        var warnings = new List<string>();

        Assert.False(EditorPreferencesSerializer.TryRead("{ not json", warnings.Add, out var read));

        Assert.Equal(EditorPreferences.Default, read);
        Assert.Single(warnings);
    }

    [Fact]
    public void Store_MissingFile_LoadsTheDefaults_AndWritesNothing()
    {
        var store = CreateStore();

        store.Load();
        _now = 10;

        Assert.Equal(EditorPreferences.Default, store.Current);
        Assert.False(store.FlushIfDue());
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void Store_UnreadableFile_IsNotOverwrittenUntilAChange()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
        File.WriteAllText(FilePath, "{ broken");
        var warnings = new List<string>();
        var store = CreateStore(warnings);

        store.Load();
        _now = 10;
        store.FlushIfDue();

        Assert.Equal("{ broken", File.ReadAllText(FilePath));
        Assert.Single(warnings);

        store.Update(Sample());
        _now = 20;
        Assert.True(store.FlushIfDue());
        Assert.True(EditorPreferencesSerializer.TryRead(File.ReadAllText(FilePath), null, out var saved));
        Assert.Equal(Sample(), saved);
    }

    [Fact]
    public void Store_ValueTypedCharacterByCharacter_IsWrittenExactlyOnce()
    {
        var store = CreateStore();
        store.Load();
        float[] typed = { 0f, 0f, 0.0f, 0.00f, 0.007f };

        for (int i = 0; i < typed.Length; i++)
        {
            _now += 0.2;
            store.Update(store.Current with { Camera3d = store.Current.Camera3d with { RotationSensitivity = typed[i] } });
            Assert.False(store.FlushIfDue());
        }

        _now += 0.5;
        Assert.False(store.FlushIfDue());
        _now += 0.6;
        Assert.True(store.FlushIfDue());
        _now += 5;
        Assert.False(store.FlushIfDue());

        Assert.Equal(1, store.WriteCount);
        Assert.Equal(0.007f, store.Current.Camera3d.RotationSensitivity);
    }

    [Fact]
    public void Store_Update_ClampsAppliesAtOnceAndNotifiesEverySubscriber()
    {
        var store = CreateStore();
        EditorPreferences first = null;
        EditorPreferences second = null;
        Action<EditorPreferences> firstHandler = p => first = p;
        store.Changed += firstHandler;
        store.Changed += p => second = p;

        store.Update(EditorPreferences.Default with { Camera3d = new Camera3dPreferences { FlySpeed = 5000f } });

        Assert.Equal(1000f, store.Current.Camera3d.FlySpeed);
        Assert.Same(store.Current, first);
        Assert.Same(store.Current, second);
        Assert.Equal(2, store.ChangedSubscriberCount);

        store.Changed -= firstHandler;
        Assert.Equal(1, store.ChangedSubscriberCount);
    }

    [Fact]
    public void Store_EqualValues_DoNotScheduleAWrite()
    {
        var store = CreateStore();

        store.Update(EditorPreferences.Default with { });

        Assert.False(store.HasPendingWrite);
    }

    [Fact]
    public void Store_Flush_WritesAPendingChangeAtOnce()
    {
        var store = CreateStore();
        store.Update(Sample());

        Assert.True(store.Flush());
        Assert.False(store.Flush());
        Assert.Equal(1, store.WriteCount);
        Assert.True(File.Exists(FilePath));
    }
}
