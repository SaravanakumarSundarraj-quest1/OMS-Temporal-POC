using OMS.Worker.Models;

namespace OMS.Worker.Services;

public sealed class MockCommerceService
{
    public Task<ValidationResult> ValidateAsync(OrderSubmission submission)
    {
        var invalid = submission.Order.Items.Any(i =>
            i.Quantity <= 0 || i.ItemId.Contains("INVALID", StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(invalid
            ? new ValidationResult(false, "Commerce API rejected the order data.")
            : new ValidationResult(true));
    }
}
