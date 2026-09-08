using OMS.Worker.Activities;
using OMS.Worker.Models;
using Temporalio.Workflows;

namespace OMS.Worker.Workflows;

[Workflow]
public sealed class OrderProcessingWorkflow
{
    private OrderStatus status = OrderStatus.Submitted;
    private string? message;
    private PaymentCapture? paymentCapture;
    private SupportCorrection? supportCorrection;
    private bool cancellationRequested;
    private EnrichedOrder? enrichedOrder;

    [WorkflowRun]
    public async Task<OrderStatusView> RunAsync(OrderSubmission submission)
    {
        await SaveStatusAsync(submission.Order.OrderId, OrderStatus.Submitted);

        while (true)
        {
            var validation = await Workflow.ExecuteActivityAsync(
                (OrderActivities a) => a.ValidateOrderAsync(submission),
                ActivityOptions());

            if (!validation.IsValid)
            {
                await SaveStatusAsync(
                    submission.Order.OrderId,
                    OrderStatus.ValidationFailed,
                    validation.Reason);

                await Workflow.WaitConditionAsync(() => supportCorrection != null || cancellationRequested);

                if (cancellationRequested)
                {
                    return await CancelAsync(submission.Order.OrderId, "Cancelled while awaiting support correction.");
                }

                submission = submission with
                {
                    Order = submission.Order with { Items = supportCorrection!.Items }
                };
                supportCorrection = null;
                continue;
            }

            break;
        }

        await SaveStatusAsync(submission.Order.OrderId, OrderStatus.Validated);

        enrichedOrder = await Workflow.ExecuteActivityAsync(
            (OrderActivities a) => a.EnrichOrderAsync(submission),
            ActivityOptions());

        await SaveStatusAsync(submission.Order.OrderId, OrderStatus.Enriched);
        await SaveStatusAsync(submission.Order.OrderId, OrderStatus.WaitingForPayment);

        var completed = await Workflow.WaitConditionAsync(
            () => paymentCapture != null || cancellationRequested,
            TimeSpan.FromDays(30));

        if (!completed)
        {
            return await ExpireAsync(submission.Order.OrderId);
        }

        if (cancellationRequested && paymentCapture == null)
        {
            return await CancelAsync(submission.Order.OrderId, "Order cancelled before payment capture.");
        }

        var capturedPayment = paymentCapture!;
        var paymentValid = await Workflow.ExecuteActivityAsync(
            (OrderActivities a) => a.ValidatePaymentAsync(capturedPayment),
            PaymentActivityOptions());

        if (!paymentValid)
        {
            return await CancelAsync(submission.Order.OrderId, "Payment capture could not be validated.");
        }

        await SaveStatusAsync(
            submission.Order.OrderId,
            OrderStatus.PaymentCaptured,
            "Payment capture validated.",
            capturedPayment.Rrn);

        await Workflow.ExecuteActivityAsync(
            (OrderActivities a) => a.FulfillAsync(enrichedOrder!, capturedPayment),
            FulfillmentActivityOptions());

        await Workflow.ExecuteActivityAsync(
            (OrderActivities a) => a.SaveFulfilledAsync(enrichedOrder!, capturedPayment),
            ActivityOptions());

        status = OrderStatus.Fulfilled;
        message = "Order forwarded to fulfillment.";

        return new OrderStatusView(submission.Order.OrderId, status, message, capturedPayment.Rrn);
    }

    [WorkflowSignal]
    public Task CapturePaymentAsync(PaymentCapture capture)
    {
        if (paymentCapture == null && status is not OrderStatus.Cancelled and not OrderStatus.Expired)
        {
            paymentCapture = capture;
        }

        return Task.CompletedTask;
    }

    [WorkflowSignal]
    public Task CancelAsync(string reason)
    {
        if (status is not OrderStatus.PaymentCaptured and not OrderStatus.Fulfilled)
        {
            cancellationRequested = true;
            message = reason;
        }

        return Task.CompletedTask;
    }

    [WorkflowSignal]
    public Task CorrectOrderAsync(SupportCorrection correction)
    {
        supportCorrection = correction;
        return Task.CompletedTask;
    }

    [WorkflowQuery]
    public OrderStatusView GetStatus()
    {
        return new OrderStatusView(
            Workflow.Info.WorkflowId,
            status,
            message,
            paymentCapture?.Rrn);
    }

    private async Task<OrderStatusView> CancelAsync(string orderId, string reason)
    {
        status = OrderStatus.Cancelled;
        message = reason;
        await SaveStatusAsync(orderId, status, reason);
        return new OrderStatusView(orderId, status, reason, paymentCapture?.Rrn);
    }

    private async Task<OrderStatusView> ExpireAsync(string orderId)
    {
        status = OrderStatus.Expired;
        message = "Payment capture was not received within 30 days.";
        await SaveStatusAsync(orderId, status, message);
        return new OrderStatusView(orderId, status, message);
    }

    private async Task SaveStatusAsync(
        string orderId,
        OrderStatus newStatus,
        string? newMessage = null,
        string? rrn = null)
    {
        status = newStatus;
        message = newMessage;
        await Workflow.ExecuteActivityAsync(
            (OrderActivities a) => a.SaveStatusAsync(
                new OrderStatusView(orderId, newStatus, newMessage, rrn)),
            ActivityOptions());
    }

    private static ActivityOptions ActivityOptions() => new()
    {
        StartToCloseTimeout = TimeSpan.FromSeconds(15),
        RetryPolicy = new()
        {
            InitialInterval = TimeSpan.FromSeconds(1),
            BackoffCoefficient = 2,
            MaximumInterval = TimeSpan.FromSeconds(10),
            MaximumAttempts = 3
        }
    };

    private static ActivityOptions PaymentActivityOptions() => new()
    {
        StartToCloseTimeout = TimeSpan.FromSeconds(10),
        RetryPolicy = new()
        {
            InitialInterval = TimeSpan.FromSeconds(1),
            BackoffCoefficient = 2,
            MaximumInterval = TimeSpan.FromSeconds(5),
            MaximumAttempts = 3
        }
    };

    private static ActivityOptions FulfillmentActivityOptions() => new()
    {
        StartToCloseTimeout = TimeSpan.FromSeconds(15),
        RetryPolicy = new()
        {
            InitialInterval = TimeSpan.FromSeconds(2),
            BackoffCoefficient = 2,
            MaximumInterval = TimeSpan.FromSeconds(15),
            MaximumAttempts = 5
        }
    };
}
