using RetailPulse.BuildingBlocks;

namespace RetailPulse.UnitTests;

public class CloudSyncTests
{
    [Fact]
    public void Retry_policy_is_bounded_and_only_transient_failures_retry()
    {
        var policy = new SyncRetryPolicy(5, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5));

        Assert.Equal(TimeSpan.FromSeconds(2), policy.GetDelay(1));
        Assert.Equal(TimeSpan.FromSeconds(4), policy.GetDelay(2));
        Assert.Equal(TimeSpan.FromSeconds(5), policy.GetDelay(3));
        Assert.True(policy.CanRetry(4, SyncAttemptClassification.TransientFailure));
        Assert.False(policy.CanRetry(4, SyncAttemptClassification.Unauthorized));
        Assert.False(policy.CanRetry(5, SyncAttemptClassification.TransientFailure));
    }
}
