using SMTMS.Core.Models;

namespace SMTMS.Core.Interfaces;

public interface INexusClient
{
    // Simplified model for now, using ModMetadata or a specific DTO
    Task<NexusModDto?> GetModInfoAsync(string modId, string apiKey);
}
