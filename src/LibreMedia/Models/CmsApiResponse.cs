using System.Text.Json.Serialization;

namespace LibreMedia.Models;

public class CmsApiResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("msg")]
    public string Msg { get; set; } = string.Empty;

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("list")]
    public List<CmsVideoItem> List { get; set; } = [];

    [JsonPropertyName("api_site")]
    public Dictionary<string, CmsSubSource>? ApiSite { get; set; }
}