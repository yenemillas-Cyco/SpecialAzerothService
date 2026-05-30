using CartoSyncServer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR(opts =>
{
    opts.MaximumReceiveMessageSize = 512 * 1024; // 512 Ko — largement suffisant pour 30 persos
    opts.KeepAliveInterval = TimeSpan.FromSeconds(15);
});
builder.Services.AddSingleton<SubscriptionStore>();
builder.Services.AddSingleton<FriendshipStore>();

var app = builder.Build();

app.MapGet("/", () => "CartoSyncServer is running.");
app.MapGet("/health", () => Results.Ok("ok"));
app.MapHub<CartoHub>("/carto");

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Run($"http://0.0.0.0:{port}");
