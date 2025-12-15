using System.Net.Http.Json;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;

namespace SMTMS.NexusClient.Services;

public class NexusClient : INexusClient
{
    private readonly HttpClient _httpClient;

    public NexusClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<NexusModDto?> GetModInfoAsync(string modId, string apiKey)
    {
        // TODO: Implement actual API call.
        
        return await Task.FromResult(new NexusModDto
        {
            UniqueID = modId,
            Summary = "Placeholder Summary"
        });
    }
}
