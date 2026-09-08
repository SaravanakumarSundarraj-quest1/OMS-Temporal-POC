using OMS.Worker.Activities;
using OMS.Worker.Models;
using OMS.Worker.Services;
using OMS.Worker.Workflows;
using Temporalio.Client;
using Temporalio.Worker;

namespace OMS.Api;

public sealed class TemporalWorkerHostedService : BackgroundService
{
    private readonly ITemporalClient client;
    private readonly InMemoryOrderRepository repository;

    public TemporalWorkerHostedService(ITemporalClient client, InMemoryOrderRepository repository)
    {
        this.client = client;
        this.repository = repository;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var activities = new OrderActivities(
            repository,
            new MockCommerceService(),
            new MockPimService(),
            new MockPaymentService(),
            new MockFulfillmentService());

        using var worker = new TemporalWorker(
            client,
            new TemporalWorkerOptions(TemporalConstants.TaskQueue)
                .AddWorkflow<OrderProcessingWorkflow>()
                .AddActivity(activities.ValidateOrderAsync)
                .AddActivity(activities.EnrichOrderAsync)
                .AddActivity(activities.ValidatePaymentAsync)
                .AddActivity(activities.SaveStatusAsync)
                .AddActivity(activities.SaveFulfilledAsync)
                .AddActivity(activities.FulfillAsync));

        await worker.ExecuteAsync(stoppingToken);
    }
}
