using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Serilog;
using WindowsOrganiserApp.Models;

namespace WindowsOrganiserApp.Services;

public interface IRaidHelperService
{
    Task<List<RaidEvent>> GetUserEventsAsync(string apiKey);
    Task<List<RaidEvent>> GetServerEventsAsync(string serverId, string apiKey);
    Task<RaidEvent?> GetEventDetailsAsync(string eventId);
}

public class RaidHelperService : IRaidHelperService
{
    private const string BaseUrl = "https://raid-helper.dev/api/v3";
    private readonly HttpClient _http;
    private readonly ILogger _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RaidHelperService(ILogger logger)
    {
        _logger = logger;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public async Task<List<RaidEvent>> GetUserEventsAsync(string apiKey)
    {
        try
        {
            var response = await _http.GetAsync($"{BaseUrl}/users/{apiKey}/events");
            response.EnsureSuccessStatusCode();

            var wrapper = await response.Content.ReadFromJsonAsync<RaidEventsResponse>(JsonOpts);
            return wrapper?.PostedEvents ?? [];
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to fetch RaidHelper user events");
            return [];
        }
    }

    public async Task<List<RaidEvent>> GetServerEventsAsync(string serverId, string apiKey)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{BaseUrl}/servers/{serverId}/events");
            request.Headers.Add("Authorization", apiKey);
            request.Headers.Add("IncludeSignUps", "true");

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var wrapper = await response.Content.ReadFromJsonAsync<RaidEventsResponse>(JsonOpts);
            return wrapper?.PostedEvents ?? [];
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to fetch RaidHelper events for server {ServerId}", serverId);
            return [];
        }
    }

    public async Task<RaidEvent?> GetEventDetailsAsync(string eventId)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<RaidEvent>(
                $"{BaseUrl}/events/{eventId}", JsonOpts);
            return response;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to fetch RaidHelper event {EventId}", eventId);
            return null;
        }
    }
}

file sealed class RaidEventsResponse
{
    public List<RaidEvent>? PostedEvents { get; set; }
}
