using AccsoCourier.Infrastructure.Data;
using AccsoCourier.Domain.Interfaces;
using AccsoCourier.Domain.Models;
using Microsoft.Data.SqlClient;
using System.Data;
using AccsoCourier.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace AccsoCourier.Infrastructure.Repositories
{
    public class SqlShipmentRepository(SqlConnectionFactory connectionFactory, ILogger<SqlShipmentRepository> logger) : IShipmentRepository
    {
        public async Task<bool> EventExistsAsync(string partner, string eventId, CancellationToken ct = default)
        {
            await using var cn = connectionFactory.Create();
            await cn.OpenAsync(ct);
            const string sql = "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.ShipmentEvent WHERE Partner=@Partner AND EventId=@EventId) THEN 1 ELSE 0 END";
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.Add(new SqlParameter("@Partner", SqlDbType.NVarChar, 100) { Value = partner });
            cmd.Parameters.Add(new SqlParameter("@EventId", SqlDbType.NVarChar, 200) { Value = eventId });
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) == 1;
        }

        public async Task<ShipmentState?> GetCurrentStateAsync(string shipmentId, CancellationToken ct = default)
        {
            await using var cn = connectionFactory.Create();
            await cn.OpenAsync(ct);
            const string sql = @"SELECT ShipmentId, CurrentStatus, CurrentEventId, CurrentOccurredAt, Location, RowVersion
                             FROM dbo.ShipmentState WHERE ShipmentId=@ShipmentId";
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.Add(new SqlParameter("@ShipmentId", SqlDbType.NVarChar, 100) { Value = shipmentId });
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) {
                logger.LogDebug("No current state found for shipment {ShipmentId}.",shipmentId);
                return null;
            } 
            return new ShipmentState(
                r.GetString(0), ParseStatus(r.GetString(1)), r.GetString(2),
                new DateTimeOffset(r.GetDateTime(3), TimeSpan.Zero), r.IsDBNull(4) ? null : r.GetString(4), (byte[])r[5]);
        }

        public async Task<IReadOnlyList<ShipmentEvent>> GetHistoryAsync(string shipmentId, CancellationToken ct = default)
        {
            var items = new List<ShipmentEvent>();
            await using var cn = connectionFactory.Create();
            await cn.OpenAsync(ct);
            const string sql = @"SELECT EventId, Partner, ShipmentId, Status, OccurredAt, ReceivedAt, Location
                             FROM dbo.ShipmentEvent WHERE ShipmentId=@ShipmentId ORDER BY OccurredAt, ShipmentEventId";
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.Add(new SqlParameter("@ShipmentId", SqlDbType.NVarChar, 100) { Value = shipmentId });
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) { 
                items.Add(new ShipmentEvent(r.GetString(0), r.GetString(1), r.GetString(2), ParseStatus(r.GetString(3)),
                    new DateTimeOffset(r.GetDateTime(4), TimeSpan.Zero), new DateTimeOffset(r.GetDateTime(5), TimeSpan.Zero),
                    r.IsDBNull(6) ? null : r.GetString(6)));
            }
            return items;
        }

        public async Task<ProcessingResult> SaveEventAndApplyStateAsync(ShipmentEvent e, ShipmentState? newState,
            ProcessingOutcome outcome, string? reason, CancellationToken ct = default)
        {
            // Create and open a new SQL Server connection for this repository operation.
            // The connection is scoped to this method and disposed automatically when
            // processing completes.
            await using var cn = connectionFactory.Create();
            await cn.OpenAsync(ct);

            // Start a database transaction using ReadCommitted isolation.
            // The shipment event and the current-state projection must remain consistent,
            // so both operations are committed or rolled back together.
            await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            try
            {
                // Ensure that the parent Shipment record exists before inserting
                // the incoming ShipmentEvent. This protects the foreign-key relationship
                // between Shipment and ShipmentEvent.
                await EnsureShipmentAsync(cn, tx, e.ShipmentId, ct);

                // Persist the courier event regardless of whether the business outcome
                // is APPLIED, STALE or CONFLICT.
                //
                // Retaining the event history is important for auditability because
                // support and incident teams must be able to see exactly what the
                // courier sent, even when the event does not change the current state.
                await InsertEventAsync(cn, tx, e, outcome, reason, ct);

                // Only update the trusted ShipmentState projection when the processor
                // supplied a new state.
                //
                // STALE and CONFLICT events deliberately pass null here so that they
                // remain in event history without overwriting the trusted current state.
                if (newState is not null) await UpsertStateAsync(cn, tx, newState, ct);

                // Commit the transaction only after both the event-history write and
                // any required state update have completed successfully.
                //
                // This prevents a partially persisted state where the event exists
                // but the current projection does not reflect it, or vice versa.
                await tx.CommitAsync(ct);

                // Return the same processing outcome determined by the domain processor.
                return new ProcessingResult(outcome, e.ShipmentId, e.EventId, reason);
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                // SQL Server error 2601 or 2627 indicates a duplicate-key violation.
                //
                // This can happen if two workers process the same courier event at nearly
                // the same time. Even if both pass the application-level duplicate check,
                // the UNIQUE(Partner, EventId) database constraint remains the final
                // concurrency-safe idempotency guard.
                await tx.RollbackAsync(ct);

                logger.LogInformation("Duplicate shipment event prevented by SQL Server. Partner={Partner}, EventId={EventId}, ShipmentId={ShipmentId}", 
                    e.Partner, e.EventId, e.ShipmentId);

                // Treat the duplicate-key condition as an expected business outcome
                // rather than an infrastructure failure.
                return new ProcessingResult(ProcessingOutcome.Duplicate, e.ShipmentId, e.EventId, "Database unique constraint detected a duplicate event.");
            }
            catch(Exception ex)
            {
                // Any unexpected database or processing exception must roll back the
                // transaction so that event history and current state are not left
                // partially updated.
                await tx.RollbackAsync(ct);

                logger.LogError(ex,"SQL failure while saving shipment event. ShipmentId={ShipmentId}, EventId={EventId}, Outcome={Outcome}",
                    e.ShipmentId,e.EventId,outcome);

                // Re-throw the exception so that the application-level/global exception
                // handler can log and translate it consistently.
                throw;
            }
        }

        // Ensures the parent shipment exists before inserting related events.
        // This is required because ShipmentEvent references Shipment through a foreign key.
        private static async Task EnsureShipmentAsync(SqlConnection cn, SqlTransaction tx, string shipmentId, CancellationToken ct)
        {
            const string sql = @"IF NOT EXISTS (SELECT 1 FROM dbo.Shipment WHERE ShipmentId=@ShipmentId)
                             INSERT INTO dbo.Shipment(ShipmentId) VALUES(@ShipmentId);";
            await using var cmd = new SqlCommand(sql, cn, tx);
            cmd.Parameters.Add(new SqlParameter("@ShipmentId", SqlDbType.NVarChar, 100) { Value = shipmentId });
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Persists the immutable courier-event history together with its processing
        // outcome and optional conflict/stale reason.
        private static async Task InsertEventAsync(SqlConnection cn, SqlTransaction tx, ShipmentEvent e, ProcessingOutcome outcome, string? reason, CancellationToken ct)
        {
            const string sql = @"INSERT INTO dbo.ShipmentEvent(EventId,Partner,ShipmentId,Status,OccurredAt,ReceivedAt,Location,ProcessingStatus,ConflictReason)
                             VALUES(@EventId,@Partner,@ShipmentId,@Status,@OccurredAt,@ReceivedAt,@Location,@ProcessingStatus,@ConflictReason);";
            await using var cmd = new SqlCommand(sql, cn, tx);
            cmd.Parameters.Add(new SqlParameter("@EventId", SqlDbType.NVarChar, 200) { Value = e.EventId });
            cmd.Parameters.Add(new SqlParameter("@Partner", SqlDbType.NVarChar, 100) { Value = e.Partner });
            cmd.Parameters.Add(new SqlParameter("@ShipmentId", SqlDbType.NVarChar, 100) { Value = e.ShipmentId });
            cmd.Parameters.Add(new SqlParameter("@Status", SqlDbType.NVarChar, 50) { Value = ToDbStatus(e.Status) });
            cmd.Parameters.Add(new SqlParameter("@OccurredAt", SqlDbType.DateTime2) { Value = e.OccurredAt.UtcDateTime });
            cmd.Parameters.Add(new SqlParameter("@ReceivedAt", SqlDbType.DateTime2) { Value = e.ReceivedAt.UtcDateTime });
            cmd.Parameters.Add(new SqlParameter("@Location", SqlDbType.NVarChar, 200) { Value = (object?)e.Location ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@ProcessingStatus", SqlDbType.NVarChar, 30) { Value = outcome.ToString().ToUpperInvariant() });
            cmd.Parameters.Add(new SqlParameter("@ConflictReason", SqlDbType.NVarChar, 1000) { Value = (object?)reason ?? DBNull.Value });
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Updates the current-state projection when the shipment already exists,
        // otherwise inserts the initial projection for a new shipment.
        private static async Task UpsertStateAsync(SqlConnection cn, SqlTransaction tx, ShipmentState s, CancellationToken ct)
        {
            const string sql = @"UPDATE dbo.ShipmentState SET CurrentStatus=@Status, CurrentEventId=@EventId,
                                 CurrentOccurredAt=@OccurredAt, Location=@Location, UpdatedAt=SYSUTCDATETIME()
                             WHERE ShipmentId=@ShipmentId;
                             IF @@ROWCOUNT=0
                             INSERT INTO dbo.ShipmentState(ShipmentId,CurrentStatus,CurrentEventId,CurrentOccurredAt,Location)
                             VALUES(@ShipmentId,@Status,@EventId,@OccurredAt,@Location);";
            await using var cmd = new SqlCommand(sql, cn, tx);
            cmd.Parameters.Add(new SqlParameter("@ShipmentId", SqlDbType.NVarChar, 100) { Value = s.ShipmentId });
            cmd.Parameters.Add(new SqlParameter("@Status", SqlDbType.NVarChar, 50) { Value = ToDbStatus(s.Status) });
            cmd.Parameters.Add(new SqlParameter("@EventId", SqlDbType.NVarChar, 200) { Value = s.EventId });
            cmd.Parameters.Add(new SqlParameter("@OccurredAt", SqlDbType.DateTime2) { Value = s.OccurredAt.UtcDateTime });
            cmd.Parameters.Add(new SqlParameter("@Location", SqlDbType.NVarChar, 200) { Value = (object?)s.Location ?? DBNull.Value });
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Converts the database string representation of a DHL status into the
        // strongly typed domain ShipmentStatus enum.
        private static ShipmentStatus ParseStatus(string value) => value switch
        {
            "LABEL_CREATED" => ShipmentStatus.LabelCreated,
            "HANDED_TO_CARRIER" => ShipmentStatus.HandedToCarrier,
            "IN_TRANSIT" => ShipmentStatus.InTransit,
            "OUT_FOR_DELIVERY" => ShipmentStatus.OutForDelivery,
            "DELIVERED" => ShipmentStatus.Delivered,
            "DELIVERY_EXCEPTION" => ShipmentStatus.DeliveryException,
            "RETURNED" => ShipmentStatus.Returned,
            _ => throw new InvalidOperationException($"Unknown shipment status '{value}'.")
        };

        // Converts the domain ShipmentStatus enum into the canonical string format
        // stored in SQL Server.
        private static string ToDbStatus(ShipmentStatus value) => value switch
        {
            ShipmentStatus.LabelCreated => "LABEL_CREATED",
            ShipmentStatus.HandedToCarrier => "HANDED_TO_CARRIER",
            ShipmentStatus.InTransit => "IN_TRANSIT",
            ShipmentStatus.OutForDelivery => "OUT_FOR_DELIVERY",
            ShipmentStatus.Delivered => "DELIVERED",
            ShipmentStatus.DeliveryException => "DELIVERY_EXCEPTION",
            ShipmentStatus.Returned => "RETURNED",
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
    }
}
