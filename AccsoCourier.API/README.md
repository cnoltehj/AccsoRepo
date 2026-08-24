# AccsoCourier Shipment Integrity API

## Overview

AccsoCourier is a thin executable slice developed for the
shipment-integrity assessment. It demonstrates how courier webhook
events can be received, persisted, queried, and processed safely when
events are duplicated, delayed, out of order, or conflicting.

The solution was developed using **Visual Studio 2026**, **.NET 10 /
ASP.NET Core**, **C#**, **ADO.NET (`Microsoft.Data.SqlClient`)**,
**Microsoft SQL Server**, **Swagger/OpenAPI**, **xUnit**, **Docker**,
and **NLog / `ILogger<T>` integration where implemented**.

The solution deliberately focuses on the integrity problem rather than
building a complete e-commerce or courier platform.

------------------------------------------------------------------------

## 1. Prerequisites

For local execution, install:

-   Visual Studio 2026 with the ASP.NET and web development workload, or
    a compatible .NET IDE
-   .NET 10 SDK
-   Microsoft SQL Server (Developer/Express is sufficient for local
    development)
-   SQL Server Management Studio (SSMS), recommended
-   Docker Desktop, only if running the API as a container

------------------------------------------------------------------------

## 2. Database Setup

The repository contains database assets in the SQL/database folder:

-   `init.sql` --- creates the local **AccsoCourier** database, tables,
    constraints, and indexes.
-   `Accso.bak` --- backup of the prepared AccsoCourier database.

Only **one** of the following database setup options is required.

### Option A --- Create the database using `init.sql`

1.  Open SQL Server Management Studio.
2.  Connect to your local SQL Server instance.
3.  Open `init.sql`.
4.  Execute the script.
5.  Confirm that the `AccsoCourier` database has been created.
6.  Confirm that the required shipment tables, constraints, and indexes
    exist.

This is the preferred setup because the database definition is visible
and repeatable.

### Option B --- Restore `Accso.bak`

1.  Open SQL Server Management Studio.
2.  Right-click **Databases** and select **Restore Database**.
3.  Select **Device** and browse to `Accso.bak`.
4.  Restore the backup as `AccsoCourier`.
5.  Confirm that the database is online and accessible.

------------------------------------------------------------------------

## 3. Configure the SQL Server Connection

Development database settings are read from:

`appsettings.Development.json`

Fill in the local SQL Server values before starting the API.

Example using SQL authentication:

``` json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=<SERVER\\INSTANCE>;Initial Catalog=AccsoCourier;User ID=<USER>;Password=<PASSWORD>;Encrypt=True;TrustServerCertificate=True"
  }
}
```

Replace:

-   `<SERVER\\INSTANCE>` with the local SQL Server instance.
-   `<USER>` with the development SQL login.
-   `<PASSWORD>` with the development SQL password.

`TrustServerCertificate=True` is intended only for local development
where SQL Server commonly uses a locally generated/untrusted
certificate.

### Security note

**Never commit real database usernames, passwords, connection strings,
certificates, API keys, or other secrets to version control.**

For local development, prefer .NET User Secrets or environment
variables. Production secrets must be supplied by the deployment
platform or an approved secrets-management service.

If credentials have previously been committed, removing them from the
current file is not sufficient: rotate the credentials and remove them
from repository history as appropriate.

------------------------------------------------------------------------

## 4. Running the API with HTTPS

Select the **https** launch profile in Visual Studio and run the
project.

The development profile is configured to launch Swagger. The exact port
is defined in `Properties/launchSettings.json`.

Typical URL:

`https://localhost:7011/swagger`

Health endpoint:

`https://localhost:7011/health`

If the browser does not launch automatically, navigate directly to
`/swagger`.

------------------------------------------------------------------------

## 5. Running with Docker

The API includes a `Dockerfile` and can be built and run as a container.

Docker Compose is **not currently required** because Microsoft SQL
Server is running separately on the developer's local machine rather
than as a Compose-managed service.

Build the image from the directory containing the Dockerfile:

``` bash
docker build -t Accso-courier-api .
```

Run the API container:

``` bash
docker run --rm -p 8080:8080 --name Accso-courier-api Accso-courier-api
```

### Important: SQL Server connectivity from Docker

Inside a container, `localhost` refers to the container itself, not the
Windows host.

