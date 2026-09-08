using OMS.Worker.Models;

namespace OMS.Worker.Services;

public sealed class MockPaymentService
{
    public Task<bool> ValidateCaptureAsync(PaymentCapture payment)
    {
        var valid = payment.Rrn.StartsWith("RRN-", StringComparison.OrdinalIgnoreCase)
                    && payment.AmountCents > 0;
        return Task.FromResult(valid);
    }
}
