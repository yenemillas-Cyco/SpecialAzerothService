using SpecialAzerothService.Core.Services;

var root = args.Length > 0 ? args[0] : @"D:\Programmes\World of Warcraft";
if (!WowInstallPaths.TryGetWtfAccountDirectory(root, out var wtf))
{
    Console.Error.WriteLine("WTF invalid for: " + root);
    return 1;
}

var files = WowWtfAccountScanner.FindWowSyncLuaFiles(wtf);
var byAccount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
var globalKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var totalRaw = 0;

foreach (var entry in files)
{
    var parsed = LuaTableParser.ParseFile(entry.FilePath);
    if (!parsed.TryGetValue("WowSyncDB", out var dbObj) || dbObj is not Dictionary<string, object?> db)
        continue;

    if (!byAccount.ContainsKey(entry.AccountFolder))
        byAccount[entry.AccountFolder] = 0;

    foreach (var (charKey, charValue) in db)
    {
        if (charValue is not Dictionary<string, object?> cd) continue;
        var name = LuaTableParser.GetString(cd, "name");
        if (string.IsNullOrWhiteSpace(name)) continue;
        totalRaw++;
        byAccount[entry.AccountFolder]++;
        globalKeys.Add($"{entry.AccountFolder}|{name}|{LuaTableParser.GetString(cd, "realm")}");
    }
}

Console.WriteLine($"Files={files.Count} raw entries={totalRaw} unique account|name|realm={globalKeys.Count}");
foreach (var kv in byAccount.OrderBy(k => k.Key))
    Console.WriteLine($"  {kv.Key}: {kv.Value}");

return 0;
