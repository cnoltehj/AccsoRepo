using AccsoCourier.Infrastructure.Data;
using AccsoCourier.Domain.Interfaces;
using AccsoCourier.Domain.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using AccsoCourier.Domain.Enums;

namespace AccsoCourier.Infrastructure.Repositories
{
    public class SqlShipmentRepository(SqlConnectionFactory connectionFactory) : IShipmentRepository
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
            if (!await r.ReadAsync(ct)) return null;
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
            while (await r.ReadAsync(ct))
                items.Add(new ShipmentEvent(r.GetString(0), r.GetString(1), r.GetString(2), ParseStatus(r.GetString(3)),
                    new DateTimeOffset(r.GetDateTime(4), TimeSpan.Zero), new DateTimeOffset(r.GetDateTime(5), TimeSpan.Zero),
                    r.IsDBNull(6) ? null : r.GetString(6)));
            return items;
        }

        public async Task<ProcessingResult> SaveEventAndApplyStateAsync(ShipmentEvent e, ShipmentState? newState,
            ProcessingOutcome outcome, string? reason, CancellationToken ct = default)
        {
            await using var cn = connectionFactory.Create();
            await cn.OpenAsync(ct);
            await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            try
            {
                await EnsureShipmentAsync(cn, tx, e.ShipmentId, ct);
                await InsertEventAsync(cn, tx, e, outcome, reason, ct);
                if (newState is not null) await UpsertStateAsync(cn, tx, newState, ct);
                await tx.CommitAsync(ct);
                return new ProcessingResult(outcome, e.ShipmentId, e.EventId, reason);
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                await tx.RollbackAsync(ct);
                return new ProcessingResult(ProcessingOutcome.Duplicate, e.ShipmentId, e.EventId, "Database unique constraint detected a duplicate event.");
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        private static async Task EnsureShipmentAsync(SqlConnection cn, SqlTransaction tx, string shipmentId, CancellationToken ct)
        {
            const string sql = @"IF NOT EXISTS (SELECT 1 FROM dbo.Shipment WHERE ShipmentId=@ShipmentId)
                             INSERT INTO dbo.Shipment(ShipmentId) VALUES(@ShipmentId);";
            await using var cmd = new SqlCommand(sql, cn, tx);
            cmd.Parameters.Add(new SqlParameter("@ShipmentId", SqlDbType.NVarChar, 100) { Value = shipmentId });
            await cmd.ExecuteNonQueryAsync(ct);
        }

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
