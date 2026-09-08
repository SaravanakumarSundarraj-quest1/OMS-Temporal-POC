using OMS.Worker.Models;

namespace OMS.Worker.Services;

public sealed class MockPimService
{
    public Task<EnrichedOrder> EnrichAsync(OrderSubmission submission)
    {
        var items = submission.Order.Items
            .Select(item => item with
            {
                SkuId = $"SKU-{item.ItemId}",
                BrandCode = $"BRAND-{Math.Abs(item.ItemId.GetHashCode()) % 1000:000}"
            })
            .ToArray();

        return Task.FromResult(new EnrichedOrder(
            submission.CustomerId,
            submission.Order.OrderId,
            items));
    }
}
