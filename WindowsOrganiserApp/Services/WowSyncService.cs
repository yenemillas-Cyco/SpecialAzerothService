using System.IO;
using WindowsOrganiserApp.Models.WowSync;

namespace WindowsOrganiserApp.Services;

public interface IWowSyncService
{
    string WowPath { get; set; }
    string ResolvedWtfPath { get; }
    void DeployAddon();
    List<WowAccountData> ReadAllAccounts();
}

public sealed class WowSyncService : IWowSyncService
{
    private readonly ISettingsService _settingsService;

    public string WowPath
    {
        get => _settingsService.Load().WowPath;
        set
        {
            var s = _settingsService.Load();
            s.WowPath = value;
            _settingsService.Save(s);
        }
    }

    public WowSyncService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public void DeployAddon()
    {
        var addonsDir = Path.Combine(WowPath.Trim(), "WowSync");
        Directory.CreateDirectory(addonsDir);

        File.WriteAllText(Path.Combine(addonsDir, "WowSync.toc"), TocContent);
        File.WriteAllText(Path.Combine(addonsDir, "WowSync.lua"), LuaContent);
    }

    public string ResolvedWtfPath
    {
        get
        {
            var path = WowPath.Trim();
            var root = Path.GetFullPath(Path.Combine(path, "..", ".."));
            return Path.Combine(root, "WTF", "Account");
        }
    }

    public List<WowAccountData> ReadAllAccounts()
    {
        var accounts = new List<WowAccountData>();
        var wtfPath = ResolvedWtfPath;
        if (!Directory.Exists(wtfPath)) return accounts;

        foreach (var accountDir in Directory.GetDirectories(wtfPath))
        {
            var svFile = Path.Combine(accountDir, "SavedVariables", "WowSync.lua");
            if (!File.Exists(svFile)) continue;

            var accountName = Path.GetFileName(accountDir);
            var account = new WowAccountData { AccountName = accountName };

            try
            {
                var parsed = LuaTableParser.ParseFile(svFile);
                if (!parsed.TryGetValue("WowSyncDB", out var dbObj) ||
                    dbObj is not Dictionary<string, object?> db)
                    continue;

                foreach (var (charKey, charValue) in db)
                {
                    if (charValue is not Dictionary<string, object?> charData) continue;
                    account.Characters.Add(ParseCharacter(charData));
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

        return ch;
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

    private const string TocContent =
        """
        ## Interface: 11506
        ## Title: WowSync
        ## Notes: Synchronise les donnees du personnage avec l'application WowSync
        ## Author: WowSync
        ## SavedVariables: WowSyncDB
        ## Version: 1.0.0

        WowSync.lua
        """;

    private const string LuaContent =
        """
        WowSyncDB = WowSyncDB or {}
        print("|cFF00FF00[WowSync]|r Addon charge.")

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

        local function ScanSlot(items, bag, slot)
            local link = _GetItemLink(bag, slot)
            if not link then return end
            local name, _, quality, _, _, _, _, _, _, icon = GetItemInfo(link)
            local count, itemId, iconId = GetSlotInfo(bag, slot)
            if itemId == 0 then itemId = GetItemIdFromLink(link) end
            if iconId == 0 and icon then iconId = icon end
            table.insert(items, {
                name = name or link,
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

        local function GetPlayerCoords()
            local coords = { x = 0, y = 0, mapId = 0 }
            pcall(function()
                local mapID = C_Map.GetBestMapForUnit("player")
                if mapID then
                    coords.mapId = mapID
                    local pos = C_Map.GetPlayerMapPosition(mapID, "player")
                    if pos then
                        coords.x = pos.x or pos:GetXY()
                        coords.y = pos.y or select(2, pos:GetXY())
                    end
                end
            end)
            return coords
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
                print("|cFFFFAA00[WowSync]|r Metiers: " .. tostring(result))
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
                print("|cFFFFAA00[WowSync]|r Courrier: " .. tostring(err))
            end
            return mails
        end

        local function SaveAll()
            local key = GetCharKey()
            local prev = WowSyncDB[key] or {}
            local coords = GetPlayerCoords()

            WowSyncDB[key] = {
                name = UnitName("player") or "?",
                realm = GetRealmName() or "?",
                level = UnitLevel("player") or 0,
                class = select(2, UnitClass("player")) or "Unknown",
                race = select(2, UnitRace("player")) or "Unknown",
                gold = GetMoney() or 0,
                zone = GetRealZoneText() or "",
                subZone = GetSubZoneText() or "",
                x = coords.x,
                y = coords.y,
                mapId = coords.mapId,
                professions = prev.professions or {},
                inventory = prev.inventory or {},
                bank = prev.bank or {},
                mail = prev.mail or {},
                lastUpdate = date("%Y-%m-%d %H:%M:%S")
            }

            local ok1, e1 = pcall(function()
                local p = ScanProfessions()
                if p and #p > 0 then WowSyncDB[key].professions = p end
            end)
            if not ok1 then print("|cFFFF0000[WowSync]|r Err metiers: " .. tostring(e1)) end

            local ok2, e2 = pcall(function()
                local inv = ScanContainer(0, 4)
                if inv and #inv > 0 then WowSyncDB[key].inventory = inv end
            end)
            if not ok2 then print("|cFFFF0000[WowSync]|r Err inventaire: " .. tostring(e2)) end

            print("|cFF00FF00[WowSync]|r Sync OK | inv:" .. #WowSyncDB[key].inventory .. " metiers:" .. #WowSyncDB[key].professions .. " pos:" .. string.format("%.1f,%.1f", coords.x*100, coords.y*100))
        end

        frame:RegisterEvent("PLAYER_ENTERING_WORLD")
        frame:RegisterEvent("PLAYER_LOGOUT")
        frame:RegisterEvent("BANKFRAME_OPENED")
        frame:RegisterEvent("BANKFRAME_CLOSED")
        frame:RegisterEvent("MAIL_SHOW")
        frame:RegisterEvent("MAIL_CLOSED")
        frame:RegisterEvent("MAIL_INBOX_UPDATE")

        frame:SetScript("OnEvent", function(self, event)
            if event == "PLAYER_ENTERING_WORLD" then
                pendingLogin = true
                loginTimer = 0
            elseif event == "PLAYER_LOGOUT" then
                SaveAll()
            elseif event == "BANKFRAME_OPENED" then
                bankOpen = true
                local key = GetCharKey()
                if WowSyncDB[key] then
                    WowSyncDB[key].bank = ScanBank()
                    print("|cFF00FF00[WowSync]|r Banque synchronisee.")
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
                        print("|cFF00FF00[WowSync]|r Courrier synchronise.")
                    end
                end
            elseif event == "MAIL_CLOSED" then
                mailOpen = false
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
        end)

        SLASH_WOWSYNC1 = "/wowsync"
        SlashCmdList["WOWSYNC"] = function()
            SaveAll()
        end
        """;

    #endregion
}
