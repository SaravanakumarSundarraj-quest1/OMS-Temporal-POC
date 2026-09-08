namespace OMS.Worker.Models;

public enum OrderStatus
{
    Submitted,
    ValidationFailed,
    Validated,
    Enriched,
    WaitingForPayment,
    PaymentCaptured,
    PaymentRejected,
    Cancelled,
    Expired,
    FulfillmentFailed,
    Fulfilled
}

public record OrderSubmission(
    string CustomerId,
    OrderPayload Order,
    RiskData? RiskData = null);

public record OrderPayload(
    string OrderId,
    IReadOnlyList<OrderItem> Items);

public record OrderItem(
    string ItemId,
    int Quantity,
    string? SkuId = null,
    string? BrandCode = null);

public record RiskData(string? RiskInput, string? RiskDecision = null);

public record PaymentCapture(
    string CustomerId,
    string Rrn,
    long AmountCents,
    string OrderId);

public record SupportCorrection(IReadOnlyList<OrderItem> Items);

public record ValidationResult(bool IsValid, string? Reason = null);

public record EnrichedOrder(
    string CustomerId,
    string OrderId,
    IReadOnlyList<OrderItem> Items);

public record FulfillmentResult(string OrderId, bool Accepted);

public record OrderStatusView(
    string OrderId,
    OrderStatus Status,
    string? Message = null,
    string? Rrn = null);
