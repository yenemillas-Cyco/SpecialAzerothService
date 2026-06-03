using SpecialAzerothService.Core.Models.Carto;

namespace WindowsOrganiserApp.Services;

public static class RosterExpandKeys
{
    public static string User(string userId) => $"u:{userId}";

    public static string Account(string userId, string accountId) => $"u:{userId}:a:{accountId}";

    public static string Category(string userId, string accountId, CharacterStatus category) =>
        $"u:{userId}:a:{accountId}:c:{category}";
}
