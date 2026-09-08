using Microsoft.AspNetCore.Mvc;
using OMS.Worker.Models;
using OMS.Worker.Services;
using OMS.Worker.Workflows;
using Temporalio.Client;

namespace OMS.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly ITemporalClient temporal;
    private readonly InMemoryOrderRepository repository;

    public OrdersController(ITemporalClient temporal, InMemoryOrderRepository repository)
    {
        this.temporal = temporal;
        this.repository = repository;
    }

    [HttpPost]
    public async Task<IActionResult> Submit(OrderSubmission request)
    {
        var workflowId = request.Order.OrderId;

        var handle = await temporal.StartWorkflowAsync(
            (OrderProcessingWorkflow wf) => wf.RunAsync(request),
            new(id: workflowId, taskQueue: TemporalConstants.TaskQueue));

        return Accepted(new
        {
            workflowId,
            runId = handle.Result.RunId
        });
    }

    [HttpGet("{orderId}")]
    public async Task<IActionResult> Get(string orderId)
    {
        var local = repository.Get(orderId);
        if (local != null)
        {
            return Ok(local);
        }

        try
        {
            var handle = temporal.GetWorkflowHandle<OrderProcessingWorkflow>(orderId);
            var status = await handle.QueryAsync(wf => wf.GetStatus());
            return Ok(status);
        }
        catch
        {
            return NotFound();
        }
    }
}