When the API runs in Docker while SQL Server runs on the host machine,
configure the container connection string to use the host address
supported by Docker Desktop, for example:

``` text
Server=host.docker.internal,<SQL_PORT>;Database=AccsoCourier;User Id=<USER>;Password=<PASSWORD>;Encrypt=True;TrustServerCertificate=True;
```

For a named SQL Server instance, confirm the SQL Server TCP port and use
that explicit port for predictable container connectivity. Ensure TCP/IP
is enabled for SQL Server and that the local firewall permits the
required connection.

Do not bake development credentials into the Docker image. Supply the
connection string through environment variables or another secure
configuration mechanism.

------------------------------------------------------------------------

## 6. Why Docker Compose Is Not Used Yet

`docker-compose.yaml` is intentionally not part of the current execution
path because the database remains a locally managed SQL Server
dependency.

Docker Compose becomes useful when the executable environment contains
multiple containerized services, for example:

``` text
AccsoCourier API
       |
       v
SQL Server Container
```

or later:

``` text
API -> Message Broker -> Event Processor -> Database
```

If SQL Server is containerized for development later, Docker Compose can
provide a one-command reproducible environment.
------------------------------------------------------------------------

## 7. Swagger / API Endpoints

Swagger provides the interactive API documentation and can be used to
exercise the executable slice.

Key endpoints include:

  ------------------------------------------------------------------------------
  Method              Endpoint                               Purpose
  ------------------- -------------------------------------- -------------------
  POST                `/api/webhooks/dhl`                    Receive and process
                                                             a DHL shipment
                                                             event

  GET                 `/api/shipments/{shipmentId}`          Return the trusted
                                                             current shipment
                                                             state

  GET                 `/api/shipments/{shipmentId}/events`   Return the shipment
                                                             event history

  GET                 `/health`                              Basic API health
                                                             check
  ------------------------------------------------------------------------------

Example current-state request:

`GET /api/shipments/ship-456`

The API returns meaningful shipment status descriptions rather than
numeric enum values.

------------------------------------------------------------------------

### Example DHL Webhook Payload

The following payload is based on the courier event supplied in the
assessment and can be submitted through Swagger, Postman, `curl`, or
another HTTP client.

``` json
{
  "eventId": "evt-123",
  "partner": "dhl",
  "shipmentId": "ship-456",
  "status": "IN_TRANSIT",
  "occurredAt": "2026-03-10T12:00:00Z",
  "receivedAt": "2026-03-10T12:00:05Z",
  "location": "Amsterdam"
}
```

Submit the payload to:

``` http
POST /api/webhooks/dhl
Content-Type: application/json
```

#### Supported DHL Statuses

``` text
LABEL_CREATED
HANDED_TO_CARRIER
IN_TRANSIT
OUT_FOR_DELIVERY
DELIVERED
DELIVERY_EXCEPTION
RETURNED
```

#### Processing Outcomes

Depending on the current shipment state and event history, the processor
classifies an event as:

  -----------------------------------------------------------------------
  Outcome                             Meaning
  ----------------------------------- -----------------------------------
  `APPLIED`                           The event is valid and the trusted
                                      current shipment state is updated.

  `DUPLICATE`                         The event has already been
                                      processed and must not change the
                                      state again.

  `STALE`                             The event occurred before the
                                      trusted current state. It is
                                      retained in history but does not
                                      regress the state.

  `CONFLICT`                          The event conflicts with the
                                      current state or transition rules.
                                      It is retained for investigation
                                      without silently overwriting the
                                      trusted state.
  -----------------------------------------------------------------------

#### Integrity Test Payload --- Out-of-Order Event

After `ship-456` has progressed to a later state such as
`OUT_FOR_DELIVERY`, the following older event can be used to demonstrate
out-of-order handling:

``` json
{
  "eventId": "evt-124",
  "partner": "dhl",
  "shipmentId": "ship-456",
  "status": "IN_TRANSIT",
  "occurredAt": "2026-03-10T11:30:00Z",
  "receivedAt": "2026-03-10T13:00:00Z",
  "location": "Amsterdam"
}
```

Expected processing outcome:

``` text
STALE
```

The event remains queryable in shipment history because it represents
what the courier sent, but it must not regress the trusted current
shipment state.

#### Duplicate Test

Submit the same event again using the same `partner` and `eventId`:

``` text
Partner = dhl
EventId = evt-123
```

