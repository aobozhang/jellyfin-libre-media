using System.Text.Json.Serialization;

namespace LibreMedia.Models;

public class CmsVideoItem
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("type_name")]
    public string TypeName { get; set; } = string.Empty;

    [JsonPropertyName("pic")]
    public string Pic { get; set; } = string.Empty;

    [JsonPropertyName("actor")]
    public string Actor { get; set; } = string.Empty;

    [JsonPropertyName("director")]
    public string Director { get; set; } = string.Empty;

    [JsonPropertyName("play_url")]
    public string PlayUrl { get; set; } = string.Empty;

    [JsonPropertyName("play_from")]
    public string PlayFrom { get; set; } = string.Empty;

    [JsonPropertyName("update_time")]
    public long UpdateTime { get; set; }

    [JsonPropertyName("hits")]
    public long Hits { get; set; }
}