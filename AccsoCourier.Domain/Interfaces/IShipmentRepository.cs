using System;
using System.Collections.Generic;
using System.Text;
using AccsoCourier.Domain.Models;
using AccsoCourier.Domain.Enums;

namespace AccsoCourier.Domain.Interfaces
{
    public interface IShipmentRepository
    {
        Task<bool> EventExistsAsync(string partner, string eventId, CancellationToken ct = default);
        Task<ShipmentState?> GetCurrentStateAsync(string shipmentId, CancellationToken ct = default);
        Task<IReadOnlyList<ShipmentEvent>> GetHistoryAsync(string shipmentId, CancellationToken ct = default);
        Task<ProcessingResult> SaveEventAndApplyStateAsync(
            ShipmentEvent shipmentEvent,
            ShipmentState? newState,
            ProcessingOutcome outcome,
            string? reason,
            CancellationToken ct = default);
    }
}
