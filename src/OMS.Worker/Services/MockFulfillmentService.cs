using System.Collections.Concurrent;
using OMS.Worker.Models;

namespace OMS.Worker.Services;

public sealed class MockFulfillmentService
{
    private readonly ConcurrentDictionary<string, byte> submittedOrders = new();

    public Task<FulfillmentResult> SubmitAsync(EnrichedOrder order, PaymentCapture payment)
    {
        submittedOrders.TryAdd(order.OrderId, 0);
        return Task.FromResult(new FulfillmentResult(order.OrderId, true));
    }

    public Task<bool> CancelAsync(string orderId)
    {
        return Task.FromResult(submittedOrders.TryRemove(orderId, out _));
    }
}
