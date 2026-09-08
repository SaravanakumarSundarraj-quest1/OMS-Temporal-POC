# OMS Temporal POC - API Payloads

## Base URL

```text
http://localhost:5000
```

Replace `5000` with the port shown by `dotnet run`.

## Endpoints

| Method | Endpoint | Purpose |
|---|---|---|
| POST | `/api/orders` | Submit an order and start the Temporal workflow |
| GET | `/api/orders/{orderId}` | Get order status |
| POST | `/api/orders/{orderId}/payment` | Send payment capture |
| POST | `/api/orders/{orderId}/cancel` | Cancel an order |
| POST | `/api/orders/{orderId}/support-correction` | Correct an invalid order |
| GET | `/health` | API health check |

---

# 1. Create Order

```http
POST /api/orders
Content-Type: application/json
```

```json
{
  "customerId": "CUST-001",
  "order": {
    "orderId": "ORD-1001",
    "items": [
      {
        "itemId": "ITEM-001",
        "quantity": 2,
        "skuId": "SKU-001",
        "brandCode": "BRAND-001"
      }
    ]
  },
  "riskData": {
    "riskInput": "STANDARD",
    "riskDecision": "APPROVED"
  }
}
```

Expected response:

```json
{
  "workflowId": "ORD-1001"
}
```

The workflow ID is the order ID.

---

# 2. Get Order Status

```http
GET /api/orders/ORD-1001
```

Example response:

```json
{
  "orderId": "ORD-1001",
  "status": "WaitingForPayment"
}
```

Possible statuses:

```text
Received
ValidationFailed
Validated
Enriched
WaitingForPayment
PaymentCaptured
Cancelled
Expired
Fulfilled
```

---

# 3. Capture Payment

Payment may arrive later after order submission.

```http
POST /api/orders/ORD-1001/payment
Content-Type: application/json
```

```json
{
  "rrn": "RRN-100001",
  "amount": 150.00
}
```

Expected response:

```json
{
  "message": "Payment signal sent",
  "orderId": "ORD-1001"
}
```

The workflow receives the payment through a Temporal Signal and validates the RRN through the payment integration/activity.

---

# 4. Cancel Order

Cancellation is allowed before payment capture.

```http
POST /api/orders/ORD-1001/cancel
Content-Type: application/json
```

```json
{
  "reason": "Customer requested cancellation"
}
```

Expected response:

```json
{
  "message": "Cancellation signal sent",
  "orderId": "ORD-1001"
}
```

---

# 5. Support Correction

Use this when Commerce validation fails.

```http
POST /api/orders/ORD-1002/support-correction
Content-Type: application/json
```

```json
{
  "items": [
    {
      "itemId": "ITEM-001",
      "quantity": 2
    },
    {
      "itemId": "ITEM-002",
      "quantity": 1
    }
  ],
  "reason": "Corrected invalid item quantity"
}
```

Expected response:

```json
{
  "message": "Support correction signal sent",
  "orderId": "ORD-1002"
}
```

The workflow receives the correction and re-runs validation.

---

# 6. Health Check

```http
GET /health
```

Example response:

```json
{
  "status": "ok",
  "temporal": "localhost:7233"
}
```

---

# End-to-End Scenarios

## Scenario 1 - Normal Order

### Step 1 - Submit

```http
POST /api/orders
Content-Type: application/json
```

```json
{
  "orderId": "ORD-1001",
  "customerId": "CUST-001",
  "items": [
    {
      "itemId": "ITEM-001",
      "quantity": 2
    }
  ]
}
```

### Step 2 - Check status

```http
GET /api/orders/ORD-1001
```

Expected:

```text
WaitingForPayment
```

### Step 3 - Capture payment

```http
POST /api/orders/ORD-1001/payment
Content-Type: application/json
```

```json
{
  "rrn": "RRN-100001",
  "amount": 150.00
}
```

### Step 4 - Check status

```http
GET /api/orders/ORD-1001
```

Expected final status:

```text
Fulfilled
```

### Workflow

```text
Order Submitted
      |
      v
Commerce Validation
      |
      v
PIM Enrichment
      |
      v
WaitingForPayment
      |
      | Payment Signal
      v
Payment Validation
      |
      v
Fulfillment
      |
      v
Fulfilled
```

---

# Scenario 2 - Invalid Order and Support Correction

### Step 1 - Submit invalid order

