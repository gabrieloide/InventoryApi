# InventoryApi

High-performance RESTful API for inventory management and stock audit tracking, built with ASP.NET Core 10 Minimal APIs, Entity Framework Core, and PostgreSQL.

## Table of Contents

- Overview
- Architecture and Tech Stack
- Domain and Data Model
- API Specification
- Error Handling Contract
- Getting Started
- Database Migrations
- Running Tests
- Engineering Decisions

---

## Overview

InventoryApi serves as the centralized backend service for the Inventory Management ecosystem. It coordinates inventory catalog maintenance, enforces SKU uniqueness constraints, records stock change audit trails, and provides predictable HTTP contracts for mobile client synchronization.

## Architecture and Tech Stack

- Framework: ASP.NET Core 10 (.NET 10.0) Minimal APIs
- ORM: Entity Framework Core 10
- Relational Database: PostgreSQL (compatible with Supabase)
- Integration Testing: xUnit, Microsoft.AspNetCore.Mvc.Testing (WebApplicationFactory), Microsoft.EntityFrameworkCore.InMemory
- API Documentation: Microsoft.AspNetCore.OpenApi / Microsoft.OpenApi

```
+-------------------------------------------------------------+
|                        HTTP Clients                         |
|                   (iOS App / Web Clients)                   |
+-------------------------------------------------------------+
                              |
                              v
+-------------------------------------------------------------+
|                  ASP.NET Core Minimal APIs                  |
|  - Input Validation & Normalization                         |
|  - DTO Mapping Layer (Request/Response separation)          |
|  - Structured Error Responses (JSON)                        |
|  - CORS & HTTPS Redirection                                 |
+-------------------------------------------------------------+
                              |
                              v
+-------------------------------------------------------------+
|                Entity Framework Core 10                     |
|  - InventoryDbContext                                       |
|  - Optimistic / Transactional Persistence                   |
|  - Unique Constraints & Relationships                       |
+-------------------------------------------------------------+
                              |
                              v
+-------------------------------------------------------------+
|                    PostgreSQL Database                      |
|  - Products (Id, Name, Sku [Unique Index], Stock)           |
|  - StockChanges (Id, ProductId [FK], Date, Delta, Source)   |
+-------------------------------------------------------------+
```

## Domain and Data Model

### Product Entity
- ProductId (int, Primary Key, Identity)
- Name (string, required)
- Sku (string, required, Unique Index)
- Stock (int, non-negative)
- StockChanges (one-to-many relationship with cascade delete)

### StockChanges Entity
- StockChangesId (int, Primary Key, Identity)
- ProductId (int, Foreign Key referencing Product)
- Date (DateTimeOffset, UTC timestamp)
- Delta (int, change in quantity, positive for additions, negative for deductions)
- Source (string, contextual reason: Initial Stock, Manual Edit, Customer Purchase, etc.)

---

## API Specification

### Health Check
- Method: GET
- Path: /health
- Description: Verifies service operational readiness.
- Response: 200 OK

### List Products
- Method: GET
- Path: /products
- Description: Retrieves all products with their associated stock change audit history.
- Response: 200 OK with array of ProductResponse objects.

### Get Product by ID
- Method: GET
- Path: /products/{id}
- Description: Retrieves a single product by its database identifier.
- Responses:
  - 200 OK: ProductResponse
  - 404 Not Found: ErrorResponse

### Create Product
- Method: POST
- Path: /products
- Request Body:
```json
{
  "name": "Mechanical Keyboard",
  "sku": "KB-MEC-01",
  "stock": 25
}
```
- Responses:
  - 201 Created: ProductResponse with location header
  - 400 Bad Request: Empty name, empty SKU, or negative initial stock
  - 409 Conflict: SKU already registered to another item

### Update Product
- Method: PUT
- Path: /products/{id}
- Request Body:
```json
{
  "name": "Mechanical Keyboard Pro",
  "sku": "KB-MEC-PRO",
  "stock": 30
}
```
- Responses:
  - 200 OK: ProductResponse
  - 400 Bad Request: Invalid input fields
  - 404 Not Found: Product identifier does not exist
  - 409 Conflict: SKU conflict with existing product

### Adjust Stock
- Method: POST
- Path: /products/{id}/adjust-stock
- Request Body:
```json
{
  "delta": -5,
  "source": "Order Fulfillment #1042"
}
```
- Description: Atomically increases or decreases product stock and records an audit log entry. Prevents stock from dropping below zero.
- Responses:
  - 200 OK: Updated ProductResponse with the new audit log entry
  - 400 Bad Request: Resulting stock would be negative
  - 404 Not Found: Product identifier does not exist

### Delete Product
- Method: DELETE
- Path: /products/{id}
- Description: Deletes the product and all associated stock change records via database cascade.
- Responses:
  - 204 No Content: Successful deletion
  - 404 Not Found: Product identifier does not exist

---

## Error Handling Contract

All error responses adhere to a uniform, machine-readable JSON structure:

```json
{
  "error": "A product with this SKU already exists.",
  "details": null
}
```

HTTP Status Codes used:
- 400 Bad Request: Client validation failures (empty fields, negative stock).
- 404 Not Found: Requested resource identifier does not exist.
- 409 Conflict: Business rule or uniqueness constraint violated (e.g. duplicate SKU).
- 500 Internal Server Error: Unhandled infrastructure exception.

---

## Getting Started

### Prerequisites
- .NET 10.0 SDK or later
- PostgreSQL database instance (local or hosted e.g. Supabase)

### Configuration
Configure your PostgreSQL connection string in `appsettings.json` or via .NET user-secrets:

```json
{
  "ConnectionStrings": {
    "SupabaseDb": "Host=your-db-host;Database=postgres;Username=postgres;Password=your-password;Port=5432"
  }
}
```

Alternatively using the CLI:
```bash
dotnet user-secrets set "ConnectionStrings:SupabaseDb" "Host=your-db-host;Database=postgres;Username=postgres;Password=your-password;Port=5432"
```

### Running the API
```bash
dotnet run --project InventoryApi.csproj
```

The service will start by default on `http://localhost:5239` and `https://localhost:7228`.

---

## Database Migrations

Apply database migrations using the EF Core command-line tool:

```bash
dotnet ef database update
```

To create a new migration after model modifications:
```bash
dotnet ef migrations add <MigrationName>
```

---

## Running Tests

The test suite contains end-to-end integration tests using in-memory databases and ASP.NET Core `WebApplicationFactory`:

```bash
dotnet test
```

Test coverage includes:
- Product creation, validation, and SKU uniqueness conflict enforcement.
- Updating product details and stock delta calculations.
- Stock adjustments with negative balance rejection.
- Cascading deletion of stock history records.
- Health check availability.

---

## Engineering Decisions

1. Minimal APIs over Controller Architecture:
   - Minimal APIs provide superior request throughput, lower memory allocations, and minimal boilerplate for cloud-native microservices.

2. DTO Isolation:
   - Request and response contracts are decoupled from database entities. This prevents over-posting attacks and allows database schema changes without breaking API clients.

3. Stock Audit Trail:
   - Inventory adjustments are tracked via explicit `StockChanges` records, allowing complete visibility into who or what triggered changes in stock levels.

4. Client vs Server Queue Decoupling:
   - The initial prototype contained a `PendingOperations` table in PostgreSQL. This was removed because offline operations are a mobile client queue concern; persisting transient client queues in the centralized transactional database introduces deadlocks and architectural coupling.
