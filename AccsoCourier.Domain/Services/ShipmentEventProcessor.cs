using AccsoCourier.Domain.Enums;
using AccsoCourier.Domain.Interfaces;
using AccsoCourier.Domain.Models;
using AccsoCourier.Domain.Rules;
using Microsoft.Extensions.Logging;

namespace AccsoCourier.Domain.Services
{
    /// <summary>
    /// Applies shipment-integrity rules to incoming courier events.
    /// </summary>
    /// <remarks>
    /// The processor classifies events as Applied, Duplicate, Stale or Conflict.
    /// It deliberately contains no SQL-specific logic so that the domain rules
    /// remain independently testable and decoupled from the persistence layer.
    /// </remarks>
    public class ShipmentEventProcessor(IShipmentRepository repository, ILogger<ShipmentEventProcessor> logger)
    {
        /// <summary>
        /// Processes an incoming shipment event and determines whether it should
        /// update the trusted current shipment state.
        /// </summary>
        /// <param name="incoming">
        /// The normalized courier event to process.
        /// </param>
        /// <returns>
        /// A <see cref="ProcessingResult"/> describing the processing outcome.
        /// </returns>
        public async Task<ProcessingResult> ProcessAsync(ShipmentEvent incoming)
        {

            // Edge case: EventId is required because it is part of the
            // idempotency key used to detect duplicate courier events.
            if (string.IsNullOrWhiteSpace(incoming.EventId))
                return new(ProcessingOutcome.Conflict,incoming.ShipmentId, incoming.EventId, "EventId is required.");

            // Edge case: Partner is required because EventId uniqueness
            // is evaluated within the courier partner boundary.
            if (string.IsNullOrWhiteSpace(incoming.Partner))
                return new(ProcessingOutcome.Conflict,incoming.ShipmentId,incoming.EventId,"Partner is required.");

            // Edge case: ShipmentId is required because the event cannot
            // be associated with a shipment without it.
            if (string.IsNullOrWhiteSpace(incoming.ShipmentId))
                return new(ProcessingOutcome.Conflict,incoming.ShipmentId,incoming.EventId,"ShipmentId is required.");

            // Edge case: protects against an invalid ShipmentStatus enum
            // value being constructed internally.
            if (!Enum.IsDefined(incoming.Status))
                return new(ProcessingOutcome.Conflict,incoming.ShipmentId,incoming.EventId,$"Unsupported shipment status: {incoming.Status}.");

            // Check whether the incoming event has already been processed.
            // The combination of Partner and EventId is used to identify the same
            // logical courier event and prevent duplicate processing.
            if (await repository.EventExistsAsync(incoming.Partner, incoming.EventId))
            {
                logger.LogInformation("Duplicate shipment event detected. Partner: {Partner}, EventId: {EventId}, ShipmentId: {ShipmentId}",
                    incoming.Partner,incoming.EventId, incoming.ShipmentId);
                return new(ProcessingOutcome.Duplicate, incoming.ShipmentId, incoming.EventId, "Event has already been processed.");
            }

            // Retrieve the current trusted state of the shipment.
            // The incoming event will be evaluated against this state to determine
            // whether it should update, preserve, or conflict with the current state.
            var current = await repository.GetCurrentStateAsync(incoming.ShipmentId);

            // If no current state exists, this is the first accepted event for the shipment.
            // The incoming event therefore establishes the initial trusted shipment state.
            if (current is null)
            {
                logger.LogInformation("Establishing initial state {Status} for shipment {ShipmentId} from event {EventId}.",
                    incoming.Status,incoming.ShipmentId,incoming.EventId);
                var initial = new ShipmentState(incoming.ShipmentId, incoming.Status, incoming.EventId, incoming.OccurredAt, incoming.Location);
                return await repository.SaveEventAndApplyStateAsync(incoming, initial, ProcessingOutcome.Applied, null);
            }

            // Detect an out-of-order or late-arriving courier event.
            // If the incoming event occurred before the event that established the
            // current trusted state, it is classified as STALE.
            // The event is retained in history but is not allowed to regress the current state.
            if (incoming.OccurredAt < current.OccurredAt)
            {
                logger.LogWarning( "Stale shipment event detected. ShipmentId: {ShipmentId}, EventId: {EventId}, IncomingOccurredAt: {IncomingOccurredAt}, CurrentOccurredAt: {CurrentOccurredAt}",
                    incoming.ShipmentId, incoming.EventId, incoming.OccurredAt, current.OccurredAt);
                return await repository.SaveEventAndApplyStateAsync(incoming, null, ProcessingOutcome.Stale,
                    $"Event occurred at {incoming.OccurredAt:u}, before current state event at {current.OccurredAt:u}.");
            }

            // Detect conflicting events with the same business occurrence timestamp.
            // If two different statuses claim to have occurred at exactly the same time,
            // there is insufficient information to determine which status is authoritative.
            // The incoming event is therefore retained as a CONFLICT without changing state.
            if (incoming.OccurredAt == current.OccurredAt && incoming.Status != current.Status)
                return await repository.SaveEventAndApplyStateAsync(incoming, null, ProcessingOutcome.Conflict,
                    "Different statuses have the same occurrence timestamp.");

            // Handle a newer event that reports the same shipment status.
            // This is not necessarily a duplicate because it has a different EventId.
            // The event is recorded and the current-state metadata is refreshed while
            // the logical shipment status remains unchanged.
            if (incoming.Status == current.Status)
            {
                logger.LogDebug("Shipment {ShipmentId} received newer event {EventId} with unchanged status {Status}.", 
                    incoming.ShipmentId, incoming.EventId, incoming.Status);
                var refreshed = new ShipmentState(incoming.ShipmentId, incoming.Status, incoming.EventId, incoming.OccurredAt, incoming.Location, current.RowVersion);
                return await repository.SaveEventAndApplyStateAsync(incoming, refreshed, ProcessingOutcome.Applied, "State unchanged; newer event recorded.");
            }

            // Validate the proposed movement through the shipment lifecycle.
            // Even when an event is newer than the current state, it must represent an
            // allowed business transition. For example, a shipment that is already
            // DELIVERED should not normally regress back to IN_TRANSIT.
            // Invalid transitions are retained for audit purposes but classified as
            // CONFLICT and do not overwrite the trusted current state.
            if (!ShipmentStateRules.IsValidTransition(current.Status, incoming.Status))
            {
                logger.LogWarning( "Invalid shipment transition detected. ShipmentId: {ShipmentId}, EventId: {EventId}, CurrentStatus: {CurrentStatus}, IncomingStatus: {IncomingStatus}",
                    incoming.ShipmentId, incoming.EventId, current.Status, incoming.Status);
                return await repository.SaveEventAndApplyStateAsync(incoming, null, ProcessingOutcome.Conflict,
                    $"Invalid transition: {current.Status} -> {incoming.Status}.");
            }

            logger.LogInformation("Applying shipment state transition. ShipmentId: {ShipmentId}, EventId: {EventId}, FromStatus: {CurrentStatus}, ToStatus: {IncomingStatus}",
                    incoming.ShipmentId, incoming.EventId, current.Status, incoming.Status);

            // All integrity checks have now passed:
            // - the event is not a duplicate;
            // - it is not stale;
            // - it does not have an ambiguous timestamp;
            // - it is not merely repeating the current status; and
            // - the transition is permitted by the shipment lifecycle rules.
            //
            // The incoming event can therefore become the new trusted current state.
            // The existing RowVersion is carried forward so that the repository can
            // perform optimistic-concurrency protection when updating SQL Server.
            var next = new ShipmentState(incoming.ShipmentId, incoming.Status, incoming.EventId, incoming.OccurredAt, incoming.Location, current.RowVersion);
            return await repository.SaveEventAndApplyStateAsync(incoming, next, ProcessingOutcome.Applied, null);
        }
    }
}
