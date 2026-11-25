using System.Text.Json.Serialization;

namespace AstralParty.Toys.Mobile.Models;

public sealed class AdbEnvironmentStatus
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = "missing";

    [JsonPropertyName("resolvedPath")]
    public string ResolvedPath { get; set; } = "";

    [JsonPropertyName("configuredPath")]
    public string ConfiguredPath { get; set; } = "";

    [JsonPropertyName("configInvalid")]
    public bool ConfigInvalid { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = "";
}

public sealed class ItunesEnvironmentStatus
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "missing";

    [JsonPropertyName("installed")]
    public bool Installed { get; set; }

    [JsonPropertyName("serviceRunning")]
    public bool ServiceRunning { get; set; }

    [JsonPropertyName("serviceName")]
    public string ServiceName { get; set; } = "";

    [JsonPropertyName("executablePath")]
    public string ExecutablePath { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = "";
}

public sealed class MobileEnvironmentReport
{
    [JsonPropertyName("adb")]
    public AdbEnvironmentStatus Adb { get; set; } = new();

    [JsonPropertyName("itunes")]
    public ItunesEnvironmentStatus Itunes { get; set; } = new();

    [JsonPropertyName("checkedAt")]
    public string CheckedAt { get; set; } = "";
}

public sealed class AdbProcessScanResult
{
    [JsonPropertyName("found")]
    public bool Found { get; set; }

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("candidates")]
    public List<string> Candidates { get; set; } = [];

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}
