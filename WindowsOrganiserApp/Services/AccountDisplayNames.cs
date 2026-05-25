namespace WindowsOrganiserApp.Services;

public static class AccountDisplayNames
{
    public static string Resolve(string accountFolderName, IReadOnlyDictionary<string, string> mappings)
    {
        if (string.IsNullOrWhiteSpace(accountFolderName))
            return accountFolderName;

        return mappings.TryGetValue(accountFolderName, out var display) && !string.IsNullOrWhiteSpace(display)
            ? display
            : accountFolderName;
    }

    public static bool NamesMatch(string a, string b, IReadOnlyDictionary<string, string> mappings)
    {
        if (a.Equals(b, StringComparison.OrdinalIgnoreCase))
            return true;

        var resolvedA = Resolve(a, mappings);
        var resolvedB = Resolve(b, mappings);

        return resolvedA.Equals(b, StringComparison.OrdinalIgnoreCase)
            || a.Equals(resolvedB, StringComparison.OrdinalIgnoreCase)
            || resolvedA.Equals(resolvedB, StringComparison.OrdinalIgnoreCase);
    }
}
