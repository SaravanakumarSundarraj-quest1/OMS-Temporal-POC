using OMS.Worker.Models;
using OMS.Worker.Services;
using Temporalio.Activities;

namespace OMS.Worker.Activities;

public sealed class OrderActivities
{
    private readonly InMemoryOrderRepository repository;
    private readonly MockCommerceService commerce;
    private readonly MockPimService pim;
    private readonly MockPaymentService payment;
    private readonly MockFulfillmentService fulfillment;

    public OrderActivities(
        InMemoryOrderRepository repository,
        MockCommerceService commerce,
        MockPimService pim,
        MockPaymentService payment,
        MockFulfillmentService fulfillment)
    {
        this.repository = repository;
        this.commerce = commerce;
        this.pim = pim;
        this.payment = payment;
        this.fulfillment = fulfillment;
    }

    [Activity]
    public async Task<ValidationResult> ValidateOrderAsync(OrderSubmission submission)
    {
        return await commerce.ValidateAsync(submission);
    }

    [Activity]
    public async Task<EnrichedOrder> EnrichOrderAsync(OrderSubmission submission)
    {
        return await pim.EnrichAsync(submission);
    }

    [Activity]
    public async Task<bool> ValidatePaymentAsync(PaymentCapture capture)
    {
        return await payment.ValidateCaptureAsync(capture);
    }

    [Activity]
    public Task SaveStatusAsync(OrderStatusView status)
    {
        repository.Save(status);
        return Task.CompletedTask;
    }

    [Activity]
    public Task SaveFulfilledAsync(EnrichedOrder order, PaymentCapture paymentCapture)
    {
        repository.Save(new OrderStatusView(
            order.OrderId,
            OrderStatus.Fulfilled,
            "Order forwarded to fulfillment.",
            paymentCapture.Rrn));
        return Task.CompletedTask;
    }

    [Activity]
    public async Task<FulfillmentResult> FulfillAsync(
        EnrichedOrder order,
        PaymentCapture paymentCapture)
    {
        return await fulfillment.SubmitAsync(order, paymentCapture);
    }
}
