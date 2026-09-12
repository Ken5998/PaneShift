using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaneShift.Core;

public sealed record SettingsLoadResult(PaneShiftSettings Settings, string? Warning = null);
public sealed record SettingsCandidateResult(PaneShiftSettings? Settings, string? Error = null);

/// <summary>Reads on demand without rewriting files. Explicit Save atomically replaces validated settings.</summary>
public sealed class SettingsStore(string filePath)
{
    public string FilePath { get; } = Path.GetFullPath(filePath);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter<RepeatBehavior>(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    public SettingsLoadResult LoadOrCreate()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                var defaults = new PaneShiftSettings();
                string json = JsonSerializer.Serialize(defaults, JsonOptions);
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                // CreateNew also protects against replacing a file created concurrently.
                using var stream = new FileStream(FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var writer = new StreamWriter(stream);
                writer.WriteLine(json);
                return new(defaults);
            }
            return new(ReadValidated());
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            return new(new PaneShiftSettings(), $"Could not load or create settings at {FilePath}. " +
                $"Using defaults for this session; existing files were left unchanged. {ex.Message}");
        }
    }

    public SettingsCandidateResult LoadCandidate()
    {
        try { return new(ReadValidated()); }
        catch (Exception ex) when (ex is JsonException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            return new(null, $"Could not reload {FilePath}. Existing settings remain active. {ex.Message}");
        }
    }

    public void Save(PaneShiftSettings settings)
    {
        settings.Validate();
        var canonical = settings with { Hotkeys = HotkeySettings.ToMap(HotkeySettings.Resolve(settings)) };
        string json = JsonSerializer.Serialize(canonical, JsonOptions) + Environment.NewLine;
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using var writer = new StreamWriter(stream, leaveOpen: true);
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null);
            else File.Move(temporary, FilePath);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private PaneShiftSettings ReadValidated()
    {
        var settings = JsonSerializer.Deserialize<PaneShiftSettings>(File.ReadAllText(FilePath), JsonOptions)
            ?? throw new JsonException("Settings must be a JSON object.");
        settings.Validate();
        return settings;
    }
}