Expected processing outcome:

``` text
DUPLICATE
```

The SQL Server unique constraint on `(Partner, EventId)` provides the
final concurrency-safe idempotency guard.

#### Conflict Test

A newer event that violates the configured shipment transition rules
should produce:

``` text
CONFLICT
```

The event is retained for audit and incident investigation, while the
trusted current state remains unchanged. The transition matrix used by
the thin slice is an explicit implementation assumption and should be
validated against the authoritative DHL/client business rules before
production rollout.


------------------------------------------------------------------------

## 8. Shipment Integrity Behaviour

The executable slice addresses the four main processing outcomes:

-   **APPLIED** --- the event is valid and updates the current shipment
    state.
-   **DUPLICATE** --- the event has already been received and must not
    be applied twice.
-   **STALE** --- the event occurred before the trusted current state;
    it is retained in history but must not regress the state.
-   **CONFLICT** --- the event conflicts with the current state or
    transition rules; it is retained for audit/investigation but does
    not silently overwrite the trusted state.

The database provides a concurrency-safe uniqueness guard using the
courier partner and event ID. Event history and current-state changes
should be performed transactionally where they must remain consistent.

------------------------------------------------------------------------

## 9. Testing

The test strategy focuses on the shipment-integrity risks rather than
broad UI testing.

The solution should cover:

-   normal state progression;
-   duplicate event processing;
-   late/out-of-order events;
-   conflicting transitions;
-   same-timestamp conflicts;
-   invalid webhook requests;
-   ADO.NET + SQL Server persistence;
-   database uniqueness constraints;
-   transaction rollback/failure behaviour;
-   concurrent processing of the same event/shipment;
-   health and post-deployment smoke tests.

Run automated tests with:

``` bash
dotnet test
```

A critical acceptance scenario is:

> Given a shipment currently at `OUT_FOR_DELIVERY`, when an older
> `IN_TRANSIT` event arrives, the event remains available in history but
> the trusted current state remains `OUT_FOR_DELIVERY`.

Other Testing tools such as Postman, Swagger and JMeter can be used to exercise the API endpoints for functional and load testing. 
The test suite should be extended to cover any new courier partners, event types, or business rules introduced in the future.

------------------------------------------------------------------------

## 10. Logging and Error Handling

The application uses the standard `ILogger<T>` abstraction so logging
can be routed through NLog without coupling domain/application code
directly to NLog APIs.

High-value logging points include:

-   webhook received;
-   validation failure;
-   duplicate detected;
-   stale/out-of-order event detected;
-   conflict detected;
-   state successfully applied;
-   SQL/database failure;
-   transaction or concurrency failure.

Structured logging should include identifiers such as `ShipmentId`,
`EventId`, `Partner`, and processing outcome where appropriate.

Exceptions should not be caught in every method. `try/catch` should be
used where the application can add useful context, translate a known
infrastructure failure, handle duplicate-key races, or safely roll back
a transaction. Unexpected failures should be handled consistently at the
API boundary/global exception handler.

------------------------------------------------------------------------

## 11. Known Issues / Technical Debt

The current implementation is intentionally a thin assessment slice. The
following work is known and should be addressed before a broader
production rollout:

1.  Expand error handling around infrastructure and integration
    boundaries.
2.  Add structured `ILogger<T>` logging to all high-value operational
    paths.
3.  Add developer comments/XML documentation where business intent is
    not obvious.
4.  Complete/extend SQL Server integration and concurrency tests.
5.  Validate the shipment transition matrix with the authoritative
    courier/business rules.
6.  Add production-grade authentication/signature verification for
    courier webhooks if not already enabled.
7.  Add production observability thresholds, dashboards, and alert
    ownership.
8.  Add replay/reconciliation tooling if operational experience shows it
    is required.
9.  Introduce a courier adapter abstraction only when a second courier
    partner is onboarded; avoid premature abstraction.

Technical debt should be tracked with an owner, rationale, priority, and
review trigger. Security, integrity constraints, testing, observability,
and rollback readiness are not considered optional technical debt for
production.

------------------------------------------------------------------------

## 12. CI/CD Pipeline

The recommended CI/CD platform is **Azure DevOps** because it aligns
with the target environment, although GitHub Actions or GitLab CI can
implement the same controls.

Recommended pipeline:

