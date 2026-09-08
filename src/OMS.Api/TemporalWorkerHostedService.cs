using OMS.Worker.Activities;
using OMS.Worker.Models;
using OMS.Worker.Services;
using OMS.Worker.Workflows;
using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Worker;

namespace OMS.Api;

public sealed class TemporalWorkerHostedService : BackgroundService
{
    private readonly ITemporalClient client;
    private readonly IOrderRepository repository;
    private readonly OrderProcessingMetrics metrics;
    private readonly IConfiguration configuration;

    public TemporalWorkerHostedService(
        ITemporalClient client,
        IOrderRepository repository,
        OrderProcessingMetrics metrics,
        IConfiguration configuration)
    {
        this.client = client;
        this.repository = repository;
        this.metrics = metrics;
        this.configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var activities = new OrderActivities(
            repository,
            new MockCommerceService(),
            new MockPimService(),
            new MockPaymentService(),
            new MockFulfillmentService(),
            metrics);

        var workerOptions = new TemporalWorkerOptions(TemporalConstants.TaskQueue)
        {
            DeploymentOptions = new WorkerDeploymentOptions(
                new WorkerDeploymentVersion(
                    configuration["Temporal:WorkerDeploymentName"] ?? "oms-order-worker",
                    configuration["Temporal:WorkerBuildId"] ?? "oms-local"),
                useWorkerVersioning: true),
            MaxConcurrentActivities = configuration.GetValue<int?>("Temporal:MaxConcurrentActivities") ?? 20,
            MaxConcurrentWorkflowTasks = configuration.GetValue<int?>("Temporal:MaxConcurrentWorkflowTasks") ?? 100
        };

        using var worker = new TemporalWorker(
            client,
            workerOptions
                .AddWorkflow<OrderProcessingWorkflow>()
                .AddActivity(activities.ValidateOrderAsync)
                .AddActivity(activities.EnrichOrderAsync)
                .AddActivity(activities.ValidatePaymentAsync)
                .AddActivity(activities.SaveStatusAsync)
                .AddActivity(activities.SaveFulfilledAsync)
                .AddActivity(activities.FulfillAsync)
                .AddActivity(activities.CompensateFulfillmentAsync));

        await worker.ExecuteAsync(stoppingToken);
    }
}
