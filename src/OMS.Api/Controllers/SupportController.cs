using Microsoft.AspNetCore.Mvc;
using OMS.Worker.Models;
using OMS.Worker.Workflows;
using Temporalio.Client;

namespace OMS.Api.Controllers;

[ApiController]
[Route("api/orders/{orderId}")]
public sealed class SupportController : ControllerBase
{
    private readonly ITemporalClient temporal;

    public SupportController(ITemporalClient temporal) => this.temporal = temporal;

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(string orderId, [FromBody] CancelRequest request)
    {
        var handle = temporal.GetWorkflowHandle<OrderProcessingWorkflow>(orderId);
        await handle.SignalAsync(wf => wf.CancelAsync(request.Reason));
        return Accepted(new { orderId, signal = "CancelOrder" });
    }

    [HttpPost("support-correction")]
    public async Task<IActionResult> Correct(string orderId, SupportCorrection correction)
    {
        var handle = temporal.GetWorkflowHandle<OrderProcessingWorkflow>(orderId);
        await handle.SignalAsync(wf => wf.CorrectOrderAsync(correction));
        return Accepted(new { orderId, signal = "SupportCorrection" });
    }
}

public sealed record CancelRequest(string Reason);