```http
POST /api/orders
Content-Type: application/json
```

```json
{
  "orderId": "ORD-1002",
  "customerId": "CUST-001",
  "items": [
    {
      "itemId": "INVALID-ITEM",
      "quantity": 0
    }
  ]
}
```

Expected status:

```text
ValidationFailed
```

### Step 2 - Correct the order

```http
POST /api/orders/ORD-1002/support-correction
Content-Type: application/json
```

```json
{
  "items": [
    {
      "itemId": "ITEM-001",
      "quantity": 1
    }
  ],
  "reason": "Corrected invalid item"
}
```

### Expected progression

```text
ValidationFailed
      |
      | Support Correction Signal
      v
Validation
      |
      v
PIM Enrichment
      |
      v
WaitingForPayment
```

---

# Scenario 3 - Cancellation Before Payment

### Submit order

```json
{
  "orderId": "ORD-1003",
  "customerId": "CUST-002",
  "items": [
    {
      "itemId": "ITEM-001",
      "quantity": 1
    }
  ]
}
```

Wait until:

```text
WaitingForPayment
```

Then:

```http
POST /api/orders/ORD-1003/cancel
Content-Type: application/json
```

```json
{
  "reason": "Customer cancelled order"
}
```

Expected:

```text
Cancelled
```

---

# Scenario 4 - Payment Never Received

Submit:

```http
POST /api/orders
Content-Type: application/json
```

```json
{
  "orderId": "ORD-1004",
  "customerId": "CUST-003",
  "items": [
    {
      "itemId": "ITEM-001",
      "quantity": 1
    }
  ]
}
```

Do not send payment.

The workflow waits for payment capture for up to:

```text
30 days
```

After the timeout:

```text
Expired
```

The expired order is written to dashboard storage.

For automated tests, Temporal time-skipping can be used so the 30-day timer does not require waiting 30 real days.

---

# Scenario 5 - Invalid Payment

Submit an order and wait for:

```text
WaitingForPayment
```

Then:

```http
POST /api/orders/ORD-1005/payment
Content-Type: application/json
```

```json
{
  "rrn": "INVALID-RRN",
  "amount": 150.00
}
```

The mock payment processor rejects an invalid RRN.

A valid example is:

```text
RRN-123456
```

---

# Scenario 6 - Multiple Items and PIM Enrichment

```http
POST /api/orders
Content-Type: application/json
```

```json
{
  "orderId": "ORD-1006",
  "customerId": "CUST-005",
  "items": [
    {
      "itemId": "ITEM-001",
      "quantity": 2
    },
    {
      "itemId": "ITEM-002",
      "quantity": 3
    },
    {
      "itemId": "ITEM-003",
      "quantity": 1
    }
  ]
}
```

PIM enrichment adds SKU ID and Brand Code.

Example:

```json
{
  "itemId": "ITEM-001",
  "quantity": 2,
  "skuId": "SKU-ITEM-001",
  "brandCode": "BRAND-001"
}
```

---

# Scenario 7 - Payment Arrives Later

### Submit order

```json
{
  "orderId": "ORD-1007",
  "customerId": "CUST-006",
  "items": [
    {
      "itemId": "ITEM-001",
      "quantity": 1
    }
  ]
}
```

The workflow waits:

```text
WaitingForPayment
```

Later send:

```http
POST /api/orders/ORD-1007/payment
Content-Type: application/json
```

```json
{
  "rrn": "RRN-100007",
  "amount": 250.00
}
```

The payment Signal wakes the waiting workflow.

---

# Scenario 8 - Payment and Cancellation Race

Submit:

```json
{
  "orderId": "ORD-1008",
  "customerId": "CUST-007",
  "items": [
    {
      "itemId": "ITEM-001",
      "quantity": 1
    }
  ]
}
```

Then test either event:

### Cancellation

```http
POST /api/orders/ORD-1008/cancel
Content-Type: application/json
```

```json
{
  "reason": "Customer requested cancellation"
}
```

### Or payment

```http
POST /api/orders/ORD-1008/payment
Content-Type: application/json
```

```json
{
  "rrn": "RRN-100008",
  "amount": 100.00
}
```

This scenario tests asynchronous event handling.

---

# Fulfillment Output

After successful payment validation, the fulfillment integration receives:

