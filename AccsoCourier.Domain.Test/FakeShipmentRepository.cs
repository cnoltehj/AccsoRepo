using AccsoCourier.Domain.Enums;
using AccsoCourier.Domain.Interfaces;
using AccsoCourier.Domain.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace AccsoCourier.Domain.Test
{
    internal class FakeShipmentRepository : IShipmentRepository
    {
        /// <summary>
        /// Stores courier events using a composite key consisting of
        /// Partner and EventId.
        /// </summary>
        private readonly Dictionary<string, ShipmentEvent> _events = new();

        /// <summary>
        /// Stores the current trusted state for each shipment.
        /// </summary>
        private readonly Dictionary<string, ShipmentState> _states = new();

        /// <summary>
        /// Determines whether a logical courier event has already been recorded.
        /// </summary>
        /// <param name="partner">
        /// The courier partner that supplied the event, for example "dhl".
        /// </param>
        public Task<bool> EventExistsAsync(string partner, string eventId, CancellationToken ct = default) =>
            Task.FromResult(_events.ContainsKey($"{partner}:{eventId}"));

        /// <summary>
        /// Retrieves the current trusted state for a shipment.
        /// </summary>
        /// <param name="shipmentId">
        /// The shipment identifier whose current state should be retrieved.
        /// </param>
        public Task<ShipmentState?> GetCurrentStateAsync(string shipmentId, CancellationToken ct = default) =>
            Task.FromResult(_states.GetValueOrDefault(shipmentId));

        /// Retrieves the complete event history for a shipment.
        /// </summary>
        /// <param name="shipmentId">
        /// The shipment identifier whose event history should be returned.
        /// </param>
        public Task<IReadOnlyList<ShipmentEvent>> GetHistoryAsync(string shipmentId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ShipmentEvent>>(_events.Values.Where(x => x.ShipmentId == shipmentId).OrderBy(x => x.OccurredAt).ToList());

        /// <summary>
        /// Saves a shipment event and optionally applies a new trusted shipment state.
        /// </summary>
        /// <param name="shipmentEvent">
        /// The courier event being persisted.
        /// </param>
        public Task<ProcessingResult> SaveEventAndApplyStateAsync(ShipmentEvent e, ShipmentState? newState, ProcessingOutcome outcome, string? reason, CancellationToken ct = default)
        {
            var key = $"{e.Partner}:{e.EventId}";
            if (_events.ContainsKey(key))
                return Task.FromResult(new ProcessingResult(ProcessingOutcome.Duplicate, e.ShipmentId, e.EventId));
            _events[key] = e;
            if (newState is not null) _states[e.ShipmentId] = newState;
            return Task.FromResult(new ProcessingResult(outcome, e.ShipmentId, e.EventId, reason));
        }
    }
}
