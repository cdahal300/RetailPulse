using RetailPulse.BuildingBlocks;

namespace RetailPulse.UnitTests;

public class PushNotificationSenderTests
{
    [Fact]
    public async Task Noop_sender_is_used_when_vapid_private_key_is_missing()
    {
        var sender = new NoOpPushNotificationSender();
        var subscription = new PushSubscription(
            "tenant-1",
            "store-1",
            "manager-1",
            "https://push.example/subscription-1",
            "p256dh-value",
            "auth-value",
            DateTimeOffset.UtcNow);

        var exception = await Record.ExceptionAsync(() => sender.SendAsync(subscription,
            new NotificationPayload("RetailPulse", "Low stock alert", "/#alerts")));

        Assert.Null(exception);
    }
}