using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;

namespace SMTMS.NexusClient.Services;

public class NexusClient(HttpClient httpClient) : INexusClient
{
    private readonly HttpClient _httpClient = httpClient;

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
