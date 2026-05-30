using System.IO;
using SpecialAzerothService.Core.Models.Carto;
using SpecialAzerothService.Core.Models.WowSync;

namespace SpecialAzerothService.Core.Services;

public interface IWowSyncService
{
    string AddonVersion { get; }
    string WowPath { get; set; }
    string ResolvedWtfPath { get; }
    void DeployAddon();
    List<WowAccountData> ReadAllAccounts(IReadOnlyDictionary<string, CartoAccountConfig>? accountSettings = null);
}

public sealed class WowSyncService : IWowSyncService
{
    public const string AddonVersionValue = "1.4.0";
    public string AddonVersion => AddonVersionValue;

    private readonly ISettingsService _settingsService;
    private readonly ICartoService _cartoService;

    public string WowPath
    {
        get => WowInstallPaths.NormalizeGameRoot(_settingsService.Load().WowPath);
        set
        {
            var s = _settingsService.Load();
            s.WowPath = WowInstallPaths.NormalizeGameRoot(value);
            _settingsService.Save(s);
        }
    }

    public WowSyncService(ISettingsService settingsService, ICartoService cartoService)
    {
        _settingsService = settingsService;
        _cartoService = cartoService;
    }

    public void DeployAddon()
    {
        var addonsDir = WowInstallPaths.GetAddonsDirectory(WowPath);
        Directory.CreateDirectory(addonsDir);

        File.WriteAllText(Path.Combine(addonsDir, "WowSync.toc"), TocContent);
        File.WriteAllText(Path.Combine(addonsDir, "WowSync.lua"), LuaContent);
    }

    public string ResolvedWtfPath => WowInstallPaths.GetWtfAccountDirectory(WowPath);

    public List<WowAccountData> ReadAllAccounts(IReadOnlyDictionary<string, CartoAccountConfig>? accountSettings = null)
    {
        var accounts = new List<WowAccountData>();
        var wtfPath = ResolvedWtfPath;
        if (!Directory.Exists(wtfPath)) return accounts;

        IReadOnlyDictionary<string, CartoAccountConfig> settings;
        if (accountSettings != null)
            settings = accountSettings;
        else
        {
            var carto = _cartoService.Load();
            CartoAccountSettings.MigrateLegacyDisplayNames(carto);
            settings = carto.AccountSettings ?? new Dictionary<string, CartoAccountConfig>(StringComparer.OrdinalIgnoreCase);
        }

        foreach (var accountDir in Directory.GetDirectories(wtfPath))
        {
            var svFile = Path.Combine(accountDir, "SavedVariables", "WowSync.lua");
            if (!File.Exists(svFile)) continue;

            var folderName = Path.GetFileName(accountDir);
            var account = new WowAccountData
            {
                SourceAccountName = folderName,
                AccountName = CartoAccountSettings.ResolveDisplayName(folderName, settings)
            };

            try
            {
                var parsed = LuaTableParser.ParseFile(svFile);
                if (!parsed.TryGetValue("WowSyncDB", out var dbObj) ||
                    dbObj is not Dictionary<string, object?> db)
                    continue;

                foreach (var (charKey, charValue) in db)
                {
                    if (charValue is not Dictionary<string, object?> charData) continue;
                    var ch = ParseCharacter(charData);
                    ch.StorageKey = charKey.Trim();
                    account.Characters.Add(ch);
                }
            }
            catch { /* skip corrupted files */ }

            if (account.Characters.Count > 0)
                accounts.Add(account);
        }

        return accounts;
    }

    private static WowCharacterData ParseCharacter(Dictionary<string, object?> d)
    {
        var ch = new WowCharacterData
        {
            Name = LuaTableParser.GetString(d, "name"),
            Realm = LuaTableParser.GetString(d, "realm"),
            Level = LuaTableParser.GetInt(d, "level"),
            XpPercent = LuaTableParser.GetDouble(d, "xpPercent", -1),
            Class = LuaTableParser.GetString(d, "class"),
            Race = LuaTableParser.GetString(d, "race"),
            Gold = LuaTableParser.GetLong(d, "gold"),
            Zone = LuaTableParser.GetString(d, "zone"),
            SubZone = LuaTableParser.GetString(d, "subZone"),
            X = LuaTableParser.GetDouble(d, "x"),
            Y = LuaTableParser.GetDouble(d, "y"),
            MapId = LuaTableParser.GetInt(d, "mapId"),
            LastUpdate = LuaTableParser.GetString(d, "lastUpdate")
        };

        var profs = LuaTableParser.GetTable(d, "professions");
        if (profs != null)
        {
            foreach (var (_, pv) in profs)
            {
                if (pv is not Dictionary<string, object?> pd) continue;
                ch.Professions.Add(new WowProfession
                {
                    Name = LuaTableParser.GetString(pd, "name"),
                    Rank = LuaTableParser.GetInt(pd, "rank"),
                    MaxRank = LuaTableParser.GetInt(pd, "maxRank")
                });
            }
        }

        ch.Inventory = ParseItems(LuaTableParser.GetTable(d, "inventory"));
        ch.Bank = ParseItems(LuaTableParser.GetTable(d, "bank"));
        ch.Mail = ParseMail(LuaTableParser.GetTable(d, "mail"));
        ch.Sync = ParseSyncMeta(LuaTableParser.GetTable(d, "syncMeta"));
        ch.Cooldowns = ParseCooldowns(LuaTableParser.GetTable(d, "cooldowns"));
        ch.KnownCooldownKeys = ParseKnownCooldownKeys(LuaTableParser.GetTable(d, "knownCooldowns"));

        return ch;
    }

    private static WowSyncMeta ParseSyncMeta(Dictionary<string, object?>? table)
    {
        if (table == null) return new WowSyncMeta();
        return new WowSyncMeta
        {
            Inventory = LuaTableParser.GetString(table, "inventory"),
            Bank = LuaTableParser.GetString(table, "bank"),
            Mail = LuaTableParser.GetString(table, "mail"),
            Professions = LuaTableParser.GetString(table, "professions"),
            Cooldowns = LuaTableParser.GetString(table, "cooldowns")
        };
    }

