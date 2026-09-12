using System.Text.Json;
using WebPush;
using RetailPulse.BuildingBlocks;
using BuildingBlockPushSubscription = RetailPulse.BuildingBlocks.PushSubscription;

namespace RetailPulse.Cloud;

public sealed class VapidPushNotificationSender(string publicKey, string privateKey, string subject = "mailto:ops@retailpulse.local") : IPushNotificationSender
{
    private readonly WebPushClient client = new();
    private readonly VapidDetails vapid = new(subject, publicKey, privateKey);

    public async Task SendAsync(BuildingBlockPushSubscription subscription, NotificationPayload payload, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(subscription.Endpoint))
        {
            return;
        }

        var request = new WebPush.PushSubscription(subscription.Endpoint, subscription.P256dh, subscription.Auth);
        var content = JsonSerializer.Serialize(new
        {
            title = payload.Title,
            body = payload.Body,
            tag = payload.Tag ?? "retailpulse-alert",
            data = new { url = payload.Url ?? "/#alerts" },
            icon = "/icon-192.png",
            badge = "/icon-192.png"
        });

        await client.SendNotificationAsync(request, content, vapid);
    }
}