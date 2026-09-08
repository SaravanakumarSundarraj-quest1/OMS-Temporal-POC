# OMS Temporal POC

A small .NET 8 proof of concept for the Partner Application Assessment: Order Processing Management System (OMS).

## Scope

- Temporal is the durable orchestration layer.
- No Kafka is used in this POC.
- Temporal CLI dev server uses its default **in-memory persistence** for the Temporal service.
- Application order/dashboard data uses an in-memory repository.
- Commerce, PIM, Payment, and Fulfillment are mocked as in-process services behind Activities.
- ASP.NET Core hosts the API and the Temporal Worker in the same process for a simple demo.
- Temporal Web UI is provided by `temporal server start-dev` at `http://localhost:8233`.

## Assessment mapping

| Assessment requirement | POC implementation |
|---|---|
| Inputs can arrive in different sequences | Payment is delivered asynchronously as a Temporal Signal |
| Missing input | Workflow waits durably for a signal |
| 30-day TTL | Temporal timer via `Workflow.WaitConditionAsync(..., timeout)` |
| Expired order | Dashboard activity records `Expired` |
| Order validation | `ValidateOrderAsync` Activity |
| Support correction | `SupportCorrection` Signal, followed by re-validation |
| Enrichment | `EnrichOrderAsync` Activity |
| Payment capture | `PaymentCaptured` Signal + payment validation Activity |
| Cancellation | `CancelOrder` Signal before capture |
| Dashboard | In-memory repository Activity |
| Fulfillment | Mock fulfillment Activity |
| Failure handling | Activity retry policies and explicit business-state handling |
| vNext risk | Optional `RiskData` model field and documented extension point |
| PII | Email is intentionally not included in the initial workflow payload; README documents the future protection approach |

## Prerequisites

- .NET 8 SDK
- Temporal CLI

The Temporal .NET SDK currently has 1.18.0 available on NuGet. This project pins that version for repeatable builds.

## Run

Terminal 1:

```powershell
temporal server start-dev
```

The Temporal dev server defaults to in-memory persistence and starts the Web UI. The server is available on `localhost:7233` and the UI on `http://localhost:8233`.

Terminal 2:

```powershell
dotnet restore
dotnet run --project src/OMS.Api
```

Open the API shown by ASP.NET Core and the Temporal UI at `http://localhost:8233`.

## Demo

Create an order:

```http
POST /api/orders
Content-Type: application/json

{
  "customerId": "CUST-1001",
  "order": {
    "orderId": "ORD-1001",
    "items": [
      { "itemId": "ITEM-001", "quantity": 2 },
      { "itemId": "ITEM-002", "quantity": 1 }
    ]
  }
}
```

The workflow validates and enriches the order, then waits for payment.

Capture payment:

```http
POST /api/orders/ORD-1001/payment
Content-Type: application/json

{
  "customerId": "CUST-1001",
  "rrn": "RRN-1001",
  "amountCents": 12999
}
```

Query status:

```http
GET /api/orders/ORD-1001
```

Cancel before payment:

```http
POST /api/orders/ORD-1001/cancel
Content-Type: application/json

{ "reason": "Customer requested cancellation" }
```

For an invalid order, include an item ID containing `INVALID`, then send:

```http
POST /api/orders/ORD-1002/support-correction
Content-Type: application/json

{
  "items": [
    { "itemId": "ITEM-001", "quantity": 1 }
  ]
}
```

## Temporal UI

The UI is the primary demo surface for the assessment. It shows Workflow Execution history, Activities, Signals, timers, retries, and final status.

## Important POC limitation

The Temporal dev server's default in-memory persistence intentionally loses Workflow histories when the dev server stops. This is suitable for a POC/demo. If we later want restart durability, change the command to `temporal server start-dev --db-filename .temporal/temporal.db`.

## PII approach for vNext

The initial assessment payload does not require customer email. When email is introduced, avoid putting unnecessary PII into Workflow history. Prefer a reference/token to protected application storage, encrypt sensitive fields at rest, restrict access, redact logs, and use a custom Temporal payload codec/encryption strategy where appropriate.

## Risk vNext

Add risk collection as a new Activity or Child Workflow between validation and enrichment. Keep the workflow input extensible with an optional `RiskData` field so the first release can remain compatible.
