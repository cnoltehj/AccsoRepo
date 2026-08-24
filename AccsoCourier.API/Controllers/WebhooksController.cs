using AccsoCourier.API.Contracts;
using AccsoCourier.Domain.Enums;
using AccsoCourier.Domain.Models;
using AccsoCourier.Domain.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace AccsoCourier.API.Controllers
{
    [ApiController]
    [Route("api/webhooks")]
    public class WebhooksController(ShipmentEventProcessor processor, ILogger<WebhooksController> logger) : ControllerBase
    {
        [HttpPost("dhl/add_event")]
        [ProducesResponseType(typeof(ProcessingResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ProcessingResult>> ReceiveDhl([FromBody] CourierEventRequest request)
        {
            if (!TryParseStatus(request.Status, out var status)) { 
                logger.LogInformation("Received unsupported status '{Status}' from partner '{Partner}' for shipment '{ShipmentId}' with event '{EventId}'.", request.Status, request.Partner, request.ShipmentId, request.EventId);
                return BadRequest(new { error = $"Unsupported status '{request.Status}'." });
            }
            if (request.OccurredAt == default || request.ReceivedAt == default)
            {
                logger.LogInformation("Received event with missing occurredAt or receivedAt from partner '{Partner}' for shipment '{ShipmentId}' with event '{EventId}'.", request.Partner, request.ShipmentId, request.EventId);
                return BadRequest(new { error = "occurredAt and receivedAt are required." });
            }

                var e = new ShipmentEvent(request.EventId.Trim(), request.Partner.Trim().ToLowerInvariant(), request.ShipmentId.Trim(),
                status, request.OccurredAt, request.ReceivedAt, request.Location?.Trim());
            return Ok(await processor.ProcessAsync(e));
        }

        private static bool TryParseStatus(string value, out ShipmentStatus status)
        {
            status = value?.Trim().ToUpperInvariant() switch
            {
                "LABEL_CREATED" => ShipmentStatus.LabelCreated,
                "HANDED_TO_CARRIER" => ShipmentStatus.HandedToCarrier,
                "IN_TRANSIT" => ShipmentStatus.InTransit,
                "OUT_FOR_DELIVERY" => ShipmentStatus.OutForDelivery,
                "DELIVERED" => ShipmentStatus.Delivered,
                "DELIVERY_EXCEPTION" => ShipmentStatus.DeliveryException,
                "RETURNED" => ShipmentStatus.Returned,
                _ => (ShipmentStatus)(-1)
            };
            return Enum.IsDefined(status);
        }
    }
}
