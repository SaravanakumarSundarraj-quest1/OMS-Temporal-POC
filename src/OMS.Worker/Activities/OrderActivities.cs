using OMS.Worker.Models;
using OMS.Worker.Services;
using Temporalio.Activities;

namespace OMS.Worker.Activities;

public sealed class OrderActivities
{
    private readonly IOrderRepository repository;
    private readonly OrderProcessingMetrics metrics;
    private readonly MockCommerceService commerce;
    private readonly MockPimService pim;
    private readonly MockPaymentService payment;
    private readonly MockFulfillmentService fulfillment;

    public OrderActivities(
        IOrderRepository repository,
        MockCommerceService commerce,
        MockPimService pim,
        MockPaymentService payment,
        MockFulfillmentService fulfillment,
        OrderProcessingMetrics metrics)
    {
        this.repository = repository;
        this.commerce = commerce;
        this.pim = pim;
        this.payment = payment;
        this.fulfillment = fulfillment;
        this.metrics = metrics;
    }

    [Activity]
    public async Task<ValidationResult> ValidateOrderAsync(OrderSubmission submission)
    {
        return await TrackAsync(nameof(ValidateOrderAsync), () => commerce.ValidateAsync(submission));
    }

    [Activity]
    public async Task<EnrichedOrder> EnrichOrderAsync(OrderSubmission submission)
    {
        return await TrackAsync(nameof(EnrichOrderAsync), () => pim.EnrichAsync(submission));
    }

    [Activity]
    public async Task<bool> ValidatePaymentAsync(PaymentCapture capture)
    {
        return await TrackAsync(nameof(ValidatePaymentAsync), () => payment.ValidateCaptureAsync(capture));
    }

    [Activity]
    public Task SaveStatusAsync(OrderStatusView status)
    {
        return TrackAsync(nameof(SaveStatusAsync), () =>
        {
            repository.Save(status);
            return Task.CompletedTask;
        });
    }

    [Activity]
    public Task SaveFulfilledAsync(EnrichedOrder order, PaymentCapture paymentCapture)
    {
        return TrackAsync(nameof(SaveFulfilledAsync), () =>
        {
            repository.Save(new OrderStatusView(
                order.OrderId,
                OrderStatus.Fulfilled,
                "Order forwarded to fulfillment.",
                paymentCapture.Rrn));
            return Task.CompletedTask;
        });
    }

    [Activity]
    public async Task<FulfillmentResult> FulfillAsync(
        EnrichedOrder order,
        PaymentCapture paymentCapture)
    {
        return await TrackAsync(nameof(FulfillAsync), () => fulfillment.SubmitAsync(order, paymentCapture));
    }

    [Activity]
    public async Task<bool> CompensateFulfillmentAsync(string orderId)
    {
        return await TrackAsync(nameof(CompensateFulfillmentAsync), () => fulfillment.CancelAsync(orderId));
    }

    private async Task<T> TrackAsync<T>(string activityName, Func<Task<T>> operation)
    {
        metrics.ActivityExecutions.Add(1, new KeyValuePair<string, object?>("activity", activityName));
        try
        {
            return await operation();
        }
        catch
        {
            metrics.ActivityFailures.Add(1, new KeyValuePair<string, object?>("activity", activityName));
            throw;
        }
    }

    private async Task TrackAsync(string activityName, Func<Task> operation)
    {
        metrics.ActivityExecutions.Add(1, new KeyValuePair<string, object?>("activity", activityName));
        try
        {
            await operation();
        }
        catch
        {
            metrics.ActivityFailures.Add(1, new KeyValuePair<string, object?>("activity", activityName));
            throw;
        }
    }
}
