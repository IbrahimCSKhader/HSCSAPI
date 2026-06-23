using System.Text.Json;

namespace HSCSAPI.Services.Standards;

public class RxNormService : IRxNormService
{
    private const int DefaultApproximateMaxEntries = 20;
    private const int MaxApproximateEntries = 100;

    private readonly HttpClient _httpClient;

    public RxNormService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<JsonElement> FindRxcuiByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        return GetJsonAsync($"rxcui.json?name={EscapeRequired(name)}", cancellationToken);
    }

    public Task<JsonElement> FindDrugsAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        return GetJsonAsync($"drugs.json?name={EscapeRequired(name)}", cancellationToken);
    }

    public Task<JsonElement> FindApproximateAsync(
        string term,
        int maxEntries,
        CancellationToken cancellationToken = default)
    {
        var normalizedMaxEntries = maxEntries <= 0
            ? DefaultApproximateMaxEntries
            : Math.Min(maxEntries, MaxApproximateEntries);

        return GetJsonAsync(
            $"approximateTerm.json?term={EscapeRequired(term)}&maxEntries={normalizedMaxEntries}",
            cancellationToken);
    }

    public Task<JsonElement> GetPropertiesAsync(
        string rxcui,
        CancellationToken cancellationToken = default)
    {
        return GetJsonAsync($"rxcui/{EscapeRequired(rxcui)}/properties.json", cancellationToken);
    }

    public Task<JsonElement> GetRelatedAsync(
        string rxcui,
        string? tty,
        CancellationToken cancellationToken = default)
    {
        var path = $"rxcui/{EscapeRequired(rxcui)}/related.json";
        if (!string.IsNullOrWhiteSpace(tty))
        {
            path += $"?tty={Uri.EscapeDataString(tty.Trim())}";
        }

        return GetJsonAsync(path, cancellationToken);
    }

    public Task<JsonElement> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        return GetJsonAsync("version.json", cancellationToken);
    }

    private async Task<JsonElement> GetJsonAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(relativeUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.Clone();
    }

    private static string EscapeRequired(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", nameof(value));
        }

        return Uri.EscapeDataString(value.Trim());
    }
}
