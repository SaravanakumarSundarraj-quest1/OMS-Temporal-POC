using System.Diagnostics.Metrics;

namespace OMS.Worker.Services;

public sealed class OrderProcessingMetrics
{
    public const string MeterName = "OMS.Temporal";

    private readonly Meter meter = new(MeterName, "1.0.0");

    public Counter<long> ActivityExecutions { get; }

    public Counter<long> ActivityFailures { get; }

    public OrderProcessingMetrics()
    {
        ActivityExecutions = meter.CreateCounter<long>(
            "oms_activity_executions_total",
            description: "Number of order activity executions.");
        ActivityFailures = meter.CreateCounter<long>(
            "oms_activity_failures_total",
            description: "Number of failed order activity executions.");
    }
}