``` text
Pull Request
    |
    v
Restore .NET 10
    |
    v
Build
    |
    v
Unit Tests
    |
    v
Integration Tests
    |
    v
Security / Quality Checks
    |
    v
Publish Versioned Artifact / Container Image
    |
    v
Deploy to Test / Staging
    |
    v
Database Compatibility Check + Smoke Tests
    |
    v
Approval Gate
    |
    v
Production
    |
    v
Health Check + Monitoring
```

The same immutable artifact/image should be promoted between
environments. Production deployment should require successful quality
gates, operational readiness, and rollback capability.

Database changes should be version-controlled and backward-compatible
where practical. An expand/contract migration strategy is preferred for
changes that could otherwise prevent application rollback.

------------------------------------------------------------------------

## 13. Secrets Management

Secrets must be external to source control and container images.

Recommended options depend on the production platform:

-   **Azure Key Vault** for an Azure deployment.
-   **Kubernetes Secrets**, preferably integrated with an external
    secrets provider, when Kubernetes is used.
-   **AWS Secrets Manager** only when deploying to AWS.
-   .NET User Secrets for local developer-only configuration.

The production platform should inject secrets through
environment/configuration mechanisms and use least-privilege identities
wherever possible.

------------------------------------------------------------------------

## 14. Production Deployment Strategy

The current Docker setup demonstrates containerization of the API. A
production deployment should use the client's approved cloud and
container platform rather than treating Docker Compose as production
orchestration.

A pragmatic Azure-oriented target is:

``` text
Courier
   |
   v
HTTPS / Ingress
   |
   v
AccsoCourier API Container
   |
   v
Azure Service Bus
   |
   v
.NET Event Processor
   |
   v
Azure SQL Database

Telemetry -> Application Insights / Azure Monitor
Secrets   -> Azure Key Vault
CI/CD     -> Azure DevOps
```

Production concerns include:

1.  **Containerization with Docker** --- build a versioned, immutable
    API image.
2.  **Managed runtime/orchestration** --- deploy using the client's
    approved Azure container platform.
3.  **Managed SQL** --- use Azure SQL Database or the client's approved
    SQL Server platform.
4.  **Durable messaging** --- introduce Azure Service Bus between
    ingestion and asynchronous processing as proposed in the production
    architecture.
5.  **Observability** --- centralize logs, metrics, traces, dashboards,
    and alerts.
6.  **Secrets** --- store credentials in Azure Key Vault or the approved
    platform equivalent.
7.  **Release control** --- deploy through staged environments with
    approval gates, smoke tests, monitoring, and rollback readiness.
8.  **Scaling** --- scale API and processor independently based on
    measured request rate, queue depth, processing latency, and database
    capacity.

------------------------------------------------------------------------

## 15. Rollback and Recovery

Application rollback and data recovery must be treated separately.

Preferred recovery sequence:

``` text
Stop harmful processing
        |
        v
Preserve event history
        |
        v
Deploy previous known-good application version
        |
        v
Validate current state
        |
        v
Replay / rebuild only after the defect is understood
```

Historical courier events should not be deleted merely because an
application release is rolled back. They are evidence required to
explain what happened and to reconstruct the trusted state.

------------------------------------------------------------------------

## 16. Scope Deliberately Deferred

The assessment implementation intentionally does not attempt to deliver:

-   a customer-facing UI;
-   multiple courier integrations;
-   a full event-sourcing framework;
-   a production Kubernetes platform;
-   advanced reconciliation/admin tooling;
-   machine-learning anomaly detection;
-   multi-region active-active deployment.

These capabilities should be introduced only when justified by business
requirements, operational evidence, scale, or availability targets.

------------------------------------------------------------------------

## 17. Repository Notes

Before submitting the repository:

-   confirm `dotnet build` succeeds;
-   confirm `dotnet test` succeeds;
-   test the documented database setup from a clean local environment;
-   verify Swagger starts using both the HTTPS profile and Docker path;
-   remove all real credentials and secrets;
-   include `.env`, local settings, database credentials, certificates,
    `bin/`, and `obj/` in `.gitignore` as appropriate;
-   confirm the database backup contains no sensitive or production
    data;
-   ensure the README commands match the final repository paths and
    project names.

The objective is that a reviewer can understand the design, create or
restore the database, configure the application, run it, execute the
tests, and exercise the API without undocumented setup steps.
