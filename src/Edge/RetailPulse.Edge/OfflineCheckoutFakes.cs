using RetailPulse.BuildingBlocks;

namespace RetailPulse.Edge;

public sealed class FakePaymentProvider(IEnumerable<PaymentResult>? results = null) : IPaymentProvider
{
    private readonly Queue<PaymentResult> results = new(results ?? [PaymentResult.Approved("fake-payment-reference")]);
    public List<PaymentRequest> Requests { get; } = [];

    public Task<PaymentResult> AuthorizeAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        return Task.FromResult(results.Count > 0 ? results.Dequeue() : PaymentResult.Pending());
    }
}

public sealed class InMemoryCheckoutPersistence : ILocalCheckoutPersistence
{
    public List<CheckoutCommit> Commits { get; } = [];

    public Task CommitAsync(CheckoutCommit commit, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Commits.Add(commit);
        return Task.CompletedTask;
    }
}