using AccsoCourier.Domain.Enums;
using AccsoCourier.Domain.Interfaces;
using AccsoCourier.Domain.Models;
using AccsoCourier.Domain.Rules;
using System;
using System.Collections.Generic;
using System.Text;

namespace AccsoCourier.Domain.Services
{
    public class ShipmentEventProcessor(IShipmentRepository repository)
    {
        public async Task<ProcessingResult> ProcessAsync(ShipmentEvent incoming)
        {
            if (await repository.EventExistsAsync(incoming.Partner, incoming.EventId))
                return new(ProcessingOutcome.Duplicate, incoming.ShipmentId, incoming.EventId, "Event has already been processed.");

            var current = await repository.GetCurrentStateAsync(incoming.ShipmentId);

            if (current is null)
            {
                var initial = new ShipmentState(incoming.ShipmentId, incoming.Status, incoming.EventId, incoming.OccurredAt, incoming.Location);
                return await repository.SaveEventAndApplyStateAsync(incoming, initial, ProcessingOutcome.Applied, null);
            }

            if (incoming.OccurredAt < current.OccurredAt)
                return await repository.SaveEventAndApplyStateAsync(incoming, null, ProcessingOutcome.Stale,
                    $"Event occurred at {incoming.OccurredAt:u}, before current state event at {current.OccurredAt:u}.");

            if (incoming.OccurredAt == current.OccurredAt && incoming.Status != current.Status)
                return await repository.SaveEventAndApplyStateAsync(incoming, null, ProcessingOutcome.Conflict,
                    "Different statuses have the same occurrence timestamp.");

            if (incoming.Status == current.Status)
            {
                var refreshed = new ShipmentState(incoming.ShipmentId, incoming.Status, incoming.EventId, incoming.OccurredAt, incoming.Location, current.RowVersion);
                return await repository.SaveEventAndApplyStateAsync(incoming, refreshed, ProcessingOutcome.Applied, "State unchanged; newer event recorded.");
            }

            if (!ShipmentStateRules.IsValidTransition(current.Status, incoming.Status))
                return await repository.SaveEventAndApplyStateAsync(incoming, null, ProcessingOutcome.Conflict,
                    $"Invalid transition: {current.Status} -> {incoming.Status}.");

            var next = new ShipmentState(incoming.ShipmentId, incoming.Status, incoming.EventId, incoming.OccurredAt, incoming.Location, current.RowVersion);
            return await repository.SaveEventAndApplyStateAsync(incoming, next, ProcessingOutcome.Applied, null);
        }
    }
}
