using System.Text.Json;

namespace HSCSAPI.Services.Standards;

public interface IRxNormService
{
    Task<JsonElement> FindRxcuiByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<JsonElement> FindDrugsAsync(string name, CancellationToken cancellationToken = default);
    Task<JsonElement> FindApproximateAsync(string term, int maxEntries, CancellationToken cancellationToken = default);
    Task<JsonElement> GetPropertiesAsync(string rxcui, CancellationToken cancellationToken = default);
    Task<JsonElement> GetRelatedAsync(string rxcui, string? tty, CancellationToken cancellationToken = default);
    Task<JsonElement> GetVersionAsync(CancellationToken cancellationToken = default);
}