```json
{
  "customer_id": "CUST-001",
  "order_id": "ORD-1001",
  "payment_details": {
    "rrn": "RRN-100001"
  },
  "items": [
    {
      "item_id": "ITEM-001",
      "quantity": 2,
      "sku_id": "SKU-ITEM-001",
      "brand_code": "BRAND-001"
    }
  ]
}
```

Kafka is intentionally not used in this POC. Fulfillment is represented by a Temporal Activity/integration boundary.

---

# Temporal Web UI

Start Temporal:

```powershell
temporal server start-dev
```

Temporal frontend:

```text
localhost:7233
```

Temporal Web UI:

```text
http://localhost:8233
```

After creating an order, look for the workflow using the order ID:

```text
ORD-1001
```

The workflow history should show activities, signals, timers, and workflow state transitions.

---

# Swagger UI

If Swagger is enabled:

```text
http://localhost:5000/swagger
```

Replace `5000` with the API port displayed by:

```text
Now listening on: http://localhost:XXXX
```

Recommended happy-path testing order:

```text
1. POST /api/orders
2. GET  /api/orders/{orderId}
3. POST /api/orders/{orderId}/payment
4. GET  /api/orders/{orderId}
```

Invalid-order testing:

```text
1. POST /api/orders
2. GET  /api/orders/{orderId}
3. POST /api/orders/{orderId}/support-correction
4. GET  /api/orders/{orderId}
```

Cancellation testing:

```text
1. POST /api/orders
2. GET  /api/orders/{orderId}
3. POST /api/orders/{orderId}/cancel
4. GET  /api/orders/{orderId}
```

---

# POC Architecture

```text
                    +----------------------+
                    |      Swagger UI      |
                    |      /swagger       |
                    +----------+-----------+
                               |
                               v
                    +----------------------+
                    |       OMS API        |
                    |    ASP.NET Core      |
                    +----------+-----------+
                               |
                         Temporal SDK
                               |
                               v
              +--------------------------------+
              |       Temporal Server          |
              |       localhost:7233           |
              |                                |
              |       Temporal Web UI           |
              |       localhost:8233            |
              +----------------+---------------+
                               |
                               v
                    +----------------------+
                    |   Temporal Worker    |
                    | Order Processing WF   |
                    +----------+-----------+
                               |
             +-----------------+-----------------+
             |                 |                 |
             v                 v                 v
      +------------+    +------------+    +------------+
      |  Commerce  |    |    PIM     |    |  Payment   |
      |    Mock    |    |    Mock    |    |    Mock    |
      +------------+    +------------+    +------------+
                               |
                               v
                       +---------------+
                       |  Fulfillment  |
                       |      Mock     |
                       +---------------+
```

---

# Temporal Features Demonstrated

| Requirement | Temporal Feature |
|---|---|
| Long-running order process | Workflow |
| Commerce validation | Activity |
| PIM enrichment | Activity |
| Payment arriving later | Signal |
| Support correction | Signal |
| Cancellation | Signal |
| 30-day waiting period | Timer |
| Order expiration | Timer + Workflow state |
| Order status | Query |
| Integration failure handling | Activity Retry Policy |
| Fulfillment integration | Activity |
| Workflow history | Temporal |
| Workflow monitoring | Temporal Web UI |

---

# PII Considerations

Customer email information is future PII data.

For a production implementation:

- Do not log customer email addresses.
- Avoid unnecessary PII in Temporal workflow history.
- Store sensitive customer information in an appropriate protected data store.
- Pass only the minimum required information to Activities.
- Apply encryption and access controls.
- Avoid exposing PII through API responses unless required.

---

# Local Persistence

This POC intentionally uses in-memory storage.

Temporal local development server:

```text
In-memory persistence
```

Application order/dashboard repository:

```text
In-memory repository
```

Therefore, restarting the local Temporal server or application can remove local state.

This setup is intended for development, demonstration, and assessment purposes rather than production durability.

---

# Start the POC

## Terminal 1 - Temporal

```powershell
temporal server start-dev
```

Keep this terminal running.

## Terminal 2 - OMS API

```powershell
cd C:\Quest1\OMS-Temporal-POC
dotnet run --project src\OMS.Api
```

## Browser

Swagger:

```text
http://localhost:5000/swagger
```

Temporal UI:

```text
http://localhost:8233
```

Health:

```text
http://localhost:5000/health
```

Replace `5000` with the port displayed by the OMS API.
