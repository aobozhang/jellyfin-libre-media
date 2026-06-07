using System.Net.Http.Json;
using LibreMedia.Models;
using Microsoft.Extensions.Logging;

namespace LibreMedia.Services;

public class CmsApiClient(HttpClient httpClient, ILogger<CmsApiClient> logger)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<CmsApiClient> _logger = logger;

    public async Task<CmsApiResponse?> GetContentAsync(string apiUrl, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        try
        {
            var url = $"{apiUrl.TrimEnd('/')}?format=1&page={page}&pageSize={pageSize}";
            var response = await _httpClient.GetFromJsonAsync<CmsApiResponse>(url, ct);
            if (response?.Code != 0)
            {
                _logger.LogWarning("CMS API returned code {Code}: {Msg}", response?.Code, response?.Msg);
                return null;
            }
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch content from {Url}", apiUrl);
            return null;
        }
    }

    public async Task<List<CmsVideoItem>> GetAllContentAsync(string apiUrl, CancellationToken ct = default)
    {
        var allItems = new List<CmsVideoItem>();
        int page = 1;
        const int pageSize = 50;

        while (true)
        {
            var response = await GetContentAsync(apiUrl, page, pageSize, ct);
            if (response?.List is not { Count: > 0 })
                break;

            allItems.AddRange(response.List);
            if (response.List.Count < pageSize)
                break;

            page++;
            if (page > 100) break; // safety limit
        }

        return allItems;
    }

    public async Task<List<CmsVideoItem>> GetLatestContentAsync(string apiUrl, int limit = 20, CancellationToken ct = default)
    {
        var allItems = await GetAllContentAsync(apiUrl, ct);
        return allItems
            .OrderByDescending(x => x.UpdateTime)
            .Take(limit)
            .ToList();
    }
}