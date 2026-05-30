using System.Net.Http;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Serilog;
using SpecialAzerothService.Core.Models;
using SpecialAzerothService.Core.Models.Carto;

namespace SpecialAzerothService.Core.Services;

public enum SyncRunTrigger
{
    Startup,
    Shutdown,
    Manual,
    RemoteRequest
}

public sealed class SyncRunResult
{
    public bool Connected { get; init; }
    public bool FriendPushed { get; init; }
    public bool TpBoyPushed { get; init; }
    public string Message { get; init; } = "";
}

public class SyncService : IDisposable
{
    private HubConnection? _hub;
    private readonly AppSettings _settings;
    private readonly ILogger _log;
    private bool _connected;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public event Action<string, FriendSyncPayload>? FriendDataReceived;
    public event Action<string, TpBoyPublicPayload>? TpBoyPublicReceived;
    public event Action<string, BountySyncPayload>? FriendBountyReceived;
    public event Action<string>? ConnectionStateChanged;
    public event Action<string, bool>? FriendOnlineChanged;
    public event Action<string, FriendLinkState>? FriendLinkStateChanged;
    public event Action? PushRequested;

    public bool IsConnected => _connected;
    public string UserGuid => _settings.UserGuid;
    public List<FriendEntry> Friends => _settings.Friends;
    public AppSettings Settings => _settings;

    public SyncService(AppSettings settings, ILogger logger)
    {
        _settings = settings;
        MigrateLegacyFriends();
        _log = logger;
    }

    private void MigrateLegacyFriends()
    {
        if (_settings.FriendGuids.Count == 0)
            return;

        foreach (var guid in _settings.FriendGuids)
        {
            if (string.IsNullOrWhiteSpace(guid))
                continue;
            if (_settings.Friends.Any(f => f.Guid.Equals(guid, StringComparison.OrdinalIgnoreCase)))
                continue;

            _settings.Friends.Add(new FriendEntry
            {
                Guid = guid.Trim(),
                Name = guid.Trim()[..Math.Min(8, guid.Trim().Length)],
                LinkState = FriendLinkState.PendingOutbound
            });
        }

        _settings.FriendGuids.Clear();
    }

    public async Task ConnectAsync()
    {
        if (_hub is { State: HubConnectionState.Connected })
        {
            ConnectionStateChanged?.Invoke("Déjà connecté");
            return;
        }

        if (_hub != null)
        {
            try { await _hub.DisposeAsync(); } catch { /* ignore */ }
            _hub = null;
        }

        _log.Information("Sync: connecting to {Url}", _settings.SyncServerUrl);
        ConnectionStateChanged?.Invoke("Connexion...");

        try
        {
            _hub = new HubConnectionBuilder()
                .WithUrl(_settings.SyncServerUrl, opts =>
                {
                    opts.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling
                                    | Microsoft.AspNetCore.Http.Connections.HttpTransportType.ServerSentEvents;
                    opts.HttpMessageHandlerFactory = innerHandler =>
                    {
                        if (innerHandler is HttpClientHandler h)
                        {
                            h.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
                            h.DefaultProxyCredentials = System.Net.CredentialCache.DefaultCredentials;
                            h.UseProxy = true;
                        }
                        return innerHandler;
                    };
                })
                .WithAutomaticReconnect([
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(15),
                    TimeSpan.FromSeconds(30)
                ])
                .Build();

            _hub.On<string, string>("ReceiveFriendUpdate", OnReceiveFriendUpdate);
            _hub.On<string, string>("ReceiveUpdate", OnReceiveLegacyUpdate);
            _hub.On<string, string>("ReceiveTpBoyPublic", OnReceiveTpBoyPublic);
            _hub.On<string, string>("ReceiveBountyUpdate", OnReceiveBountyUpdate);
            _hub.On<string, string>("FriendLinkState", OnFriendLinkState);
            _hub.On<string>("FriendOnline", guid => FriendOnlineChanged?.Invoke(guid, true));
            _hub.On<string>("FriendOffline", guid => FriendOnlineChanged?.Invoke(guid, false));
            _hub.On("RequestPush", () => PushRequested?.Invoke());

            _hub.Reconnected += async _ =>
            {
                _connected = true;
                ConnectionStateChanged?.Invoke("Connecté");
                await RegisterAndSubscribeAsync();
            };

            _hub.Closed += _ =>
            {
                _connected = false;
                ConnectionStateChanged?.Invoke("Déconnecté");
                return Task.CompletedTask;
            };

            await _hub.StartAsync();
            _connected = true;
            ConnectionStateChanged?.Invoke("Connecté");

            await RegisterAndSubscribeAsync();
            _log.Information("Sync: connected OK");
        }
        catch (Exception ex)
        {
            _connected = false;
            _hub = null;
            ConnectionStateChanged?.Invoke($"Erreur: {ex.Message}");
            _log.Warning(ex, "Sync: failed to connect");
        }
    }

