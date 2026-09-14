using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MinkQuickLax.Core.Model;

namespace MinkQuickLax.Core.Config;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(AppConfig))]
internal sealed partial class ConfigJsonContext : JsonSerializerContext;

/// <summary>The file is not a readable config (not JSON, or not an object).</summary>
public sealed class ConfigFormatException(string message, Exception? inner = null) : Exception(message, inner);

public sealed record DeserializedConfig(AppConfig Config, int FileSchemaVersion)
{
    public bool WasMigrated => FileSchemaVersion < ConfigMigrator.CurrentVersion;
    public bool IsNewerThanApp => FileSchemaVersion > ConfigMigrator.CurrentVersion;
}

public static class ConfigSerializer
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    // Keep Thai names and symbols readable in the file; it is never embedded in HTML.
    private static readonly ConfigJsonContext Writer = new(new JsonSerializerOptions(ConfigJsonContext.Default.Options)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });

    public static string Serialize(AppConfig config) =>
        JsonSerializer.Serialize(config, Writer.AppConfig);

    /// <exception cref="ConfigFormatException">The text is not a JSON object that maps to a config.</exception>
    public static DeserializedConfig Deserialize(string json)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json, documentOptions: DocumentOptions);
        }
        catch (JsonException ex)
        {
            throw new ConfigFormatException("Config is not valid JSON.", ex);
        }
        if (node is not JsonObject root)
        {
            throw new ConfigFormatException("Config root is not a JSON object.");
        }

        var fileVersion = ConfigMigrator.Migrate(root);
        try
        {
            var config = root.Deserialize(ConfigJsonContext.Default.AppConfig)
                ?? throw new ConfigFormatException("Config deserialized to null.");
            return new DeserializedConfig(config, fileVersion);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            throw new ConfigFormatException("Config does not match the expected shape.", ex);
        }
    }
}

/// <summary>Reads enums from camelCase strings; unknown values become the enum's 0 value instead of failing the whole file.</summary>
public sealed class TolerantEnumConverter<T> : JsonConverter<T>
    where T : struct, Enum
{
    private static readonly Dictionary<string, T> ByName = Enum.GetValues<T>()
        .ToDictionary(v => JsonNamingPolicy.CamelCase.ConvertName(v.ToString()), StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<T, string> NameOf = ByName.ToDictionary(p => p.Value, p => p.Key);

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && ByName.TryGetValue(reader.GetString()!, out var value))
        {
            return value;
        }
        reader.Skip();
        return default;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(NameOf.TryGetValue(value, out var name) ? name : JsonNamingPolicy.CamelCase.ConvertName(value.ToString()));
}
