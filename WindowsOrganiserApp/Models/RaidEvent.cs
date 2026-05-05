using System.Text.Json.Serialization;

namespace WindowsOrganiserApp.Models;

public sealed class RaidEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("startTime")]
    public long StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public long EndTime { get; set; }

    [JsonPropertyName("leaderId")]
    public string LeaderId { get; set; } = string.Empty;

    [JsonPropertyName("leaderName")]
    public string LeaderName { get; set; } = string.Empty;

    [JsonPropertyName("channelId")]
    public string ChannelId { get; set; } = string.Empty;

    [JsonPropertyName("signUps")]
    public List<RaidSignUp>? SignUps { get; set; }

    public DateTime StartDateTime =>
        DateTimeOffset.FromUnixTimeSeconds(StartTime).LocalDateTime;

    public DateTime EndDateTime =>
        DateTimeOffset.FromUnixTimeSeconds(EndTime).LocalDateTime;

    public int SignUpCount => SignUps?.Count ?? 0;
}

public sealed class RaidSignUp
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("className")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("specName")]
    public string SpecName { get; set; } = string.Empty;

    [JsonPropertyName("roleName")]
    public string RoleName { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}
