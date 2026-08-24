using AccsoCourier.Domain.Enums;
using AccsoCourier.Domain.Models;
using AccsoCourier.Domain.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace AccsoCourier.Domain.Test
{
    public class ShipmentEventProcessorTests 
    {
        /// <summary>
        /// Creates a shipment event for the default DHL shipment used by these tests.
        /// The received timestamp is intentionally five seconds after the occurred timestamp
        /// to represent a normal courier delivery delay.
        /// </summary>
        private static ShipmentEvent CreateEvent(string eventId, ShipmentStatus status, string occurredAt)
            {
                var occurred = DateTimeOffset.Parse(occurredAt);

                return new ShipmentEvent(
                    eventId,
                    "dhl",
                    "ship-456",
                    status,
                    occurred,
                    occurred.AddSeconds(5),
                    "Amsterdam");
            }


        [Fact]
        public async Task First_event_establishes_current_state()
        {
            // Arrange
            var repository = new FakeShipmentRepository();
            var processor = new ShipmentEventProcessor(repository);

            var firstEvent = CreateEvent(
                "evt-1",
                ShipmentStatus.LabelCreated,
                "2026-03-10T10:00:00Z");

            // Act
            var result = await processor.ProcessAsync(firstEvent);

            // Assert
            Assert.Equal(ProcessingOutcome.Applied, result.Outcome);

            var currentState =
                await repository.GetCurrentStateAsync("ship-456");

            Assert.NotNull(currentState);
            Assert.Equal(
                ShipmentStatus.LabelCreated,
                currentState.Status);
        }

        [Fact]
        public async Task Duplicate_event_is_idempotent()
        {
            // Arrange
            var repository = new FakeShipmentRepository();
            var processor = new ShipmentEventProcessor(repository);

            var shipmentEvent = CreateEvent(
                "evt-1",
                ShipmentStatus.LabelCreated,
                "2026-03-10T10:00:00Z");

            // Process the event once successfully.
            await processor.ProcessAsync(shipmentEvent);

            // Act
            // The exact same logical courier event is delivered again.
            var duplicateResult =
                await processor.ProcessAsync(shipmentEvent);

            // Assert
            // The second delivery must not cause another business state change.
            Assert.Equal(
                ProcessingOutcome.Duplicate,
                duplicateResult.Outcome);
        }

        [Fact]
        public async Task Older_event_is_stale_and_does_not_regress_state()
        {
            // Arrange
            var repository = new FakeShipmentRepository();
            var processor = new ShipmentEventProcessor(repository);

            // Establish a valid shipment progression.
            await processor.ProcessAsync(
                CreateEvent(
                    "evt-1",
                    ShipmentStatus.InTransit,
                    "2026-03-10T11:00:00Z"));

            await processor.ProcessAsync(
                CreateEvent(
                    "evt-2",
                    ShipmentStatus.OutForDelivery,
                    "2026-03-10T12:00:00Z"));

            // Act
            // This event arrives later, but occurred earlier than the trusted current state.
            var result = await processor.ProcessAsync(
                CreateEvent(
                    "evt-3",
                    ShipmentStatus.InTransit,
                    "2026-03-10T11:30:00Z"));

            // Assert
            Assert.Equal(
                ProcessingOutcome.Stale,
                result.Outcome);

            var currentState =
                await repository.GetCurrentStateAsync("ship-456");

            Assert.NotNull(currentState);

            // The trusted state must not move backwards.
            Assert.Equal(
                ShipmentStatus.OutForDelivery,
                currentState.Status);
        }

        [Fact]
        public async Task Invalid_transition_is_conflict_and_state_is_unchanged()
        {
            // Arrange
            var repository = new FakeShipmentRepository();
            var processor = new ShipmentEventProcessor(repository);

            // Establish DELIVERED as the trusted current state.
            await processor.ProcessAsync(
                CreateEvent(
                    "evt-1",
                    ShipmentStatus.Delivered,
                    "2026-03-10T15:00:00Z"));

            // Act
            // A later IN_TRANSIT event conflicts with the configured transition rules.
            var result = await processor.ProcessAsync(
                CreateEvent(
                    "evt-2",
                    ShipmentStatus.InTransit,
                    "2026-03-10T16:00:00Z"));

            // Assert
            Assert.Equal(
                ProcessingOutcome.Conflict,
                result.Outcome);

            var currentState =
                await repository.GetCurrentStateAsync("ship-456");

            Assert.NotNull(currentState);

            // A conflict is retained for investigation but must not overwrite trusted state.
            Assert.Equal(
                ShipmentStatus.Delivered,
                currentState.Status);
        }

        [Fact]
        public async Task Same_timestamp_with_different_status_is_conflict()
        {
            // Arrange
            var repository = new FakeShipmentRepository();
            var processor = new ShipmentEventProcessor(repository);

            const string timestamp =
                "2026-03-10T12:00:00Z";

            await processor.ProcessAsync(
                CreateEvent(
                    "evt-1",
                    ShipmentStatus.InTransit,
                    timestamp));

            // Act
            // Two different statuses claim to have occurred at exactly the same time.
            var result = await processor.ProcessAsync(
                CreateEvent(
                    "evt-2",
                    ShipmentStatus.OutForDelivery,
                    timestamp));

            // Assert
            Assert.Equal(
                ProcessingOutcome.Conflict,
                result.Outcome);
        }

        [Fact]
        public async Task Valid_progression_is_applied()
        {
            // Arrange
            var repository = new FakeShipmentRepository();
            var processor = new ShipmentEventProcessor(repository);

            await processor.ProcessAsync(
                CreateEvent(
                    "evt-1",
                    ShipmentStatus.InTransit,
                    "2026-03-10T12:00:00Z"));

            // Act
            var result = await processor.ProcessAsync(
                CreateEvent(
                    "evt-2",
                    ShipmentStatus.OutForDelivery,
                    "2026-03-10T13:00:00Z"));

            // Assert
            Assert.Equal(
                ProcessingOutcome.Applied,
                result.Outcome);

            var currentState =
                await repository.GetCurrentStateAsync("ship-456");

            Assert.NotNull(currentState);

            Assert.Equal(
                ShipmentStatus.OutForDelivery,
                currentState.Status);
        }
    }
}