    private async Task RegisterAndSubscribeAsync()
    {
        if (_hub == null) return;
        await _hub.InvokeAsync("Connect", _settings.UserGuid);
        foreach (var f in _settings.Friends)
            await _hub.InvokeAsync("Subscribe", _settings.UserGuid, f.Guid);
    }

    public async Task SubscribeToFriendAsync(string friendGuid, string friendName)
    {
        friendGuid = friendGuid.Trim();
        var existing = _settings.Friends.FirstOrDefault(f =>
            f.Guid.Equals(friendGuid, StringComparison.OrdinalIgnoreCase));

        if (existing == null)
        {
            _settings.Friends.Add(new FriendEntry
            {
                Guid = friendGuid,
                Name = friendName,
                LinkState = FriendLinkState.PendingOutbound
            });
        }
        else
        {
            existing.Name = friendName;
        }

        if (_hub?.State == HubConnectionState.Connected)
            await _hub.InvokeAsync("Subscribe", _settings.UserGuid, friendGuid);
    }

    public async Task UnsubscribeFromFriendAsync(string friendGuid)
    {
        _settings.Friends.RemoveAll(f => f.Guid.Equals(friendGuid, StringComparison.OrdinalIgnoreCase));
        _settings.ReceivedFriendRevisions.Remove(friendGuid);
        if (_hub?.State == HubConnectionState.Connected)
            await _hub.InvokeAsync("Unsubscribe", _settings.UserGuid, friendGuid);
    }

    public FriendEntry? GetFriend(string guid) =>
        _settings.Friends.FirstOrDefault(f => f.Guid.Equals(guid, StringComparison.OrdinalIgnoreCase));

    public async Task<List<string>> GetOnlineFriendsAsync()
    {
        if (_hub?.State != HubConnectionState.Connected) return [];
        try
        {
            var guids = _settings.Friends.Select(f => f.Guid).ToList();
            return await _hub.InvokeAsync<List<string>>("GetOnlineFriends", guids);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Sync: GetOnlineFriends failed");
            return [];
        }
    }

    public async Task<SyncRunResult> RunSyncAsync(CartoSyncBuildInput input, SyncRunTrigger trigger, bool force = false)
    {
        if (_hub?.State != HubConnectionState.Connected)
            await ConnectAsync();

        if (_hub?.State != HubConnectionState.Connected)
        {
            return new SyncRunResult
            {
                Connected = false,
                Message = "Hors ligne — reconnectez-vous."
            };
        }

        try
        {
            if (trigger is SyncRunTrigger.Manual or SyncRunTrigger.Startup)
                await _hub.InvokeAsync("RequestFullRefresh", _settings.UserGuid);

            var friendPayload = CartoSyncPayloadBuilder.BuildFriend(input);
            var tpPayload = CartoSyncPayloadBuilder.BuildTpBoyPublic(input);

            var friendPushed = false;
            var tpPushed = false;

            if (force || friendPayload.Revision != _settings.LastPushedFriendRevision)
            {
                var json = JsonSerializer.Serialize(friendPayload, JsonOpts);
                await _hub.InvokeAsync("PushFriendUpdate", _settings.UserGuid, friendPayload.Revision, json);
                _settings.LastPushedFriendRevision = friendPayload.Revision;
                friendPushed = true;
            }

            if (force || tpPayload.Revision != _settings.LastPushedTpBoyRevision)
            {
                var json = JsonSerializer.Serialize(tpPayload, JsonOpts);
                await _hub.InvokeAsync("PushTpBoyPublic", _settings.UserGuid, tpPayload.Revision, json);
                _settings.LastPushedTpBoyRevision = tpPayload.Revision;
                tpPushed = true;
            }

            var parts = new List<string>();
            if (friendPushed) parts.Add("amis");
            if (tpPushed) parts.Add("TP Boy");
            var msg = parts.Count > 0
                ? $"Envoyé : {string.Join(", ", parts)}."
                : "Déjà à jour — réception en cours si le serveur a du nouveau.";

            return new SyncRunResult
            {
                Connected = true,
                FriendPushed = friendPushed,
                TpBoyPushed = tpPushed,
                Message = msg
            };
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Sync: RunSync failed ({Trigger})", trigger);
            return new SyncRunResult { Connected = _connected, Message = $"Erreur sync : {ex.Message}" };
        }
    }

