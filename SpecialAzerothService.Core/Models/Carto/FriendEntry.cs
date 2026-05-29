using System.Text.Json.Serialization;

namespace SpecialAzerothService.Core.Models.Carto;

public sealed class FriendEntry
{
    public string Guid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;

    [JsonIgnore]
    public bool IsOnline { get; set; }
}
