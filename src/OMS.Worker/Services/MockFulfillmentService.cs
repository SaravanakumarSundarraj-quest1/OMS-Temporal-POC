using OMS.Worker.Models;

namespace OMS.Worker.Services;

public sealed class MockFulfillmentService
{
    public Task<FulfillmentResult> SubmitAsync(EnrichedOrder order, PaymentCapture payment)
    {
        return Task.FromResult(new FulfillmentResult(order.OrderId, true));
    }
}