    public async Task PushBountyUpdateAsync(BountySyncPayload payload)
    {
        if (_hub?.State != HubConnectionState.Connected) return;

        try
        {
            var json = JsonSerializer.Serialize(payload, JsonOpts);
            await _hub.InvokeAsync("PushBountyUpdate", _settings.UserGuid, json);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Sync: bounty push failed");
        }
    }

    private void OnReceiveLegacyUpdate(string friendGuid, string json) => OnReceiveFriendUpdate(friendGuid, json);

    private void OnReceiveFriendUpdate(string friendGuid, string json)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<FriendSyncPayload>(json, JsonOpts);
            if (payload == null)
            {
                var legacy = JsonSerializer.Deserialize<SyncPayload>(json, JsonOpts);
                if (legacy == null) return;
                payload = new FriendSyncPayload
                {
                    Revision = DateTimeOffset.UtcNow.Ticks,
                    SentAt = DateTimeOffset.UtcNow,
                    Accounts = legacy.Accounts,
                    Characters = legacy.Characters
                };
            }

            if (_settings.ReceivedFriendRevisions.TryGetValue(friendGuid, out var prev)
                && prev == payload.Revision)
                return;

            _settings.ReceivedFriendRevisions[friendGuid] = payload.Revision;
            FriendDataReceived?.Invoke(friendGuid, payload);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Sync: failed to deserialize friend data from {Guid}", friendGuid[..Math.Min(8, friendGuid.Length)]);
        }
    }

    private void OnReceiveTpBoyPublic(string ownerGuid, string json)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<TpBoyPublicPayload>(json, JsonOpts);
            if (payload == null) return;

            if (_settings.ReceivedTpBoyRevisions.TryGetValue(ownerGuid, out var prev)
                && prev == payload.Revision)
                return;

            _settings.ReceivedTpBoyRevisions[ownerGuid] = payload.Revision;
            TpBoyPublicReceived?.Invoke(ownerGuid, payload);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Sync: failed to deserialize TP public from {Guid}", ownerGuid[..Math.Min(8, ownerGuid.Length)]);
        }
    }

    private void OnFriendLinkState(string friendGuid, string stateText)
    {
        if (!Enum.TryParse<FriendLinkState>(stateText, ignoreCase: true, out var state))
        {
            if (Enum.TryParse<ServerFriendLinkStateCompat>(stateText, ignoreCase: true, out var serverState))
                state = serverState switch
                {
                    ServerFriendLinkStateCompat.Mutual => FriendLinkState.Mutual,
                    ServerFriendLinkStateCompat.PendingOutbound => FriendLinkState.PendingOutbound,
                    _ => FriendLinkState.PendingInbound
                };
            else
                return;
        }

        var friend = GetFriend(friendGuid);
        if (friend != null)
            friend.LinkState = state;

        FriendLinkStateChanged?.Invoke(friendGuid, state);
    }

    private enum ServerFriendLinkStateCompat
    {
        PendingOutbound,
        PendingInbound,
        Mutual
    }

    private void OnReceiveBountyUpdate(string friendGuid, string json)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<BountySyncPayload>(json, JsonOpts);
            if (payload != null)
                FriendBountyReceived?.Invoke(friendGuid, payload);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Sync: failed to deserialize bounty from {Guid}", friendGuid[..Math.Min(8, friendGuid.Length)]);
        }
    }

    public async Task DisconnectAsync()
    {
        if (_hub != null)
        {
            try { await _hub.DisposeAsync(); } catch { /* ignore */ }
            _hub = null;
            _connected = false;
        }
    }

    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}
