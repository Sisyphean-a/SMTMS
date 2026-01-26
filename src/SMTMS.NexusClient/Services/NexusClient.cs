using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;

namespace SMTMS.NexusClient.Services;

public class NexusClient(HttpClient httpClient, ILogger<NexusClient> logger) : INexusClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<NexusClient> _logger = logger;
    private const string BaseUrl = "https://api.nexusmods.com";

    public async Task<NexusModDto?> GetModInfoAsync(string modId, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(modId) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("GetModInfoAsync called with empty modId or apiKey.");
            return null;
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/v1/games/stardewvalley/mods/{modId}.json");
            request.Headers.Add("apikey", apiKey);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("SMTMS", "1.0.0"));

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch mod info for {ModId}. Status Code: {StatusCode}", modId, response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var modInfo = JsonConvert.DeserializeObject<NexusModResponse>(json);

            if (modInfo == null)
            {
                _logger.LogWarning("Failed to deserialize mod info for {ModId}.", modId);
                return null;
            }

            return new NexusModDto
            {
                UniqueID = modInfo.ModId.ToString(), // Assuming UniqueID in DTO maps to mod_id
                Summary = modInfo.Summary,
                Description = modInfo.Description,
                PictureUrl = modInfo.PictureUrl,
                DownloadCount = modInfo.Downloads,
                EndorsementCount = modInfo.EndorsementCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching mod info for {ModId}.", modId);
            return null;
        }
    }

    private class NexusModResponse
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("summary")]
        public string Summary { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("picture_url")]
        public string PictureUrl { get; set; } = string.Empty;

        [JsonProperty("mod_id")]
        public long ModId { get; set; }

        [JsonProperty("downloads")]
        public long Downloads { get; set; }

        [JsonProperty("endorsement_count")]
        public long EndorsementCount { get; set; }
    }
}
