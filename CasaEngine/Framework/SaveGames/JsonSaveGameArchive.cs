using System.Globalization;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.SaveGames;

/// <summary>
/// The JSON save-game format (ADR-0044): readable, fields found by name inside their object, for debugging and
/// hand editing. No checksum.
/// </summary>
/// <remarks>
/// <para>
/// Values: booleans as JSON booleans, integers as JSON integers, strings as JSON strings, integer arrays as JSON
/// arrays of integers, nested objects as JSON objects. A float is written as the exact <see cref="double"/> of its
/// value (for example <c>0.10000000149011612</c> for <c>0.1f</c>), so narrowing the double read back gives the
/// identical float, negative zero included; the shortest float text is not used because parsing it as a double and
/// narrowing could round differently.
/// </para>
/// <para>
/// Reading checks every value's <see cref="JTokenType"/> before converting it: an integer field accepts only an
/// integer token (not <c>1.5</c>, <c>true</c>, <c>null</c> nor a string); an integer token beyond 64 bits is refused;
/// a float field accepts a float or an integer token, since JSON has one number type and a hand-edited <c>2</c> is
/// a float; a boolean or string field accepts only its own token; <c>null</c> is never accepted. The archive then
/// checks the range of the C# type and refuses non-finite floats (<c>NaN</c>, <c>1e999</c>). Fields the object
/// does not read are ignored.
/// </para>
/// <para>
/// Writing a field name twice in one object is a developer error in <see cref="ISaveGameData.Serialize"/>, not a data
/// error: it throws <see cref="ArgumentException"/>, which propagates like any misuse of the archive. (The binary
/// format ignores names and cannot detect it.)
/// </para>
/// <para>
/// The document is parsed with explicit reader settings: no date parsing (a date-like string stays a string), floats
/// as doubles, depth at most <see cref="MaxDocumentDepth"/>, duplicate property names refused, strict UTF-8. No
/// serializer, no type names: the file never chooses what is instantiated.
/// </para>
/// </remarks>
internal sealed class JsonSaveGameArchive : SaveGameArchive
{
    /// <summary>
    /// Deepest JSON nesting the reader accepts. A written document stays well inside it:
    /// root, data, <see cref="SaveGameArchive.MaxObjectDepth"/> objects and one array.
    /// </summary>
    public const int MaxDocumentDepth = 64;

    private const string ContainerProperty = "container";
    private const string ContainerVersionProperty = "containerVersion";
    private const string DataVersionProperty = "dataVersion";
    private const string MetadataProperty = "metadata";
    private const string DataProperty = "data";

    private readonly List<JObject> _enclosingObjects = new();
    private JObject _currentObject;
    private JArray _currentArray;

    private JsonSaveGameArchive(bool isLoading, int dataVersion, JObject root)
        : base(isLoading, dataVersion)
    {
        Root = root;
        _currentObject = root;
    }

    /// <summary>The <c>data</c> object: written by a saving archive, read by a loading one.</summary>
    internal JObject Root { get; }

    internal static JsonSaveGameArchive ForReading(JObject data, int dataVersion)
    {
        ArgumentNullException.ThrowIfNull(data);

        return new JsonSaveGameArchive(true, dataVersion, data);
    }

    internal static JsonSaveGameArchive ForWriting(int dataVersion)
    {
        return new JsonSaveGameArchive(false, dataVersion, new JObject());
    }

