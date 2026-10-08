using System.Text.Json;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Npgsql;
using NpgsqlTypes;

namespace InternManagement.Services;

public sealed class WebPushService(
    IConfiguration configuration,
    PushServiceClient client,
    ILogger<WebPushService> logger)
{
    private readonly string connectionString = configuration.GetConnectionString("InternManagement")!;
    private readonly string publicKey = configuration["WebPush:PublicKey"] ?? "";
    private readonly string privateKey = configuration["WebPush:PrivateKey"] ?? "";
    private readonly string subject = configuration["WebPush:Subject"] ?? "mailto:admin@example.com";

    public string? PublicKey => IsConfigured ? publicKey : null;
    private bool IsConfigured => !string.IsNullOrWhiteSpace(publicKey) && !string.IsNullOrWhiteSpace(privateKey);

    public async Task SaveSubscriptionAsync(int userId, string endpoint, string p256dh, string auth,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.pushsubscriptions(userid,endpoint,p256dh,auth)
            VALUES (@user,@endpoint,@p256dh,@auth)
            ON CONFLICT (endpoint) DO UPDATE SET userid=EXCLUDED.userid,p256dh=EXCLUDED.p256dh,auth=EXCLUDED.auth,createdat=CURRENT_TIMESTAMP
            """;
        command.Parameters.Add("@user", NpgsqlDbType.Integer).Value = userId;
        command.Parameters.Add("@endpoint", NpgsqlDbType.Text).Value = endpoint;
        command.Parameters.Add("@p256dh", NpgsqlDbType.Text).Value = p256dh;
        command.Parameters.Add("@auth", NpgsqlDbType.Text).Value = auth;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RemoveSubscriptionAsync(int userId, string endpoint, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM dbo.pushsubscriptions WHERE userid=@user AND endpoint=@endpoint";
        command.Parameters.Add("@user", NpgsqlDbType.Integer).Value = userId;
        command.Parameters.Add("@endpoint", NpgsqlDbType.Text).Value = endpoint;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SendToUserAsync(int userId, string title, string message, string? actionUrl,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured) return;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT endpoint,p256dh,auth FROM dbo.pushsubscriptions WHERE userid=@user";
        command.Parameters.Add("@user", NpgsqlDbType.Integer).Value = userId;
        var subscriptions = new List<(string Endpoint, string P256dh, string Auth)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
                subscriptions.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));

        if (subscriptions.Count == 0) return;
        using var authentication = new VapidAuthentication(publicKey, privateKey) { Subject = subject };
        var payload = JsonSerializer.Serialize(new { title, body = message, url = actionUrl });
        foreach (var saved in subscriptions)
        {
            var subscription = new PushSubscription
            {
                Endpoint = saved.Endpoint,
                Keys = new Dictionary<string, string> { ["p256dh"] = saved.P256dh, ["auth"] = saved.Auth }
            };
            try
            {
                await client.RequestPushMessageDeliveryAsync(subscription,
                    new PushMessage(payload) { TimeToLive = 60 * 60 }, authentication, cancellationToken);
            }
            catch (PushServiceClientException error)
            {
                logger.LogWarning("Web Push delivery failed (HTTP {StatusCode}); user notification remains available in-app.",
                    (int?)error.StatusCode);
                // Endpoint is a capability secret; do not include it in logs.
                if (error.StatusCode is System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound)
                    await RemoveSubscriptionAsync(userId, saved.Endpoint, cancellationToken);
            }
            catch (Exception error)
            {
                logger.LogWarning(error, "Web Push delivery failed; user notification remains available in-app.");
            }
        }
    }
}
