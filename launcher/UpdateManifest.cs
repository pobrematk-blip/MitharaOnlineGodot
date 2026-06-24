using System.Text.Json.Serialization;

namespace Mithara.Launcher;

public sealed class UpdateManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "0.0.0";

    [JsonPropertyName("archive")]
    public string Archive { get; set; } = "MitharaOnline_Update.zip";

    [JsonPropertyName("archiveSha256")]
    public string ArchiveSha256 { get; set; } = "";

    [JsonPropertyName("gameExecutable")]
    public string GameExecutable { get; set; } = "MitharaOnlineTeste.exe";

    [JsonPropertyName("files")]
    public List<ManifestFile> Files { get; set; } = new();

    [JsonPropertyName("remove")]
    public List<string> Remove { get; set; } = new();
}

public sealed class ManifestFile
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = "";
}

public sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = "";

    [JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; set; } = new();
}

public sealed class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("browser_download_url")]
    public string DownloadUrl { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