    private static List<string> ParseKnownCooldownKeys(Dictionary<string, object?>? table)
    {
        var keys = new List<string>();
        if (table == null)
            return keys;

        foreach (var (_, value) in table)
        {
            if (value is Dictionary<string, object?> row)
            {
                var key = LuaTableParser.GetString(row, "key");
                if (!string.IsNullOrWhiteSpace(key))
                    keys.Add(key.Trim());
            }
            else if (value is string s && !string.IsNullOrWhiteSpace(s))
                keys.Add(s.Trim());
        }

        return keys
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<WowProfessionCooldown> ParseCooldowns(Dictionary<string, object?>? table)
    {
        var list = new List<WowProfessionCooldown>();
        if (table == null) return list;
        foreach (var (_, cv) in table)
        {
            if (cv is not Dictionary<string, object?> cd) continue;
            list.Add(new WowProfessionCooldown
            {
                Key = LuaTableParser.GetString(cd, "key"),
                Name = LuaTableParser.GetString(cd, "name"),
                RemainingSec = LuaTableParser.GetDouble(cd, "remainingSec"),
                ScannedAt = LuaTableParser.GetDouble(cd, "scannedAt")
            });
        }
        return CollapseAlchemySyncCooldowns(list.OrderBy(c => c.IsReady).ThenBy(c => c.ReadyAtUtc).ToList());
    }

    private static List<WowProfessionCooldown> CollapseAlchemySyncCooldowns(List<WowProfessionCooldown> list)
    {
        var alchemy = list.Where(c => CooldownGroups.IsAlchemySyncKey(c.Key)).ToList();
        if (alchemy.Count <= 1)
            return list;

        var rest = list.Where(c => !CooldownGroups.IsAlchemySyncKey(c.Key)).ToList();
        var best = alchemy
            .Where(c => !c.IsReady)
            .OrderByDescending(c => c.ReadyAtUtc ?? DateTime.MinValue)
            .FirstOrDefault() ?? alchemy[0];

        rest.Add(best);
        return rest;
    }

    private static List<WowItem> ParseItems(Dictionary<string, object?>? table)
    {
        if (table == null) return [];

        var aggregated = new Dictionary<string, WowItem>();
        foreach (var (_, iv) in table)
        {
            if (iv is not Dictionary<string, object?> id) continue;
            var name = LuaTableParser.GetString(id, "name");
            var count = Math.Max(1, LuaTableParser.GetInt(id, "count", 1));
            var itemId = LuaTableParser.GetInt(id, "itemId");
            var quality = LuaTableParser.GetInt(id, "quality");

            if (string.IsNullOrWhiteSpace(name))
            {
                if (itemId > 0) name = $"item:{itemId}";
                else continue;
            }

            if (aggregated.TryGetValue(name, out var existing))
            {
                existing.Count += count;
            }
            else
            {
                aggregated[name] = new WowItem
                {
                    Name = name,
                    Count = count,
                    ItemId = itemId,
                    Icon = LuaTableParser.GetLong(id, "icon"),
                    Quality = quality
                };
            }
        }

        return aggregated.Values
            .OrderByDescending(i => i.Quality)
            .ThenBy(i => i.Name)
            .ToList();
    }

    private static List<WowMailEntry> ParseMail(Dictionary<string, object?>? table)
    {
        var mails = new List<WowMailEntry>();
        if (table == null) return mails;
        foreach (var (_, mv) in table)
        {
            if (mv is not Dictionary<string, object?> md) continue;
            var mail = new WowMailEntry
            {
                Sender = LuaTableParser.GetString(md, "sender"),
                Subject = LuaTableParser.GetString(md, "subject"),
                Money = LuaTableParser.GetLong(md, "money"),
                DaysLeft = LuaTableParser.GetDouble(md, "daysLeft"),
                Items = ParseItems(LuaTableParser.GetTable(md, "items"))
            };
            mails.Add(mail);
        }
        return mails;
    }

    #region Addon content

    private static string TocContent =>
        $"""
        ## Interface: 11506
        ## Title: WowSync
        ## Notes: Synchronise les donnees du personnage avec l'application WowSync
        ## Author: WowSync
        ## SavedVariables: WowSyncDB
        ## Version: {AddonVersionValue}

        WowSync.lua
        """;

    private const string LuaContent =
        """
        local WOWSYNC_VERSION = "1.4.0"
        local WOWSYNC_DEBUG = false
        local function WSLog(msg)
            if WOWSYNC_DEBUG then print(msg) end
        end

        WowSyncDB = WowSyncDB or {}

        local _GetNumSlots = C_Container and C_Container.GetContainerNumSlots or GetContainerNumSlots
        local _GetItemLink = C_Container and C_Container.GetContainerItemLink or GetContainerItemLink
        local _GetItemInfo = C_Container and C_Container.GetContainerItemInfo or GetContainerItemInfo

        local frame = CreateFrame("Frame")
        local bankOpen = false
        local mailOpen = false
        local loginTimer = 0
        local pendingLogin = false

        local function GetCharKey()
            return UnitName("player") .. "-" .. GetRealmName()
        end

        local function GetSlotInfo(bag, slot)
            local info = _GetItemInfo(bag, slot)
            if type(info) == "table" then
                return info.stackCount or info.itemCount or 1, info.itemID or 0, info.iconFileID or 0
            end
            local _, count = _GetItemInfo(bag, slot)
            return count or 1, 0, 0
        end

        local function GetItemIdFromLink(link)
            if not link then return 0 end
            local id = link:match("item:(%d+)")
            return tonumber(id) or 0
        end

        local function GetItemNameFromLink(link)
            if not link then return "" end
            local name = link:match("%[(.-)%]")
            if name and name ~= "" then return name end
            return link
        end

        local function ScanSlot(items, bag, slot)
            local link = _GetItemLink(bag, slot)
            if not link then return end
            local count, itemId, iconId = GetSlotInfo(bag, slot)
            if itemId == 0 then itemId = GetItemIdFromLink(link) end
            local name, _, quality, _, _, _, _, _, _, icon = GetItemInfo(link)
            if not name or name == "" then
                name = GetItemNameFromLink(link)
            end
            if iconId == 0 and icon then iconId = icon end
            table.insert(items, {
                name = name,
                count = count,
                itemId = itemId,
                icon = iconId,
                quality = quality or 0
            })
        end

        local function ScanContainer(fromBag, toBag)
            local items = {}
            for bag = fromBag, toBag do
                local slots = _GetNumSlots(bag)
                for slot = 1, slots do
                    ScanSlot(items, bag, slot)
                end
            end
            return items
        end

        local function ScanBank()
            local items = {}
            local mainSlots = _GetNumSlots(-1)
            if mainSlots == 0 then mainSlots = 28 end
            for slot = 1, mainSlots do
                ScanSlot(items, -1, slot)
            end
            for bag = 5, 10 do
                local slots = _GetNumSlots(bag)
                for slot = 1, slots do
                    ScanSlot(items, bag, slot)
                end
            end
            return items
        end

        local function normZone(s)
            if not s or s == "" then return "" end
            s = string.lower(s)
            s = s:gsub("é", "e"):gsub("è", "e"):gsub("ê", "e"):gsub("à", "a")
            s = s:gsub("ô", "o"):gsub("ù", "u"):gsub("ç", "c"):gsub("â", "a"):gsub("î", "i")
            s = s:gsub("'", ""):gsub("'", ""):gsub("-", " ")
            return s
        end

        local ZONE_TO_MAP = {
            ["durotar"] = 1411, ["mulgore"] = 1412, ["les tarides"] = 1413, ["tarides"] = 1413,
            ["teldrassil"] = 1438, ["sombrivage"] = 1439, ["orneval"] = 1440, ["mille pointes"] = 1441,
            ["serres rocheuses"] = 1442, ["desolace"] = 1443, ["feralas"] = 1444,
            ["marecage d aprefange"] = 1445, ["tanaris"] = 1446, ["azshara"] = 1447,
            ["gangrebois"] = 1448, ["cratere d ungoro"] = 1449, ["ungoro"] = 1449,
            ["refuge du marechal"] = 1449, ["reflet de lune"] = 1450, ["silithus"] = 1451,
            ["fort cenarien"] = 1451, ["cenarion hold"] = 1451,
            ["berceau de l hiver"] = 1452, ["long guet"] = 1452,
            ["orgrimmar"] = 1454, ["les pitons du tonnerre"] = 1456, ["darnassus"] = 1457,
            ["montagnes d alterac"] = 1416, ["hautes terres d arathi"] = 1417,
            ["terres ingrates"] = 1418, ["terres foudroyees"] = 1419,
            ["clairieres de tirisfal"] = 1420, ["tirisfal"] = 1420,
            ["foret des pins argentes"] = 1421, ["maleterres de l ouest"] = 1422,
            ["maleterres de l est"] = 1423, ["contreforts de hillsbrad"] = 1424,
            ["les hinterlands"] = 1425, ["dun morogh"] = 1426, ["gorge des vents brulants"] = 1427,
            ["steppes ardentes"] = 1428, ["foret d elwynn"] = 1429, ["defile de deuillevent"] = 1430,
            ["bois de la penombre"] = 1431, ["loch modan"] = 1432, ["les carmines"] = 1433,
            ["vallee de strangleronce"] = 1434, ["strangleronce"] = 1434,
            ["marais des chagrins"] = 1435, ["marche de l ouest"] = 1436,
            ["les paluns"] = 1437, ["hurlevent"] = 1453, ["forgefer"] = 1455,
            ["fossoyeuse"] = 1458, ["auberdine"] = 1439, ["gadgetzan"] = 1446, ["croisee"] = 1413,
        }

        local function ResolveMapIdFromNames(zone, sub)
            for _, z in ipairs({ sub, zone }) do
                if z and z ~= "" then
                    local id = ZONE_TO_MAP[normZone(z)]
                    if id then return id end
                end
            end
            return 0
        end

        local function TouchSync(key, field)
            if not WowSyncDB[key] then return end
            WowSyncDB[key].syncMeta = WowSyncDB[key].syncMeta or {}
            WowSyncDB[key].syncMeta[field] = date("%Y-%m-%d %H:%M:%S")
        end

        local function FormatDuration(sec)
            sec = math.max(0, math.floor(sec or 0))
            if sec >= 86400 then
                return string.format("%dj %dh", math.floor(sec / 86400), math.floor((sec % 86400) / 3600))
            end
            if sec >= 3600 then
                return string.format("%dh %dm", math.floor(sec / 3600), math.floor((sec % 3600) / 60))
            end
            return string.format("%dm", math.floor(sec / 60))
        end

        -- Classic Era : pas de GetItemCooldown(itemId) global — parcourir les sacs
        local function GetItemCooldownByItemId(itemId)
            if not itemId then return 0, 0 end
            for bag = 0, 4 do
                local slots = _GetNumSlots(bag) or 0
                for slot = 1, slots do
                    local slotId = nil
                    if C_Container and C_Container.GetContainerItemInfo then
                        local info = C_Container.GetContainerItemInfo(bag, slot)
                        slotId = info and (info.itemID or info.itemId)
                    else
                        local link = _GetItemLink(bag, slot)
                        if link then slotId = tonumber(link:match("item:(%d+)")) end
                    end
                    if slotId == itemId then
                        if C_Container and C_Container.GetContainerItemCooldown then
                            return C_Container.GetContainerItemCooldown(bag, slot)
                        end
                        if type(GetContainerItemCooldown) == "function" then
                            return GetContainerItemCooldown(bag, slot)
                        end
                        return 0, 0
                    end
                end
            end
            return 0, 0
        end

        local function SpellIsKnown(spellId)
            if not spellId or spellId <= 0 then return false end
            if IsSpellKnown then
                if IsSpellKnown(spellId, false) then return true end
                if IsSpellKnown(spellId) then return true end
            end
            if IsPlayerSpell and IsPlayerSpell(spellId) then return true end
            return false
        end

        local function PlayerHasItemId(itemId)
            if not itemId or itemId <= 0 then return false end
            for bag = 0, 4 do
                local slots = _GetNumSlots(bag) or 0
                for slot = 1, slots do
                    local slotId = nil
                    if C_Container and C_Container.GetContainerItemInfo then
                        local info = C_Container.GetContainerItemInfo(bag, slot)
                        slotId = info and (info.itemID or info.itemId)
                    else
                        local link = _GetItemLink(bag, slot)
                        if link then slotId = tonumber(link:match("item:(%d+)")) end
                    end
                    if slotId == itemId then return true end
                end
            end
            return false
        end

        local function AddKnownKey(keys, seen, key)
            if not key or seen[key] then return end
            seen[key] = true
            table.insert(keys, { key = key })
        end

        local function ScanKnownProfessionCooldowns()
            local keys = {}
            local seen = {}
            if SpellIsKnown(18560) then AddKnownKey(keys, seen, "mooncloth") end
            if SpellIsKnown(17187) then AddKnownKey(keys, seen, "arcanite") end
            for _, spellId in ipairs({ 17559, 17560, 17561, 17562, 17563, 17564, 17565, 17566 }) do
                if SpellIsKnown(spellId) then AddKnownKey(keys, seen, "elemental") end
            end
            if PlayerHasItemId(15846) then AddKnownKey(keys, seen, "salt") end
            if TradeSkillFrame and TradeSkillFrame:IsShown() and GetNumTradeSkills then
                for i = 1, GetNumTradeSkills() do
                    local skillName, skillType = GetTradeSkillInfo(i)
                    if skillType ~= "header" then
                        local lname = string.lower(skillName or "")
                        if lname:find("lunaire") or lname:find("mooncloth") then
                            AddKnownKey(keys, seen, "mooncloth")
                        elseif lname:find("arcanite") then
                            AddKnownKey(keys, seen, "arcanite")
                        elseif lname:find("element") or lname:find("elementaire") then
                            AddKnownKey(keys, seen, "elemental")
                        elseif lname:find("sel") and (lname:find("rafin") or lname:find("tamis") or lname:find("shaker")) then
                            AddKnownKey(keys, seen, "salt")
                        end
                    end
                end
            end
            return keys
        end

        local function MergeKnownCooldowns(prev, fresh)
            local seen = {}
            local out = {}
            local function ingest(list)
                if not list then return end
                for _, item in ipairs(list) do
                    local k = type(item) == "table" and item.key or item
                    AddKnownKey(out, seen, k)
                end
            end
            ingest(prev)
            ingest(fresh)
            return out
        end

        local function RefreshKnownCooldowns(entry)
            if not entry then return end
            entry.knownCooldowns = MergeKnownCooldowns(entry.knownCooldowns, ScanKnownProfessionCooldowns())
            if entry.knownCooldowns and #entry.knownCooldowns > 0 then
                TouchSync(GetCharKey(), "knownCooldowns")
            end
        end

        local function ScanCooldowns()
            local cds = {}
            local now = time()
            -- Transmu alchimie : un seul CD partage (arcanite + elementaires)
            local alchemySpells = {
                { key = "arcanite", name = "Transmu. Arcanite", id = 17187 },
                { key = "elemental", name = "Transmu. Air / Feu", id = 17559 },
                { key = "elemental", name = "Transmu. Feu / Terre", id = 17560 },
                { key = "elemental", name = "Transmu. Terre / Eau", id = 17561 },
                { key = "elemental", name = "Transmu. Eau / Air", id = 17562 },
                { key = "elemental", name = "Transmu. Mort / Eau", id = 17563 },
                { key = "elemental", name = "Transmu. Eau / Mort", id = 17564 },
                { key = "elemental", name = "Transmu. Vie / Terre", id = 17565 },
                { key = "elemental", name = "Transmu. Terre / Vie", id = 17566 },
            }
            local bestAlchemyRemaining = 0
            local bestAlchemyKey, bestAlchemyName = nil, nil
            for _, spell in ipairs(alchemySpells) do
                local start, duration = GetSpellCooldown(spell.id)
                if start and duration and duration > 2 then
                    local remaining = start + duration - GetTime()
                    if remaining > bestAlchemyRemaining then
                        bestAlchemyRemaining = remaining
                        bestAlchemyKey = spell.key
                        bestAlchemyName = spell.name
                    end
                end
            end
            if bestAlchemyRemaining > 1 and bestAlchemyKey then
                table.insert(cds, {
                    key = bestAlchemyKey, name = bestAlchemyName,
                    remainingSec = bestAlchemyRemaining, scannedAt = now
                })
            end
            local tracked = {
                { key = "mooncloth", name = "Etoffe lunaire", spells = { 18560 } },
            }
            for _, t in ipairs(tracked) do
                for _, spellId in ipairs(t.spells) do
                    local start, duration = GetSpellCooldown(spellId)
                    if start and duration and duration > 2 then
                        local remaining = start + duration - GetTime()
                        if remaining > 1 then
                            table.insert(cds, {
                                key = t.key, name = t.name,
                                remainingSec = remaining, scannedAt = now
                            })
                            break
                        end
                    end
                end
            end
            local saltStart, saltDur = GetItemCooldownByItemId(15846)
            if saltStart and saltDur and saltDur > 2 then
                local remaining = saltStart + saltDur - GetTime()
                if remaining > 1 then
                    table.insert(cds, {
                        key = "salt", name = "Tamis a sel",
                        remainingSec = remaining, scannedAt = now
                    })
                end
            end
            if TradeSkillFrame and TradeSkillFrame:IsShown() and GetNumTradeSkills then
                for i = 1, GetNumTradeSkills() do
                    local skillName, skillType, _, _, _, cooldown = GetTradeSkillInfo(i)
                    if skillType ~= "header" and cooldown and cooldown > 0 then
                        local lname = string.lower(skillName or "")
                        local key, label
                        if lname:find("lunaire") or lname:find("mooncloth") then
                            key, label = "mooncloth", "Etoffe lunaire"
                        elseif lname:find("arcanite") then
                            key, label = "arcanite", skillName
                        elseif lname:find("element") or lname:find("elementaire") then
                            key, label = "elemental", skillName
                        end
                        if key then
                            local found = false
                            for _, c in ipairs(cds) do
                                if c.key == key or c.key == "arcanite" or c.key == "elemental" then
                                    found = true
                                    if cooldown > (c.remainingSec or 0) then
                                        c.remainingSec = cooldown
                                        c.key = key
                                        c.name = label
                                    end
                                    break
                                end
                            end
                            if not found then
                                table.insert(cds, {
                                    key = key, name = label,
                                    remainingSec = cooldown, scannedAt = now
                                })
                            end
                        end
                    end
                end
            end
            return cds
        end

        local function GetPlayerMoney()
            if type(GetMoney) == "function" then
                local ok, v = pcall(GetMoney)
                if ok and type(v) == "number" and v > 0 then
                    return v
                end
            end
            return 0
        end

        local function ReadMapPositionFromCMap(mapID)
            if not mapID or not C_Map or not C_Map.GetPlayerMapPosition then
                return nil, nil
            end
            local pos = C_Map.GetPlayerMapPosition(mapID, "player")
            if not pos then return nil, nil end
            if pos.GetXY then
                return pos:GetXY()
            end
            if pos.x and pos.y then
                return pos.x, pos.y
            end
            return nil, nil
        end

        local function ReadMapPositionMinimap()
            if C_Minimap and C_Minimap.GetPlayerMapPosition then
                local ok, mx, my = pcall(C_Minimap.GetPlayerMapPosition)
                if ok and type(mx) == "number" and type(my) == "number" and (mx > 0 or my > 0) then
                    return mx, my
                end
            end
            if type(GetPlayerMapPosition) == "function" then
                local ok, mx, my = pcall(GetPlayerMapPosition, "player")
                if ok and type(mx) == "number" and type(my) == "number" and (mx > 0 or my > 0) then
                    return mx, my
                end
            end
            return nil, nil
        end

        local function ResolveZoneMapID(continentMapID)
            local zoneName = GetSubZoneText()
            if not zoneName or zoneName == "" then
                zoneName = GetRealZoneText()
            end
            if not zoneName or zoneName == "" then
                return continentMapID
            end

            if C_Map.GetMapChildrenInfo then
                local children = C_Map.GetMapChildrenInfo(continentMapID, Enum.UIMapType.Zone, true)
                if children then
                    for _, child in ipairs(children) do
                        if child.name == zoneName then
                            return child.mapID
                        end
                    end
                end
            end

            return continentMapID
        end

        local function GetPlayerCoords()
            local coords = { x = 0, y = 0, mapId = 0 }
            local zoneText = GetRealZoneText() or ""
            local subText = GetSubZoneText() or ""
            local zidFromName = ResolveMapIdFromNames(zoneText, subText)

            pcall(function()
                local mapID = C_Map.GetBestMapForUnit("player")
                if not mapID then return end

                local info = C_Map.GetMapInfo and C_Map.GetMapInfo(mapID)
                local zoneMapID = mapID
                if info and info.mapType == Enum.UIMapType.Continent then
                    zoneMapID = ResolveZoneMapID(mapID)
                end

                -- Texte de zone in-game plus fiable que l'UiMapID API (souvent continent / parent)
                if zidFromName > 0 then
                    zoneMapID = zidFromName
                end

                local x, y = ReadMapPositionFromCMap(zoneMapID)
                if (not x or x == 0) and (not y or y == 0) and zoneMapID ~= mapID then
                    x, y = ReadMapPositionFromCMap(mapID)
                end

                if x and y and (x > 0 or y > 0) then
                    coords.x = x
                    coords.y = y
                    coords.mapId = zoneMapID
                end
            end)

            if (coords.x == 0 and coords.y == 0) then
                local mx, my = ReadMapPositionMinimap()
                if mx and my then
                    coords.x, coords.y = mx, my
                    coords.mapId = zidFromName
                    if (coords.mapId or 0) == 0 then
                        local mapID = C_Map and C_Map.GetBestMapForUnit and C_Map.GetBestMapForUnit("player")
                        if mapID then coords.mapId = mapID end
                    end
                end
            end

            if (coords.mapId or 0) == 0 and zidFromName > 0 then
                coords.mapId = zidFromName
            end

            return coords
        end

        local function ScanInventory()
            return ScanContainer(0, 4)
        end

        local function SaveLiveSnapshot(scanBags)
            local key = GetCharKey()
            local prev = WowSyncDB[key] or {}
            local coords = GetPlayerCoords()
            local money = GetPlayerMoney()

            local entry = WowSyncDB[key]
            if not entry then
                entry = {
                    name = UnitName("player") or "?",
                    realm = GetRealmName() or "?",
                    level = UnitLevel("player") or 0,
                    class = select(2, UnitClass("player")) or "Unknown",
                    race = select(2, UnitRace("player")) or "Unknown",
                    inventory = prev.inventory or {},
                    bank = prev.bank or {},
                    mail = prev.mail or {},
                    professions = prev.professions or {},
                    knownCooldowns = prev.knownCooldowns or {},
                    syncMeta = prev.syncMeta or {},
                }
                WowSyncDB[key] = entry
            end
            entry.syncMeta = entry.syncMeta or {}

            if money > 0 then
                entry.gold = money
            end
            if coords.x and coords.y and (coords.x > 0 or coords.y > 0) then
                entry.x = coords.x
                entry.y = coords.y
                entry.mapId = coords.mapId or 0
            end
            local zone = GetRealZoneText() or entry.zone or ""
            local sub = GetSubZoneText() or entry.subZone or ""
            entry.zone = zone
            entry.subZone = sub
            if (entry.mapId or 0) == 0 then
                local zid = ResolveMapIdFromNames(zone, sub)
                if zid > 0 then entry.mapId = zid end
            end
            entry.lastUpdate = date("%Y-%m-%d %H:%M:%S")
            do
                local xp, maxXp = UnitXP("player"), UnitXPMax("player")
                entry.xpPercent = (maxXp and maxXp > 0) and (xp / maxXp * 100) or -1
            end

            if scanBags then
                local inv = ScanInventory()
                entry.inventory = inv
                TouchSync(key, "inventory")
            end

            local okCd, cds = pcall(ScanCooldowns)
            if okCd and cds and #cds > 0 then
                entry.cooldowns = cds
                TouchSync(key, "cooldowns")
            end
        end

        local function ScanProfessions()
            local profs = {}
            local ok, result = pcall(function()
                if C_TradeSkillUI and C_TradeSkillUI.GetAllProfessionTradeSkillLines then
                    local lines = C_TradeSkillUI.GetAllProfessionTradeSkillLines()
                    if lines then
                        for _, id in ipairs(lines) do
                            local info = C_TradeSkillUI.GetProfessionInfoBySkillLineID(id)
                            if info and info.professionName then
                                table.insert(profs, {
                                    name = info.professionName,
                                    rank = info.skillLevel or 0,
                                    maxRank = info.maxSkillLevel or 0
                                })
                            end
                        end
                    end
                end
                if #profs == 0 and GetNumSkillLines then
                    for i = 1, GetNumSkillLines() do
                        local name, isHeader, _, rank, _, _, maxRank, isAbandonable = GetSkillLineInfo(i)
                        if not isHeader and isAbandonable then
                            table.insert(profs, { name = name, rank = rank, maxRank = maxRank })
                        end
                    end
                end
                if #profs == 0 and GetProfessions then
                    local prof1, prof2 = GetProfessions()
                    for _, idx in ipairs({prof1, prof2}) do
                        if idx then
                            local name, _, rank, maxRank = GetProfessionInfo(idx)
                            if name then
                                table.insert(profs, { name = name, rank = rank or 0, maxRank = maxRank or 0 })
                            end
                        end
                    end
                end
            end)
            if not ok then
                WSLog("|cFFFFAA00[WowSync]|r Metiers: " .. tostring(result))
            end
            return profs
        end

        local function ScanMail()
            local mails = {}
            local ok, err = pcall(function()
                local numItems = GetInboxNumItems()
                if not numItems then return end
                for i = 1, numItems do
                    local _, _, sender, subject, money, _, daysLeft, hasItem = GetInboxHeaderInfo(i)
                    local items = {}
                    if hasItem then
                        for j = 1, 16 do
                            local name, _, _, count = GetInboxItem(i, j)
                            if name then
                                table.insert(items, { name = name, count = count or 1 })
                            end
                        end
                    end
                    table.insert(mails, {
                        sender = sender or "?",
                        subject = subject or "",
                        money = money or 0,
                        daysLeft = daysLeft or 0,
                        items = items
                    })
                end
            end)
            if not ok then
                WSLog("|cFFFFAA00[WowSync]|r Courrier: " .. tostring(err))
            end
            return mails
        end

        local function SaveAll(fromLogout)
            local key = GetCharKey()
            local prev = WowSyncDB[key] or {}

            -- Sacs en premier : indispensable avant fermeture du client (PLAYER_LOGOUT)
            local inv = {}
            local okInv, errInv = pcall(function()
                inv = ScanInventory()
            end)
            if not okInv then
                WSLog("|cFFFF0000[WowSync]|r Err inventaire: " .. tostring(errInv))
                inv = prev.inventory or {}
            end
            -- 2e appel a la deco : sacs deja fermes → ne pas ecraser un inventaire valide
            if fromLogout and #inv == 0 and prev.inventory and #prev.inventory > 0 then
                inv = prev.inventory
                WSLog("|cFFFFAA00[WowSync]|r Sacs: scan vide a la deco, inventaire precedant conserve (" .. #inv .. " objets).")
            end

            local coords = GetPlayerCoords()
            local money = GetPlayerMoney()
            if money == 0 and (prev.gold or 0) > 0 then money = prev.gold end
            if (coords.x or 0) == 0 and (coords.y or 0) == 0 then
                coords.x = prev.x or 0
                coords.y = prev.y or 0
                coords.mapId = prev.mapId or 0
            end

            local entry = {
                name = UnitName("player") or "?",
                realm = GetRealmName() or "?",
                level = UnitLevel("player") or 0,
                class = select(2, UnitClass("player")) or "Unknown",
                race = select(2, UnitRace("player")) or "Unknown",
                gold = money,
                zone = GetRealZoneText() or prev.zone or "",
                subZone = GetSubZoneText() or prev.subZone or "",
                x = coords.x,
                y = coords.y,
                mapId = coords.mapId or 0,
                professions = prev.professions or {},
                inventory = inv,
                bank = prev.bank or {},
                mail = prev.mail or {},
                syncMeta = prev.syncMeta or {},
                cooldowns = prev.cooldowns or {},
                knownCooldowns = prev.knownCooldowns or {},
                lastUpdate = date("%Y-%m-%d %H:%M:%S"),
                xpPercent = (function()
                    local xp, maxXp = UnitXP("player"), UnitXPMax("player")
                    return (maxXp and maxXp > 0) and (xp / maxXp * 100) or -1
                end)()
            }
            WowSyncDB[key] = entry
            entry.syncMeta = entry.syncMeta or {}
            if #inv > 0 then
                TouchSync(key, "inventory")
            end

            local ok1, e1 = pcall(function()
                local p = ScanProfessions()
                if p and #p > 0 then
                    entry.professions = p
                    TouchSync(key, "professions")
                end
            end)
            if not ok1 then WSLog("|cFFFF0000[WowSync]|r Err metiers: " .. tostring(e1)) end

            local okKnown, eKnown = pcall(function()
                RefreshKnownCooldowns(entry)
            end)
            if not okKnown then WSLog("|cFFFF0000[WowSync]|r Err CD connus: " .. tostring(eKnown)) end

            local ok3, e3 = pcall(function()
                local cds = ScanCooldowns()
                entry.cooldowns = cds
                if #cds > 0 then TouchSync(key, "cooldowns") end
            end)
            if not ok3 then WSLog("|cFFFF0000[WowSync]|r Err CD: " .. tostring(e3)) end

            local g = math.floor((entry.gold or 0) / 10000)
            local s = math.floor(((entry.gold or 0) % 10000) / 100)
            local c = (entry.gold or 0) % 100
            if fromLogout then
                WSLog(string.format(
                    "|cFF00FF00[WowSync]|r Deconnexion — %d objets en sacs sauvegardes.",
                    #entry.inventory))
            else
                WSLog(string.format(
                    "|cFF00FF00[WowSync]|r Sync OK | or: %dg %ds %dc | pos: %.1f, %.1f | inv: %d",
                    g, s, c,
                    (entry.x or 0) * 100, (entry.y or 0) * 100,
                    #entry.inventory))
            end
        end

        -- Une seule sauvegarde a la deco (Logout/Quit AVANT sacs fermes ; PLAYER_LOGOUT = souvent vide)
        local exitSaveDone = false
        local function SaveOnExit()
            if exitSaveDone then return end
            exitSaveDone = true
            SaveAll(true)
        end
        if hooksecurefunc then
            pcall(function() hooksecurefunc("Logout", SaveOnExit) end)
            pcall(function() hooksecurefunc("Quit", SaveOnExit) end)
        end

        local liveSaveTimer = 0
        local bagScanPending = false
        local bagScanTimer = 0

        frame:RegisterEvent("PLAYER_ENTERING_WORLD")
        frame:RegisterEvent("PLAYER_LOGOUT")
        frame:RegisterEvent("PLAYER_MONEY")
        frame:RegisterEvent("ZONE_CHANGED")
        frame:RegisterEvent("ZONE_CHANGED_INDOORS")
        frame:RegisterEvent("ZONE_CHANGED_NEW_AREA")
        frame:RegisterEvent("BANKFRAME_OPENED")
        frame:RegisterEvent("BANKFRAME_CLOSED")
        frame:RegisterEvent("MAIL_SHOW")
        frame:RegisterEvent("MAIL_CLOSED")
        frame:RegisterEvent("MAIL_INBOX_UPDATE")
        frame:RegisterEvent("TRADE_SKILL_UPDATE")
        frame:RegisterEvent("TRADE_SKILL_SHOW")
        frame:RegisterEvent("SPELL_UPDATE_COOLDOWN")
        frame:RegisterEvent("BAG_UPDATE_DELAYED")

        frame:SetScript("OnEvent", function(self, event)
            if event == "PLAYER_ENTERING_WORLD" then
                pendingLogin = true
                loginTimer = 0
            elseif event == "PLAYER_MONEY" or event == "ZONE_CHANGED"
                or event == "ZONE_CHANGED_INDOORS" or event == "ZONE_CHANGED_NEW_AREA" then
                SaveLiveSnapshot(false)
            elseif event == "BANKFRAME_OPENED" then
                bankOpen = true
                local key = GetCharKey()
                if WowSyncDB[key] then
                    WowSyncDB[key].bank = ScanBank()
                    TouchSync(key, "bank")
                    WSLog("|cFF00FF00[WowSync]|r Banque synchronisee.")
                end
            elseif event == "BANKFRAME_CLOSED" then
                bankOpen = false
            elseif event == "MAIL_SHOW" then
                mailOpen = true
            elseif event == "MAIL_INBOX_UPDATE" then
                if mailOpen then
                    local key = GetCharKey()
                    if WowSyncDB[key] then
                        WowSyncDB[key].mail = ScanMail()
                        TouchSync(key, "mail")
                        WSLog("|cFF00FF00[WowSync]|r Courrier synchronise.")
                    end
                end
            elseif event == "MAIL_CLOSED" then
                mailOpen = false
            elseif event == "TRADE_SKILL_UPDATE" or event == "TRADE_SKILL_SHOW"
                or event == "SPELL_UPDATE_COOLDOWN" then
                local key = GetCharKey()
                if WowSyncDB[key] then
                    RefreshKnownCooldowns(WowSyncDB[key])
                    local cds = ScanCooldowns()
                    WowSyncDB[key].cooldowns = cds
                    if #cds > 0 then TouchSync(key, "cooldowns") end
                end
            elseif event == "BAG_UPDATE_DELAYED" then
                bagScanPending = true
            end
        end)

        frame:SetScript("OnUpdate", function(self, elapsed)
            if pendingLogin then
                loginTimer = loginTimer + elapsed
                if loginTimer >= 3 then
                    pendingLogin = false
                    SaveAll()
                end
            end
            if bagScanPending then
                bagScanTimer = bagScanTimer + elapsed
                if bagScanTimer >= 0.4 then
                    bagScanPending = false
                    bagScanTimer = 0
                    local key = GetCharKey()
                    if WowSyncDB[key] then
                        WowSyncDB[key].inventory = ScanInventory()
                        TouchSync(key, "inventory")
                    end
                end
            end
            liveSaveTimer = liveSaveTimer + elapsed
            if liveSaveTimer >= 12 then
                liveSaveTimer = 0
                SaveLiveSnapshot(true)
            end
        end)

        -- === Panneau WowSync (en jeu) ===
        if WowSyncDB.showPos == nil then
            WowSyncDB.showPos = false
        end

        local function SyncMark(has)
            return has and "|cFF00FF00OK|r" or "|cFF666666--|r"
        end

        local posFrame = CreateFrame("Frame", "WowSyncPosFrame", UIParent, "BackdropTemplate")
        posFrame:SetSize(268, 192)
        posFrame:SetPoint("TOPRIGHT", UIParent, "TOPRIGHT", -16, -120)
        posFrame:SetBackdrop({
            bgFile = "Interface\\DialogFrame\\UI-DialogBox-Background-Dark",
            edgeFile = "Interface\\DialogFrame\\UI-DialogBox-Border",
            tile = true, tileSize = 32, edgeSize = 16,
            insets = { left = 4, right = 4, top = 4, bottom = 4 }
        })
        posFrame:SetMovable(true)
        posFrame:EnableMouse(true)
        posFrame:RegisterForDrag("LeftButton")
        posFrame:SetScript("OnDragStart", posFrame.StartMoving)
        posFrame:SetScript("OnDragStop", posFrame.StopMovingOrSizing)
        posFrame:SetFrameStrata("MEDIUM")

        local posTitle = posFrame:CreateFontString(nil, "OVERLAY", "GameFontNormalSmall")
        posTitle:SetPoint("TOPLEFT", 10, -8)
        posTitle:SetText("|cFFFFD700WowSync|r")

        local posVersion = posFrame:CreateFontString(nil, "OVERLAY", "GameFontHighlightSmall")
        posVersion:SetPoint("TOPRIGHT", -10, -8)
        posVersion:SetText("|cFF88CCFFv" .. WOWSYNC_VERSION .. "|r")

        local posText = posFrame:CreateFontString(nil, "OVERLAY", "GameFontHighlightSmall")
        posText:SetPoint("TOPLEFT", 10, -26)
        posText:SetPoint("BOTTOMRIGHT", -10, 8)
        posText:SetJustifyH("LEFT")
        posText:SetWordWrap(true)

        local function UpdatePosHUD()
            if not posFrame:IsShown() then return end
            local coords = GetPlayerCoords()
            local zone = GetRealZoneText() or "?"
            local sub = GetSubZoneText() or ""
            local loc = sub ~= "" and (zone .. " - " .. sub) or zone
            local xPct = (coords.x and coords.x > 0) and string.format("%.1f", coords.x * 100) or "—"
            local yPct = (coords.y and coords.y > 0) and string.format("%.1f", coords.y * 100) or "—"
            local money = GetPlayerMoney()
            local g = math.floor(money / 10000)
            local s = math.floor((money % 10000) / 100)
            local c = money % 100

            local key = GetCharKey()
            local entry = WowSyncDB[key] or {}
            local meta = entry.syncMeta or {}
            local syncLine = string.format(
                "Sacs %s  Banque %s  Courrier %s  Metiers %s",
                SyncMark(meta.inventory), SyncMark(meta.bank),
                SyncMark(meta.mail), SyncMark(meta.professions))

            local cdLine = ""
            local cds = entry.cooldowns or {}
            if #cds > 0 then
                local parts = {}
                for i = 1, math.min(#cds, 3) do
                    local cd = cds[i]
                    table.insert(parts, string.format("%s %s",
                        cd.name or cd.key, FormatDuration(cd.remainingSec)))
                end
                cdLine = "\n|cFFFFFF00CD:|r " .. table.concat(parts, " | ")
            end

            local invCount = entry.inventory and #entry.inventory or 0
            posText:SetText(string.format(
                "%s\n|cFFFFFF00Or:|r %dg %ds %dc\n|cFFFFFF00Pos:|r %s, %s (HG)  |cFF88CCFFmap %s|r\n|cFFFFFF00Sync:|r %s\n|cFFAAAAAASacs: %d objets|r%s",
                loc, g, s, c, xPct, yPct, tostring(coords.mapId or 0), syncLine, invCount, cdLine))
        end

        local hudFrame = CreateFrame("Frame")
        hudFrame:RegisterEvent("PLAYER_MONEY")
        hudFrame:SetScript("OnEvent", function(_, event)
            if event == "PLAYER_MONEY" then UpdatePosHUD() end
        end)

        local hudTicker = CreateFrame("Frame")
        hudTicker:SetScript("OnUpdate", function(_, elapsed)
            hudTicker._t = (hudTicker._t or 0) + elapsed
            if hudTicker._t < 0.35 then return end
            hudTicker._t = 0
            UpdatePosHUD()
        end)

        local function SetPosHUDVisible(show)
            WowSyncDB.showPos = show
            if show then posFrame:Show() else posFrame:Hide() end
            UpdatePosHUD()
        end

        posFrame:Hide()
        WowSyncDB.showPos = false

        -- Bouton minimap (compact)
        local minimapBtn = CreateFrame("Button", "WowSyncMinimapButton", Minimap)
        minimapBtn:SetSize(22, 22)
        minimapBtn:SetFrameStrata("MEDIUM")
        minimapBtn:SetFrameLevel(8)
        minimapBtn:SetHighlightTexture("Interface\\Minimap\\UI-Minimap-ZoomButton-Highlight")
        local minimapIcon = minimapBtn:CreateTexture(nil, "ARTWORK")
        minimapIcon:SetSize(16, 16)
        minimapIcon:SetPoint("CENTER")
        minimapIcon:SetTexture("Interface\\Icons\\INV_Misc_Bag_08")
        minimapBtn:SetPoint("TOPLEFT", Minimap, "TOPLEFT", 8, -8)
        minimapBtn:RegisterForClicks("LeftButtonUp", "RightButtonUp")
        minimapBtn:SetScript("OnEnter", function(self)
            GameTooltip:SetOwner(self, "ANCHOR_LEFT")
            GameTooltip:AddLine("|cFFFFD700WowSync|r v" .. WOWSYNC_VERSION)
            GameTooltip:AddLine("Clic: panneau on/off", 1, 1, 1)
            GameTooltip:AddLine("/wowsync : sauvegarder", 0.8, 0.8, 0.8)
            GameTooltip:Show()
        end)
        minimapBtn:SetScript("OnLeave", function() GameTooltip:Hide() end)
        minimapBtn:SetScript("OnClick", function(_, button)
            if button == "RightButton" then
                SaveAll()
                UpdatePosHUD()
            else
                SetPosHUDVisible(not posFrame:IsShown())
            end
        end)

        local function PrintMapPOIs()
            local mapID = C_Map.GetBestMapForUnit("player")
            if not mapID then
                WSLog("|cFFFF0000[WowSync]|r Pas de carte.")
                return
            end
            local info = C_Map.GetMapInfo and C_Map.GetMapInfo(mapID)
            local mapName = info and info.name or ("map " .. mapID)
            WSLog("|cFF00FF00[WowSync]|r POI / points sur |cFFFFFF00" .. mapName .. "|r (id " .. mapID .. "):")

            local count = 0
            if C_AreaPoi and C_AreaPoi.GetQuestRelatedMapPOIs then
                local ok, ids = pcall(C_AreaPoi.GetQuestRelatedMapPOIs, mapID)
                if ok and ids then
                    for _, poiID in ipairs(ids) do
                        local poiInfo = C_AreaPoi.GetAreaPOIInfo and C_AreaPoi.GetAreaPOIInfo(mapID, poiID)
                        if poiInfo and poiInfo.name then
                            count = count + 1
                            WSLog(string.format("  [%d] %s", poiID, poiInfo.name))
                        end
                    end
                end
            end

            if C_TaxiMap and C_TaxiMap.GetTaxiNodesForMap then
                local ok, nodes = pcall(C_TaxiMap.GetTaxiNodesForMap, mapID)
                if ok and nodes then
                    for _, node in ipairs(nodes) do
                        if node.name then
                            count = count + 1
                            WSLog("  [Vol] " .. node.name)
                        end
                    end
                end
            end

            if count == 0 then
                WSLog("  (aucun POI via API — utilisez la carte monde du jeu)")
            end
        end

        SLASH_WOWSYNC1 = "/wowsync"
        SlashCmdList["WOWSYNC"] = function(msg)
            msg = string.lower(msg or "")
            if msg == "pos" or msg == "position" then
                SetPosHUDVisible(not WowSyncDB.showPos)
                WSLog("|cFF00FF00[WowSync]|r Panneau position: " .. (WowSyncDB.showPos and "ON" or "OFF"))
            elseif msg == "poi" then
                PrintMapPOIs()
            elseif msg == "version" or msg == "ver" then
                WSLog("|cFF00FF00[WowSync]|r version " .. WOWSYNC_VERSION)
            elseif msg == "or" or msg == "gold" then
                SaveLiveSnapshot()
                local money = GetPlayerMoney()
                local g = math.floor(money / 10000)
                local s = math.floor((money % 10000) / 100)
                local c = money % 100
                WSLog(string.format("|cFF00FF00[WowSync]|r Or: %dg %ds %dc", g, s, c))
                UpdatePosHUD()
            else
                SaveAll()
                UpdatePosHUD()
                WSLog("|cFF00FF00[WowSync]|r Donnees sauvegardees. Deconnectez-vous pour ecrire le fichier. /wowsync pos | /wowsync or")
            end
        end
        """;

    #endregion
}