    /// <summary>
    /// Writes the whole JSON file: envelope, <paramref name="sortedMetadata"/> in the given order, then the data
    /// <paramref name="data"/> serializes. UTF-8 without BOM, two-space indentation, '\n' line ends on every platform.
    /// </summary>
    internal static byte[] WriteFile(ISaveGameData data, List<KeyValuePair<string, string>> sortedMetadata)
    {
        var archive = ForWriting(data.LatestDataVersion);
        data.Serialize(archive);
        archive.ThrowIfObjectOpen();

        var metadataObject = new JObject();
        foreach (KeyValuePair<string, string> pair in sortedMetadata)
        {
            metadataObject.Add(pair.Key, JValue.CreateString(pair.Value));
        }

        var document = new JObject
        {
            { ContainerProperty, JValue.CreateString(SaveGameEnvelope.JsonContainerName) },
            { ContainerVersionProperty, new JValue((long)SaveGameEnvelope.CurrentContainerVersion) },
            { DataVersionProperty, new JValue((long)archive.DataVersion) },
            { MetadataProperty, metadataObject },
            { DataProperty, archive.Root },
        };

        using var stream = new MemoryStream();
        using (var textWriter = new StreamWriter(stream, SaveGameEnvelope.StrictUtf8, 4096, leaveOpen: true))
        {
            textWriter.NewLine = "\n";
            using var jsonWriter = new JsonTextWriter(textWriter)
            {
                Formatting = Formatting.Indented,
                Indentation = 2,
                IndentChar = ' ',
                Culture = CultureInfo.InvariantCulture,
                CloseOutput = false,
            };

            document.WriteTo(jsonWriter);
            jsonWriter.Flush();
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Parses the JSON file starting at <paramref name="start"/> (after an optional BOM) and checks its envelope.
    /// Parser exceptions escape to <see cref="SaveGameEnvelope.TryOpen"/>, which turns them into a result.
    /// </summary>
    internal static SaveGameEnvelopeResult TryOpenFile(
        byte[] fileBytes,
        int start,
        out int dataVersion,
        out IReadOnlyDictionary<string, string> metadata,
        out JObject data)
    {
        dataVersion = 0;
        metadata = null;
        data = null;

        JToken rootToken;
        using (var stream = new MemoryStream(fileBytes, start, fileBytes.Length - start, writable: false))
        using (var textReader = new StreamReader(stream, SaveGameEnvelope.StrictUtf8, detectEncodingFromByteOrderMarks: false))
        using (var jsonReader = new JsonTextReader(textReader))
        {
            jsonReader.DateParseHandling = DateParseHandling.None;
            jsonReader.FloatParseHandling = FloatParseHandling.Double;
            jsonReader.MaxDepth = MaxDocumentDepth;

            var loadSettings = new JsonLoadSettings
            {
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                CommentHandling = CommentHandling.Ignore,
                LineInfoHandling = LineInfoHandling.Ignore,
            };

            rootToken = JToken.Load(jsonReader, loadSettings);

            while (jsonReader.Read())
            {
                if (jsonReader.TokenType != JsonToken.Comment)
                {
                    return SaveGameEnvelopeResult.InvalidData("Save-game JSON file has content after its document.");
                }
            }
        }

        if (rootToken is not JObject document)
        {
            return SaveGameEnvelopeResult.InvalidData("Save-game JSON document is not an object.");
        }

        if (!document.TryGetValue(ContainerProperty, out JToken container)
            || container.Type != JTokenType.String
            || !string.Equals((string)((JValue)container).Value, SaveGameEnvelope.JsonContainerName, StringComparison.Ordinal))
        {
            return SaveGameEnvelopeResult.InvalidData(
                $"Save-game JSON document has no \"{ContainerProperty}\": \"{SaveGameEnvelope.JsonContainerName}\" field.");
        }

        if (!document.TryGetValue(ContainerVersionProperty, out JToken containerVersion)
            || containerVersion.Type != JTokenType.Integer)
        {
            return SaveGameEnvelopeResult.InvalidData(
                $"Save-game JSON field '{ContainerVersionProperty}' is missing or not an integer.");
        }

        if (((JValue)containerVersion).Value is not long containerVersionValue
            || containerVersionValue != SaveGameEnvelope.CurrentContainerVersion)
        {
            return SaveGameEnvelopeResult.UnsupportedContainer(
                $"Save-game JSON container version {DescribeInteger((JValue)containerVersion)} is not supported; this engine reads version {SaveGameEnvelope.CurrentContainerVersion}.");
        }

        if (!document.TryGetValue(DataVersionProperty, out JToken dataVersionToken)
            || dataVersionToken.Type != JTokenType.Integer
            || ((JValue)dataVersionToken).Value is not long dataVersionValue
            || dataVersionValue < 0
            || dataVersionValue > int.MaxValue)
        {
            return SaveGameEnvelopeResult.InvalidData(
                $"Save-game JSON field '{DataVersionProperty}' is missing, not an integer, negative or beyond {int.MaxValue}.");
        }

        if (!document.TryGetValue(MetadataProperty, out JToken metadataToken) || metadataToken is not JObject metadataObject)
        {
            return SaveGameEnvelopeResult.InvalidData($"Save-game JSON field '{MetadataProperty}' is missing or not an object.");
        }

        var metadataValues = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (JProperty property in metadataObject.Properties())
        {
            if (property.Value.Type != JTokenType.String)
            {
                return SaveGameEnvelopeResult.InvalidData(
                    $"Save-game JSON metadata value '{property.Name}' is a {property.Value.Type}, not a string.");
            }

            string value = (string)((JValue)property.Value).Value;
            if (!SaveGameEnvelope.IsWellFormedUtf16(property.Name) || !SaveGameEnvelope.IsWellFormedUtf16(value))
            {
                return SaveGameEnvelopeResult.InvalidData("Save-game JSON metadata has an unpaired UTF-16 surrogate.");
            }

            // The reader already refuses duplicate names; kept so the dictionary never silently keeps one of two.
            if (!metadataValues.TryAdd(property.Name, value))
            {
                return SaveGameEnvelopeResult.InvalidData($"Save-game JSON metadata key '{property.Name}' appears twice.");
            }
        }

        if (!document.TryGetValue(DataProperty, out JToken dataToken) || dataToken is not JObject dataObject)
        {
            return SaveGameEnvelopeResult.InvalidData($"Save-game JSON field '{DataProperty}' is missing or not an object.");
        }

        dataVersion = (int)dataVersionValue;
        metadata = metadataValues;
        data = dataObject;
        return SaveGameEnvelopeResult.Success;
    }

    private protected override bool ReadBooleanCore(string name)
    {
        JValue value = GetFieldValue(name, JTokenType.Boolean, "a boolean");
        return (bool)value.Value;
    }

    private protected override void WriteBooleanCore(string name, bool value)
    {
        AddField(name, new JValue(value));
    }

    private protected override long ReadIntegerCore(string name, SaveGameIntegerKind kind)
    {
        JValue value = GetFieldValue(name, JTokenType.Integer, "an integer");
        if (value.Value is long integer)
        {
            return integer;
        }

        throw CreateDataException(name, DescribeUnsupportedInteger(value));
    }

    private protected override void WriteIntegerCore(string name, long value, SaveGameIntegerKind kind)
    {
        AddField(name, new JValue(value));
    }

    private protected override float ReadSingleCore(string name)
    {
        if (!_currentObject.TryGetValue(name, out JToken token))
        {
            throw CreateDataException(name, "the field is missing.");
        }

        if (token is JValue value)
        {
            switch (value.Value)
            {
                case double number when token.Type == JTokenType.Float:
                    return (float)number;
                case long integer when token.Type == JTokenType.Integer:
                    return integer;
                case BigInteger bigInteger when token.Type == JTokenType.Integer:
                    // Beyond the float range: the archive refuses the resulting infinity.
                    return (float)(double)bigInteger;
            }
        }

        throw CreateDataException(name, $"expected a number, found {DescribeToken(token)}.");
    }

    private protected override void WriteSingleCore(string name, float value)
    {
        AddField(name, new JValue((double)value));
    }

    private protected override string ReadStringCore(string name)
    {
        JValue value = GetFieldValue(name, JTokenType.String, "a string");
        return (string)value.Value;
    }

    private protected override void WriteStringCore(string name, string value)
    {
        AddField(name, JValue.CreateString(value));
    }

    private protected override int ReadArrayStartCore(string name, SaveGameIntegerKind kind)
    {
        if (!_currentObject.TryGetValue(name, out JToken token))
        {
            throw CreateDataException(name, "the field is missing.");
        }

        if (token is not JArray array)
        {
            throw CreateDataException(name, $"expected an array, found {DescribeToken(token)}.");
        }

        _currentArray = array;
        return array.Count;
    }

    private protected override void WriteArrayStartCore(string name, int length, SaveGameIntegerKind kind)
    {
        var array = new JArray();
        AddField(name, array);
        _currentArray = array;
    }

    private protected override long ReadArrayElementCore(int index, SaveGameIntegerKind kind)
    {
        JToken token = _currentArray[index];
        if (token.Type != JTokenType.Integer)
        {
            throw CreateElementDataException(index, $"expected an integer, found {DescribeToken(token)}.");
        }

        var value = (JValue)token;
        if (value.Value is long integer)
        {
            return integer;
        }

        throw CreateElementDataException(index, DescribeUnsupportedInteger(value));
    }

    private protected override void WriteArrayElementCore(int index, long value, SaveGameIntegerKind kind)
    {
        _currentArray.Add(new JValue(value));
    }

    private protected override void EndArrayCore()
    {
        _currentArray = null;
    }

    private protected override void BeginObjectCore(string name)
    {
        JObject nested;
        if (IsLoading)
        {
            if (!_currentObject.TryGetValue(name, out JToken token))
            {
                throw CreateDataException(name, "the object is missing.");
            }

            if (token is not JObject tokenObject)
            {
                throw CreateDataException(name, $"expected an object, found {DescribeToken(token)}.");
            }

            nested = tokenObject;
        }
        else
        {
            nested = new JObject();
            AddField(name, nested);
        }

        _enclosingObjects.Add(_currentObject);
        _currentObject = nested;
    }

    private protected override void EndObjectCore()
    {
        _currentObject = _enclosingObjects[_enclosingObjects.Count - 1];
        _enclosingObjects.RemoveAt(_enclosingObjects.Count - 1);
    }

    private JValue GetFieldValue(string name, JTokenType expectedType, string expectedDescription)
    {
        if (!_currentObject.TryGetValue(name, out JToken token))
        {
            throw CreateDataException(name, "the field is missing.");
        }

        if (token.Type != expectedType)
        {
            throw CreateDataException(name, $"expected {expectedDescription}, found {DescribeToken(token)}.");
        }

        return (JValue)token;
    }

    private void AddField(string name, JToken value)
    {
        if (_currentObject.ContainsKey(name))
        {
            throw new ArgumentException(
                $"Save-game field '{GetFieldPath(name)}' is written twice in the same object; field names must be unique inside one object.",
                nameof(name));
        }

        _currentObject.Add(name, value);
    }

    private static string DescribeToken(JToken token)
    {
        // The type only: the value may be a hostile, arbitrarily long string.
        return token.Type == JTokenType.Null ? "null" : "a JSON " + token.Type.ToString().ToLowerInvariant();
    }

    private static string DescribeUnsupportedInteger(JValue value)
    {
        return value.Value is BigInteger
            ? "the integer is outside the 64-bit range."
            : "the integer has an unsupported representation.";
    }

    private static string DescribeInteger(JValue value)
    {
        return value.Value is long integer
            ? integer.ToString(CultureInfo.InvariantCulture)
            : "(beyond 64 bits)";
    }
}
