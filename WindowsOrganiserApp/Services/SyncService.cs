using System.Net.Http;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Serilog;
using WindowsOrganiserApp.Models;
using WindowsOrganiserApp.Models.Carto;

namespace WindowsOrganiserApp.Services;

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

    public event Action<string, SyncPayload>? FriendDataReceived;
    public event Action<string>? ConnectionStateChanged;
    public event Action<string, bool>? FriendOnlineChanged;
    public event Action? PushRequested;

    public bool IsConnected => _connected;
    public string UserGuid => _settings.UserGuid;
    public List<FriendEntry> Friends => _settings.Friends;
    public AppSettings Settings => _settings;

    public SyncService(AppSettings settings, ILogger logger)
    {
        _settings = settings;
        _log = logger;
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
            try { await _hub.DisposeAsync(); } catch { }
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

            _hub.On<string, string>("ReceiveUpdate", OnReceiveUpdate);
            _hub.On<string>("FriendOnline", guid => FriendOnlineChanged?.Invoke(guid, true));
            _hub.On<string>("FriendOffline", guid => FriendOnlineChanged?.Invoke(guid, false));
            _hub.On("RequestPush", () => PushRequested?.Invoke());

            _hub.Reconnected += async _ =>
            {
                _connected = true;
                ConnectionStateChanged?.Invoke("Connecté");
                await RegisterAndSubscribe();
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

            await RegisterAndSubscribe();
            _log.Information("Sync: connected OK");
        }
        catch (Exception ex)
        {
            _connected = false;
            _hub = null;
            var msg = $"Erreur: {ex.Message}";
            ConnectionStateChanged?.Invoke(msg);
            _log.Warning(ex, "Sync: failed to connect");
        }
    }

    private async Task RegisterAndSubscribe()
    {
        if (_hub == null) return;
        await _hub.InvokeAsync("Connect", _settings.UserGuid);
        foreach (var f in _settings.Friends)
            await _hub.InvokeAsync("Subscribe", _settings.UserGuid, f.Guid);
    }

    public async Task SubscribeToFriend(string friendGuid, string friendName)
    {
        if (_settings.Friends.All(f => f.Guid != friendGuid))
            _settings.Friends.Add(new FriendEntry { Guid = friendGuid, Name = friendName });

        if (_hub?.State == HubConnectionState.Connected)
            await _hub.InvokeAsync("Subscribe", _settings.UserGuid, friendGuid);
    }

    public async Task UnsubscribeFromFriend(string friendGuid)
    {
        _settings.Friends.RemoveAll(f => f.Guid == friendGuid);
        if (_hub?.State == HubConnectionState.Connected)
            await _hub.InvokeAsync("Unsubscribe", _settings.UserGuid, friendGuid);
    }

    public FriendEntry? GetFriend(string guid) => _settings.Friends.FirstOrDefault(f => f.Guid == guid);

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

    public async Task PushUpdateAsync(CartoData data)
    {
        if (_hub?.State != HubConnectionState.Connected) return;

        try
        {
            var payload = new SyncPayload
            {
                Accounts = data.Accounts,
                Characters = data.Characters.Where(c => !c.IsExternal).ToList()
            };
            var json = JsonSerializer.Serialize(payload, JsonOpts);
            await _hub.InvokeAsync("PushUpdate", _settings.UserGuid, json);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Sync: push failed");
        }
    }

    private void OnReceiveUpdate(string friendGuid, string json)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<SyncPayload>(json, JsonOpts);
            if (payload != null)
                FriendDataReceived?.Invoke(friendGuid, payload);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Sync: failed to deserialize from {Guid}", friendGuid[..8]);
        }
    }

    public async Task DisconnectAsync()
    {
        if (_hub != null)
        {
            try { await _hub.DisposeAsync(); } catch { }
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
