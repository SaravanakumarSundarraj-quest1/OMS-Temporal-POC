using Microsoft.AspNetCore.Mvc;
using OMS.Worker.Models;
using OMS.Worker.Workflows;
using Temporalio.Client;

namespace OMS.Api.Controllers;

[ApiController]
[Route("api/orders/{orderId}/payment")]
public sealed class PaymentsController : ControllerBase
{
    private readonly ITemporalClient temporal;

    public PaymentsController(ITemporalClient temporal) => this.temporal = temporal;

    [HttpPost]
    public async Task<IActionResult> Capture(string orderId, PaymentCapture request)
    {
        if (!string.Equals(orderId, request.OrderId, StringComparison.Ordinal))
        {
            return BadRequest("Order ID in the route and payload must match.");
        }

        var handle = temporal.GetWorkflowHandle<OrderProcessingWorkflow>(orderId);
        await handle.SignalAsync(wf => wf.CapturePaymentAsync(request));
        return Accepted(new { orderId, signal = "PaymentCaptured" });
    }
}
