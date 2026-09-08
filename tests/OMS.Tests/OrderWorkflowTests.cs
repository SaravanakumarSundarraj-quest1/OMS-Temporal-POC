using OMS.Worker.Models;
using OMS.Worker.Workflows;
using Temporalio.Testing;
using Temporalio.Worker;

namespace OMS.Tests;

public class OrderWorkflowTests
{
    private static OrderSubmission ValidOrder(string id = "ORD-TEST") => new(
        "CUST-1",
        new OrderPayload(id, new[]
        {
            new OrderItem("ITEM-1", 1)
        }));

    [Fact]
    public async Task ValidOrder_WaitsForPayment()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions("test-orders")
                .AddWorkflow<OrderProcessingWorkflow>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await env.Client.StartWorkflowAsync(
                (OrderProcessingWorkflow wf) => wf.RunAsync(ValidOrder()),
                new(id: "ORD-TEST", taskQueue: "test-orders"));

            var status = await handle.QueryAsync(wf => wf.GetStatus());
            Assert.Equal(OrderStatus.WaitingForPayment, status.Status);
        });
    }

    [Fact]
    public async Task CancelBeforePayment_CompletesAsCancelled()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions("test-orders")
                .AddWorkflow<OrderProcessingWorkflow>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await env.Client.StartWorkflowAsync(
                (OrderProcessingWorkflow wf) => wf.RunAsync(ValidOrder("ORD-CANCEL")),
                new(id: "ORD-CANCEL", taskQueue: "test-orders"));

            await handle.SignalAsync(wf => wf.CancelAsync("Customer request"));
            var result = await handle.GetResultAsync();

            Assert.Equal(OrderStatus.Cancelled, result.Status);
        });
    }
}
